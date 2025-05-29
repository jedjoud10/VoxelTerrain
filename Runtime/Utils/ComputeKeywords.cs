using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public static class ComputeKeywords {
        public enum Type {
            OctalReadback,
            SegmentVoxels,
            SegmentProps,
            Preview
        }

        public const string OCTAL_READBACK = "_ASYNC_READBACK_OCTAL";
        public const string SEGMENT_VOXELS = "_SEGMENT_VOXELS";
        public const string SEGMENT_PROPS = "_SEGMENT_PROPS";
        public const string PREVIEW = "_PREVIEW";

        public static readonly string PRAGMA_MULTI_COMPILE = $"#pragma multi_compile {PREVIEW} {OCTAL_READBACK} {SEGMENT_VOXELS} {SEGMENT_PROPS}\n";

        private static readonly Dictionary<Type, string> map = new Dictionary<Type, string> {
            { Type.OctalReadback, OCTAL_READBACK },
            { Type.SegmentVoxels, SEGMENT_VOXELS },
            { Type.SegmentProps, SEGMENT_PROPS },
            { Type.Preview, PREVIEW }
        };

        public static void ApplyKeywords(CommandBuffer cmds, ComputeShader shader, Type type) {
            foreach (var (_type, keyword) in map) {
                if (type == _type) {
                    cmds.EnableKeyword(shader, shader.keywordSpace.FindKeyword(keyword));
                } else {
                    cmds.DisableKeyword(shader, shader.keywordSpace.FindKeyword(keyword));
                }
            }

                        
        }
    }
}