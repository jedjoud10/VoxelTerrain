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
                    var job = new EditStoreJob2<T> {
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

        public List<EditTypeDynDispatch> registry;

        public void Register<T>(SystemBase system) where T: unmanaged, IEdit, IComponentData {
            registry.Add(new Bruh<T>() {
                lookup = system.GetComponentLookup<T>(true),
            });
        }

        public void Update(SystemBase system) {
            foreach (var registered in registry) {
                registered.Update(system);
            }
        }

        public JobHandle ApplyEdit(Entity entity, NativeArray<Voxel> voxels, int3 chunkOffset, JobHandle dep) {
            // in case the entity contains more than one registered edit component
            foreach (var registered in registry) {
                dep = registered.Apply(entity, voxels, chunkOffset, dep);
            }

            return dep;
        }
    }
}