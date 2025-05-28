using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class ManagedTerrain : MonoBehaviour {
        public static ManagedTerrain instance;

        [HideInInspector]
        public Generation.ManagedTerrainCompiler compiler;

        [HideInInspector]
        public Generation.ManagedTerrainSeeder seeder;

        [HideInInspector]
        public Generation.ManagedTerrainGraph graph;

        void Awake() {
            instance = this;
        }

        void Start() {
            compiler = GetComponent<Generation.ManagedTerrainCompiler>();
            seeder = GetComponent<Generation.ManagedTerrainSeeder>();
            graph = GetComponent<Generation.ManagedTerrainGraph>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() {
            instance = null;
        }
    }
}