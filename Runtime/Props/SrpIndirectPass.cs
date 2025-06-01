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
                //public TerrainPropStuff stuff;
                public TerrainPropsConfig config;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                /*
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalLightData lights = frameData.Get<UniversalLightData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                var a = entityManager.CreateEntityQuery(typeof(TerrainPropStuff));
                var b = entityManager.CreateEntityQuery(typeof(TerrainPropsConfig));
                TerrainPropStuff stuff = null;
                TerrainPropsConfig config = null;
                a.TryGetSingleton<TerrainPropStuff>(out stuff);
                b.TryGetSingleton<TerrainPropsConfig>(out config);

                if (stuff == null || config == null)
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("amogous", out var passData)) {
                    builder.AllowPassCulling(false);
                    passData.stuff = stuff;
                    passData.config = config;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => {
                        data.stuff.RenderProps(data.config, context);
                    });
                }
                */
            }
        }

        SrpIndirectPass pass;
        public override void Create() {
            pass = new SrpIndirectPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            //renderer.EnqueuePass(pass);
        }
    }
}