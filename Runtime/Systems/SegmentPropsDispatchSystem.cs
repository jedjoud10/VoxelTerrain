using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropsDispatchSystem : SystemBase {
        private bool countersFetched;
        private bool propsFetched;
        private bool needPropsToBeFetched;
        private bool free;
        private Entity segmentEntity;
        private SegmentPropExecutor propExecutor;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropStuff>();

            free = true;
            propExecutor = new SegmentPropExecutor();
        }

        int copyKernel;
        

        protected override void OnUpdate() {
            TerrainPropsConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            TerrainPropStuff stuff = SystemAPI.ManagedAPI.GetSingleton<TerrainPropStuff>();

            if (free) {
                TryBeginDispatchAndReadback(stuff, config);
            } else {
                TryCheckIfReadbackComplete(stuff, config);
            }

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentPropsDispatch = free;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var buffer in SystemAPI.Query<DynamicBuffer<TerrainSegmentOwnedPropBuffer>>().WithAll<TerrainSegmentPendingRemoval>()) {
                stuff.DespawnPropEntities(buffer, ref ecb);
            }

            foreach (var component in SystemAPI.Query<TerrainSegmentPermPropLookup>().WithAll<TerrainSegmentPendingRemoval>()) {
                stuff.ResetPermBufferForSegment(component);
            }

            foreach (var _cleanup in SystemAPI.Query<RefRW<TerrainSegmentPendingRemoval>>()) {
                _cleanup.ValueRW.propsNeedCleanup = true;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }


        private void TryBeginDispatchAndReadback(TerrainPropStuff stuff, TerrainPropsConfig config) {
            SegmentVoxelSystem dispatcher = World.GetExistingSystemManaged<SegmentVoxelSystem>();
            Entity entity = dispatcher.entity;
            TerrainSegment segment = dispatcher.segment;
            GraphicsFence fence = dispatcher.fence;

            if (entity == Entity.Null)
                return;

            this.segmentEntity = entity;
            needPropsToBeFetched = segment.lod == TerrainSegment.LevelOfDetail.High;
            countersFetched = false;
            propsFetched = false;

            int invocations = stuff.maxCombinedTempProps;

            Texture segmentDensityTexture = dispatcher.voxelExecutor.Textures["densities"];
            fence = propExecutor.ExecuteWithInvocationCount(new int3(invocations, 1, 1), new SegmentPropExecutorParameters() {
                commandBufferName = "Terrain Segment Props Dispatch",
                kernelName = "CSProps",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                segment = segment,
                tempCountersBuffer = stuff.tempCountersBuffer,
                tempBuffer = stuff.tempBuffer,
                tempBufferOffsetsBuffer = stuff.tempBufferOffsetsBuffer,
                segmentDensityTexture = segmentDensityTexture,
            }, previous: fence);

            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Segment Readback Prop Counts";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref stuff.tempCounters,
                stuff.tempCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    countersFetched = true;
                }
            );

            if (needPropsToBeFetched) {
                cmds.RequestAsyncReadbackIntoNativeArray(
                    ref stuff.tempBufferReadback,
                    stuff.tempBuffer,
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        propsFetched = true;
                    }
                );
            }


            free = false;

            Graphics.ExecuteCommandBuffer(cmds);
            SystemAPI.SetComponentEnabled<TerrainSegmentRequestPropsTag>(entity, false);
        }

        private void TryCheckIfReadbackComplete(TerrainPropStuff stuff, TerrainPropsConfig config) {
            if (countersFetched && (propsFetched || !needPropsToBeFetched)) {
                free = true;
                countersFetched = false;
                propsFetched = false;

                if (!EntityManager.Exists(segmentEntity))
                    return;

                if (needPropsToBeFetched) {
                    stuff.SpawnPropEntities(segmentEntity, EntityManager, config);
                } else {
                    stuff.CopyTempToPermBuffers(segmentEntity, EntityManager, config);
                }

                EntityManager.SetComponentEnabled<TerrainSegmentEndOfPipeTag>(segmentEntity, true);
            }
        }

        

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();
            propExecutor.DisposeResources();
        }
    }
}