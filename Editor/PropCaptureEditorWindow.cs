using System;
using System.Collections.Generic;
using jedjoud.VoxelTerrain.Props;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;


namespace jedjoud.VoxelTerrain.Editor {
    public class PropCaptureEditorWindow : UnityEditor.EditorWindow {
        private List<PropType> props;
        private Shader captureShader;
        private Material captureMaterial;

        [MenuItem("Voxel Terrain Rools/Prop Capture Utility")]
        static void Init() {
            PropCaptureEditorWindow window = (PropCaptureEditorWindow)GetWindow(typeof(PropCaptureEditorWindow));
            window.props = new List<PropType>();
            window.captureShader = LoadAsset<Shader>("621bb0a2631b5bb468b28e974c55d663");
            window.captureMaterial = new Material(window.captureShader);
            window.Show();

            var guids = AssetDatabase.FindAssets($"t:{typeof(PropType)}");
            foreach (var t in guids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(t);
                var asset = AssetDatabase.LoadAssetAtPath<PropType>(assetPath);
                if (asset != null) {
                    window.props.Add(asset);
                }
            }
        }

        private void OnGUI() {
            GUILayout.Label("Prop Capture Utility", EditorStyles.boldLabel);
            GUILayout.Label($"Prop Types to capture: {props.Count}");

            if (GUILayout.Button("Capture Maps")) {
                if (!AssetDatabase.IsValidFolder($"Assets/Voxel Terrain/")) {
                    AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
                }

                if (!AssetDatabase.IsValidFolder($"Assets/Voxel Terrain/Captured Prop Textures")) {
                    AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Captured Prop Textures");
                }

                if (props.Count > 0) {
                    foreach (var item in props) {
                        CaptureMaps(item);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
            }
        }

        const int AZIMUTH_ITERS = PropUtils.IMPOSTOR_CAPTURE_AZIMUTH_ITERATIONS;

        private void CaptureMaps(PropType type) {
            GameObject cameraGO = new GameObject("TempCamera");
            cameraGO.layer = 31;
            Camera cam = cameraGO.AddComponent<Camera>();
            cam.backgroundColor = new Color(0,0,0,0);
            cam.clearFlags = CameraClearFlags.Color;
            cam.orthographic = true;

            int width = type.impostorTextureWidth;
            int height = type.impostorTextureHeight;
            bool mips = false;

            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.autoGenerateMips = mips;
            rt.useMipMap = mips;
            rt.depthStencilFormat = GraphicsFormat.D24_UNorm;
            rt.depth = 24;
            cam.targetTexture = rt;

            Texture2DArray diffuseMapOut = new Texture2DArray(width, height, type.variants.Count * AZIMUTH_ITERS, TextureFormat.ARGB32, mips);
            Texture2DArray normalMapOut = new Texture2DArray(width, height, type.variants.Count * AZIMUTH_ITERS, TextureFormat.ARGB32, mips);
            Texture2DArray maskMapOut = new Texture2DArray(width, height, type.variants.Count * AZIMUTH_ITERS, TextureFormat.ARGB32, mips);
            Texture2DArray[] tempOut = new Texture2DArray[3] { diffuseMapOut, normalMapOut, maskMapOut };

            try {
                for (int i = 0; i < type.variants.Count; i++) {
                    CaptureVariant(type, cam, rt, tempOut, i);
                }

                string name = type.name.Replace(' ', '_').ToLower();
                int count = type.variants.Count;

                string diffuseMapPath = $"Assets/Voxel Terrain/Captured Prop Textures/{name}_diffuse.asset";
                AssetDatabase.CreateAsset(diffuseMapOut, diffuseMapPath);
                string normalMapPath = $"Assets/Voxel Terrain/Captured Prop Textures/{name}_normal.asset";
                AssetDatabase.CreateAsset(normalMapOut, normalMapPath);
                string maskMapPath = $"Assets/Voxel Terrain/Captured Prop Textures/{name}_mask.asset";
                AssetDatabase.CreateAsset(maskMapOut, maskMapPath);

                type.impostorDiffuseMaps = diffuseMapOut;
                type.impostorNormalMaps = normalMapOut;
                type.impostorMaskMaps = maskMapOut;

                EditorUtility.SetDirty(type);

                AssetDatabase.SaveAssets();
                
                AssetDatabase.ImportAsset(diffuseMapPath);
                AssetDatabase.ImportAsset(normalMapPath);
                AssetDatabase.ImportAsset(maskMapPath);
                
                AssetDatabase.Refresh();
            } finally {
                DestroyImmediate(cameraGO);
                rt.Release();
            }
        }

        private void CaptureVariant(PropType type, Camera cam, RenderTexture rt, Texture2DArray[] tempOut, int variant) {
            if (type.variants[variant].prefab == null) {
                throw new NullReferenceException("Cannot take capture of prop variant without a prefab");
            }

            GameObject faker = Instantiate(type.variants[variant].prefab);

            try {
                Bounds bounds = CalculateBounds(faker);
                faker.layer = 31;

                float orthoScale = Mathf.Sqrt(Mathf.Pow(bounds.size.x, 2f) + Mathf.Pow(bounds.size.y, 2f) + Mathf.Pow(bounds.size.z, 2f));
                cam.orthographicSize = orthoScale / 2f;

                MeshRenderer renderer = faker.GetComponent<MeshRenderer>();
                Material material = renderer.sharedMaterial;
                if (!material.HasTexture("_DiffuseMap"))
                    throw new NullReferenceException($"Missing _DiffuseMap at material from type '{type.name}' at variant {variant}");
                Texture2D diffuse = (Texture2D)material.GetTexture("_DiffuseMap");

                if (!material.HasTexture("_NormalMap"))
                    throw new NullReferenceException($"Missing _NormalMap at material from type '{type.name}' at variant {variant}");
                Texture2D normal = (Texture2D)material.GetTexture("_NormalMap");

                if (!material.HasTexture("_MaskMap"))
                    throw new NullReferenceException($"Missing _MaskMap at material from type '{type.name}' at variant {variant}");
                Texture2D mask = (Texture2D)material.GetTexture("_MaskMap");
                Texture2D[] inputMaps = new Texture2D[3] { diffuse, normal, mask };

                faker.GetComponent<MeshRenderer>().material = captureMaterial;
                captureMaterial.SetTexture("_CaptureDiffuseMap", diffuse);
                captureMaterial.SetTexture("_CaptureNormalMap", normal);
                captureMaterial.SetTexture("_CaptureMaskMap", mask);

                captureMaterial.SetInt("_NormalXSwizzle", (int)type.impostorNormalX);
                captureMaterial.SetInt("_NormalYSwizzle", (int)type.impostorNormalY);
                captureMaterial.SetInt("_NormalZSwizzle", (int)type.impostorNormalZ);

                faker.transform.position = Vector3.zero;
                faker.transform.rotation = Quaternion.identity;

                for (int map = 0; map < 3; map++) {
                    CaptureMap(type.impostorCaptureAxis, bounds, cam, rt, tempOut[map], variant, map);
                }
            } finally {
                DestroyImmediate(faker);
            }
        }

        private void CaptureMap(PropType.ImpostorCapturePolarAxis mode, Bounds bounds, Camera cam, RenderTexture rt, Texture2DArray output, int variant, int map) {
            captureMaterial.SetInt("_CaptureMapSelector", map);

            for (int azimuth = 0; azimuth < AZIMUTH_ITERS; azimuth++) {
                Graphics.SetRenderTarget(rt);
                GL.Clear(true, true, Color.clear);
                Graphics.SetRenderTarget(null);

                (Vector3 camPos, Quaternion camRot) = CalculateCameraOrbit(mode, bounds, (azimuth / (float)AZIMUTH_ITERS) * 360f);
                cam.transform.position = camPos;
                cam.transform.rotation = camRot;
                cam.Render();

                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, rt.useMipMap);
                RenderTexture.active = rt;
                GL.Flush();
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);

                for (int m = 0; m < tex.mipmapCount; m++) {
                    output.CopyPixels(tex, 0, m, variant * AZIMUTH_ITERS + azimuth, m);
                }

                tex.Apply();
                RenderTexture.active = null;
            }


            rt.Release();
        }

