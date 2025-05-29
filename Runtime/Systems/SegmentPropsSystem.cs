using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using jedjoud.VoxelTerrain.Props;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropsSystem : SystemBase {
        private ComputeBuffer[] tempPropBuffers;
        private NativeBitArray[] permPropsBitsets;
        private ComputeBuffer tempCountersBuffer;
        private NativeArray<uint> tempCounters;
        private bool countersFetched;
        private bool free;
        private bool disposed;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();

            tempPropBuffers = new ComputeBuffer[PropUtils.MAX_PROP_TYPES];
            permPropsBitsets = new NativeBitArray[PropUtils.MAX_PROP_TYPES];
            for (int i = 0; i < PropUtils.MAX_PROP_TYPES; i++) {
                tempPropBuffers[i] = new ComputeBuffer(PropUtils.MAX_PROPS_PER_SEGMENT, BlittableProp.size);
                permPropsBitsets[i] = new NativeBitArray(PropUtils.MAX_PROPS_EVER, Allocator.Persistent);
            }

            tempCountersBuffer = new ComputeBuffer(PropUtils.MAX_PROP_TYPES, sizeof(uint), ComputeBufferType.Structured);
            tempCounters = new NativeArray<uint>(PropUtils.MAX_PROP_TYPES, Allocator.Persistent);


            free = true;
            disposed = false;
        }

        protected override void OnUpdate() {
            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentProps = free;

            if (free) {
                TryBeginDispatchAndReadback();
            } else {
                TryCheckIfReadbackComplete();
            }
        }

        private void TryBeginDispatchAndReadback() {
            SegmentVoxelSystem dispatcher = World.GetExistingSystemManaged<SegmentVoxelSystem>();
            Entity entity = dispatcher.entity;
            TerrainSegment segment = dispatcher.segment;
            GraphicsFence fence = dispatcher.fence;

            if (entity == Entity.Null)
                return;

            fence = dispatcher.executor.Execute(new SegmentExecutorParameters() {
                commandBufferName = "Terrain Segment Props Dispatch",
                kernelName = "CSProps",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                position = segment.position,
                tempPropBuffers = tempPropBuffers,
                tempCountersBuffer = tempCountersBuffer,
            }, previous: fence);

            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Segment Readback Prop Counts";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            NativeArray<uint> tempCountersData = tempCounters;
            cmds.RequestAsyncReadbackIntoNativeArray(
                ref tempCountersData,
                tempCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    if (disposed)
                        return;

                    countersFetched = true;
                }
            );

            free = false;

            Graphics.ExecuteCommandBuffer(cmds);
            SystemAPI.SetComponentEnabled<TerrainSegmentRequestPropsTag>(entity, false);
        }


        private void TryCheckIfReadbackComplete() {
            if (countersFetched) {
                free = true;
                countersFetched = false;
            }
        }

        protected override void OnDestroy() {
            disposed = true;
            AsyncGPUReadback.WaitAllRequests();

            for (int i = 0; i < PropUtils.MAX_PROP_TYPES; i++) {
                tempPropBuffers[i].Dispose();
                permPropsBitsets[i].Dispose();
            }

            tempCountersBuffer.Dispose();
            tempCounters.Dispose();
        }
    }
}