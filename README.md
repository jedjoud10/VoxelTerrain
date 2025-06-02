# New Features compared to the main branch:
- Chunk size of 32, with some pros:
  - Faster meshing (sub 1ms on 14 threads for 1-2 chunks, pretty darn good, though could be fasteerrrrr).
  - We can now compute 64 chunks on the GPU all at *once* in the same compute dispatch instead of 8. This speeds up meshing a lot since we catch empty chunks and discard them early.
  - Faster rendering (less triangles). Does make mushy terrain since we don't have any proper terrain-wide normals but I'm only going to use this for low-poly games so whatever lol.
  - MUCH MUCH MUCH FASTER MESH COLLIDER BAKING!!!! (8ms vs 40ms) Why is it so fucking slow with a chunk size of 64??? Unity ECS Physics gotta lock in frfr.
- Async Compute Queue Support (DX12 works on Win11, Vulkan not workey (plus also very slow for some reason)) with fallback to normal queues. Used for practically all the new compute shaders
- Proper Surface Nets Skirts by running 2D and 1D S.N on the chunk boundary. Skirt entities are enabled/disabled based on the direction that they face relative to the octree loader position.
  - Implemented fallback normals system for flat shading so that skirts aren't as visible when you use DDY/DDX normals
- Graph system. You can write the voxel generation code in C# and a *transplire* will compile it to HLSL in the editor which will then compile to GPU executable code.Basically a preprocessor "find and replace" but on steroids
  - Allows you to simply define layers of noise that you add on top of each other like so:
  ```cs
  // Create simplex and fractal noise
  Simplex<float2> simplex = new Simplex<float2>(scale, amplitude);
  Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, others);

  // Execute fractal noise as 2D function
  Variable<float> density = fractal.Evaluate(xz) + y;

  // Create some extra "detail" voronoi noise
  Voronoi<float3> voronoi = new Voronoi<float3>(voronoiScale, voronoiAmplitude);
  density += voronoi.Evaluate(projected);
  ```

- Better optimized meshing jobs:
  - Optimized corner (mc-mask opt) & check job (bitsetter) using custom intrinsics that actually do something!!! (profiled).
  - Optimized normal job by splitting it into a "prefetch" part and a "calculate" part. Prefetcher is vectorized by Burst, but not the calculate part (need to fix).

- Better (but slower) prop generation:
  - Improved surface detection: since we use a graph based system, we can now just fetch the voxel density at any given point, without having to write to a texture first. This allows us to use binary search with prop surface generation to place the props *exactly* on the surface of the terrain. Much better than the previous iteration.
  - Improved normals: Uses finite differences but with the slow density fetch instead of the cached one, so better quality normals!!!
  - Copy and culling compute are now asynchronous! (not like they are slow lol but that's nice anyways)
  - Free memory block lookup is now on the CPU instead of GPU. Yes this does mean that we *need* to do counts readback, but considering that we're only reading a few ints (only a few) this is fine. Drops the complexity of the prop copy system by a lot by doing this on the CPU, worth the few frames of latency.

# TODO / Ideas
- *Some* VXAO. Currently disabled with the octree system since it is not only very slow but also requires re-meshing every-time we get a new neighbour
  - If we decouple "neighbour-fetching" jobs (like AO and a possible light propagation system) from our main meshing we could avoid having to recalculate the WHOLE mesh and instead only modify the vertices
- Figure out how to handle per voxel color (nointerpolation in shader trick)
- Biome generation (custom data readback?)
- Custom graph buffer initialization and readback (material, color, smoothness / metallic, custom user data)
  - Works for props, but I don't know if I should extend it to make it work with custom user input
- Implement smart range checking using texture value summation (cached textures)
- Switch the voxel data storage to ``SoA`` instead of ``AoS``, should help with cache hits, though we can only read ``AoS`` data from the GPU, so we'll need to do some packing/unpacking (shouldn't be that expensive).
- Do some async chunk culling!!
  - There's this for caves: https://tomcc.github.io/2014/08/31/visibility-1.html
  - For surface chunks, ig do some funky stuff with bounds?  
  - You can also do software rasterization with Burst and do some DDA / octree shenanigans
- Do some material variant stuff with multiple optional UV channels on a PER CHUNK basis
  - In total we could have up 15 material variants per chunk (since 4 uv with 4 floats/half each, minus the single AO channel)
  - We can do the same dedupe/lookup system as normal material values.
  - Maybe rename "materials" to shaders and "material variants" to materials? would make more sense...
- Do some sort of Minecraft-style spreading lighting calculations

# Current Screenies
Runtime terrain gen with some shiddy old props
![Screenshot 2025-04-23 162113](https://github.com/user-attachments/assets/69548b73-7dc9-409a-85c0-98f5f2279cc6)
![Screenshot 2025-04-23 162127](https://github.com/user-attachments/assets/4e6c4f6e-8cac-418a-8f66-9f0612d59771)

Better pic with the new prop generator + instanced indirect + async GPU culling
![Screenshot 2025-05-31 202859](https://github.com/user-attachments/assets/9da85ead-e4b7-46e7-8fd7-caf62ca1fd86)

Editor GPU preview & material ID colouring
![image](https://github.com/user-attachments/assets/79bbe315-f015-403e-a6d6-1ea756db7128)

also supports height-map simplification (which is what I think I will use to replace LODs)
![image](https://github.com/user-attachments/assets/c65636cb-95a2-4b03-972e-db5864f594c5)

the C# graph that was used for the terrain. This gets converted to HLSL and then executed on the GPU.
![image](https://github.com/user-attachments/assets/7180872a-1e4f-4311-9d18-f4895e9aa1a6)

screenie with some triplanar texturing (thanks to PolyHaven)
![image](https://github.com/user-attachments/assets/5232947a-bc81-4e0e-91bf-36166efdcc71)

vehicle demo test (posted on subreddit a few days ago)
![image](https://github.com/user-attachments/assets/1857ebc8-06d1-475d-ba71-c39e9e05c515)


# Credits
- PolyHaven for their free awesome PBR 4k textures and models. I used these in the "Default" folder for default testing materials and some default props (non shiddy)
