using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropsSystem : SystemBase {
        private int types;

        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        // allows us to do a SINGLE async readback for ALL the props in a segment
        private ComputeBuffer tempBuffer;

        // temp counter buffer
        private ComputeBuffer tempCountersBuffer;

        // max count per segment dispatch (temp)
        private uint maxCombinedTempProps;

        // max count for the whole world. can't have more props that this
        private uint maxCombinedPermProps;

        // contains offsets for each prop type inside tempPropBuffer
        private uint[] tempBufferOffsets;
        private ComputeBuffer tempBufferOffsetsBuffer;

        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        public ComputeBuffer permBuffer;

        // contains offsets for each prop type inside permPropBuffer
        private uint[] permBufferOffsets;
        private ComputeBuffer permBufferOffsetsBuffer;

        private SegmentPropExecutor propExecutor;


        private NativeBitArray[] permPropsBitsets;
        private NativeArray<uint> tempCounters;
        private NativeArray<uint4> tempBufferReadback;




        private bool countersFetched;
        private bool propsFetched;
        private bool needPropsToBeFetched;
        private bool free;
        private bool disposed;
        private bool initialized;
        private Entity entity;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();

            initialized = false;
            free = true;
            disposed = false;
            propExecutor = new SegmentPropExecutor();
        }

        protected override void OnUpdate() {
            TerrainPropsConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();

            if (!initialized) {
                initialized = true;
                InitResources(config);
            }

            if (free) {
                TryBeginDispatchAndReadback(config);
            } else {
                TryCheckIfReadbackComplete(config);
            }

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentProps = free;
        }


        private void InitResources(TerrainPropsConfig config) {
            types = config.props.Count;

            // Create temp offsets for temp allocation
            tempBufferOffsets = new uint[types];
            maxCombinedTempProps = 0;
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                tempBufferOffsets[i] = maxCombinedTempProps;
                maxCombinedTempProps += (uint)count;
            }

            // Create a SINGLE HUGE temp allocation for a single segment dispatch
            tempBuffer = new ComputeBuffer((int)maxCombinedTempProps, BlittableProp.size, ComputeBufferType.Structured);

            // Create a smaller offsets buffer that will gives us offsets inside the temp buffer
            tempBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured); 
            tempBufferOffsetsBuffer.SetData(tempBufferOffsets);

            // Create perm offsets for perm allocation
            permPropsBitsets = new NativeBitArray[types];
            maxCombinedPermProps = 0;
            permBufferOffsets = new uint[types];
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                permPropsBitsets[i] = new NativeBitArray(count, Allocator.Persistent);
                permBufferOffsets[i] = maxCombinedPermProps;
                maxCombinedPermProps += (uint)count;
            }

            // Create a SINGLE HUGE perm allocation that contains ALL the props. EVER
            permBuffer = new ComputeBuffer((int)maxCombinedPermProps, BlittableProp.size);

            // Create a smaller offsets buffer that will gives us offsets inside the perm buffer
            permBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferOffsetsBuffer.SetData(permBufferOffsets);

            // Temp prop counters
            tempCountersBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            uint[] emptyCounters = new uint[types];
            tempCountersBuffer.SetData(emptyCounters);
            tempCounters = new NativeArray<uint>(types, Allocator.Persistent);

            // Temp prop readback buffer
            tempBufferReadback = new NativeArray<uint4>((int)maxCombinedTempProps, Allocator.Persistent);
        }

        private void TryBeginDispatchAndReadback(TerrainPropsConfig config) {
            SegmentVoxelSystem dispatcher = World.GetExistingSystemManaged<SegmentVoxelSystem>();
            Entity entity = dispatcher.entity;
            TerrainSegment segment = dispatcher.segment;
            GraphicsFence fence = dispatcher.fence;

            if (entity == Entity.Null)
                return;

            this.entity = entity;
            needPropsToBeFetched = segment.lod == TerrainSegment.LevelOfDetail.High;
            countersFetched = false;
            propsFetched = false;

            int invocations = (int)maxCombinedTempProps;

            Texture segmentDensityTexture = dispatcher.voxelExecutor.Textures["densities"];
            fence = propExecutor.ExecuteWithInvocationCount(new int3(invocations, 1, 1), new SegmentPropExecutorParameters() {
                commandBufferName = "Terrain Segment Props Dispatch",
                kernelName = "CSProps",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                segment = segment,
                tempCountersBuffer = tempCountersBuffer,
                tempBuffer = tempBuffer,
                tempBufferOffsetsBuffer = tempBufferOffsetsBuffer,
                segmentDensityTexture = segmentDensityTexture,
            }, previous: fence);

            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Segment Readback Prop Counts";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref tempCounters,
                tempCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    if (disposed)
                        return;
                    countersFetched = true;
                }
            );

            if (needPropsToBeFetched) {
                cmds.RequestAsyncReadbackIntoNativeArray(
                    ref tempBufferReadback,
                    tempBuffer,
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        if (disposed)
                            return;
                        propsFetched = true;
                    }
                );
            }


            free = false;

            Graphics.ExecuteCommandBuffer(cmds);
            SystemAPI.SetComponentEnabled<TerrainSegmentRequestPropsTag>(entity, false);
        }


        private void TryCheckIfReadbackComplete(TerrainPropsConfig config) {
            if (countersFetched && (propsFetched || !needPropsToBeFetched)) {
                free = true;
                countersFetched = false;
                propsFetched = false;

                if (needPropsToBeFetched) {
                    if (!EntityManager.Exists(entity))
                        return;

                    int lilSum = 0;
                    for (int i = 0; i < types; i++) {
                        lilSum += (int)tempCounters[i];
                    }

                    EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

                    int index = 0;
                    for (int i = 0; i < types; i++) {
                        int tempSubBufferOffset = (int)tempBufferOffsets[i];
                        int tempSubBufferCount = (int)tempCounters[i];
                        if (tempSubBufferCount > 0 && config.props[i].SpawnEntities) {
                            NativeArray<uint4> raw = tempBufferReadback.GetSubArray(tempSubBufferOffset, tempSubBufferCount);
                            NativeArray<BlittableProp> transmuted = raw.Reinterpret<BlittableProp>();
                            Entity[] variants = config.baked[i].variants;

                            for (int j = 0; j < tempSubBufferCount; j++) {
                                BlittableProp prop = transmuted[j];
                                float3 position = 0f;
                                float scale = 1f;
                                quaternion rotation = quaternion.identity;
                                byte variant = 0;

                                PropUtils.UnpackProp(prop, out position, out scale, out rotation, out variant);

                                if (variant >= variants.Length) {
                                    Debug.LogWarning($"Variant index {variant} exceeds prop type's (type: {i}) defined variant count {variants.Length}");
                                    continue;
                                }

                                Entity prototype = variants[variant];
                                Entity entity = ecb.Instantiate(prototype);

                                ecb.SetComponent<LocalTransform>(entity, LocalTransform.FromPositionRotationScale(position, rotation, scale));
                                ecb.AppendToBuffer(this.entity, new TerrainSegmentOwnedProp { entity = entity });
                                index++;
                            }
                        }
                    }

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                }
            }
        }

        protected override void OnDestroy() {
            disposed = true;
            AsyncGPUReadback.WaitAllRequests();

            if (initialized) {
                tempCountersBuffer.Dispose();
                tempCounters.Dispose();
                tempBuffer.Dispose();
                tempBufferOffsetsBuffer.Dispose();
                tempBufferReadback.Dispose();

                for (int i = 0; i < types; i++) {
                    permPropsBitsets[i].Dispose();
                }

                permBuffer.Dispose();
                permBufferOffsetsBuffer.Dispose();
            }

            propExecutor.DisposeResources();
        }
    }
}