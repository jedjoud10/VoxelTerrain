//using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// Common utils and shorthand forms
namespace jedjoud.VoxelTerrain {
    using jedjoud.VoxelTerrain.Generation;
    using System;
    using UnityEngine.Rendering;

    public static class GraphUtils {
        // Convert a strict type to a graphics format to be used for texture format
        public static GraphicsFormat ToGfxFormat(VariableType type) {
            switch (type.strict) {
                case VariableType.StrictType.Float:
                    return GraphicsFormat.R16_SFloat;
                case VariableType.StrictType.Float2:
                    return GraphicsFormat.R16G16_SFloat;
                case VariableType.StrictType.Float3:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                case VariableType.StrictType.Float4:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                default:
                    throw new System.Exception();
            }
        }

        public static Variable<T> Zero<T>() {
            T def = default(T);
            return new DefineNode<T> { value = VariableType.ToDefinableString(def), constant = true };
        }

        public static Variable<T> One<T>(bool negate = false) {
            string Test(VariableType value) {
                switch (value.strict) {
                    case VariableType.StrictType.Float2:
                        return "float2(1.0,1.0)";
                    case VariableType.StrictType.Float3:
                        return "float3(1.0,1.0,1.0)";
                    case VariableType.StrictType.Float4:
                        return "float4(1.0,1.0,1.0)";
                    case VariableType.StrictType.Float or VariableType.StrictType.Int:
                        return "1";
                    case VariableType.StrictType.Int2:
                        return "int2(1,1)";
                    case VariableType.StrictType.Int3:
                        return "int3(1,1,1)";
                    case VariableType.StrictType.Int4:
                        return "int4(1,1,1,1)";
                    case VariableType.StrictType.Bool2:
                        return "bool2(true,true)";
                    case VariableType.StrictType.Bool3:
                        return "bool3(true,true,true)";
                    case VariableType.StrictType.Bool4:
                        return "bool4(true,true,true,true)";
                    case VariableType.StrictType.Bool:
                        return "true";
                    default:
                        throw new Exception("jed forgot to implement the rest");
                }
            }

            string temp = Test(VariableType.TypeOf<T>());

            if (negate) {
                temp = $"(-{temp})";
            }

            return new DefineNode<T> { value = temp, constant = true };
        }


        public static void SetComputeShaderObj(CommandBuffer cmds, ComputeShader shader, string id, object val, VariableType type) {
            switch (type.strict) {
                case VariableType.StrictType.Float:
                    cmds.SetComputeFloatParam(shader, id, (float)val);
                    break;
                case VariableType.StrictType.Float2:
                    float2 temp = (float2)val;
                    cmds.SetComputeVectorParam(shader, id, new float4(temp, 0.0f));
                    break;
                case VariableType.StrictType.Float3:
                    float3 temp2 = (float3)val;
                    cmds.SetComputeVectorParam(shader, id, new float4(temp2, 0.0f));
                    break;
                case VariableType.StrictType.Float4:
                    float4 temp3 = (float4)val;
                    cmds.SetComputeVectorParam(shader, id, temp3);
                    break;
                case VariableType.StrictType.Int:
                    cmds.SetComputeIntParam(shader, id, (int)val);
                    break;
                case VariableType.StrictType.Int2:
                    uint2 temp4 = (uint2)val;
                    cmds.SetComputeIntParams(shader, id, (int)temp4.x, (int)temp4.y);
                    break;
                case VariableType.StrictType.Int3:
                    uint3 temp5 = (uint3)val;
                    cmds.SetComputeIntParams(shader, id, (int)temp5.x, (int)temp5.y, (int)temp5.z);
                    break;
            }
        }

        public static string SwizzleFromFloat4<T>() {
            switch (VariableType.TypeOf<T>().strict) {
                case VariableType.StrictType.Float:
                    return "x";
                case VariableType.StrictType.Float2:
                    return "xy";
                case VariableType.StrictType.Float3:
                    return "xyz";
                case VariableType.StrictType.Float4:
                    return "xyzw";
                default:
                    throw new System.Exception();
            }
        }

        public static string VectorConstructor<T>() {
            switch (VariableType.TypeOf<T>().strict) {
                case VariableType.StrictType.Float2:
                    return "x, y";
                case VariableType.StrictType.Float3:
                    return "x, y, z";
                case VariableType.StrictType.Float4:
                    return "x, y, z, w";
                default:
                    throw new System.Exception();
            }
        }

        internal static Variable<T> CreateStruct<T>(params (string, object)[] values) {
            return null;
        }
    }
}