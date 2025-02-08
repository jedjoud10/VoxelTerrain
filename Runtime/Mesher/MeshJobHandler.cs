using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// Contains the allocation data for a single job
// There are multiple instances of this class stored inside the voxel mesher to saturate the other threads
internal class MeshJobHandler {
    // Copy of the voxel data that we will use for meshing
    public NativeArray<Voxel> voxels;

    // Native buffers for mesh data
    public NativeArray<float3> vertices;
    public NativeArray<float3> normals;
    public NativeArray<float2> uvs;
    public NativeArray<int> tempTriangles;
    public NativeArray<int> permTriangles;

    // Native buffer for mesh generation data
    public NativeArray<int> indices;
    public NativeArray<byte> enabled;
    public NativeMultiCounter countersQuad;
    public NativeCounter counter;
    public NativeMultiCounter voxelCounters;

    // Native buffer for handling multiple materials
    public NativeParallelHashMap<ushort, int> materialHashMap;
    public NativeParallelHashSet<ushort> materialHashSet;
    public NativeArray<int> materialSegmentOffsets;
    public NativeCounter materialCounter;
    public JobHandle finalJobHandle;
    public VoxelChunk chunk;
    public bool colisions = false;
    public int startingFrame = 0;
    public int maxFrames = 0;
    internal VertexAttributeDescriptor[] vertexAttributeDescriptors;

