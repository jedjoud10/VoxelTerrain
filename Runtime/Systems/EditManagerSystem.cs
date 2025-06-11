using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateBefore(typeof(EditStoreSystem))]
    [UpdateBefore(typeof(EditApplySystem))]
    public partial class EditManagerSystem : SystemBase {
        public TerrainEdits singleton;
        const float BOUNDS_EXPAND_OFFSET = 2f;

        protected override void OnCreate() {
            singleton = new TerrainEdits {
                chunkPositionsToChunkEditIndices = new NativeHashMap<int3, int>(0, Allocator.Persistent),
                chunkEdits = new UnsafeList<NativeArray<Voxel>>(0, Allocator.Persistent),
                applySystemHandle = default,
                registry = new EditTypeRegistry(this),
            };
            EntityManager.CreateSingleton<TerrainEdits>(singleton);

            singleton.registry.Register<TerrainSphereEdit>(this);
        }

        protected override void OnDestroy() {
            TerrainEdits backing = SystemAPI.ManagedAPI.GetSingleton<TerrainEdits>();

            backing.chunkPositionsToChunkEditIndices.Dispose();

            foreach (var editData in backing.chunkEdits) {
                editData.Dispose();
            }

            backing.chunkEdits.Dispose();
        }

        protected override void OnUpdate() {
            singleton.registry.Update(this);
        }

        public static void CreateEditEntity<T>(EntityManager mgr, T edit) where T: unmanaged, IComponentData, IEdit {
            Entity entity = mgr.CreateEntity();
            mgr.AddComponent<TerrainEdit>(entity);
            mgr.AddComponent<TerrainEditBounds>(entity);
            mgr.AddComponent<T>(entity);

            MinMaxAABB bounds = edit.GetBounds();
            bounds.Expand(BOUNDS_EXPAND_OFFSET);

            mgr.SetComponentData<TerrainEdit>(entity, new TerrainEdit { type = ComponentType.ReadOnly<T>().TypeIndex });
            mgr.SetComponentData<TerrainEditBounds>(entity, new TerrainEditBounds() { bounds = bounds });
            mgr.SetComponentData<T>(entity, edit);
        }

        public static void CreateEditEntity<T>(EntityCommandBuffer ecb, T edit) where T : unmanaged, IComponentData, IEdit {
            Entity entity = ecb.CreateEntity();

            MinMaxAABB bounds = edit.GetBounds();
            bounds.Expand(BOUNDS_EXPAND_OFFSET);

            ecb.AddComponent<TerrainEdit>(entity, new TerrainEdit { type = ComponentType.ReadOnly<T>().TypeIndex });
            ecb.AddComponent<TerrainEditBounds>(entity, new TerrainEditBounds() { bounds = bounds });
            ecb.AddComponent<T>(entity, edit);
        }
    }
}