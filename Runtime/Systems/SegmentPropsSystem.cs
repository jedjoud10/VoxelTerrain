using System.Linq;
using Codice.Client.BaseCommands.BranchExplorer.ExplorerTree;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        // only on the CPU side, as we just use this when looking for bits in permPropsInUseBitset
        private uint[] permBufferOffsets;

        private SegmentPropExecutor propExecutor;

        // bitset that tells us what elements in permBuffer are in use or are free
        // when we run the compute copy to copy temp to perm buffers we use this beforehand
        // to check for "free" blocks of contiguous memory in the appropriate prop type's region
        private NativeBitArray permPropsInUseBitset;

        // buffer that is CONTINUOUSLY updated right before we submit a compute dispatch call
        // tells us the dst offset in the permBuffer to write our prop data
        public ComputeBuffer permBufferDstCopyOffsetsBuffer;

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

        public int MaxPermProps() {
            return (int)maxCombinedPermProps;
        }

        public int PermPropsInUse() {
            return permPropsInUseBitset.CountBits(0, permPropsInUseBitset.Length);
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
            maxCombinedPermProps = 0;
            permBufferOffsets = new uint[types];
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                permBufferOffsets[i] = maxCombinedPermProps;
                maxCombinedPermProps += (uint)count;
            }
            permPropsInUseBitset = new NativeBitArray((int)maxCombinedPermProps, Allocator.Persistent);

            // Create a SINGLE HUGE perm allocation that contains ALL the props. EVER
            permBuffer = new ComputeBuffer((int)maxCombinedPermProps, BlittableProp.size, ComputeBufferType.Structured);

            // Create a smaller offsets buffer that will gives us offsets inside the perm buffer
            permBufferDstCopyOffsetsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferDstCopyOffsetsBuffer.SetData(permBufferOffsets);

            // Temp prop counters
            tempCountersBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            uint[] emptyCounters = new uint[types];
            tempCountersBuffer.SetData(emptyCounters);
            tempCounters = new NativeArray<uint>(types, Allocator.Persistent);

            // Temp prop readback buffer
            tempBufferReadback = new NativeArray<uint4>((int)maxCombinedTempProps, Allocator.Persistent);
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

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var buffer in SystemAPI.Query<DynamicBuffer<TerrainSegmentOwnedPropBuffer>>().WithAll<TerrainSegmentPendingRemoval>()) {
                DespawnPropEntities(buffer, ref ecb);
            }

            foreach (var component in SystemAPI.Query<TerrainSegmentPermPropLookup>().WithAll<TerrainSegmentPendingRemoval>()) {
                ResetPermBufferForSegment(component);
            }

            foreach (var _cleanup in SystemAPI.Query<RefRW<TerrainSegmentPendingRemoval>>()) {
                _cleanup.ValueRW.propsNeedCleanup = true;
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
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

                if (!EntityManager.Exists(entity))
                    return;

                if (needPropsToBeFetched) {
                    SpawnPropEntities(config);
                } else {
                    CopyTempToPermBuffers(config);
                }
            }
        }

        private void CopyTempToPermBuffers(TerrainPropsConfig config) {
            int maxInvocationsX = tempCounters.Select(x => (int)x).Max();
            
            if (maxInvocationsX == 0)
                return;
            
            CommandBuffer cmds = new CommandBuffer();
            cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            cmds.name = "Compute Copy Props Buffers Dispatch";

            int[] permBufferDstCopyOffsets = new int[types];

            // find the sequences...
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = (int)tempCounters[i];

                if (tempSubBufferCount == 0) {
                    permBufferDstCopyOffsets[i] = -1;
                    continue;
                }

                int permSubBufferOffset = (int)permBufferOffsets[i];

                // find free blocks of contiguous tempSubBufferCount elements in permPropsInUseBitset at the appropriate offsets
                int permSubBufferStartIndex = permPropsInUseBitset.Find(permSubBufferOffset, tempSubBufferCount);

                if (permSubBufferStartIndex == -1)
                    throw new System.Exception("Could not find contiguous sequence of free props. Ran out of perm prop memory!!");

                permBufferDstCopyOffsets[i] = permSubBufferStartIndex;
            }

            FixedList128Bytes<int> offsetsList = new FixedList128Bytes<int>();
            FixedList128Bytes<int> countsList = new FixedList128Bytes<int>();
            offsetsList.AddReplicate(0, types);
            countsList.AddReplicate(0, types);
            
            // set them to used...
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = (int)tempCounters[i];

                if (tempSubBufferCount == 0)
                    continue;

                permPropsInUseBitset.SetBits(permBufferDstCopyOffsets[i], true, tempSubBufferCount);
                offsetsList[i] = permBufferDstCopyOffsets[i];
                countsList[i] = tempSubBufferCount;
            }

            // add component to the segment desu
            EntityManager.AddComponentData(entity, new TerrainSegmentPermPropLookup {
                offsetsList = offsetsList,
                countsList = countsList
            });

            ComputeShader compute = config.copyTempToPermCompute;
            cmds.SetBufferData(permBufferDstCopyOffsetsBuffer, permBufferDstCopyOffsets);
            cmds.SetComputeBufferParam(compute, 0, "temp_counters_buffer", tempCountersBuffer);
            cmds.SetComputeBufferParam(compute, 0, "temp_buffer_offsets_buffer", tempBufferOffsetsBuffer);
            cmds.SetComputeBufferParam(compute, 0, "temp_buffer", tempBuffer);
            cmds.SetComputeBufferParam(compute, 0, "perm_buffer", permBuffer);
            cmds.SetComputeBufferParam(compute, 0, "perm_buffer_dst_copy_offsets_buffer", permBufferDstCopyOffsetsBuffer);

            int threadCountX = Mathf.CeilToInt((float)maxInvocationsX / 32.0f);
            cmds.DispatchCompute(compute, 0, threadCountX, types, 1);
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Background);
        }

        private void ResetPermBufferForSegment(TerrainSegmentPermPropLookup component) {
            for (int i = 0; i < types; i++) {
                int offset = component.offsetsList[i];
                int count = component.countsList[i];

                if (count == 0)
                    continue;

                permPropsInUseBitset.SetBits(offset, false, count);
            }
        }

        private void SpawnPropEntities(TerrainPropsConfig config) {
            int lilSum = 0;
            for (int i = 0; i < types; i++) {
                lilSum += (int)tempCounters[i];
            }

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddBuffer<TerrainSegmentOwnedPropBuffer>(entity);

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
                        Entity instantiated = ecb.Instantiate(prototype);

                        ecb.SetComponent<LocalTransform>(instantiated, LocalTransform.FromPositionRotationScale(position, rotation, scale));
                        ecb.AppendToBuffer(this.entity, new TerrainSegmentOwnedPropBuffer { entity = instantiated });
                        index++;
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void DespawnPropEntities(DynamicBuffer<TerrainSegmentOwnedPropBuffer> buffer, ref EntityCommandBuffer ecb) {
            foreach (var prop in buffer) {
                ecb.DestroyEntity(prop.entity);
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
                permPropsInUseBitset.Dispose();

                permBuffer.Dispose();
                permBufferDstCopyOffsetsBuffer.Dispose();
            }

            propExecutor.DisposeResources();
        }
    }
}