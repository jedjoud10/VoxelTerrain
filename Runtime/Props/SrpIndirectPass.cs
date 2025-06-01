using Unity.Mathematics;
using jedjoud.VoxelTerrain.Generation;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Unity.Entities;
using jedjoud.VoxelTerrain.Segments;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public class SrpIndirectFeature : ScriptableRendererFeature {
        public class SrpIndirectPass : ScriptableRenderPass {
            public SrpIndirectPass() {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            private class PassData {
                public SegmentPropsRenderSystem system;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalLightData lights = frameData.Get<UniversalLightData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                SegmentPropsRenderSystem system = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SegmentPropsRenderSystem>();
                
                if (system == null)
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("amogous", out var passData)) {
                    builder.AllowPassCulling(false);
                    passData.system = system;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        system.RenderPropsOfType(0, ctx);
                    });
                }
            }
        }

        SrpIndirectPass pass;
        public override void Create() {
            pass = new SrpIndirectPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            renderer.EnqueuePass(pass);
        }
    }
}