        private Bounds CalculateBounds(GameObject obj) {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) {
                bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        private (Vector3, Quaternion) CalculateCameraOrbit(PropType.ImpostorCapturePolarAxis mode, Bounds bounds, float azimuthAngle) {
            float distance = 10f;

            float rad = azimuthAngle * Mathf.Deg2Rad;
            Vector3 position = mode switch {
                PropType.ImpostorCapturePolarAxis.XZ => new Vector3(
                    Mathf.Cos(rad) * distance,
                    0f,
                    Mathf.Sin(rad) * distance
                ),
                PropType.ImpostorCapturePolarAxis.XY => new Vector3(
                    Mathf.Cos(rad) * distance,
                    Mathf.Sin(rad) * distance,
                    0f
                ),
                PropType.ImpostorCapturePolarAxis.YZ => new Vector3(
                    0f,
                    Mathf.Cos(rad) * distance,
                    Mathf.Sin(rad) * distance
                ),
                _ => throw new Exception("what"),
            };

            Vector3 up = mode switch {
                PropType.ImpostorCapturePolarAxis.XZ => Vector3.up,
                PropType.ImpostorCapturePolarAxis.XY => Vector3.forward,
                PropType.ImpostorCapturePolarAxis.YZ => Vector3.right,
                _ => throw new Exception("what"),
            };

            Quaternion rotation = Quaternion.LookRotation(-position, up);

            return (position + bounds.center, rotation);
        }

        // https://anja-haumann.de/unity-load-assets-in-editor/
        public static T LoadAsset<T>(string guid) where T : class {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T)) as T;
            return asset;
        }
    }
}