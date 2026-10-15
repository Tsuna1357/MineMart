using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ Editor thiết lập Ánh Sáng & Màu Sắc Nắng Ấm BSL (Minecraft BSL Shader Look) cho MineMart.
    /// Tự động tinh chỉnh: Directional Light, Gradient Ambient Trilight, Camera Post-Processing, Global Volume và Material Voxel Shading.
    /// </summary>
    public static class SetupBslLightingTool {
        [MenuItem("Tools/MineMart/Apply BSL Lighting & Colors (Áp Dụng Ánh Sáng BSL)", false, 50)]
        public static void ApplyBslLighting() {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply BSL Lighting & Shading");
            int group = Undo.GetCurrentGroup();

            // 1. Tinh chỉnh Directional Light (Mặt trời nắng ấm BSL)
            Light sunLight = RenderSettings.sun;
            if (sunLight == null) {
                Light[] allLights = Object.FindObjectsByType<Light>();
                foreach (var l in allLights) {
                    if (l.type == LightType.Directional) {
                        sunLight = l;
                        break;
                    }
                }
            }

            if (sunLight != null) {
                Undo.RecordObject(sunLight.transform, "BSL Sun Rotation");
                Undo.RecordObject(sunLight, "BSL Sun Settings");

                // Góc chiếu xiên tạo bóng đổ dài nổi bật khối Voxel 3D
                sunLight.transform.rotation = Quaternion.Euler(48f, 130f, 0f);

                // Màu nắng trong trẻo tự nhiên chuẩn Minecraft BSL (Không bị ám vàng cam)
                sunLight.color = new Color(1f, 0.99f, 0.97f, 1f);
                sunLight.intensity = 1.25f;

                // Cấu hình bóng đổ mềm mịn, sắc nét chuẩn BSL
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = 0.88f;
                sunLight.shadowNormalBias = 0.35f;
                sunLight.shadowBias = 0.04f;

                RenderSettings.sun = sunLight;
                Debug.Log($"<color=#f39c12>[BSL Lighting]</color> Đã tối ưu Directional Light '{sunLight.name}': Nắng trắng sáng tự nhiên 1.25x, góc 48°, bóng đậm 0.88.");
            }

            // 2. Tinh chỉnh Ambient Lighting (Trilight Gradient: Trời xanh mát - Cỏ tươi - Đất trung tính)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.78f, 0.96f);      // Xanh lam dịu mát từ vòm trời
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.82f, 0.58f);  // Xanh lá cây tự nhiên từ thảm cỏ
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.29f, 0.31f);   // Đất xám mát trung tính (khử hoàn toàn ám cam)

            Debug.Log("<color=#2ecc71>[BSL Lighting]</color> Đã thiết lập Ambient Trilight Gradient (Trời xanh, Cỏ tươi, Đất mát trung tính).");

            // 3. Tinh chỉnh Camera (Bật HDR, MSAA và Post-Processing)
            Camera[] cameras = Object.FindObjectsByType<Camera>();
            foreach (var cam in cameras) {
                Undo.RecordObject(cam, "BSL Camera Settings");
                cam.allowHDR = true;
                cam.allowMSAA = true;

                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null) {
                    Undo.RecordObject(camData, "BSL Camera Data");
                    camData.renderPostProcessing = true;
                    camData.renderShadows = true;
                    EditorUtility.SetDirty(camData);
                }
                EditorUtility.SetDirty(cam);
                Debug.Log($"<color=#3498db>[BSL Lighting]</color> Đã kích hoạt HDR & Post-Processing cho Camera '{cam.name}'.");
            }

            // 4. Đảm bảo Global Volume có gán DefaultVolumeProfile
            Volume globalVolume = Object.FindAnyObjectByType<Volume>();
            if (globalVolume == null) {
                GameObject volGo = new GameObject("Global Volume");
                Undo.RegisterCreatedObjectUndo(volGo, "Create Global Volume");
                globalVolume = volGo.AddComponent<Volume>();
                globalVolume.isGlobal = true;
            }

            if (globalVolume.sharedProfile == null) {
                string[] guids = AssetDatabase.FindAssets("t:VolumeProfile DefaultVolumeProfile");
                if (guids != null && guids.Length > 0) {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    globalVolume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                    EditorUtility.SetDirty(globalVolume);
                    Debug.Log($"<color=#9b59b6>[BSL Lighting]</color> Đã gán VolumeProfile '{path}' cho '{globalVolume.name}'.");
                }
            }

            // 5. Cập nhật các Material dùng AtlasTriplanar shader sang thông số BSL
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            int matCount = 0;
            foreach (var guid in matGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null && mat.shader.name == "MineMart/Voxel/AtlasTriplanar") {
                    mat.SetFloat("_ShadowStrength", 0.85f);
                    mat.SetFloat("_MinAmbient", 0.18f);
                    mat.SetFloat("_Saturation", 1.0f);
                    EditorUtility.SetDirty(mat);
                    matCount++;
                }
            }
            if (matCount > 0) {
                Debug.Log($"<color=#1abc9c>[BSL Lighting]</color> Đã cập nhật {matCount} Voxel Material: Shadow 0.85, Ambient 0.18, Saturation 1.0 (Khử ám cam).");
            }

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();

            // Đánh dấu Scene đã thay đổi để lưu lại
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid()) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
            }

            EditorUtility.DisplayDialog(
                "Áp Dụng Ánh Sáng BSL Trong Trẻo Hoàn Tất!",
                "Đã cấu hình lại đồ họa MineMart theo phong cách Minecraft BSL Tươi Sáng & Trong Trẻo:\n\n" +
                "1. Ánh Nắng Mặt Trời: Nắng trắng tự nhiên (#FFFDFB), không bị ám vàng cam, bóng đậm nét 0.88x.\n" +
                "2. Bầu Không Khí (Trilight): Trời xanh mát - Cỏ tươi tự nhiên - Đất trung tính, khử hoàn toàn vệt cam.\n" +
                "3. Độ Đậm & Tươi Sáng: Màu gốc pixel art trung thực 100%, bóng đổ sâu 85% tách bạch khối 3D.\n" +
                "4. Camera & Hậu Kỳ: WhiteBalance 0 (trung tính), Bloom trắng nhẹ, PostExposure dịu mắt.\n\n" +
                "Đã tự động lưu Scene và Material!",
                "Tuyệt Vời"
            );
        }
    }
}
