using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class ManagedTerrainExecutor : MonoBehaviour {
        [Header("Seeding")]
        public int seed = 1234;
        public Vector3Int permutationSeed;
        public Vector3Int moduloSeed;


        private ComputeBuffer posScaleOctalBuffer;
        public ComputeBuffer negPosOctalCountersBuffer;
        private Dictionary<string, ExecutorTexture> textures;
        private Dictionary<string, ExecutorBuffer> buffers;

        public Dictionary<string, ExecutorTexture> Textures { get { return textures; } }
        public Dictionary<string, ExecutorBuffer> Buffers { get { return buffers; } }

        private ManagedTerrainCompiler compiler => GetComponent<ManagedTerrainCompiler>();

        // Cache the size so that we don't need to re-initialize the texture and buffers
        private int setSize;

        public void Start() {
            DisposeResources();
        }

        private void OnDestroy() {
            DisposeResources();
        }

        public void DisposeResources() {
            if (posScaleOctalBuffer != null) {
                posScaleOctalBuffer.Dispose();
            }

            if (negPosOctalCountersBuffer != null) {
                negPosOctalCountersBuffer.Dispose();
            }

            if (textures != null) {
                foreach (var (name, tex) in textures) {
                    tex.Dispose();
                }

                textures = null;
            }

            if (buffers != null) {
                foreach (var (name, buffer) in buffers) {
                    buffer.Dispose();
                }

                buffers = null;
            }
        }

        // Create intermediate textures (cached, gradient) to be used for voxel graph shader execution
        // Texture size will correspond to execution size property
        private void CreateResources(int size, bool readback) {
            DisposeResources();

            posScaleOctalBuffer = new ComputeBuffer(64, sizeof(int) * 4, ComputeBufferType.Structured);
            negPosOctalCountersBuffer = new ComputeBuffer(64, sizeof(int), ComputeBufferType.Structured);

            // Creates dictionary with the default voxel graph textures (density + custom data)
            textures = new Dictionary<string, ExecutorTexture> {
            };

            // TODO: for some reason unity thinks there's a memory leak here due to the compute buffers??
            // I dispose of them just like the render textures idk why it's complaining
            buffers = new Dictionary<string, ExecutorBuffer> {
            };

            if (readback) {
                buffers.Add("voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxel" }, new ComputeBuffer(VoxelUtils.VOLUME * 64, Voxel.size, ComputeBufferType.Structured)));
            } else {
                textures.Add("voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt), -1));
            }

            foreach (var (name, descriptor) in compiler.ctx.textures) {
                textures.Add(name, descriptor.Create(size));
            }

            // TODO: do the same custom texture stuff but with buffers instead!!
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PosScaleOctalData {
            public Vector3 position;
            public float scale;
        }

        // If only we could have enums with values, rust style
        // :(
        public abstract class ExecutionParameters {
            public int newSize = -1;
            public int dispatchIndex = -1;
            public bool updateInjected = true;
        }
        public class EditorPreviewParameters: ExecutionParameters {
            public Vector3 previewOffset;
            public Vector3 previewScale;
        }
        public class ReadbackParameters: ExecutionParameters {
            public PosScaleOctalData[] posScaleOctals;
        }

        public GraphicsFence ExecuteShader(ExecutionParameters parameters) {
            if (parameters == null) {
                throw new ArgumentNullException("Exec. Parameters are not set");
            }

            int newSize = parameters.newSize;
            int dispatchIndex = parameters.dispatchIndex;
            bool updateInjected = parameters.updateInjected;

            if (compiler.ctx == null) {
                compiler.ParsedTranspilation();
            }

            if (newSize != setSize || textures == null || buffers == null) {
                setSize = newSize;
                CreateResources(newSize, parameters is ReadbackParameters);
            }

            CommandBuffer commands = new CommandBuffer();
            commands.name = "Execute Terrain Generator Dispatches";
            commands.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            ComputeShader shader = compiler.shader;
            
            LocalKeyword keyword = shader.keywordSpace.FindKeyword("_ASYNC_READBACK_OCTAL");
            if (parameters is ReadbackParameters readback) {
                commands.EnableKeyword(shader, keyword);
                
                commands.SetBufferData(posScaleOctalBuffer, readback.posScaleOctals);
                commands.SetComputeBufferParam(shader, dispatchIndex, "pos_scale_octals", posScaleOctalBuffer);

                commands.SetBufferData(negPosOctalCountersBuffer, new int[64]);
                commands.SetComputeBufferParam(shader, dispatchIndex, "neg_pos_octal_counters", negPosOctalCountersBuffer);
            } else if (parameters is EditorPreviewParameters editor) {
                commands.DisableKeyword(shader, keyword);
                commands.SetComputeVectorParam(shader, "previewOffset", editor.previewOffset);
                commands.SetComputeVectorParam(shader, "previewScale", editor.previewScale);
            }

            commands.SetComputeIntParam(shader, "size", newSize);
            commands.SetComputeIntParams(shader, "permuationSeed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
            commands.SetComputeIntParams(shader, "moduloSeed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });

            if (updateInjected) {
                compiler.ctx.injector.UpdateInjected(commands, shader, textures);
            }


            foreach (var (name, texture) in textures) {
                texture.PreDispatch(commands, shader);
            }

            foreach (var (name, buffer) in buffers) {
                buffer.PreDispatch(commands, shader);
            }

            foreach (var (name, texture) in textures) {
                texture.BindToComputeShader(commands, shader);
            }

            foreach (var (name, buffer) in buffers) {
                buffer.BindToComputeShader(commands, shader);
            }

            // Execute the kernels sequentially
            // TODO: Add back said kernels since we removed the whole cached node kek
            KernelDispatch kernel =  compiler.ctx.dispatches[dispatchIndex];
            int id = shader.FindKernel(kernel.name);
            int tempSize = newSize;
            tempSize = Mathf.Max(tempSize, 1);

            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            commands.DispatchCompute(shader, id, minScaleBase3D, minScaleBase3D, minScaleBase3D);

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(commands, shader, id);
            }

            // This works! Only in the builds, but async compute queue is being utilized!!!
            // If the target arch doesn't support async compute this will just revert to the normal queue
            GraphicsFence fence = commands.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            Graphics.ExecuteCommandBufferAsync(commands, UnityEngine.Rendering.ComputeQueueType.Background);
            return fence;
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
            compiler.OnPropertiesChanged();
        }
    }
}