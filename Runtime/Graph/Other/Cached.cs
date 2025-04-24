using System;
using System.Collections.Generic;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class CachedNode<T> : Variable<T> {
        public Variable<T> inner;
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
            int dimensions = swizzle.Length;
            bool _3d = dimensions == 3;

            string scopeName = context.GenId($"CachedScope");
            string outputName = $"{scopeName}_output";
            string textureName = context.GenId($"_cached_texture");
            tempTextureName = textureName;
            context.properties.Add($"RWTexture{dimensions}D<{VariableType.TypeOf<T>().ToStringType()}> {textureName}_write;");
            context.properties.Add($"Texture{dimensions}D {textureName}_read;");
            context.properties.Add($"SamplerState sampler{textureName}_read;");

            ScopeArgument output = new ScopeArgument(outputName, VariableType.TypeOf<T>(), inner, true);

            int index = context.scopes.Count;
            int oldScopeIndex = context.currentScope;

            UntypedVariable idNode = context.id.node;
            string idNodeName = context[context.id.node];

            UntypedVariable positionNode = context.position.node;
            string positionNodeName = context[context.position.node];


            context.scopes.Add(new TreeScope(context.scopeDepth + 1) {
                name = scopeName,
                arguments = new ScopeArgument[] { context.position, output, },
                namesToNodes = new Dictionary<UntypedVariable, string> { { idNode, idNodeName }, { positionNode, positionNodeName } },
            });

            // ENTER NEW SCOPE!!!
            context.currentScope = index;
            context.scopeDepth++;

            // Add the start node (id node and position node) to the new scope
            context.scopes[index].namesToNodes.TryAdd(idNode, idNodeName);
            context.scopes[index].namesToNodes.TryAdd(positionNode, positionNodeName);

            // Call the recursive handle function within the indented scope
            inner.Handle(context);

            // Copy of the name of the inner variable
            var tempName = context[inner];

            // EXIT SCOPE!!!
            context.scopeDepth--;
            context.currentScope = oldScopeIndex;

            /*
            string idCtor = _3d ? $"ConvertFromWorldPosition({positionName})" : $"ConvertFromWorldPosition({positionName}).{swizzle}";
            if (sampler.bicubic) {
                context.DefineAndBindNode<T>(this, $"{tempName}_cached", $"SampleBicubic({textureName}_read, sampler{textureName}_read, ({idCtor} / size) * {context[sampler.scale]}.{swizzle} + {context[sampler.offset]}.{swizzle}, {context[sampler.level]}, {aa}).{GraphUtils.SwizzleFromFloat4<T>()}");
            } else {
                context.DefineAndBindNode<T>(this, $"{tempName}_cached", $"SampleBounded({textureName}_read, sampler{textureName}_read, ({idCtor} / (size+1)) * {context[sampler.scale]}.{swizzle} + {context[sampler.offset]}.{swizzle}, {context[sampler.level]}, {aa}).{GraphUtils.SwizzleFromFloat4<T>()}");
            }
            */

            //context.DefineAndBindNode<T>(this, $"{tempName}_cached", $"SampleBounded({textureName}_read, sampler{textureName}_read, ({idCtor} / (size+1)), {context[sampler.level]}, {frac}, uint2({idCtor})).{GraphUtils.SwizzleFromFloat4<T>()}");

            Vector3Int numThreads = dimensions == 2 ?  new Vector3Int(32, 32, 1) : new Vector3Int(8, 8, 8);
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

                // what the fuck?
                char[] chars = swizzle.ToCharArray();
                char first = chars[0]; // x
                char second = chars[1]; // z

                char[] temp6 = new char[3] { '@', '@', '@' };
                temp6[Indexify(first)] = 'x';
                temp6[Indexify(second)] = 'y';


                // x, 0, y
                remappedCoords = $"{Clean(temp6[0])}, {Clean(temp6[1])}, {Clean(temp6[2])}";
            }

            // todo: jeddie weddie pls fix
            throw new NotImplementedException();
            /*
            context.dispatches.Add(new KernelDispatch {
                name = $"CS{scopeName}",
                depth = context.scopeDepth + 1,
                threeDimensions = _3d,
                numThreads = numThreads,
                scopeName = scopeName,
                scopeIndex = index,
                writeCoords = writeCoords,
                remappedCoords = remappedCoords,
                outputs = new KernelOutput[] {
                    new KernelOutput() {
                        output = output,
                        outputTextureName = textureName,
                    }
                }
            });

            context.textures.Add(tempTextureName, new TempTextureDescriptor {
                type = VariableType.TypeOf<T>(),
                writeKernel = $"CS{scopeName}",
                filter = FilterMode.Point,
                wrap = TextureWrapMode.Clamp,
                mips = false,
                threeDimensions = _3d,
                readKernels = new List<string>() { $"CS{context.scopes[oldScopeIndex].name}" },
                name = tempTextureName,
            });
            */
        }
    }
}