using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.NoiseVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {

    // Add random noise to the terrain as a voxel edit
    public struct NoiseVoxelEdit : IVoxelEdit {
        public enum NoiseType {
            Snoise,
            CellularF1,
            CellularF2
        }

        public enum Dimensionality {
            Two,
            Three,
        }

        [ReadOnly] public float3 center;
        [ReadOnly] public float noiseScale;
        [ReadOnly] public NoiseType noiseType;
        [ReadOnly] public Dimensionality dimensionality;
        [ReadOnly] public float strength;
        [ReadOnly] public float radius;

        public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, NativeMultiCounter counters) {
            return IVoxelEdit.ApplyGeneric(this, offset, voxels, counters);
        }

        public Bounds GetBounds() {
            return new Bounds {
                center = center,
                extents = new Vector3(radius, radius, radius)
            };
        }

        public Voxel Modify(float3 position, Voxel voxel) {
            float density = math.length(position - center) - radius;
            float falloff = math.saturate(-(density / radius));

            float noiseVal = 0.0f;
            switch (dimensionality) {
                case Dimensionality.Two:
                    switch (noiseType) {
                        case NoiseType.Snoise:
                            noiseVal = noise.snoise(position.xz * noiseScale);
                            break;
                        case NoiseType.CellularF1:
                            noiseVal = noise.cellular(position.xz * noiseScale).x;
                            break;
                        case NoiseType.CellularF2:
                            noiseVal = noise.cellular(position.xz * noiseScale).y;
                            break;
                        default:
                            break;
                    }
                    break;
                case Dimensionality.Three:
                    switch (noiseType) {
                        case NoiseType.Snoise:
                            noiseVal = noise.snoise(position * noiseScale);
                            break;
                        case NoiseType.CellularF1:
                            noiseVal = noise.cellular(position * noiseScale).x;
                            break;
                        case NoiseType.CellularF2:
                            noiseVal = noise.cellular(position * noiseScale).y;
                            break;
                        default:
                            break;
                    }
                    break;
            }


            voxel.density += (half)(strength * noiseVal * falloff);
            return voxel;
        }
    }
}