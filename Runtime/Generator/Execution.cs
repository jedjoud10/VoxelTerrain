using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public partial class VoxelGenerator : VoxelBehaviour {
    [Header("Seeding")]
    public int seed = 1234;
    public Vector3Int permutationSeed;
    public Vector3Int moduloSeed;

    [HideInInspector]
    [NonSerialized]
    public Dictionary<string, ExecutorTexture> textures;

    [HideInInspector]
    [NonSerialized]
    public List<Texture> texturesTest;
    private int setSize;

    private void DisposeIntermediateTextures() {
        if (textures != null) {
            foreach (var (name, tex) in textures) {
                if (tex.texture is RenderTexture casted) {
                    casted.Release();
                }
            }

            textures = null;
        }
    }

    // Create intermediate textures (cached, gradient) to be used for voxel graph shader execution
    // Texture size will correspond to execution size property
    private void CreateIntermediateTextures(int size) {
        DisposeIntermediateTextures();

        // Creates dictionary with the default voxel graph textures (density + custom data)
        textures = new Dictionary<string, ExecutorTexture> {
            { "voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R16_SFloat)) },
            { "colors", new OutputExecutorTexture("colors", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R8G8B8A8_UNorm)) },
        };

        foreach (var (name, descriptor) in ctx.textures) {
            textures.Add(name, descriptor.Create(size));
        }

        texturesTest = textures.Values.AsEnumerable().Select((x) => x.texture).ToList();
    }

    public void ExecuteShader(int newSize, Vector3 offset, Vector3 scale, bool morton, bool updateInjected) {
        if (ctx == null) {
            ParsedTranspilation();
        }

        if (newSize != setSize || textures == null) {
            setSize = newSize;
            CreateIntermediateTextures(newSize);
        }

        shader.SetInt("size", newSize);
        shader.SetInts("permuationSeed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
        shader.SetInts("moduloSeed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });
        shader.SetVector("offset", offset);
        shader.SetVector("scale", scale);
        shader.SetBool("morton", morton);

        if (updateInjected) {
            ctx.injector.UpdateInjected(shader, textures);
        }

        foreach (var (name, texture) in textures) {
            texture.BindToComputeShader(shader);
        }

        /*
        CommandBuffer buffer = new CommandBuffer();
        buffer.name = "Execute Terrain Generator Dispatches";
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        */
        // Execute the kernels sequentially
        foreach (var kernel in ctx.dispatches) {
            int id = shader.FindKernel(kernel.name);
            int tempSize = newSize / (1 << kernel.sizeReductionPower);
            tempSize = Mathf.Max(tempSize, 1);

            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            int minScaleBase2D = Mathf.CeilToInt((float)tempSize / 32.0f);

            if (kernel.threeDimensions) {
                //buffer.DispatchCompute(shader, id, minScaleBase2D, minScaleBase2D, 1);
                //buffer.DispatchCompute(shader, id, minScaleBase3D, minScaleBase3D, minScaleBase3D);
                shader.Dispatch(id, minScaleBase3D+1, minScaleBase3D+1, minScaleBase3D+1);
            } else {
                //buffer.DispatchCompute(shader, id, minScaleBase2D, minScaleBase2D, 1);
                shader.Dispatch(id, minScaleBase2D, minScaleBase2D, 1);
            }

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(shader, id);
            }
        }

        // TODO: figure out how 2 do this pls???
        //Graphics.ExecuteCommandBufferAsync(buffer, UnityEngine.Rendering.ComputeQueueType.Background);
    }

    private void ComputeSecondarySeeds() {
        var random = new System.Random(seed);
        permutationSeed.x = random.Next(-1000, 1000);
        permutationSeed.y = random.Next(-1000, 1000);
        permutationSeed.z = random.Next(-1000, 1000);
        moduloSeed.x = random.Next(-1000, 1000);
        moduloSeed.y = random.Next(-1000, 1000);
        moduloSeed.z = random.Next(-1000, 1000);
    }

    public void RandomizeSeed() {
        seed = UnityEngine.Random.Range(-9999, 9999);
        ComputeSecondarySeeds();
        OnPropertiesChanged();
    }
}