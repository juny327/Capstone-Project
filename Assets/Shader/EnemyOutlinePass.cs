using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class EnemyOutlinePass : ScriptableRenderPass
{
    private Material outlineMaterial;
    private LayerMask layerMask;

    public EnemyOutlinePass(Material material, LayerMask layerMask)
    {
        this.outlineMaterial = material;
        this.layerMask = layerMask;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (outlineMaterial == null)
            return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var renderingData = frameData.Get<UniversalRenderingData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        // 🔥 RendererList 생성 (핵심)
        var sortingSettings = new SortingSettings(cameraData.camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawingSettings = new DrawingSettings(
            new ShaderTagId("UniversalForward"),
            sortingSettings
        )
        {
            enableInstancing = true,
            overrideMaterial = outlineMaterial
        };

        var filteringSettings = new FilteringSettings(
            RenderQueueRange.opaque,
            layerMask
        );

        var rendererListParams = new RendererListParams(
            renderingData.cullResults,
            drawingSettings,
            filteringSettings
        );

        var rendererList = renderGraph.CreateRendererList(rendererListParams);

        // 🔥 Raster Pass
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(
            "Enemy Outline Pass",
            out var passData))
        {
            passData.rendererList = rendererList;

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

            // 🔥 RendererList 등록
            builder.UseRendererList(passData.rendererList);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // ✅ 이게 최신 방식
                context.cmd.DrawRendererList(data.rendererList);
            });
        }
    }

    class PassData
    {
        public RendererListHandle rendererList;
    }
}