using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public static class RenderGraphBlitUtil
{
    class PassData
    {
        public TextureHandle source;
        public Material material;
    }

    // src を dst へ転送する。material が null なら単純コピー
    public static void AddCopyPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst,
                                   string passName, Material material = null)
    {
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
        {
            passData.source = src;
            passData.material = material;

            builder.UseTexture(src);
            builder.SetRenderAttachment(dst, 0);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                if (data.material != null)
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                else
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0f, false);
            });
        }
    }
}