# TODO
- Figure out how to handle per voxel color (nointerpolation in shader trick)
- Re-implement AO calculations (but based on a larger scale / blurred voxel repr.)
- Re-implement props with hopefully better implementation
  - Currently WIP, got prop readback working, but need to clean up that mess graph wise
- Wait until Shader Graph supports indirect indexed rendering
  - This would allow us to generate meshes on the GPU and render them directly
- Biome generation (custom data readback?)
- Custom graph buffer initialization and readback (material, color, smoothness / metallic, custom user data)
  - Works for props, but I don't know if I should extend it to make it work with custom user input
- Figure out ``CachedVar`` node stuff
  - Ok figured it out, you just can't make a lower resolution kernel (unless you want to use point sampling)
  - Well, no, not really. You *can* make a lower resolution kernel, but it needs to have n+1 dispatches in the axii instead of n dispatches because bilinear sampling the lower-res texture needs that extra texel for the border. This is also true for finite-difference normal calculation, so maybe we can make that use a cached node internally? 
- Implement some sort of ``Diff`` node that create intermediate texture of size ``n+1``, and calculates finite diffs. from it.
- Figure out how to get Async Compute Queue working (only seems to work in DX12, didn't test on Vulkan). Async readback works on DX11 as well.
- Implement smart range checking using texture value summation (cached textures)
- Go octal mode and add inter chunk dependency unfortunately (will be good though since we can read a LOT more data back from the gpu)
- Decide where to use ``SoA`` or ``AoS`` design for voxel data.
  - How much voxel data will we need anyways?
  - If it's a lot then just go with ``SoA``.

# Current Screenies

Runtime terrain gen with some simple props
![Screenshot 2025-04-23 162113](https://github.com/user-attachments/assets/69548b73-7dc9-409a-85c0-98f5f2279cc6)
![Screenshot 2025-04-23 162127](https://github.com/user-attachments/assets/4e6c4f6e-8cac-418a-8f66-9f0612d59771)

Editor GPU preview & material ID colouring
![image](https://github.com/user-attachments/assets/79bbe315-f015-403e-a6d6-1ea756db7128)

also supports height-map simplification (which is what I think I will use to replace LODs)
![image](https://github.com/user-attachments/assets/c65636cb-95a2-4b03-972e-db5864f594c5)

the C# graph that was used for the terrain. This gets converted to HLSL and then executed on the GPU.
![image](https://github.com/user-attachments/assets/7180872a-1e4f-4311-9d18-f4895e9aa1a6)
