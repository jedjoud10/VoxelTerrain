using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// CPU representation of what a voxel is. The most important value here is the density value
[StructLayout(LayoutKind.Sequential)]
public struct Voxel {
    public const int size = sizeof(int);

    // Density of the voxel as a half to save some memory
    public half density;

    // Material of the voxel that depicts its color and other parameters
    public byte material;

    // Used for extra color data on a per vertex basis
    public byte _padding;

    // Empty voxel with the empty material
    public readonly static Voxel Empty = new Voxel {
        density = half.zero,
        material = byte.MaxValue,
        _padding = 0,
    };
}

// Voxel container with custom dispose methods
// (implemented for voxel readback request and voxel edit request)
public abstract class VoxelContainer {
    public NativeArray<Voxel> voxels;

    // Create the voxel container
    public abstract void Init();

    // Dispose of the voxel container
    public abstract void Dispose();
}

// Voxel container that contains a unique voxel native array that is not to be shared with any other chunk
public class UniqueVoxelContainer : VoxelContainer {
    public override void Init() {
        voxels = new NativeArray<Voxel>(VoxelUtils.Volume, Allocator.Persistent);
    }

    public override void Dispose() {
        voxels.Dispose();
    }
}