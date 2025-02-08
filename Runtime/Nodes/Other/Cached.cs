using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


// TODO: Keep track of the inputs used for this variable so that we can use a 2d texture instead of a 3d one each time
// technically no need for this since we can just always pass the given position (since we always branch off of there)

// TODO: Must create a different compute shader with required variables
// done, this now does the recursive handling within another scope so it's fine

// TODO: Must create texture and create a variable that reads from it in the OG shader
// yep, done! even comes with a texture scale parameter to scale the texture if we wish to do so


// sub-TODO: can squish multiple cached calls into a single RGBA texture (of the same size) to help performance

// TODO: dedupe stuff pls thx
// fixed
public class CachedNode<T> : Variable<T> {
    public Variable<T> inner;
    public int sizeReductionPower;
    public CachedSampler sampler;
    public string swizzle;

    private string tempTextureName;
    public override void Handle(TreeContext context) {
        if (!context.Contains(this)) {
            HandleInternal(context);
        } else {
            context.textures[tempTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
        }
    }

    // looks up all the dependencies of a and makes sure that they are 2D (could be xy, yx, xz, whatever)
    // clones those dependencies to a secondary compute kernel
    // create temporary texture that is written to by that kernel
    // read said texture with appropriate swizzles in the main kernel

    public override void HandleInternal(TreeContext context) {
        context.Hash(sizeReductionPower);
        context.Hash(sampler.filter);
        context.Hash(sampler.wrap);
        context.Hash(sampler.generateMips);
        context.Hash(sampler.bicubic);
        sampler.scale.Handle(context);
        sampler.offset.Handle(context);
        sampler.level.Handle(context);

        int dimensions = swizzle.Length;
        bool _3d = dimensions == 3;

        string scopeName = context.GenId($"CachedScope");
        string outputName = $"{scopeName}_output";
        string textureName = context.GenId($"_cached_texture");
        tempTextureName = textureName;
        context.properties.Add($"RWTexture{dimensions}D<{GraphUtils.TypeOf<T>().ToStringType()}> {textureName}_write;");
        context.properties.Add($"Texture{dimensions}D {textureName}_read;");
        context.properties.Add($"SamplerState sampler{textureName}_read;");

        ScopeArgument output = new ScopeArgument(outputName, GraphUtils.TypeOf<T>(), inner, true);

        int index = context.scopes.Count;
        int oldScopeIndex = context.currentScope;

        TreeNode positionNode = context.position.node;
        string positionName = context[context.position.node];
        
        context.scopes.Add(new TreeScope(context.scopeDepth + 1) {
            name = scopeName,
            arguments = new ScopeArgument[] { context.position, output, },
            namesToNodes = new Dictionary<TreeNode, string> { { positionNode, positionName } },
        });

        // ENTER NEW SCOPE!!!
        context.currentScope = index;
        context.scopeDepth++;

        // Add the start node (position node) to the new scope
        context.scopes[index].namesToNodes.TryAdd(positionNode, positionName);

        // Call the recursive handle function within the indented scope
        inner.Handle(context);

        // Copy of the name of the inner variable
        var tempName = context[inner];        

        // EXIT SCOPE!!!
        context.scopeDepth--;
        context.currentScope = oldScopeIndex;

        int frac = (1 << sizeReductionPower);
        string aa = $"(float(size) / {frac})";

        string idCtor = _3d ? $"ConvertFromWorldPosition({positionName})" : $"ConvertFromWorldPosition({positionName}).{swizzle}";
        if (sampler.bicubic) {
            context.DefineAndBindNode<T>(this, $"{tempName}_cached", $"SampleBicubic({textureName}_read, sampler{textureName}_read, ({idCtor} / size) * {context[sampler.scale]}.{swizzle} + {context[sampler.offset]}.{swizzle}, {context[sampler.level]}, {aa}).{GraphUtils.SwizzleFromFloat4<T>()}");
        } else {
            context.DefineAndBindNode<T>(this, $"{tempName}_cached", $"SampleBounded({textureName}_read, sampler{textureName}_read, ({idCtor} / size) * {context[sampler.scale]}.{swizzle} + {context[sampler.offset]}.{swizzle}, {context[sampler.level]}, {aa}).{GraphUtils.SwizzleFromFloat4<T>()}");
        }

        string numThreads = dimensions == 2 ? "[numthreads(32, 32, 1)]" : "[numthreads(8, 8, 8)]";
        string writeCoords = _3d ? "xyz" : "xy";
        string remappedCoords;

        if (_3d) {
            remappedCoords = "id";
        } else {
            int Indexify(char a) {
                switch (a) {
                    case 'x':
                        return 0;
                    case 'y':
                        return 1;
                    case 'z':
                        return 2;
                    default:
                        throw new Exception();
                }
            }

            string Clean(char temp) {
                if (temp == '@') {
                    return "0.0";
                } else {
                    return $"id.{temp}";
                }
            }

            char[] chars = swizzle.ToCharArray();
            char first = chars[0]; // x
            char second = chars[1]; // z

            char[] temp6 = new char[3] { '@', '@', '@' };
            temp6[Indexify(first)] = 'x';
            temp6[Indexify(second)] = 'y';


            // x, 0, y
            remappedCoords = $"{Clean(temp6[0])}, {Clean(temp6[1])}, {Clean(temp6[2])}";
        }

        context.dispatches.Add(new KernelDispatch {
            name = $"CS{scopeName}",
            depth = context.scopeDepth + 1,
            sizeReductionPower = sizeReductionPower,
            threeDimensions = _3d,
            numThreads = numThreads,
            scopeName = scopeName,
            scopeIndex = index,
            //outputTextureName = textureName,
            writeCoords = writeCoords,
            remappedCoords = remappedCoords,
            frac = frac,
            //type = Utils.TypeOf<T>()
            outputs = new KernelOutput[] {
                new KernelOutput() {
                    output = output,
                    outputTextureName = textureName,
                }
            }
        });

        context.textures.Add(tempTextureName, new TempTextureDescriptor {
            sizeReductionPower = sizeReductionPower,
            type = GraphUtils.TypeOf<T>(),
            writeKernel = $"CS{scopeName}",
            filter = sampler.filter,
            wrap = sampler.wrap,
            mips = sampler.generateMips,
            threeDimensions = _3d,
            readKernels = new List<string>() { $"CS{context.scopes[oldScopeIndex].name}" },
            name = tempTextureName,
        });
    }
}

public class Cacher<T> {
    public int sizeReductionPower;
    public Variable<float> scale;
    public CachedSampler sampler;

    public Cacher() {
        this.sizeReductionPower = 0;
        this.scale = 1.0f;
        this.sampler = new CachedSampler();
    }

    public Variable<T> Cache(Variable<T> input, string swizzle = "xyz") {
        return new CachedNode<T> {
            inner = input,
            sizeReductionPower = sizeReductionPower,
            sampler = sampler,
            swizzle = swizzle,
        };
    }
}

public class CachedSampler {
    public Variable<float3> scale;
    public Variable<float3> offset;

    public FilterMode filter;
    public TextureWrapMode wrap;

    public Variable<float> level;
    public bool generateMips;

    public bool bicubic;

    public CachedSampler() {
        this.scale = new float3(1.0);
        this.filter = FilterMode.Trilinear;
        this.wrap = TextureWrapMode.Clamp;
        this.offset = float3.zero;
        this.level = 0.0f;
        this.bicubic = false;
        this.generateMips = false;
    }
}