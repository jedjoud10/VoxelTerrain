using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class GeneratedTextureHelper {
        private string name = null;
        private TextureDescriptor descriptor;

        public void RegisterFirstTimeIfNeeded(TreeContext context, Action<Texture2D> injector, Func<TextureDescriptor> creator) {
            if (context.dedupe.Contains(name) && !string.IsNullOrEmpty(name))
                return;


            name = context.GenId($"_gradient_texture");
            context.dedupe.Add(name);

            context.properties.Add($"Texture2D {name}_texture_read;");
            context.properties.Add($"SamplerState sampler{name}_texture_read;");

            context.Inject((cmds, compute, textures) => {
                Texture2D tex = (Texture2D)textures[name].texture;
                injector(tex);
            });

            descriptor = creator();
            descriptor.name = name;
            descriptor.readKernels = new List<string>();
            context.textures.Add(name, descriptor);
        }

        public void RegisterCurrentScopeAsReading(TreeContext context) {
            context.textures[name].readKernels.Add($"CS{context.currentScope.name}");
        }

        public Variable<float4> SampleLevelAtCoords(TreeContext context, Variable<float2> coords) {
            coords.Handle(context);
            return context.AssignTempVariable<float4>($"huh", $"{name}_texture_read.SampleLevel(sampler{name}_texture_read, {context[coords]}, 0)");
        }
    }
}