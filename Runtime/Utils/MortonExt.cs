// GPT-ed using DeepSeek
// Can't be bothered to write my own tbh

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static partial class Morton {

        /// <summary>
        /// Encode a 2 dimensional coordinate to morton code (32-bit).
        /// </summary>
        /// <param name="coordinate">x,y coordinate</param>
        /// <returns>The morton code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint EncodeMorton2D_32(uint2 coordinate) {
            return (Part1By1_32(coordinate.y) << 1) + Part1By1_32(coordinate.x);
        }

        /// <summary>
        /// Decode a 2D morton code to (x,y) coordinate (32-bit).
        /// </summary>
        /// <param name="code">The morton code</param>
        /// <returns>The (x,y) coordinate</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 DecodeMorton2D_32(uint code) {
            var x = Compact1By1_32(code);
            var y = Compact1By1_32(code >> 1);
            return new uint2(x, y);
        }

        // Spread bits for 32-bit 2D Morton encoding (e.g., 0b1010 -> 0b1000100)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Part1By1_32(uint x) {
            x &= 0x0000FFFF;                 // Clear upper 16 bits
            x = (x ^ (x << 8)) & 0x00FF00FF; // Spread bits 8 apart
            x = (x ^ (x << 4)) & 0x0F0F0F0F; // Spread bits 4 apart
            x = (x ^ (x << 2)) & 0x33333333; // Spread bits 2 apart
            x = (x ^ (x << 1)) & 0x55555555; // Spread bits 1 apart
            return x;
        }

        // Inverse of Part1By1_32 (compact bits)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Compact1By1_32(uint x) {
            x &= 0x55555555;                 // Isolate even bits (x)
            x = (x ^ (x >> 1)) & 0x33333333; // Compact pairs
            x = (x ^ (x >> 2)) & 0x0F0F0F0F; // Compact nibbles
            x = (x ^ (x >> 4)) & 0x00FF00FF; // Compact bytes
            x = (x ^ (x >> 8)) & 0x0000FFFF; // Final compact
            return x;
        }
    }
}