using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [StructLayout(LayoutKind.Sequential)]
    public struct OctalReadbackPosScaleData {
        public Vector3 position;
        public float scale;
    }

    public class OctalReadbackExecutorParameters : ExecutorParameters {
        public OctalReadbackPosScaleData[] posScaleOctals;
        public ComputeBuffer negPosOctalCountersBuffer;
    }

    public class OctalReadbackExecutor : VolumeExecutor<OctalReadbackExecutorParameters> {
        private ComputeBuffer posScaleOctalBuffer;

        public OctalReadbackExecutor() : base(VoxelUtils.SIZE * VoxelUtils.OCTAL_CHUNK_SIZE_RATIO) {
        }

        public override void DisposeResources() {
            base.DisposeResources();
            posScaleOctalBuffer?.Dispose();
        }

        protected override void CreateResources(ManagedTerrainCompiler compiler) {
            base.CreateResources(compiler);
            posScaleOctalBuffer = new ComputeBuffer(VoxelUtils.OCTAL_CHUNK_COUNT, sizeof(int) * 4, ComputeBufferType.Structured);
            buffers.Add("voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxels" }, new ComputeBuffer(VoxelUtils.VOLUME * VoxelUtils.OCTAL_CHUNK_COUNT, Voxel.size, ComputeBufferType.Structured)));
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, ManagedTerrainSeeder seeder, OctalReadbackExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, seeder, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.OctalReadback);
            commands.SetBufferData(posScaleOctalBuffer, parameters.posScaleOctals);
            commands.SetComputeBufferParam(shader, kernelIndex, "pos_scale_octals", posScaleOctalBuffer);

            commands.SetBufferData(parameters.negPosOctalCountersBuffer, new int[VoxelUtils.OCTAL_CHUNK_COUNT]);
            commands.SetComputeBufferParam(shader, kernelIndex, "neg_pos_octal_counters", parameters.negPosOctalCountersBuffer);
        }
    }
}
