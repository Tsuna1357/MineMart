using MineMart.UI.Binders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ Editor tạo và cấu hình Virtual Joystick trực tiếp trong Hierarchy và tạo Prefab.
    /// Giúp người phát triển dễ dàng nhìn thấy và kéo thả tùy chỉnh trong Inspector cả lúc Edit Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class VirtualJoystickEditorCreator {
        private const string PREFAB_PATH = "Assets/_project/UI/Prefabs/VirtualJoystick_Prefab.prefab";

        static VirtualJoystickEditorCreator() {
            EditorApplication.delayCall += EnsureVirtualJoystickInScene;
        }

        private static void EnsureVirtualJoystickInScene() {
            if (Application.isPlaying) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            // Kiểm tra xem Scene hiện tại có MainCanvasHud hay Canvas không
            var mainHud = Object.FindAnyObjectByType<MainCanvasHud>(FindObjectsInactive.Include);
            if (mainHud == null) return;

            // Kiểm tra xem đã có VirtualJoystick trong Scene chưa
            var existing = Object.FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (existing == null) {
                CreateVirtualJoystickInScene(null);
            }
        }

        [MenuItem("GameObject/MineMart UI/Tạo Virtual Joystick (Cần Điều Khiển)", false, 26)]
        [MenuItem("Tools/MineMart/Tạo Virtual Joystick Vào Scene", false, 26)]
        public static void CreateVirtualJoystickInScene(MenuCommand menuCommand) {
            var existing = Object.FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (existing != null) {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log($"[MineMart UI] 🕹️ Virtual Joystick đã có sẵn trong Hierarchy tại: {existing.gameObject.name}");
                return;
            }

            Canvas canvas = null;
            var mainHud = Object.FindAnyObjectByType<MainCanvasHud>(FindObjectsInactive.Include);
            if (mainHud != null) {
                canvas = mainHud.GetComponentInParent<Canvas>();
            }

            if (canvas == null) {
                canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            }

            if (canvas == null) {
                Debug.LogWarning("[MineMart UI] Không tìm thấy Canvas nào trong Scene để tạo Virtual Joystick!");
                return;
            }

            var joystick = VirtualJoystickBootstrap.CreateRuntimeJoystick(canvas);
            if (joystick != null) {
                Undo.RegisterCreatedObjectUndo(joystick.gameObject, "Create Virtual Joystick");
                Selection.activeGameObject = joystick.gameObject;
                EditorGUIUtility.PingObject(joystick.gameObject);

                // Tạo / cập nhật Prefab asset để em có thể xem và chỉnh trực tiếp trong thư mục Project
                try {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(joystick.gameObject, PREFAB_PATH, InteractionMode.AutomatedAction);
                    Debug.Log($"[MineMart UI] ✅ Đã lưu Prefab Joystick tại: {PREFAB_PATH}");
                } catch (System.Exception ex) {
                    Debug.LogWarning($"[MineMart UI] Không thể lưu Prefab (vẫn tạo trong Scene bình thường): {ex.Message}");
                }

                EditorSceneManager.MarkSceneDirty(joystick.gameObject.scene);
                Debug.Log("[MineMart UI] ✅ Đã tạo thành công Virtual Joystick trong Hierarchy (Canvas -> MainGamePanel -> VirtualJoystick)!");
            }
        }
    }
}
