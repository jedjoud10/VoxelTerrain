using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public class VariableType {
        public StrictType strict;

        public enum StrictType {
            Float,
            Float2,
            Float3,
            Float4,
            Int,
            Int2,
            Int3,
            Int4,
            Bool,
            Bool2,
            Bool3,
            Bool4,

            // TODO: decide if we should allow custom structs like the prop struct in strict type or not
            // How 2 do pls???
            Prop
        }

        public VariableType(StrictType strict) {
            this.strict = strict;
        }

        public static implicit operator VariableType(StrictType value) {
            return new VariableType(value);
        }


        public string ToStringType() {
            switch (strict) {
                case StrictType.Prop:
                    return "GpuProp";
                default:
                    return strict.ToString().ToLower();
            }
        }

        // Convert type data to string
        public static VariableType TypeOf<T>() {
            string tn = typeof(T).Name;
            VariableType output;

            switch (tn) {
                case "Single":
                    output = new(StrictType.Float); break;
                case "float2":
                    output = new(StrictType.Float2); break;
                case "float3":
                    output = new(StrictType.Float3); break;
                case "float4":
                    output = new(StrictType.Float4); break;
                case "Int32":
                    output = new(StrictType.Int); break;
                case "int2":
                    output = new(StrictType.Int2); break;
                case "int3":
                    output = new(StrictType.Int3); break;
                case "int4":
                    output = new(StrictType.Int4); break;
                case "Boolean":
                    output = new(StrictType.Bool); break;
                case "bool2":
                    output = new(StrictType.Bool2); break;
                case "bool3":
                    output = new(StrictType.Bool3); break;
                case "bool4":
                    output = new(StrictType.Bool4); break;
                case "GpuProp":
                    output = new(StrictType.Prop); break;

                default:
                    throw new System.Exception($"Type {tn} not supported");
            }

            return output;
        }

        public static string ToDefinableString<T>(T value) {
            string a = value.ToString();
            object temp = (object)value;

            switch (TypeOf<T>().strict) {
                case StrictType.Float2:
                    float2 f2 = (float2)temp;
                    return $"float2({f2.x},{f2.y})";
                case StrictType.Float3:
                    float3 f3 = (float3)temp;
                    return $"float3({f3.x},{f3.y},{f3.z})";
                case StrictType.Float4:
                    float4 f4 = (float4)temp;
                    return $"float4({f4.x},{f4.y},{f4.z},{f4.w})";
                case StrictType.Prop:
                    GpuProp prop = (GpuProp)temp;
                    return $"{{ {ToDefinableString<float3>(prop.position)}, {ToDefinableString<float3>(prop.rotation)}, {prop.scale}, {prop.type}, {prop.variant} }}";
                default:
                    return value.ToString();
            }
        }

        public static int Dimensionality<T>() {
            switch (TypeOf<T>().strict) {
                case StrictType.Float: return 1;
                case StrictType.Float2: return 2;
                case StrictType.Float3: return 3;
                case StrictType.Float4: return 4;

                case StrictType.Int: return 1;
                case StrictType.Int2: return 2;
                case StrictType.Int3: return 3;
                case StrictType.Int4: return 4;

                case StrictType.Bool: return 1;
                case StrictType.Bool2: return 2;
                case StrictType.Bool3: return 3;
                case StrictType.Bool4: return 4;
                default:
                    throw new System.Exception("Type not supported");
            }
        }
    }
}