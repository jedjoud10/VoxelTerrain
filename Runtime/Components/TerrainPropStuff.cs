using System;
using System.Linq;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    public class TerrainPropStuff : IComponentData {
        public int types;

        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        // allows us to do a SINGLE async readback for ALL the props in a segment
        public ComputeBuffer tempBuffer;

        // temp counter buffer
        public ComputeBuffer tempCountersBuffer;

        // max count per segment dispatch (temp)
        public int maxCombinedTempProps;

        // max count for the whole world. can't have more props that this
        public int maxCombinedPermProps;

        // contains offsets for each prop type inside tempPropBuffer
        public int[] tempBufferOffsets;
        public ComputeBuffer tempBufferOffsetsBuffer;

        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        public ComputeBuffer permBuffer;

        // contains offsets for each prop type inside permPropBuffer
        public int[] permBufferOffsets;
        public ComputeBuffer permBufferOffsetsBuffer;

        // contains counts for each prop type inside permPropBuffer
        public int[] permBufferCounts;
        public ComputeBuffer permBufferCountsBuffer;

        // bitset that tells us what elements in permBuffer are in use or are free
        // when we run the compute copy to copy temp to perm buffers we use this beforehand
        // to check for "free" blocks of contiguous memory in the appropriate prop type's region

        public NativeBitArray permPropsInUseBitset;
        public int paddingBitsetBitsCount;
        public int bitsetBlocks;
        public ComputeBuffer permPropsInUseBitsetBuffer;

        // buffer that is CONTINUOUSLY updated right before we submit a compute dispatch call
        // tells us the dst offset in the permBuffer to write our prop data
        public ComputeBuffer permBufferDstCopyOffsetsBuffer;

        public NativeArray<int> tempCounters;
        public NativeArray<uint4> tempBufferReadback;

        // contains 4x4 matrices for props that are drawn indirectly using billboards or just normal mesh instancing
        public ComputeBuffer permMatricesBuffer;

        // Counters for the number of visible (non-culled) props for each prop type
        public ComputeBuffer visiblePropsCountersBuffer;

        // indirection buffer that we use to read data from permBufferMatrices and permBuffer
        // each value represents an index into those buffers
        public ComputeBuffer indirectionBuffer;

        // draw args buffer ykhiibbg
        public GraphicsBuffer drawArgsBuffer;

        public int MaxPermProps() {
            return maxCombinedPermProps;
        }

        public int PermPropsInUse() {
            return permPropsInUseBitset.CountBits(0, permPropsInUseBitset.Length);
        }

        public void Init(TerrainPropsConfig config) {
            types = config.props.Count;

            // Create temp offsets for temp allocation
            tempBufferOffsets = new int[types];
            maxCombinedTempProps = 0;
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                tempBufferOffsets[i] = maxCombinedTempProps;
                maxCombinedTempProps += count;
            }

            // Create a SINGLE HUGE temp allocation for a single segment dispatch
            tempBuffer = new ComputeBuffer(maxCombinedTempProps, BlittableProp.size, ComputeBufferType.Structured);

            // Create a smaller offsets buffer that will gives us offsets inside the temp buffer
            tempBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);
            tempBufferOffsetsBuffer.SetData(tempBufferOffsets);

            // Create perm offsets for perm allocation
            maxCombinedPermProps = 0;
            permBufferOffsets = new int[types];
            permBufferCounts = new int[types];
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                permBufferOffsets[i] = maxCombinedPermProps;
                permBufferCounts[i] = count;
                maxCombinedPermProps += count;
            }

            // add a few padding bits so that we are always dealing with multiples of 32 bits (size of int)
            bitsetBlocks = (int)math.ceil((float)maxCombinedPermProps / 32.0);
            paddingBitsetBitsCount = bitsetBlocks * 32 - maxCombinedPermProps;

            permPropsInUseBitset = new NativeBitArray(bitsetBlocks * 32, Allocator.Persistent);
            permPropsInUseBitsetBuffer = new ComputeBuffer(bitsetBlocks, sizeof(uint), ComputeBufferType.Structured);

            // Create a SINGLE HUGE perm allocation that contains ALL the props. EVER
            permBuffer = new ComputeBuffer(maxCombinedPermProps, BlittableProp.size, ComputeBufferType.Structured);

            // Create an offsets buffer that will gives us offsets inside the perm buffer
            permBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferOffsetsBuffer.SetData(permBufferOffsets);

            // Create an counts buffer that contains counts for each type inside the perm buffer
            permBufferCountsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferCountsBuffer.SetData(permBufferCounts);

            // Buffer that will be updated each time we need to copy temp props to perm props on the GPU
            // Updated before each compute dispatch call
            permBufferDstCopyOffsetsBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);

            // Temp prop counters
            tempCountersBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);
            int[] emptyCounters = new int[types];
            tempCountersBuffer.SetData(emptyCounters);
            tempCounters = new NativeArray<int>(types, Allocator.Persistent);

            // Temp prop readback buffer
            tempBufferReadback = new NativeArray<uint4>(maxCombinedTempProps, Allocator.Persistent);

            permMatricesBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(float) * 16, ComputeBufferType.Structured);
            indirectionBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(int), ComputeBufferType.Structured);
            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, types, GraphicsBuffer.IndirectDrawIndexedArgs.size);


            // create some draw arguments that contain the index count for the simple mesh for each type
            GraphicsBuffer.IndirectDrawIndexedArgs[] args = new GraphicsBuffer.IndirectDrawIndexedArgs[types];
            for (int i = 0; i < types; i++) {
                args[i] = new GraphicsBuffer.IndirectDrawIndexedArgs() {
                    baseVertexIndex = 0,

                    // this value gets updated in the compute shader
                    instanceCount = 0,

                    startInstance = 0,
                    startIndex = 0,

                    indexCountPerInstance = config.props[i].instancedMesh.GetIndexCount(0),
                };
            }

            visiblePropsCountersBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            visiblePropsCountersBuffer.SetData(new uint[types]);
            drawArgsBuffer.SetData(args);
        }

        public void RenderProps(TerrainPropsConfig config) {
            ComputeShader compute = config.compute;

            permPropsInUseBitsetBuffer.SetData(permPropsInUseBitset.AsNativeArrayExt<uint>());
            visiblePropsCountersBuffer.SetData(new uint[types]);

            int cullKernel = compute.FindKernel("CSCull");
            int applyIndirectArgsKernel = compute.FindKernel("CSApplyIndirectArgs");
            compute.SetBuffer(cullKernel, "perm_props_in_use_bitset_buffer", permPropsInUseBitsetBuffer);
            compute.SetBuffer(cullKernel, "perm_buffer_offsets_buffer", permBufferOffsetsBuffer);
            compute.SetBuffer(cullKernel, "perm_buffer_counts_buffer", permBufferCountsBuffer);
            compute.SetBuffer(cullKernel, "perm_buffer", permBuffer);
            compute.SetBuffer(cullKernel, "perm_matrices_buffer", permMatricesBuffer);
            compute.SetBuffer(cullKernel, "draw_args_buffer", drawArgsBuffer);
            compute.SetBuffer(cullKernel, "indirection_buffer", indirectionBuffer);
            compute.SetBuffer(cullKernel, "visible_props_counters_buffer", visiblePropsCountersBuffer);

            int threadCountX = Mathf.CeilToInt((float)maxCombinedPermProps / 32.0f);
            compute.Dispatch(cullKernel, threadCountX, 1, 1);

            compute.SetBuffer(applyIndirectArgsKernel, "draw_args_buffer", drawArgsBuffer);
            compute.SetBuffer(applyIndirectArgsKernel, "visible_props_counters_buffer", visiblePropsCountersBuffer);
            compute.Dispatch(applyIndirectArgsKernel, types, 1, 1);

            for (int i = 0; i < types; i++) {
                RenderPropsOfType(config.props[i], i);
            }
        }

        public void RenderPropsOfType(PropType type, int i) {
            Material material = type.material;

            RenderParams renderParams = new RenderParams(material);
            renderParams.shadowCastingMode = ShadowCastingMode.On;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };

            var mat = new MaterialPropertyBlock();
            renderParams.matProps = mat;
            mat.SetBuffer("_PermMatricesBuffer", permMatricesBuffer);
            mat.SetBuffer("_PermBuffer", permBuffer);
            mat.SetBuffer("_IndirectionBuffer", indirectionBuffer);
            mat.SetBuffer("_PermBufferOffsetsBuffer", permBufferOffsetsBuffer);
            mat.SetInt("_PropType", i);

            Mesh mesh = type.instancedMesh;
            /*
            GraphicsBuffer.IndirectDrawIndexedArgs[] args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            drawArgsBuffer.GetData(args);
            Debug.Log(args[0].instanceCount);
            */
            Graphics.RenderMeshIndirect(renderParams, mesh, drawArgsBuffer, 1, i);
        }

        public void CopyTempToPermBuffers(Entity segmentEntity, EntityManager manager, TerrainPropsConfig config) {
            int invocations = tempCounters.Select(x => (int)x).Sum();

            if (invocations == 0)
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

                if (tempSubBufferCount > config.props[i].maxPropsPerSegment)
                    throw new System.Exception("Temp counter exceeded max counter, not possible. Definite poopenfarten moment.");

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
            manager.AddComponentData(segmentEntity, new TerrainSegmentPermPropLookup {
                offsetsList = offsetsList,
                countsList = countsList
            });

            ComputeShader compute = config.compute;
            cmds.SetBufferData(permBufferDstCopyOffsetsBuffer, permBufferDstCopyOffsets);

            int copyKernel = compute.FindKernel("CSCopy");
            cmds.SetComputeBufferParam(compute, copyKernel, "temp_counters_buffer", tempCountersBuffer);
            cmds.SetComputeBufferParam(compute, copyKernel, "temp_buffer_offsets_buffer", tempBufferOffsetsBuffer);
            cmds.SetComputeBufferParam(compute, copyKernel, "temp_buffer", tempBuffer);
            cmds.SetComputeBufferParam(compute, copyKernel, "perm_buffer", permBuffer);
            cmds.SetComputeBufferParam(compute, copyKernel, "perm_buffer_dst_copy_offsets_buffer", permBufferDstCopyOffsetsBuffer);
            cmds.SetComputeBufferParam(compute, copyKernel, "perm_matrices_buffer", permMatricesBuffer);

            int threadCountX = Mathf.CeilToInt((float)invocations / 32.0f);
            cmds.DispatchCompute(compute, copyKernel, threadCountX, 1, 1);
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Background);
        }

        public void ResetPermBufferForSegment(TerrainSegmentPermPropLookup component) {
            for (int i = 0; i < types; i++) {
                int offset = component.offsetsList[i];
                int count = component.countsList[i];

                if (count == 0)
                    continue;

                permPropsInUseBitset.SetBits(offset, false, count);
            }
        }

        public void SpawnPropEntities(Entity segmentEntity, EntityManager manager, TerrainPropsConfig config) {
            int lilSum = 0;
            for (int i = 0; i < types; i++) {
                lilSum += (int)tempCounters[i];
            }

            manager.AddBuffer<TerrainSegmentOwnedPropBuffer>(segmentEntity);

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            int index = 0;
            for (int i = 0; i < types; i++) {
                int tempSubBufferOffset = (int)tempBufferOffsets[i];
                int tempSubBufferCount = (int)tempCounters[i];
                if (tempSubBufferCount > 0 && config.props[i].SpawnEntities) {
                    NativeArray<uint4> raw = tempBufferReadback.GetSubArray(tempSubBufferOffset, tempSubBufferCount);
                    NativeArray<BlittableProp> transmuted = raw.Reinterpret<BlittableProp>();
                    Entity[] variants = config.baked[i].prototypes;

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
                        ecb.AppendToBuffer(segmentEntity, new TerrainSegmentOwnedPropBuffer { entity = instantiated });
                        index++;
                    }
                }
            }

            ecb.Playback(manager);
            ecb.Dispose();
        }

        public void DespawnPropEntities(DynamicBuffer<TerrainSegmentOwnedPropBuffer> buffer, ref EntityCommandBuffer ecb) {
            foreach (var prop in buffer) {
                ecb.DestroyEntity(prop.entity);
            }
        }

        public void Dispose() {
            tempCountersBuffer.Dispose();
            tempCounters.Dispose();
            tempBuffer.Dispose();
            tempBufferOffsetsBuffer.Dispose();
            tempBufferReadback.Dispose();
            permPropsInUseBitset.Dispose();

            permBuffer.Dispose();
            permBufferDstCopyOffsetsBuffer.Dispose();
            indirectionBuffer.Dispose();
            permMatricesBuffer.Dispose();

            drawArgsBuffer.Dispose();
            permPropsInUseBitsetBuffer.Dispose();
            permBufferCountsBuffer.Dispose();
        }
    }
}