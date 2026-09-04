using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class UserOccludableAuthoring : MonoBehaviour {
    }

    class UserOccludableBaker : Baker<UserOccludableAuthoring> {
        public override void Bake(UserOccludableAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.Renderable | TransformUsageFlags.Dynamic);
            AddComponent<UserOccludableTag>(self);
            AddComponent<OccludableTag>(self);
            SetComponentEnabled<OccludableTag>(self, false);
        }
    }
}