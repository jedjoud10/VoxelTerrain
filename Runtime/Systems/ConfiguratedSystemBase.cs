using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    public abstract partial class ConfiguratedSystemBase<T> : SystemBase where T: unmanaged, IComponentData {
        private bool initialized;
        
        protected override void OnCreate() {
            RequireForUpdate<T>();
            initialized = false;
        }

        public abstract void OnCreateConfigurated(T config);
        public abstract void OnUpdateConfigurated(T config);
        public abstract void OnDestroyConfigurated(T config, bool initialized);

        protected override void OnUpdate() {
            T config = SystemAPI.GetSingleton<T>();

            if (!initialized) {
                OnCreateConfigurated(config);
                initialized = true;
            }

            OnUpdateConfigurated(config);
        }
        protected override void OnDestroy() {
            if (initialized) {
                T config = SystemAPI.GetSingleton<T>();
                OnDestroyConfigurated(config, true);
            } else {
                OnDestroyConfigurated(default, false);
            }
        }
    }
}