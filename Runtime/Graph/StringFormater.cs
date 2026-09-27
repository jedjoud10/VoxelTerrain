using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public static class StringFormater {
        public static string Format(float x) {
            return x.ToString("G", new System.Globalization.CultureInfo("en-US"));;
        }
        
        public static string Format(float2 x) {
            return x.ToString("G", new System.Globalization.CultureInfo("en-US"));;
        }
        
        public static string Format(float3 x) {
            return x.ToString("G", new System.Globalization.CultureInfo("en-US"));;
        }

        public static string Format(float4 x) {
            return x.ToString("G", new System.Globalization.CultureInfo("en-US"));;
        }
    }
}