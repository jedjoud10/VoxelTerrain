using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh baking jobs
    // Can also be used to check for collisions based on the stored voxel data (needed for props)
    public class VoxelCollisions : VoxelBehaviour {
        public delegate void OnCollisionBakingComplete(VoxelChunk chunk);
        public event OnCollisionBakingComplete onCollisionBakingComplete;
        internal List<(JobHandle, VoxelChunk)> ongoingBakeJobs;
        private Dictionary<int, VoxelChunk> instanceIDs;

        public override void CallerStart() {
            Physics.ContactModifyEvent += CustomTerrainPhysicsContactEvent;
            ongoingBakeJobs = new List<(JobHandle, VoxelChunk)>();
            instanceIDs = new Dictionary<int, VoxelChunk>();
        }

        public override void CallerDispose() {
            Physics.ContactModifyEvent -= CustomTerrainPhysicsContactEvent;
        }

        public void GenerateCollisions(VoxelChunk chunk, VoxelMesh voxelMesh) {
            if (voxelMesh.VertexCount > 0 && voxelMesh.TriangleCount > 0 && voxelMesh.ComputeCollisions) {
                BakeJob bakeJob = new BakeJob {
                    meshId = chunk.sharedMesh.GetInstanceID(),
                };

                var handle = bakeJob.Schedule();
                ongoingBakeJobs.Add((handle, chunk));
            } else {
                onCollisionBakingComplete?.Invoke(chunk);
            }
        }

        public override void CallerTick() {
            foreach (var (handle, chunk) in ongoingBakeJobs) {
                if (handle.IsCompleted) {
                    handle.Complete();
                    MeshCollider collider = chunk.GetComponent<MeshCollider>();
                    collider.hasModifiableContacts = true;
                    collider.sharedMesh = chunk.sharedMesh;
                    onCollisionBakingComplete?.Invoke(chunk);
                    int instanceID = collider.GetInstanceID();
                    instanceIDs.TryAdd(instanceID, chunk);
                }
            }
            ongoingBakeJobs.RemoveAll(item => item.Item1.IsCompleted);
        }

        private void CustomTerrainPhysicsContactEvent(PhysicsScene arg1, NativeArray<ModifiableContactPair> pairs) {
            void Check(int instanceID, ModifiableContactPair pair) {
                if (instanceIDs.TryGetValue(instanceID, out VoxelChunk chunk)) {
                    for (int i = 0; i < pair.contactCount; i++) {
                        uint face = pair.GetFaceIndex(i);

                        if (chunk.TryLookupMaterialTriangleIndex((int)face * 3, out byte material)) {
                            pair.SetDynamicFriction(i, terrain.materials[material].dynamicFriction);
                            pair.SetStaticFriction(i, terrain.materials[material].staticFriction);
                            pair.SetBounciness(i, terrain.materials[material].bounce);
                        }
                    }
                }
            }

            foreach (var pair in pairs) {
                if (pair.colliderInstanceID != pair.otherColliderInstanceID) {
                    Check(pair.colliderInstanceID, pair);
                    Check(pair.otherColliderInstanceID, pair);
                }
            }
        }
    }
}