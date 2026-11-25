using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class WipeRendererFeature : ScriptableRendererFeature
{
    public enum WipeMode { Iris = 0, Horizontal = 1 }

    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Shader shader;
        public WipeMode mode = WipeMode.Iris;
        public Color wipeColor = Color.black;
        public Color edgeColor = new Color(1f, 0.6f, 0.1f, 1f);
        [Range(0f, 0.1f)] public float edgeWidth = 0.02f;
        [Range(0.001f, 0.3f)] public float softness = 0.03f;
    }

    // 進行度（0 = 全部見える、1 = 全部覆われる）。WipeController から設定する
    public static float Progress;

    public Settings settings = new Settings();

    WipePass _pass;
    Material _material;

    public override void Create()
    {
        CoreUtils.Destroy(_material);
        _material = settings.shader != null ? CoreUtils.CreateEngineMaterial(settings.shader) : null;

        _pass = new WipePass
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        _pass.Setup(_material, settings);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }
}

public class WipePass : ScriptableRenderPass
{
    Material _material;
    WipeRendererFeature.Settings _settings;

    public void Setup(Material material, WipeRendererFeature.Settings settings)
    {
        _material = material;
        _settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        // 値はレコード時にマテリアルへ反映する
        _material.SetFloat("_Progress", WipeRendererFeature.Progress);
        _material.SetColor("_WipeColor", _settings.wipeColor);
        _material.SetColor("_EdgeColor", _settings.edgeColor);
        _material.SetFloat("_EdgeWidth", _settings.edgeWidth);
        _material.SetFloat("_Softness", _settings.softness);
        _material.SetFloat("_Mode", (float)_settings.mode);

        TextureHandle source = resourceData.activeColorTexture;

        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "_WipeTemp";
        desc.clearBuffer = false;
        desc.depthBufferBits = DepthBits.None;
        TextureHandle temp = renderGraph.CreateTexture(desc);

        // パス 1：ワイプを重ねて temp へ書く
        RenderGraphBlitUtil.AddCopyPass(renderGraph, source, temp, "Wipe09", _material);

        // パス 2：temp の結果をカメラのカラーへ戻す
        RenderGraphBlitUtil.AddCopyPass(renderGraph, temp, source, "Wipe09 Copy");
    }
}