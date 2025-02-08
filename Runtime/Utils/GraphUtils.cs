//using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// Common utils and shorthand forms
public static class GraphUtils {
    public enum StrictType {
        Float,
        Float2,
        Float3,
        Float4,
        Uint,
        Uint2,
        Uint3,
        Int,
    }

    public static string ToStringType(this StrictType data) {
        return data.ToString().ToLower();
    }

    public static string ToStringType<T>() {
        return TypeOf<T>().ToString().ToLower();
    }

    public static string ToDefinableString<T>(T value) {
        string a = value.ToString();
        object temp = (object)value;

        switch (TypeOf<T>()) {
            case StrictType.Float2:
                float2 f2 = (float2)temp;
                return $"float2({f2.x},{f2.y})";
            case StrictType.Float3:
                float3 f3 = (float3)temp;
                return $"float3({f3.x},{f3.y},{f3.z})";
            case StrictType.Float4:
                float4 f4 = (float4)temp;
                return $"float4({f4.x},{f4.y},{f4.z},{f4.w})";
            default:
                return value.ToString();
        }
    }

    // Convert a strict type to a graphics format to be used for texture format
    public static GraphicsFormat ToGfxFormat(StrictType type) {
        switch (type) {
            case StrictType.Float:
                return GraphicsFormat.R16_SFloat;
            case StrictType.Float2:
                return GraphicsFormat.R16G16_SFloat;
            case StrictType.Float3:
                return GraphicsFormat.R16G16B16A16_SFloat;
            case StrictType.Float4:
                return GraphicsFormat.R16G16B16A16_SFloat;
            default:
                throw new System.Exception();
        }
    }

    public static Variable<T> Zero<T>() {
        T def = default(T);
        return new DefineNode<T> { value = ToDefinableString(def), constant = true };
    }

    public static Variable<T> One<T>(bool negate = false) {
        string Test(StrictType value) {
            switch (value) {
                case GraphUtils.StrictType.Float2:
                    return "float2(1.0,1.0)";
                case GraphUtils.StrictType.Float3:
                    return "float3(1.0,1.0,1.0)";
                case GraphUtils.StrictType.Float4:
                    return "float4(1.0,1.0,1.0)";
                default:
                    return value.ToString();
            }
        }

        string temp = Test(TypeOf<T>());

        if (negate) {
            temp = $"(-{temp})";
        }

        return new DefineNode<T> { value = temp, constant = true };
    }

    // Convert type data to string
    public static StrictType TypeOf<T>() {
        string tn = typeof(T).Name;
        StrictType output;

        switch (tn) {
            case "Single":
                output = StrictType.Float; break;
            case "float2":
                output = StrictType.Float2; break;
            case "float3":
                output = StrictType.Float3; break;
            case "float4":
                output = StrictType.Float4; break;
            case "uint2":
                output = StrictType.Uint2; break;
            case "uint3":
                output = StrictType.Uint3; break;
            case "UInt32":
                output = StrictType.Uint; break;
            case "Int32":
                output = StrictType.Int; break;
            default:
                throw new System.Exception("Type not supported");
        }

        return output;
    }

    public static int Dimensionality<T>() {
        switch (TypeOf<T>()) {
            case StrictType.Float: return 1;
            case StrictType.Float2: return 2;
            case StrictType.Float3: return 3;
            case StrictType.Float4: return 3;
            case StrictType.Uint: return 1;
            case StrictType.Uint2: return 2;
            case StrictType.Uint3: return 3;
            case StrictType.Int: return 1;
            default:
                throw new System.Exception("Type not supported");
        }
    }

    public static void SetComputeShaderObj(ComputeShader shader, string id, object val, StrictType type) {
        switch (type) {
            case StrictType.Float:
                shader.SetFloat(id, (float)val);
                break;
            case StrictType.Float2:
                float2 temp = (float2)val;
                shader.SetVector(id, new float4(temp, 0.0f));
                break;
            case StrictType.Float3:
                float3 temp2 = (float3)val;
                shader.SetVector(id, new float4(temp2, 0.0f));
                break;
            case StrictType.Float4:
                float4 temp3 = (float4)val;
                shader.SetVector(id, temp3);
                break;
            case StrictType.Uint:
                shader.SetInt(id, (int)(uint)val);
                break;
            case StrictType.Uint2:
                uint2 temp4 = (uint2)val;
                shader.SetInts(id, (int)temp4.x, (int)temp4.y);
                break;
            case StrictType.Uint3:
                uint3 temp5 = (uint3)val;
                shader.SetInts(id, (int)temp5.x, (int)temp5.y, (int)temp5.z);
                break;
            case StrictType.Int:
                shader.SetInt(id, (int)val);
                break;
        }
    }

    public static RenderTexture Create3DRenderTexture(int size, GraphicsFormat format, FilterMode filter = FilterMode.Trilinear, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = false) {
        RenderTexture texture = new RenderTexture(size, size, 0, format);
        texture.width = size;
        texture.height = size;
        texture.depth = 0;
        texture.volumeDepth = size;
        texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        texture.enableRandomWrite = true;
        texture.useMipMap = mips;
        texture.autoGenerateMips = false;
        texture.filterMode = filter;
        texture.wrapMode = wrap;
        texture.Create();
        return texture;
    }

    public static RenderTexture Create2DRenderTexture(int size, GraphicsFormat format, FilterMode filter = FilterMode.Trilinear, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = false) {
        RenderTexture texture = new RenderTexture(size, size, 0, format);
        texture.width = size;
        texture.height = size;
        texture.depth = 0;
        texture.volumeDepth = 1;
        texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        texture.enableRandomWrite = true;
        texture.useMipMap = mips;
        texture.autoGenerateMips = false;
        texture.filterMode = filter;
        texture.wrapMode = wrap;
        texture.Create();
        return texture;
    }

    public static string SwizzleFromFloat4<T>() {
        switch (TypeOf<T>()) {
            case StrictType.Float:
                return "x";
            case StrictType.Float2:
                return "xy";
            case StrictType.Float3:
                return "xyz";
            case StrictType.Float4:
                return "xyzw";
            default:
                throw new System.Exception();
        }
    }

    public static string VectorConstructor<T>() {
        switch (TypeOf<T>()) {
            case StrictType.Float2:
                return "x, y";
            case StrictType.Float3:
                return "x, y, z";
            case StrictType.Float4:
                return "x, y, z, w";
            default:
                throw new System.Exception();
        }
    }
}