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
            Uint,
            Uint2,
            Uint3,
            Uint4,
            Bool,
            Bool2,
            Bool3,
            Bool4,
            Quaternion,
        }

        public VariableType(StrictType strict) {
            this.strict = strict;
        }

        public static implicit operator VariableType(StrictType value) {
            return new VariableType(value);
        }

        public string ToStringType() {
            string output = strict switch {
                StrictType.Quaternion => "float4",
                var x => x.ToString().ToLower(),
            };

            return output;
        }

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

                case "UInt32":
                    output = new(StrictType.Uint); break;
                case "uint2":
                    output = new(StrictType.Uint2); break;
                case "uint3":
                    output = new(StrictType.Uint3); break;
                case "uint4":
                    output = new(StrictType.Uint4); break;

                case "Boolean":
                    output = new(StrictType.Bool); break;
                case "bool2":
                    output = new(StrictType.Bool2); break;
                case "bool3":
                    output = new(StrictType.Bool3); break;
                case "bool4":
                    output = new(StrictType.Bool4); break;

                case "quaternion":
                    output = new(StrictType.Quaternion); break;

                default:
                    throw new System.Exception($"Type {tn} not supported");
            }

            return output;
        }

        public static string ToDefinableString<T>(T value) {
            object temp = value;

            switch (TypeOf<T>().strict) {
                case StrictType.Float2:
                    var f2 = (float2)temp;
                    return $"float2({f2.x},{f2.y})";
                case StrictType.Float3:
                    var f3 = (float3)temp;
                    return $"float3({f3.x},{f3.y},{f3.z})";
                case StrictType.Float4:
                    var f4 = (float4)temp;
                    return $"float4({f4.x},{f4.y},{f4.z},{f4.w})";

                case StrictType.Int:
                    return ((int)temp).ToString();
                case StrictType.Int2:
                    var i2 = (int2)temp;
                    return $"int2({i2.x},{i2.y})";
                case StrictType.Int3:
                    var i3 = (int3)temp;
                    return $"int3({i3.x},{i3.y},{i3.z})";
                case StrictType.Int4:
                    var i4 = (int4)temp;
                    return $"int4({i4.x},{i4.y},{i4.z},{i4.w})";

                case StrictType.Uint:
                    return ((uint)temp).ToString();
                case StrictType.Uint2:
                    var u2 = (uint2)temp;
                    return $"uint2({u2.x},{u2.y})";
                case StrictType.Uint3:
                    var u3 = (uint3)temp;
                    return $"uint3({u3.x},{u3.y},{u3.z})";
                case StrictType.Uint4:
                    var u4 = (uint4)temp;
                    return $"uint4({u4.x},{u4.y},{u4.z},{u4.w})";

                case StrictType.Bool:
                    return ((bool)temp).ToString().ToLower();
                case StrictType.Bool2:
                    var b2 = (bool2)temp;
                    return $"bool2({b2.x.ToString().ToLower()},{b2.y.ToString().ToLower()})";
                case StrictType.Bool3:
                    var b3 = (bool3)temp;
                    return $"bool3({b3.x.ToString().ToLower()},{b3.y.ToString().ToLower()},{b3.z.ToString().ToLower()})";
                case StrictType.Bool4:
                    var b4 = (bool4)temp;
                    return $"bool4({b4.x.ToString().ToLower()},{b4.y.ToString().ToLower()},{b4.z.ToString().ToLower()},{b4.w.ToString().ToLower()})";

                case StrictType.Quaternion:
                    var q = (quaternion)temp;
                    return $"float4({q.value.x},{q.value.y},{q.value.z},{q.value.w})";

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
                case StrictType.Quaternion: return 4;

                case StrictType.Int: return 1;
                case StrictType.Int2: return 2;
                case StrictType.Int3: return 3;
                case StrictType.Int4: return 4;

                case StrictType.Uint: return 1;
                case StrictType.Uint2: return 2;
                case StrictType.Uint3: return 3;
                case StrictType.Uint4: return 4;

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