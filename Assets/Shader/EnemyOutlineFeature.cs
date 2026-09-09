using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class EnemyOutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Material stencilWriteMaterial;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private LayerMask enemyLayer;

    private StencilWritePass _stencilPass;
    private OutlinePass _outlinePass;

    public override void Create()
    {
        _stencilPass = new StencilWritePass(stencilWriteMaterial, enemyLayer);
        _outlinePass = new OutlinePass(outlineMaterial, enemyLayer);

        // 오파크 렌더링 직후에 실행
        _stencilPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        _outlinePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (stencilWriteMaterial == null || outlineMaterial == null) return;
        renderer.EnqueuePass(_stencilPass);
        renderer.EnqueuePass(_outlinePass);
    }

    // --------------------------------------------------
    // 1. Stencil Write Pass (영역 채우기)
    // --------------------------------------------------
    class StencilWritePass : ScriptableRenderPass
    {
        private Material _mat;
        private LayerMask _layer;

        public StencilWritePass(Material mat, LayerMask layer) { _mat = mat; _layer = layer; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            var sortingSettings = new SortingSettings(cameraData.camera) 
            { 
                criteria = SortingCriteria.CommonOpaque | SortingCriteria.OptimizeStateChanges 
            };
            var drawingSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sortingSettings) 
            { 
                overrideMaterial = _mat,
                // 🔥 핵심: GPU Instancing을 명시적으로 허용
                enableInstancing = true, 
                enableDynamicBatching = true 
            };
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layer);

            var rendererList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stencil Write Pass", out var passData))
            {
                passData.list = rendererList;
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                builder.UseRendererList(rendererList);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(data.list));
            }
        }
    }

    // --------------------------------------------------
    // 2. Outline Pass (외곽선 그리기)
    // --------------------------------------------------
    class OutlinePass : ScriptableRenderPass
    {
        private Material _mat;
        private LayerMask _layer;

        public OutlinePass(Material mat, LayerMask layer) { _mat = mat; _layer = layer; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            var sortingSettings = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sortingSettings) { overrideMaterial = _mat };
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layer);

            var rendererList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Draw Pass", out var passData))
            {
                passData.list = rendererList;
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                builder.UseRendererList(rendererList);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(data.list));
            }
        }
    }

    class PassData { public RendererListHandle list; }
}