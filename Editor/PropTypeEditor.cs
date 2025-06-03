using System;
using System.Collections;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(PropType))]
    public class PropTypeEditor : UnityEditor.Editor {
        // not an issue for now, I'll prob gpt it later when I'm done with everything
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
        }
    }
}