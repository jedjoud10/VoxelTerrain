using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance)]
    public struct NormalsCalculateJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<half> baseVal;
        [ReadOnly]
        public NativeArray<half> xVal;
        [ReadOnly]
        public NativeArray<half> yVal;
        [ReadOnly]
        public NativeArray<half> zVal;

        [WriteOnly]
        public NativeArray<float3> normals;

        // TODO: for some reason really does not want to vectorize this... :(
        public void Execute(int index) {
            float src = baseVal[index];
            float x = xVal[index];
            float y = yVal[index];
            float z = zVal[index];
            float3 normal = math.normalizesafe(new float3(x - src, y - src, z - src), math.up());

            normals[index] = normal;
        }
    }
}