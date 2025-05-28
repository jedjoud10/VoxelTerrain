using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [StructLayout(LayoutKind.Sequential)]
    public struct OctalReadbackPosScaleData {
        public Vector3 position;
        public float scale;
    }

    public class OctalReadbackExecutorParameters : ExecutorParameters {
        public OctalReadbackPosScaleData[] posScaleOctals;
    }

    public class OctalReadbackExecutor : Executor<OctalReadbackExecutorParameters> {
        private ComputeBuffer posScaleOctalBuffer;
        public ComputeBuffer negPosOctalCountersBuffer;

        public OctalReadbackExecutor(int size) : base(size) {
        }

        public ComputeBuffer OctalCountersBuffer { get { return negPosOctalCountersBuffer; } }

        public override void DisposeResources() {
            base.DisposeResources();

            if (posScaleOctalBuffer != null) {
                posScaleOctalBuffer.Dispose();
            }

            if (negPosOctalCountersBuffer != null) {
                negPosOctalCountersBuffer.Dispose();
            }
        }

        protected override void CreateMainResources() {
            posScaleOctalBuffer = new ComputeBuffer(VoxelUtils.OCTAL_CHUNK_COUNT, sizeof(int) * 4, ComputeBufferType.Structured);
            negPosOctalCountersBuffer = new ComputeBuffer(VoxelUtils.OCTAL_CHUNK_COUNT, sizeof(int), ComputeBufferType.Structured);
            buffers.Add("voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxel" }, new ComputeBuffer(VoxelUtils.VOLUME * VoxelUtils.OCTAL_CHUNK_COUNT, Voxel.size, ComputeBufferType.Structured)));
        }

        protected override void ExecuteSetCommands(CommandBuffer commands, ComputeShader shader, OctalReadbackExecutorParameters parameters, int dispatchIndex) {
            LocalKeyword keyword = shader.keywordSpace.FindKeyword(ComputeDispatchUtils.OCTAL_READBACK_KEYWORD);
            commands.EnableKeyword(shader, keyword);

            commands.SetBufferData(posScaleOctalBuffer, parameters.posScaleOctals);
            commands.SetComputeBufferParam(shader, dispatchIndex, "pos_scale_octals", posScaleOctalBuffer);

            commands.SetBufferData(negPosOctalCountersBuffer, new int[VoxelUtils.OCTAL_CHUNK_COUNT]);
            commands.SetComputeBufferParam(shader, dispatchIndex, "neg_pos_octal_counters", negPosOctalCountersBuffer);
        }
    }
}
