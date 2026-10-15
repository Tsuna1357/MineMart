using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MineMart.Feel {
    /// <summary>
    /// Tự động áp dụng ánh sáng môi trường & màu sắc BSL Shaders (Nắng vàng ấm, bóng đổ sắc nét, trời xanh tươi sáng)
    /// chuẩn Minecraft BSL cho MineMart cả khi chạy Play mode lẫn khi chuyển Scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BslLightingSetup : MonoBehaviour {
        [Header("Ambient Trilight Colors (BSL Palette)")]
        [Tooltip("Màu xanh lam mát dịu từ vòm trời hắt xuống")]
        [SerializeField] private Color _skyColor = new Color(0.60f, 0.78f, 0.96f);

        [Tooltip("Màu xanh lá cây tự nhiên từ thảm cỏ")]
        [SerializeField] private Color _equatorColor = new Color(0.58f, 0.82f, 0.58f);

        [Tooltip("Màu đất xám mát trung tính (khử hoàn toàn ánh cam)")]
        [SerializeField] private Color _groundColor = new Color(0.28f, 0.29f, 0.31f);

        private void Awake() {
            ApplyLighting();
            ConfigureCameraAndSun();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoApplyOnSceneLoad() {
            ApplyAllBslSettings();
        }

        public void ApplyLighting() {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _skyColor;
            RenderSettings.ambientEquatorColor = _equatorColor;
            RenderSettings.ambientGroundColor = _groundColor;
        }

        public static void ApplyAllBslSettings() {
            // 1. Ambient Lighting (Trilight Gradient chuẩn BSL trong trẻo)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.78f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.82f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.29f, 0.31f);

            // 2. Tinh chỉnh Sun & Shadows
            ConfigureCameraAndSun();
        }

        private static void ConfigureCameraAndSun() {
            // Cấu hình Directional Light
            Light sun = RenderSettings.sun;
            if (sun == null) {
                Light[] lights = Object.FindObjectsByType<Light>();
                foreach (var l in lights) {
                    if (l.type == LightType.Directional) {
                        sun = l;
                        break;
                    }
                }
            }

            if (sun != null) {
                sun.transform.rotation = Quaternion.Euler(48f, 130f, 0f);
                sun.color = new Color(1f, 0.99f, 0.97f, 1f); // Nắng trắng sáng tự nhiên
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.88f;
                sun.shadowNormalBias = 0.35f;
                sun.shadowBias = 0.04f;
                RenderSettings.sun = sun;
            }

            // Cấu hình Camera để bật Post-Processing, HDR & MSAA
            Camera[] cameras = Object.FindObjectsByType<Camera>();
            foreach (var cam in cameras) {
                cam.allowHDR = true;
                cam.allowMSAA = true;

                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null) {
                    camData.renderPostProcessing = true;
                    camData.renderShadows = true;
                }
            }
        }
    }
}
