using System;
using System.Collections.Generic;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainSegmentVoxelSystem))]
    public partial class TerrainSegmentPropsDispatchSystem : SystemBase {
        private bool countersFetched;
        private bool propsFetched;
        private bool free;
        private Entity segmentEntity;
        private TerrainSegment.LevelOfDetail lod;
        private SegmentPropExecutor propExecutor;

        private TerrainPropsConfig config;
        private TerrainPropPermBuffers perm;
        private TerrainPropTempBuffers temp;
        int types;


        private NativeHashMap<int3, NativeArray<uint>> modifiedTempRemovedBitsets;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropPermBuffers>();
            RequireForUpdate<TerrainPropTempBuffers>();

            free = true;
            propExecutor = new SegmentPropExecutor();
            modifiedTempRemovedBitsets = new NativeHashMap<int3, NativeArray<uint>>(0, Allocator.Persistent);
        }        

        protected override void OnUpdate() {
            config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            perm = SystemAPI.ManagedAPI.GetSingleton<TerrainPropPermBuffers>();
            temp = SystemAPI.ManagedAPI.GetSingleton<TerrainPropTempBuffers>();
            types = config.props.Count;

            Profiler.BeginSample("Tag Removed Props");
            {
                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (sharedCleanup, cleanup, entity) in SystemAPI.Query<TerrainPropSharedCleanup, TerrainPropCleanup>().WithAbsent<TerrainPropTag>().WithEntityAccess()) {
                    ecb.RemoveComponent(entity, typeof(TerrainPropCleanup));
                    ecb.RemoveComponent(entity, typeof(TerrainPropSharedCleanup));
                    ecb.DestroyEntity(entity);

                    if (!modifiedTempRemovedBitsets.ContainsKey(sharedCleanup.segmentPosition)) {
                        modifiedTempRemovedBitsets.Add(sharedCleanup.segmentPosition, new NativeArray<uint>(temp.removedBitsetUintCount, Allocator.Persistent));
                    }

                    NativeArray<uint> item = modifiedTempRemovedBitsets[sharedCleanup.segmentPosition];
                    int idx = (int)cleanup.id + temp.tempBufferOffsets[sharedCleanup.type];
                    int local = idx % 32;
                    int batch = idx / 32;
                    item[batch] |= (uint)(1 << local);
                }

                ecb.Playback(EntityManager);
                ecb.Dispose();
            }
            Profiler.EndSample();


            if (free) {
                TryBeginDispatchAndReadback();
            } else {
                TryCheckIfReadbackComplete();
            }

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentPropsDispatch = free;

            Profiler.BeginSample("Handle Pending Removal Prop Segments");
            {
                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (buffer, segment) in SystemAPI.Query<DynamicBuffer<TerrainSegmentOwnedPropBuffer>, TerrainSegment>().WithAll<TerrainSegmentPendingRemoval>()) {
                    DespawnPropEntities(buffer, segment, ref ecb);
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
            Profiler.EndSample();
        }


        private void TryBeginDispatchAndReadback() {
            TerrainSegmentVoxelSystem dispatcher = World.GetExistingSystemManaged<TerrainSegmentVoxelSystem>();
            Entity entity = dispatcher.entity;
            TerrainSegment segment = dispatcher.segment;
            GraphicsFence fence = dispatcher.fence;

            if (entity == Entity.Null)
                return;

            Profiler.BeginSample("Begin Prop Dispatch & Readback");
            
            this.segmentEntity = entity;
            lod = segment.lod;
            countersFetched = false;
            propsFetched = false;

            NativeArray<uint> removedBitset = temp.tempRemovedBitsetEmptyDefault;
            if (modifiedTempRemovedBitsets.TryGetValue(segment.position, out var tempAllowedToSpawnBitset)) {
                removedBitset = tempAllowedToSpawnBitset;
            } 

            temp.tempRemovedBitsetBuffer.SetData(removedBitset);

            int invocations = temp.maxCombinedTempProps;
            Texture segmentDensityTexture = dispatcher.voxelExecutor.Textures["densities"];
            fence = propExecutor.ExecuteWithInvocationCount(new int3(invocations, 1, 1), new SegmentPropExecutorParameters() {
                commandBufferName = "Terrain Segment Props Dispatch",
                kernelName = "CSProps",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                segment = segment,
                tempCountersBuffer = temp.tempCountersBuffer,
                tempBuffer = temp.tempBuffer,
                tempBufferOffsetsBuffer = temp.tempBufferOffsetsBuffer,
                segmentDensityTexture = segmentDensityTexture,
                enabledPropsTypesFlag = (int)config.enabledPropTypesFlag,
                tempRemovedBitsetBuffer = temp.tempRemovedBitsetBuffer,
                seed = SystemAPI.GetSingleton<TerrainSeed>(),
            }, previous: fence);

            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Segment Readback Prop Counts";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref temp.tempCountersBufferReadback,
                temp.tempCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    countersFetched = true;
                }
            );

            if (lod == TerrainSegment.LevelOfDetail.High) {
                cmds.RequestAsyncReadbackIntoNativeArray(
                    ref temp.tempBufferReadback,
                    temp.tempBuffer,
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        propsFetched = true;
                    }
                );
            } else {
                propsFetched = true;
            }


            free = false;

            Graphics.ExecuteCommandBuffer(cmds);
            SystemAPI.SetComponentEnabled<TerrainSegmentRequestPropsTag>(entity, false);
            Profiler.EndSample();
        }

        private void TryCheckIfReadbackComplete() {
            if (countersFetched && propsFetched) {
                free = true;
                countersFetched = false;
                propsFetched = false;

                if (!EntityManager.Exists(segmentEntity))
                    return;

                if (lod == TerrainSegment.LevelOfDetail.High) {
                    SpawnPropEntities();
                }
                    
                CopyTempToPermBuffers();
            
                EntityManager.SetComponentEnabled<TerrainSegmentEndOfPipeTag>(segmentEntity, true);
            }
        }


        public void CopyTempToPermBuffers() {            
            bool[] propTypesWeShouldCopy = new bool[types];
            for (int i = 0; i < types; i++) {
                PropType prop = config.props[i];

                // most of the time, instances will be spawned for low LOD segments
                // however, if we swap instances for entities, then we need another check
                if (lod == TerrainSegment.LevelOfDetail.Low) {
                    if (prop.spawnEntities) {
                        // means that we will not swap instaces for entities
                        propTypesWeShouldCopy[i] = true;
                    } else {
                        // means that we WILL swap instances for entities, need extra check for avoiding cluttering the map with rats
                        if (prop.alwaysSpawnInstances) {
                            propTypesWeShouldCopy[i] = true;
                        }
                    }
                }

                // if entities are disabled and this is a high LOD segment, generate instances
                if (!prop.spawnEntities) {
                    if (lod == TerrainSegment.LevelOfDetail.High) {
                        propTypesWeShouldCopy[i] = true;
                    }
                }
            }

            int invocations = 0;

            for (int i = 0; i < types; i++) {
                if (propTypesWeShouldCopy[i]) {
                    invocations += temp.tempCountersBufferReadback[i];
                }
            }

            if (invocations == 0)
                return;

            int[] permBufferDstCopyOffsets = new int[types];
            Array.Fill(permBufferDstCopyOffsets, -1);

            int[] copyOffsets = new int[types];
            Array.Fill(copyOffsets, -1);

            int[] copyTypeLookup = new int[types];
            Array.Fill(copyTypeLookup, -1);

            // find the sequences...
            int contiguousOffset = 0;
            int what = 0;
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = temp.tempCountersBufferReadback[i];

                if (tempSubBufferCount == 0 || !propTypesWeShouldCopy[i])
                    continue;

                if (tempSubBufferCount > config.props[i].maxPropsPerSegment)
                    throw new System.Exception("Temp counter exceeded max counter, not possible. Definite poopenfarten moment.");

                int permSubBufferOffset = perm.permBufferOffsets[i];
                int permSubBufferCount = perm.permBufferCounts[i];

                // find free blocks of contiguous tempSubBufferCount elements in permPropsInUseBitset at the appropriate offsets
                int permSubBufferStartIndex = perm.permPropsInUseBitset.Find(permSubBufferOffset, permSubBufferCount, tempSubBufferCount);

                // wtf?
                if (permSubBufferStartIndex == -1 || permSubBufferStartIndex == int.MaxValue)
                    throw new System.Exception("Could not find contiguous sequence of free props. Ran out of perm prop memory!! (either global perm prop memory or specifically for this type)");

                permBufferDstCopyOffsets[i] = permSubBufferStartIndex;

                copyOffsets[what] = contiguousOffset;
                copyTypeLookup[what] = i;


                contiguousOffset += tempSubBufferCount;
                what++;
            }

            FixedList128Bytes<int> offsetsList = new FixedList128Bytes<int>();
            FixedList128Bytes<int> countsList = new FixedList128Bytes<int>();
            offsetsList.AddReplicate(-1, types);
            countsList.AddReplicate(0, types);

            // set them to used...
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = temp.tempCountersBufferReadback[i];

                if (tempSubBufferCount == 0 || permBufferDstCopyOffsets[i] == -1)
                    continue;

                perm.permPropsInUseBitset.SetBits(permBufferDstCopyOffsets[i], true, tempSubBufferCount);
                offsetsList[i] = permBufferDstCopyOffsets[i];
                countsList[i] = tempSubBufferCount;
            }

            // add component to the segment desu
            EntityManager.AddComponentData(segmentEntity, new TerrainSegmentPermPropLookup {
                offsetsList = offsetsList,
                countsList = countsList
            });

            CommandBuffer cmds = new CommandBuffer();
            cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            cmds.name = "Compute Copy Props Buffers Dispatch";

            cmds.SetBufferData(perm.permBufferDstCopyOffsetsBuffer, permBufferDstCopyOffsets);
            cmds.SetBufferData(perm.copyOffsetsBuffer, copyOffsets);
            cmds.SetBufferData(perm.copyTypeLookupBuffer, copyTypeLookup);

            cmds.SetComputeIntParam(config.copy, "invocations", invocations);
            cmds.SetComputeIntParam(config.copy, "what", what);


            cmds.SetComputeBufferParam(config.copy, 0, "copy_offsets_buffer", perm.copyOffsetsBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "copy_type_lookup_buffer", perm.copyTypeLookupBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "temp_buffer_offsets_buffer", temp.tempBufferOffsetsBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "temp_buffer", temp.tempBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "perm_buffer", perm.permBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "perm_buffer_dst_copy_offsets_buffer", perm.permBufferDstCopyOffsetsBuffer);
            cmds.SetComputeBufferParam(config.copy, 0, "perm_matrices_buffer", perm.permMatricesBuffer);

            int threadCountX = Mathf.CeilToInt((float)invocations / 64.0f);
            cmds.DispatchCompute(config.copy, 0, threadCountX, 1, 1);

            perm.copyFence = cmds.CreateAsyncGraphicsFence();
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Background);
        }

        public void ResetPermBufferForSegment(TerrainSegmentPermPropLookup component) {
            for (int i = 0; i < types; i++) {
                int offset = component.offsetsList[i];
                int count = component.countsList[i];

                if (count == 0 || offset == -1)
                    continue;

                perm.permPropsInUseBitset.SetBits(offset, false, count);
            }
        }

        public void SpawnPropEntities() {
            Profiler.BeginSample("Spawn Segment Prop Entities");
            EntityManager.AddBuffer<TerrainSegmentOwnedPropBuffer>(segmentEntity);
            int3 segmentPosition = SystemAPI.GetComponent<TerrainSegment>(segmentEntity).position;

            for (int i = 0; i < types; i++) {
                int tempSubBufferOffset = temp.tempBufferOffsets[i];
                int tempSubBufferCount = temp.tempCountersBufferReadback[i];
                if (tempSubBufferCount > 0 && config.props[i].spawnEntities) {
                    NativeArray<uint4> raw = temp.tempBufferReadback.GetSubArray(tempSubBufferOffset, tempSubBufferCount);
                    NativeArray<BlittableProp> transmuted = raw.Reinterpret<BlittableProp>();
                    TerrainPropsConfig.BakedPropVariant[] variants = config.baked[i];

                    Profiler.BeginSample($"Record Spawn Props (type: {i}, count:{tempSubBufferCount})");

                    NativeList<int>[] usedVariantsCounts = new NativeList<int>[variants.Length];

                    for (int v = 0; v < usedVariantsCounts.Length; v++) {
                        usedVariantsCounts[v] = new NativeList<int>(0, Allocator.Temp);
                    }

                    NativeArray<LocalTransform> transforms = new NativeArray<LocalTransform>(tempSubBufferCount, Allocator.TempJob);
                    NativeArray<TerrainPropCleanup> cleanup = new NativeArray<TerrainPropCleanup>(tempSubBufferCount, Allocator.TempJob);

                    Profiler.BeginSample($"Unpack Props");
                    int entityIndex = 0;                    
                    for (int j = 0; j < tempSubBufferCount; j++) {
                        BlittableProp prop = transmuted[j];

                        PropUtils.UnpackProp(prop, out float3 position, out float scale, out quaternion rotation, out byte variant, out uint id);

                        if (variant >= variants.Length) {
                            Debug.LogWarning($"Variant index {variant} exceeds prop type's (type: {i}) defined variant count {variants.Length}");
                            continue;
                        }

                        transforms[entityIndex] = LocalTransform.FromPositionRotationScale(position, rotation, scale);
                        usedVariantsCounts[(int)variant].Add(entityIndex);
                        cleanup[entityIndex] = new TerrainPropCleanup { id = id };
                        entityIndex++;
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample($"Instantiate Props");
                    var shared = new TerrainPropSharedCleanup { segmentPosition = segmentPosition, type = i };
                    NativeArray<Entity> dstEntities = new NativeArray<Entity>(entityIndex, Allocator.TempJob);
                    for (int v = 0; v < variants.Length; v++) {
                        NativeArray<Entity> tmpDstEntities = new NativeArray<Entity>(usedVariantsCounts[v].Length, Allocator.Temp);
                        EntityManager.Instantiate(variants[v].prototype, tmpDstEntities);

                        for (int n = 0; n < usedVariantsCounts[v].Length; n++) {
                            int dstIndex = usedVariantsCounts[v][n];
                            dstEntities[dstIndex] = tmpDstEntities[n];
                        }
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample($"Set Prop Data");
                    EntityManager.AddSharedComponent<TerrainPropSharedCleanup>(dstEntities, shared);
                    EntityManager.AddComponent<TerrainPropCleanup>(dstEntities);


                    for (int n = 0; n < dstEntities.Length; n++) {
                        Entity entity = dstEntities[n];
                        EntityManager.SetComponentData(entity, transforms[n]);
                        EntityManager.SetComponentData(entity, cleanup[n]);
                    }

                    var ownedBuffer = EntityManager.GetBuffer<TerrainSegmentOwnedPropBuffer>(segmentEntity);
                    ownedBuffer.AddRange(dstEntities.Reinterpret<TerrainSegmentOwnedPropBuffer>());


                    transforms.Dispose();
                    cleanup.Dispose();
                    dstEntities.Dispose();
                    Profiler.EndSample();


                    Profiler.EndSample();
                }
            }

            Profiler.EndSample();
        }

        public void DespawnPropEntities(DynamicBuffer<TerrainSegmentOwnedPropBuffer> buffer, TerrainSegment segment, ref EntityCommandBuffer ecb) {
            Profiler.BeginSample("Despawn Segment Prop Entities");
            NativeArray<Entity> entities = buffer.Reinterpret<Entity>().AsNativeArray();
            ecb.RemoveComponent<TerrainPropTag>(entities);
            ecb.RemoveComponent<TerrainPropSharedCleanup>(entities);
            ecb.RemoveComponent<TerrainPropCleanup>(entities);
            ecb.DestroyEntity(entities);
            Profiler.EndSample();
        }

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();
            propExecutor.DisposeResources();

            foreach (var value in modifiedTempRemovedBitsets) {
                value.Value.Dispose();
            }

            modifiedTempRemovedBitsets.Dispose();
        }
    }
}