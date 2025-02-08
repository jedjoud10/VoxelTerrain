using System;
using System.Collections.Generic;
using UnityEngine;

public class GradientNode<T> : Variable<T> {
    public Gradient gradient;
    public Variable<float> mixer;
    public Variable<float> inputMin;
    public Variable<float> inputMax;
    public bool remapOutput;
    public int size;

    private string gradientTextureName;

    public override void Handle(TreeContext context) {
        if (!context.Contains(this)) {
            HandleInternal(context);
        } else {
            context.textures[gradientTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
        }
    }

    public override void HandleInternal(TreeContext context) {
        inputMin.Handle(context);
        inputMax.Handle(context);
        mixer.Handle(context);
        context.Hash(size);

        string textureName = context.GenId($"_gradient_texture");
        gradientTextureName = textureName;
        context.properties.Add($"Texture2D {textureName}_read;");
        context.properties.Add($"SamplerState sampler{textureName}_read;");

        context.Inject2((compute, textures) => {
            Texture2D tex = (Texture2D)textures[textureName].texture;

            /*
            Color32[] colors = new Color32[size];
            for (int i = 0; i < size; i++) {
                float t = (float)i / size;
                colors[i] = gradient.Evaluate(t);
            }
            tex.SetPixels32(colors);
            */

            Color[] colors = new Color[size];
            for (int i = 0; i < size; i++) {
                float t = (float)i / size;
                colors[i] = gradient.Evaluate(t);
            }
            tex.SetPixelData(colors, 0);
            tex.Apply();
        });

        string swizzle = GraphUtils.SwizzleFromFloat4<T>();
        Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_gradient_remapped", $"Remap({context[mixer]}, {context[inputMin]}, {context[inputMax]}, 0.0, 1.0)");
        Variable<T> sample = context.AssignTempVariable<T>($"{textureName}_gradient", $"{textureName}_read.SampleLevel(sampler{textureName}_read, float2({context[firstRemap]}, 0), 0).{swizzle}");
        //Variable<T> sample = context.AssignTempVariable<T>( $"{textureName}_gradient", $"SampleBicubic({textureName}_read, sampler{textureName}_read, {context[firstRemap]}, 0, 128).{swizzle}"); ;
        
        if (remapOutput) {
            Variable<T> secondRemap = context.AssignTempVariable<T>($"{context[mixer]}_gradient_second_remapped", $"Remap({context[sample]}, 0.0, 1.0, {context[inputMin]}, {context[inputMax]})");
            context.DefineAndBindNode<T>(this, $"{textureName}_gradient_sampled", context[secondRemap]);
        } else {
            context.DefineAndBindNode<T>(this, $"{textureName}_gradient_sampled", context[sample]);
        }

        context.textures.Add(gradientTextureName, new GradientTextureDescriptor {
            size = size,
            name = gradientTextureName,
            filter = FilterMode.Bilinear,
            wrap = TextureWrapMode.Clamp,
            readKernels = new List<string>() { $"CS{context.scopes[context.currentScope].name}" },
        });
    }
}

public class Ramp<T> {
    public static Variable<T> Evaluate(Variable<float> mixer, Gradient gradient, Variable<float> inputMin = null, Variable<float> inputMax = null, int size = 128, bool remapOutput = true) {
        if (gradient == null) {
            throw new NullReferenceException("Ramp gradient is not set");
        }

        return new GradientNode<T> {
            gradient = gradient,
            mixer = mixer,
            size = size,
            inputMin = inputMin != null ? inputMin : 0.0f,
            inputMax = inputMax != null ? inputMax : 1.0f,
            remapOutput = remapOutput
        };
    }
}
