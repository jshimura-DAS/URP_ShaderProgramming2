using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class OutlinePass : ScriptableRenderPass
{
    Material _material;
    OutlineRendererFeature.Settings _settings;

    class PassData
    {
        public TextureHandle source;
        public Material material;
    }

    public OutlinePass()
    {
        // 深度を使うことを URP に伝える（Depth Texture を生成させるため）
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public void Setup(Material material, OutlineRendererFeature.Settings settings)
    {
        _material = material;
        _settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        // 設定値はレコード時にマテリアルへ反映する（描画時に読まれる）
        _material.SetColor("_OutlineColor", _settings.outlineColor);
        _material.SetFloat("_Threshold", _settings.threshold);
        _material.SetFloat("_Sharpness", _settings.sharpness);
        _material.SetFloat("_Thickness", _settings.thickness);

        TextureHandle source = resourceData.activeColorTexture;
        TextureHandle depth = resourceData.cameraDepthTexture;

        // カメラと同じ形式の一時テクスチャを宣言する（深度は不要）
        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "_OutlineTemp";
        desc.clearBuffer = false;
        desc.depthBufferBits = DepthBits.None;
        TextureHandle temp = renderGraph.CreateTexture(desc);

        // パス 1：カラーと深度を読み、輪郭を重ねて temp へ書く
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline08", out var passData))
        {
            passData.source = source;
            passData.material = _material;

            builder.UseTexture(source);
            builder.UseTexture(depth);
            builder.SetRenderAttachment(temp, 0);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        // パス 2：temp の結果を、カメラのカラーへ戻す
        RenderGraphBlitUtil.AddCopyPass(renderGraph, temp, source, "Outline08 Copy");
    }
}