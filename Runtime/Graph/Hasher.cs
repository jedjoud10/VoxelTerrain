using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class Hasher {
        public int hash;

        public Hasher() {
            this.hash = 0;
        }

        public void Hash(object val) {
            hash = HashCode.Combine(hash, val.GetHashCode());
        }
    }
}