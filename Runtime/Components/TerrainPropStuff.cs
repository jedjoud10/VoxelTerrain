using System;
using System.Linq;
using System.Runtime.InteropServices;
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
        public int[] tempBufferCounts;
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

        // Counters for the number of visible (non-culled) props for each prop type
        public ComputeBuffer visiblePropsCountersBuffer;

        // type indices buffer that is the same size of tempBuffer that contains the original types' indices inside tempBuffer (stored as bytes)
        public ComputeBuffer tempBufferTypeIndicesBuffer;

        // type indices buffer that is the same size of permBuffer that contains the original types' indices inside permBuffer (stored as bytes)
        public ComputeBuffer permBufferTypeIndicesBuffer;

        // indirection buffer that we use to read data from permBufferMatrices and permBuffer
        // each value represents an index into those buffers
        public ComputeBuffer indirectionBuffer;

        // draw args buffer ykhiibbg
        public GraphicsBuffer drawArgsBuffer;

        private ComputeShader copyCompute, cullCompute, applyCompute;
        public int cullComputeThreadCount;
        public int copyComputeThreadCount;

        public int MaxPermProps() {
            return maxCombinedPermProps;
        }

        public int PermPropsInUse() {
            return permPropsInUseBitset.CountBits(0, permPropsInUseBitset.Length);
        }

        private static ComputeBuffer GenerateTypeIndicesBuffer(int maxCombinedProps, int types, int[] typeOffsets, int[] typeCounts) {
            int typeIndicesBufferBlockAligned = (int)math.ceil((float)maxCombinedProps / 4.0);
            ComputeBuffer typeIndicesBuffer = new ComputeBuffer(typeIndicesBufferBlockAligned, 4, ComputeBufferType.Structured);

            NativeArray<byte> typeIndices = new NativeArray<byte>(typeIndicesBufferBlockAligned * 4, Allocator.Temp);
            for (int i = 0; i < types; i++) {
                int offset = typeOffsets[i];
                int count = typeCounts[i];
                NativeArray<byte> sequential = typeIndices.GetSubArray(offset, count);

                for (int j = 0; j < count; j++) {
                    sequential[j] = (byte)i;
                }
            }
            NativeArray<uint> typeIndicesBlocks = typeIndices.Reinterpret<uint>(1);
            typeIndicesBuffer.SetData(typeIndicesBlocks);
            return typeIndicesBuffer;
        }

        public void Init(TerrainPropsConfig config) {
            copyCompute = config.copy;
            cullCompute = config.cull;
            applyCompute = config.apply;
            types = config.props.Count;

            // Create temp offsets for temp allocation
            tempBufferOffsets = new int[types];
            tempBufferCounts = new int[types];
            maxCombinedTempProps = 0;
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                tempBufferOffsets[i] = maxCombinedTempProps;
                tempBufferCounts[i] = count;
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
                int count = config.props[i].maxPropsInTotal;
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

            indirectionBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(uint), ComputeBufferType.Structured);

            // do NOT use a struct here / on the GPU!
            // since buffers are aligned to 4 bytes, using a struct on the GPU makes it uh... shit itself... hard
            // just index the raw indices. wtv
            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, types * 5, sizeof(uint));
            uint[] args = new uint[types * 5];
            for (int i = 0; i < types; i++) {
                // Set the IndexCountPerInstance value...
                args[i * 5] = config.props[i].instancedMesh.GetIndexCount(0);                
            }
            drawArgsBuffer.SetData(args);

            visiblePropsCountersBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            visiblePropsCountersBuffer.SetData(new uint[types]);

            permBufferTypeIndicesBuffer = GenerateTypeIndicesBuffer(maxCombinedPermProps, types, permBufferOffsets, permBufferCounts);
            tempBufferTypeIndicesBuffer = GenerateTypeIndicesBuffer(maxCombinedTempProps, types, tempBufferOffsets, tempBufferCounts);
        }

        // For some reason Unity doesn't set the names of *some* buffers (probably small scratch upload ones)
        // Meaning that if I set the name of a buffer during init, and write a bit of memory to it, in renderdoc the name does not show up
        // Raw dogging it by forcefully setting the name right before dispatch, but this is a bug!!!
        private void SetDebugNames() {
            tempBuffer.name = "Temp Prop Buffer";
            tempBufferOffsetsBuffer.name = "Temp Buffer Prop Offsets Buffer";
            permBuffer.name = "Perm Prop Buffer";
            permPropsInUseBitsetBuffer.name = "NativeBitArray Prop Buffer";
            permBufferOffsetsBuffer.name = "Perm Prop Buffer Offsets Buffer";
            permBufferCountsBuffer.name = "Perm Prop Buffer Counts Buffer";
            visiblePropsCountersBuffer.name = "Visible Prop Counters Buffer";
            permBufferTypeIndicesBuffer.name = "Perm Prop Buffer Type Indices Buffer";
            tempBufferTypeIndicesBuffer.name = "Temp Prop Buffer Type Indices Buffer";
            drawArgsBuffer.name = "Prop Draw Args Buffer";
            indirectionBuffer.name = "Prop Draw Indirection Buffer";
            tempCountersBuffer.name = "Temp Prop Counters";
            permBufferDstCopyOffsetsBuffer.name = "Perm Prop Buffer Dst Copy Offsets Buffer";
        }

        public void RenderProps(TerrainPropsConfig config) {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            permPropsInUseBitsetBuffer.SetData(permPropsInUseBitset.AsNativeArrayExt<uint>());
            visiblePropsCountersBuffer.SetData(new uint[types]);

            SetDebugNames();
            cullCompute.SetBuffer(0, "perm_buffer", permBuffer);
            cullCompute.SetBuffer(0, "indirection_buffer", indirectionBuffer);
            cullCompute.SetBuffer(0, "perm_props_in_use_bitset_buffer", permPropsInUseBitsetBuffer);
            cullCompute.SetBuffer(0, "visible_props_counters_buffer", visiblePropsCountersBuffer);
            cullCompute.SetBuffer(0, "perm_buffer_counts_buffer", permBufferCountsBuffer);
            cullCompute.SetBuffer(0, "perm_buffer_offsets_buffer", permBufferOffsetsBuffer);
            cullCompute.SetBuffer(0, "perm_buffer_type_indices_buffer", permBufferTypeIndicesBuffer);
            cullCompute.SetInt("max_combined_perm_props", maxCombinedPermProps);

            Vector3 cameraPosition = cam.transform.position;
            Vector3 camerForward = cam.transform.forward;
            cullCompute.SetVector("camera_position", cameraPosition);
            cullCompute.SetVector("camera_forward", camerForward);

            const int THREAD_GROUP_SIZE_X = 32;
            const int CULL_INNER_LOOP_SIZE = 32;
            int threadCountX = Mathf.CeilToInt((float)maxCombinedPermProps / (THREAD_GROUP_SIZE_X * CULL_INNER_LOOP_SIZE));
            cullComputeThreadCount = threadCountX;
            cullCompute.Dispatch(0, threadCountX, 1, 1);

            applyCompute.SetBuffer(0, "draw_args_buffer", drawArgsBuffer);
            applyCompute.SetBuffer(0, "visible_props_counters_buffer", visiblePropsCountersBuffer);
            applyCompute.Dispatch(0, types, 1, 1);

            for (int i = 0; i < types; i++) {
                if (config.props[i].RenderInstanced) {
                    RenderPropsOfType(config.props[i], i);
                }
            }
        }

        public void RenderPropsOfType(PropType type, int i) {
            Material material = type.material;

            RenderParams renderParams = new RenderParams(material);
            renderParams.shadowCastingMode = type.RenderInstancedShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };

            var mat = new MaterialPropertyBlock();
            renderParams.matProps = mat;
            mat.SetBuffer("_PermBuffer", permBuffer);
            mat.SetBuffer("_IndirectionBuffer", indirectionBuffer);
            mat.SetInt("_PropType", i);
            mat.SetInt("_PermBufferOffset", permBufferOffsets[i]);

            Mesh mesh = type.instancedMesh;
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
            Array.Fill(permBufferDstCopyOffsets, -1);

            // find the sequences...
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = (int)tempCounters[i];

                if (tempSubBufferCount == 0 || !config.props[i].RenderInstanced) {
                    permBufferDstCopyOffsets[i] = -1;
                    continue;
                }

                if (tempSubBufferCount > config.props[i].maxPropsPerSegment)
                    throw new System.Exception("Temp counter exceeded max counter, not possible. Definite poopenfarten moment.");

                int permSubBufferOffset = permBufferOffsets[i];
                int permSubBufferCount = permBufferCounts[i];

                // find free blocks of contiguous tempSubBufferCount elements in permPropsInUseBitset at the appropriate offsets
                int permSubBufferStartIndex = permPropsInUseBitset.Find(permSubBufferOffset, permSubBufferCount, tempSubBufferCount);

                if (permSubBufferStartIndex == -1)
                    throw new System.Exception("Could not find contiguous sequence of free props. Ran out of perm prop memory!! (either global perm prop memory or specifically for this type)");

                permBufferDstCopyOffsets[i] = permSubBufferStartIndex;
                //Debug.Log($"type {i} found n-bit free bits sequence starting at {permSubBufferStartIndex}");
            }

            FixedList128Bytes<int> offsetsList = new FixedList128Bytes<int>();
            FixedList128Bytes<int> countsList = new FixedList128Bytes<int>();
            offsetsList.AddReplicate(-1, types);
            countsList.AddReplicate(0, types);

            // set them to used...
            for (int i = 0; i < types; i++) {
                int tempSubBufferCount = (int)tempCounters[i];

                if (tempSubBufferCount == 0 || permBufferDstCopyOffsets[i] == -1)
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

            cmds.SetBufferData(permBufferDstCopyOffsetsBuffer, permBufferDstCopyOffsets);

            cmds.SetComputeBufferParam(copyCompute, 0, "temp_counters_buffer", tempCountersBuffer);
            cmds.SetComputeBufferParam(copyCompute, 0, "temp_buffer_offsets_buffer", tempBufferOffsetsBuffer);
            cmds.SetComputeBufferParam(copyCompute, 0, "temp_buffer", tempBuffer);
            cmds.SetComputeBufferParam(copyCompute, 0, "perm_buffer", permBuffer);
            cmds.SetComputeBufferParam(copyCompute, 0, "perm_buffer_dst_copy_offsets_buffer", permBufferDstCopyOffsetsBuffer);
            cmds.SetComputeBufferParam(copyCompute, 0, "temp_buffer_type_indices_buffer", tempBufferTypeIndicesBuffer);

            int threadCountX = Mathf.CeilToInt((float)invocations / 32.0f);
            copyComputeThreadCount = threadCountX;
            cmds.DispatchCompute(copyCompute, 0, threadCountX, 1, 1);
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Background);
        }

        public void ResetPermBufferForSegment(TerrainSegmentPermPropLookup component) {
            for (int i = 0; i < types; i++) {
                int offset = component.offsetsList[i];
                int count = component.countsList[i];

                if (count == 0 || offset == -1)
                    continue;

                permPropsInUseBitset.SetBits(offset, false, count);
            }
        }

        public void SpawnPropEntities(Entity segmentEntity, EntityManager manager, TerrainPropsConfig config) {
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

            drawArgsBuffer.Dispose();
            permPropsInUseBitsetBuffer.Dispose();
            permBufferCountsBuffer.Dispose();
            tempBufferTypeIndicesBuffer.Dispose();
            permBufferTypeIndicesBuffer.Dispose();
        }
    }
}