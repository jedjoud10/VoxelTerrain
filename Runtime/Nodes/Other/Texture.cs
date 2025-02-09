using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class SampleableTexture<T> {
        public string textureName;
        public Variable<float> level;
        public Variable<T> scale;
        public Variable<T> offset;
        public TreeContext context;

        public Variable<float4> SampleLeBruh(Variable<T> coordinates) {
            return context.AssignTempVariable<float4>("hehehehe", $"{textureName}_read.SampleLevel(sampler{textureName}_read, {context[coordinates]} * {context[scale]} + {context[offset]}, {context[level]})");
        }
    }

    public class TextureSampleNode<T> : Variable<float4> {
        public Variable<T> coordinates;
        public TextureSampler<T> sampler;

        private string tempTextureName;
        public override void Handle(TreeContext context) {
            if (!context.Contains(this)) {
                HandleInternal(context);
            } else {
                context.textures[tempTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
            }
        }


        public override void HandleInternal(TreeContext context) {
            coordinates.Handle(context);
            sampler.level.Handle(context);
            sampler.scale.Handle(context);
            sampler.offset.Handle(context);
            context.Hash(sampler.texture.GetInstanceID());

            int dimensionality = GraphUtils.Dimensionality<T>();

            string textureName = context.GenId($"_user_texture");

            tempTextureName = textureName;
            context.properties.Add($"Texture{dimensionality}D {textureName}_read;");
            context.properties.Add($"SamplerState sampler{textureName}_read;");

            Variable<float4> aaa = sampler.function(new SampleableTexture<T> {
                level = sampler.level,
                offset = sampler.offset,
                scale = sampler.scale,
                textureName = tempTextureName,
                context = context,
            }, coordinates);
            aaa.Handle(context);
            context.DefineAndBindNode<T>(this, "aaa", $"{context[aaa]}.{GraphUtils.SwizzleFromFloat4<T>()}");

            context.textures.Add(tempTextureName, new UserTextureDescriptor {
                readKernels = new List<string>() { $"CS{context.scopes[context.currentScope].name}" },
                name = tempTextureName,
                texture = sampler.texture,
            });
        }
    }

    public class TextureSampler<T> {
        public Texture texture;
        public Variable<float> level;
        public Variable<T> scale;
        public Variable<T> offset;
        public delegate Variable<float4> FindNameForThisPls(SampleableTexture<T> texture, Variable<T> coords);
        public FindNameForThisPls function;

        public TextureSampler(Texture texture) {
            this.level = 0.0f;
            this.scale = GraphUtils.One<T>();
            this.offset = GraphUtils.Zero<T>();
            this.texture = texture;
            this.function = (texture, coords) => texture.SampleLeBruh(coords);
        }

        public FindNameForThisPls LeConvolutionTahiniSauce() {
            return (texture, coords) => {
                return null;
                // aaaaaaa
            };
        }

        public Variable<float4> Sample(Variable<T> input) {
            if (texture == null) {
                throw new NullReferenceException("TextureSampler texture is not set");
            }

            if (texture.dimension == UnityEngine.Rendering.TextureDimension.Tex2D && GraphUtils.Dimensionality<T>() != 2) {
                throw new Exception("TextureSampler input coordinate dimension is 2D, but texture dimension isn't 2D");
            }

            if (texture.dimension == UnityEngine.Rendering.TextureDimension.Tex3D && GraphUtils.Dimensionality<T>() != 3) {
                throw new Exception("TextureSampler input coordinate dimension is 3D, but texture dimension isn't 3D");
            }

            return new TextureSampleNode<T> {
                coordinates = input,
                sampler = this,
            };
        }
    }
}