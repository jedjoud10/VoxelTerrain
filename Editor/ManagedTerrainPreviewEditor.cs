using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainPreview), true)]
    public class ManagedTerrainPreviewEditor : UnityEditor.Editor {
        Gradient gradient;
        private void OnSceneViewGUI(SceneView sv) {
            var preview = (ManagedTerrainPreview)target;
            if (preview.type == ManagedTerrainPreview.PreviewType.Volume) {
                Handles.matrix = Matrix4x4.TRS(Vector3.one * preview.size * 0.5f, Quaternion.identity, Vector3.one * preview.size);
                Handles.DrawTexture3DVolume(preview.handlesTexture, 1f, useColorRamp: true, customColorRamp: gradient);
            }
        }

        void OnEnable() {
            gradient = new Gradient();
            gradient.SetKeys(new GradientColorKey[] {
                new GradientColorKey(Color.black, 0.8f),
                new GradientColorKey(Color.white, 1),
            }, new GradientAlphaKey[] {
                new GradientAlphaKey(0, 0),
                new GradientAlphaKey(1, 1),
            }); 
            SceneView.duringSceneGui += OnSceneViewGUI;
        }

        void OnDisable() {
            SceneView.duringSceneGui -= OnSceneViewGUI;
        }
    }
}
