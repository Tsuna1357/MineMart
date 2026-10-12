using MineMart.UI.Binders;
using UnityEditor;
using UnityEngine;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ sinh và cấu hình giao diện Hộp Thoại Cài Đặt (Settings Dialog) trong Unity Editor với 1 cú click.
    /// </summary>
    public static class SettingsUIEditorCreator {
        [MenuItem("MineMart/UI/Tạo Mới Settings Dialog (Bảng Cài Đặt)", false, 25)]
        [MenuItem("GameObject/MineMart UI/Tạo Settings Dialog (Bảng Cài Đặt)", false, 25)]
        public static void CreateSettingsDialogInScene(MenuCommand menuCommand) {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) {
                GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var dialog = SettingsUIBootstrap.CreateRuntimeSettingsDialog(canvas);
            if (dialog != null) {
                Undo.RegisterCreatedObjectUndo(dialog.transform.parent.gameObject, "Create Settings Dialog");
                Selection.activeGameObject = dialog.gameObject;
                EditorUtility.SetDirty(dialog);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dialog.gameObject.scene);
                Debug.Log("[MineMart UI] ✅ Đã tạo thành công Settings Dialog trên Canvas!");
            }
        }

        [MenuItem("MineMart/UI/Thêm Nút Đặt Lại Tiến Trình (Reset Progress)", false, 26)]
        [MenuItem("GameObject/MineMart UI/Thêm Nút Đặt Lại Tiến Trình Vào Settings Dialog", false, 26)]
        public static void AddResetButtonToSettingsDialogInScene() {
            var dialog = Object.FindAnyObjectByType<SettingsDialogUI>(FindObjectsInactive.Include);
            if (dialog == null) {
                Debug.LogWarning("[MineMart UI] ⚠️ Không tìm thấy SettingsDialogUI trong Scene hiện tại. Hãy tạo mới bảng cài đặt trước!");
                CreateSettingsDialogInScene(null);
                return;
            }

            dialog.EnsureResetProgressButtonExists();

            // Tìm nút vừa tạo để đăng ký Undo và chọn nó trong Hierarchy
            var searchRoot = dialog.transform.Find("RowsContainer") ?? dialog.transform;
            var buttons = searchRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var b in buttons) {
                if (b.name.Contains("Reset") || b.name.Contains("Clear")) {
                    Undo.RegisterCreatedObjectUndo(b.gameObject, "Add Reset Progress Button");
                    Selection.activeGameObject = b.gameObject;
                    break;
                }
            }

            EditorUtility.SetDirty(dialog);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dialog.gameObject.scene);
            Debug.Log("[MineMart UI] ✅ Đã cập nhật thành công nút 'Đặt Lại Tiến Trình' (Reset Progress) vào Settings Dialog trong Scene!");
        }
    }
}
