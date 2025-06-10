using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    public class EditTypeRegistry {
        public abstract class EditTypeDynDispatch {
            public abstract void Update(SystemBase system);
            public abstract JobHandle Apply(Entity entity, NativeArray<Voxel> voxels, int3 chunkOffset, JobHandle dep);
        }

        public class Bruh<T>: EditTypeDynDispatch where T : unmanaged, IEdit, IComponentData {
            public ComponentLookup<T> lookup;

            public override void Update(SystemBase system) {
                lookup.Update(system);
            }

            public override JobHandle Apply(Entity entity, NativeArray<Voxel> voxels, int3 chunkOffset, JobHandle dep) {
                if (lookup.TryGetComponent(entity, out T edit)) {
                    var job = new EditStoreJob<T> {
                        chunkOffset = chunkOffset,
                        edit = edit,
                        voxels = voxels                    
                    };

                    return job.Schedule(VoxelUtils.VOLUME, BatchUtils.SMALLEST_BATCH, dep);
                } else {
                    return dep;
                }
            }
        }

        public ComponentLookup<TerrainEdit> lookup;
        public Dictionary<TypeIndex, EditTypeDynDispatch> registry;

        public EditTypeRegistry(SystemBase system) {
            lookup = system.GetComponentLookup<TerrainEdit>(true);
            registry = new Dictionary<TypeIndex, EditTypeDynDispatch>();
        }

        public void Register<T>(SystemBase system) where T: unmanaged, IEdit, IComponentData {
            registry.Add(ComponentType.ReadOnly<T>().TypeIndex, new Bruh<T>() {
                lookup = system.GetComponentLookup<T>(true),
            });
        }

        public void Update(SystemBase system) {
            lookup.Update(system);
            foreach (var (_, registered) in registry) {
                registered.Update(system);
            }
        }

        public JobHandle ApplyEdit(Entity entity, NativeArray<Voxel> voxels, int3 chunkOffset, JobHandle dep) {
            TypeIndex index = lookup[entity].type;
            dep = registry[index].Apply(entity, voxels, chunkOffset, dep);
            return dep;
        }
    }
}