using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    [BurstCompile(CompileSynchronously = true)]
    public struct RasterizeJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<bool> insideSurfaceVoxels;
        public float4x4 proj;
        public float4x4 view;
        public float4x4 invProj;
        public float4x4 invView;
        public float3 cameraPosition;
        public float2 nearFarPlanes;

        [WriteOnly]
        public NativeArray<float> screenDepth;

        public void Execute(int index) {
            screenDepth[index] = 1f;

            int x = index % OcclusionUtils.WIDTH;
            int y = index / OcclusionUtils.WIDTH;
            float2 uvs = new float2(x, y) / new float2(OcclusionUtils.WIDTH - 1, OcclusionUtils.HEIGHT - 1);
            float4 clip = new float4(uvs * 2f - 1f, 1f, 1f);
            float4 rayView = math.mul(invProj, clip);
            rayView /= rayView.w;
            float3 rayDir = math.normalize(math.mul(invView, new float4(rayView.xyz, 0)).xyz);

            float3 rayPos = cameraPosition + 0.5f;

            float3 invDir = math.rcp(rayDir);
            float3 dirSign = math.sign(rayDir);

            float3 flooredPos = math.floor(rayPos);
            float3 sideDist = flooredPos - rayPos + 0.5f + 0.5f * dirSign;

            for (int i = 0; i < OcclusionUtils.SIZE*2; i++) {
                int3 voxelPos = (int3)flooredPos;

                int3 pos = voxelPos;
                pos -= (int3)math.floor(cameraPosition);
                pos += OcclusionUtils.SIZE / 2;
                
                if (VoxelUtils.CheckPositionInsideVolume(pos, OcclusionUtils.SIZE)) {
                    if (insideSurfaceVoxels[VoxelUtils.PosToIndex((uint3)pos, OcclusionUtils.SIZE)]) {
                        float3 test = (flooredPos - rayPos + 0.5f - 0.5f * dirSign) * invDir;
                        float max = math.cmax(test);
                        float3 world = rayPos + rayDir * max;

                        float4 clipPos = math.mul(proj, math.mul(view, new float4(world, 1.0f)));
                        clipPos /= clipPos.w;
                        screenDepth[index] = math.saturate(OcclusionUtils.LinearizeDepthStandard(clipPos.z, nearFarPlanes));
                        return;
                    }
                } else {
                    return;
                }

                float3 reconst = sideDist * invDir;
                float3 eqs = math.select(0f, 1f, new float3(math.cmin(reconst)) == reconst);
                sideDist += dirSign * eqs;
                flooredPos += dirSign * eqs;
            }
        }
    }
}