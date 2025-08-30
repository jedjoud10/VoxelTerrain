using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [StructLayout(LayoutKind.Sequential)]
    public struct MultiReadbackTransform {
        public Vector3 position;
        public float scale;
        public const int size = sizeof(int) * 4;
    }

    public class MultiReadbackExecutorParameters : ExecutorParameters {
        public NativeArray<MultiReadbackTransform> transforms;
        public ComputeBuffer multiSignCountersBuffer;
    }

    public class MultiReadbackExecutor : VolumeExecutor<MultiReadbackExecutorParameters> {
        private ComputeBuffer transformsBuffer;
        private int[] defaultClearingInts;

        public MultiReadbackExecutor() : base(VoxelUtils.SIZE * VoxelUtils.MULTI_READBACK_CHUNK_SIZE_RATIO) {
            defaultClearingInts = new int[VoxelUtils.MULTI_READBACK_CHUNK_COUNT];
        }

        public override void DisposeResources() {
            base.DisposeResources();
            transformsBuffer?.Dispose();
        }

        protected override void CreateResources(ManagedTerrainCompiler compiler) {
            base.CreateResources(compiler);
            transformsBuffer = new ComputeBuffer(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, MultiReadbackTransform.size, ComputeBufferType.Structured);
            buffers.Add("voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxels", "CSLayers" }, new ComputeBuffer(VoxelUtils.VOLUME * VoxelUtils.MULTI_READBACK_CHUNK_COUNT, GpuVoxel.size, ComputeBufferType.Structured)));
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, MultiReadbackExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.OctalReadback);
            commands.SetBufferData(transformsBuffer, parameters.transforms);
            commands.SetComputeBufferParam(shader, kernelIndex, "multi_transforms_buffer", transformsBuffer);

            commands.SetBufferData(parameters.multiSignCountersBuffer, defaultClearingInts);
            commands.SetComputeBufferParam(shader, kernelIndex, "multi_counters_buffer", parameters.multiSignCountersBuffer);
        }
    }
}
