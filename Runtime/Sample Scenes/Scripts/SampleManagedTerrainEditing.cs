using jedjoud.VoxelTerrain;
using jedjoud.VoxelTerrain.Edits;
using jedjoud.VoxelTerrain.Serialization;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class TestTerrainEditor : MonoBehaviour
{
    void Update()
    {
        EntityManager mgr = World.DefaultGameObjectInjectionWorld.EntityManager;
        bool add = Input.GetKey(KeyCode.Q);

        if (Input.GetMouseButtonDown(0)) {
            EditUtils.CreateEditEntity(mgr, new TerrainSphereEdit {
                center = transform.position + transform.forward * 2f,
                radius = 2f,
                layers = 1,
                add = add
            });
        }

        if (Input.GetMouseButton(1)) {
            EditUtils.CreateEditEntity(mgr, new TerrainAddEdit {
                center = transform.position + transform.forward * 2f,
                radius = 2f,
                strength = 2f,
                layers = 1,
                add = add
            });
        }

        if (Input.GetKeyDown(KeyCode.H)) {
            DoRaycastAndDestroyEntity();
        }


        if (Input.GetKeyDown(KeyCode.L)) {
            mgr.CreateSingleton<TerrainSerializeTag>();
        }
    }

    void DoRaycastAndDestroyEntity() {
        var world = World.DefaultGameObjectInjectionWorld;
        var query = world.EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        var singleton = query.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = singleton.CollisionWorld;

        float3 rayOrigin = transform.position;
        float3 rayDirection = transform.forward;
        float maxDistance = 100f;

        var rayInput = new RaycastInput {
            Start = rayOrigin,
            End = rayOrigin + rayDirection * maxDistance,
            Filter = new CollisionFilter {
                BelongsTo = ~0u, // Everything
                CollidesWith = ~0u,
                GroupIndex = 0
            }
        };

        if (collisionWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit)) {
            Entity hitEntity = singleton.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity;
            EntityManager mgr = world.EntityManager;

            if (mgr.Exists(hitEntity)) {
                mgr.DestroyEntity(hitEntity);
            }
        }
    }
}
