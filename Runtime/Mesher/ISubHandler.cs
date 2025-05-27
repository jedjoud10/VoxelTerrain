using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    public interface ISubHandler {
        public abstract void Init();
        public abstract void Dispose();
    }
}