    internal MeshJobHandler() {
        // Native buffers for mesh data
        int materialCount = VoxelUtils.MAX_MATERIAL_COUNT;
        voxels = new NativeArray<Voxel>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        vertices = new NativeArray<float3>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        normals = new NativeArray<float3>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        uvs = new NativeArray<float2>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        tempTriangles = new NativeArray<int>(VoxelUtils.Volume * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        permTriangles = new NativeArray<int>(VoxelUtils.Volume * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        voxelCounters = new NativeMultiCounter(materialCount, Allocator.Persistent);

        // Native buffer for mesh generation data
        indices = new NativeArray<int>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        enabled = new NativeArray<byte>(VoxelUtils.Volume, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        countersQuad = new NativeMultiCounter(materialCount, Allocator.Persistent);
        counter = new NativeCounter(Allocator.Persistent);

        // Native buffer for handling multiple materials
        materialHashMap = new NativeParallelHashMap<ushort, int>(materialCount, Allocator.Persistent);
        materialHashSet = new NativeParallelHashSet<ushort>(materialCount, Allocator.Persistent);
        materialSegmentOffsets = new NativeArray<int>(materialCount, Allocator.Persistent);
        materialCounter = new NativeCounter(Allocator.Persistent);

        VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
        VertexAttributeDescriptor uvDesc = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);

        List<VertexAttributeDescriptor> descriptors = new List<VertexAttributeDescriptor> {
            positionDesc
        };
        if (VoxelUtils.PerVertexNormals)
            descriptors.Add(normalDesc);
        if (VoxelUtils.PerVertexUvs)
            descriptors.Add(uvDesc);
        vertexAttributeDescriptors = descriptors.ToArray();
    }
    public bool Free { get; private set; } = true;

    // Begin the vertex + quad job that will generate the mesh
    internal JobHandle BeginJob(JobHandle dependency) {
        countersQuad.Reset();
        counter.Count = 0;
        materialCounter.Count = 0;
        materialHashSet.Clear();
        materialHashMap.Clear();
        Free = false;

        // Handles fetching MC corners for the SN edges
        CornerJob cornerJob = new CornerJob {
            voxels = voxels,
            enabled = enabled,
            size = VoxelUtils.Size,
        };

        // Calculates the number of materials within the mesh
        MaterialJob materialJob = new MaterialJob {
            voxels = voxels,
            materialHashSet = materialHashSet.AsParallelWriter(),
            materialHashMap = materialHashMap.AsParallelWriter(),
            materialCounter = materialCounter,
        };

        // Generate the vertices of the mesh
        // Executed only onces, and shared by multiple submeshes
        VertexJob vertexJob = new VertexJob {
            enabled = enabled,
            voxels = voxels,
            indices = indices,
            vertices = vertices,
            normals = normals,
            uvs = uvs,
            counter = counter,
            voxelScale = VoxelUtils.VoxelSizeFactor,
            vertexScale = VoxelUtils.VertexScaling,
            size = VoxelUtils.Size,
            smoothing = VoxelUtils.Smoothing,
            perVertexNormals = VoxelUtils.PerVertexNormals,
            perVertexUvs = VoxelUtils.PerVertexUvs,
            aoOffset = VoxelUtils.AmbientOcclusionOffset,
            aoPower = VoxelUtils.AmbientOcclusionPower,
            aoSpread = VoxelUtils.AmbientOcclusionSpread,
            aoGlobalOffset = VoxelUtils.AmbientOcclusionGlobalOffset,
        };

        // Generate the quads of the mesh (handles materials internally)
        QuadJob quadJob = new QuadJob {
            enabled = enabled,
            voxels = voxels,
            vertexIndices = indices,
            counters = countersQuad,
            triangles = tempTriangles,
            materialHashMap = materialHashMap.AsReadOnly(),
            materialCounter = materialCounter,
            size = VoxelUtils.Size,
        };

        // Create sum job to calculate offsets for each material type 
        SumJob sumJob = new SumJob {
            materialCounter = materialCounter,
            materialSegmentOffsets = materialSegmentOffsets,
            countersQuad = countersQuad
        };

        // Create a copy job that will copy temp memory to perm memory
        CopyJob copyJob = new CopyJob {
            materialSegmentOffsets = materialSegmentOffsets,
            tempTriangles = tempTriangles,
            permTriangles = permTriangles,
            materialCounter = materialCounter,
            counters = countersQuad,
        };

        // Start the corner job
        JobHandle cornerJobHandle = cornerJob.Schedule(VoxelUtils.Volume, 2048 * 16, dependency);

        // Start the material job
        JobHandle materialJobHandle = materialJob.Schedule(VoxelUtils.Volume, 2048 * 16, dependency);

        // Start the vertex job
        JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency);
        JobHandle vertexJobHandle = vertexJob.Schedule(VoxelUtils.Volume, 2048 * 16, vertexDep);

        // Start the quad job
        JobHandle merged = JobHandle.CombineDependencies(materialJobHandle, vertexJobHandle, cornerJobHandle);
        JobHandle quadJobHandle = quadJob.Schedule(VoxelUtils.Volume, 2048 * 16, merged);

        // Start the sum job 
        JobHandle sumJobHandle = sumJob.Schedule(VoxelUtils.MAX_MATERIAL_COUNT, 32, quadJobHandle);

        // Start the copy job
        JobHandle copyJobHandle = copyJob.Schedule(VoxelUtils.MAX_MATERIAL_COUNT, 1, sumJobHandle);

        finalJobHandle = copyJobHandle;
        return finalJobHandle;
    }

    // Complete the jobs and return a mesh
    internal VoxelMesh Complete(Mesh mesh) {
        if (voxels == null || chunk == null) {
            return VoxelMesh.Empty;
        }

        finalJobHandle.Complete();
        Free = true;

        // Get the max number of materials we generated for this mesh
        int maxMaterials = materialCounter.Count;

        // Get the max number of vertices (shared by submeshes)
        int maxVertices = counter.Count;

        // Count the max number of indices (sum of all submesh indices)
        int maxIndices = 0;

        // Count the number of indices we will have in maximum (all material indices combined)
        for (int i = 0; i < maxMaterials; i++) {
            maxIndices += countersQuad[i] * 6;
        }

        // Set mesh shared vertices
        mesh.Clear();

        mesh.SetVertexBufferParams(maxVertices, vertexAttributeDescriptors);
        mesh.SetVertexBufferData(vertices.Reinterpret<Vector3>(), 0, 0, maxVertices, 0, MeshUpdateFlags.DontValidateIndices);
        
        if (VoxelUtils.PerVertexNormals)
            mesh.SetVertexBufferData(normals.Reinterpret<Vector3>(), 0, 0, maxVertices, 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

        if (VoxelUtils.PerVertexUvs)
            mesh.SetVertexBufferData(uvs.Reinterpret<Vector2>(), 0, 0, maxVertices, 2, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

        // Set mesh indices
        mesh.SetIndexBufferParams(maxIndices, IndexFormat.UInt32);
        mesh.SetIndexBufferData(permTriangles, 0, 0, maxIndices);
        mesh.subMeshCount = maxMaterials;

        // Create a material array for the new materials
        int[] lookup = new int[maxMaterials];

        // Convert material index to material *count* index
        foreach (var item in materialHashMap) {
            lookup[item.Value] = item.Key;
        }

        // Set mesh submeshes
        for (int i = 0; i < maxMaterials; i++) {
            int countIndices = countersQuad[i] * 6;
            int segmentOffset = materialSegmentOffsets[i];

            if (countIndices > 0) {
                mesh.SetSubMesh(i, new SubMeshDescriptor {
                    indexStart = segmentOffset,
                    indexCount = countIndices,
                    topology = MeshTopology.Triangles,
                }, MeshUpdateFlags.DontValidateIndices);
            }
        }

        chunk = null;
        return new VoxelMesh {
            VoxelMaterialsLookup = lookup,
            ComputeCollisions = colisions,
            VertexCount = maxVertices,
            TriangleCount = maxIndices / 3,
        };
    }

    // Dispose of the underlying memory allocations
    internal void Dispose() {
        voxels.Dispose();
        indices.Dispose();
        vertices.Dispose();
        normals.Dispose();
        uvs.Dispose();
        counter.Dispose();
        countersQuad.Dispose();
        tempTriangles.Dispose();
        permTriangles.Dispose();
        materialCounter.Dispose();
        materialHashMap.Dispose();
        materialHashSet.Dispose();
        materialSegmentOffsets.Dispose();
        enabled.Dispose();
        voxelCounters.Dispose();
    }
}