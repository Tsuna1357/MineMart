using MineMart.UI.Binders;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ sinh giao diện tự động cho chuỗi khởi động game:
    /// - Canvas khởi động (Sort Order 9999 cao nhất)
    /// - Panel 1: Miyuki Studio Splash (Chỗ để người dùng thiết kế logo/visual)
    /// - Panel 2: Loading Screen (Thanh Slider, % nạp, text trạng thái, tips)
    /// - Tự động nối dây toàn bộ references vào GameStartupFlowUI.
    /// </summary>
    public static class StartupFlowEditorCreator {
        [MenuItem("MineMart/UI/Tạo Khung Startup Flow (Miyuki Studio & Loading Panels)", false, 30)]
        [MenuItem("GameObject/MineMart UI/Tạo Khung Startup Flow (Miyuki Studio & Loading)", false, 30)]
        public static void CreateStartupFlowCanvas(MenuCommand menuCommand) {
            // Kiểm tra xem đã có GameStartupFlowUI nào trong Scene chưa
            var existing = Object.FindAnyObjectByType<GameStartupFlowUI>();
            if (existing != null) {
                bool replace = EditorUtility.DisplayDialog(
                    "Đã tồn tại Startup Flow Canvas",
                    $"Đã có [{existing.gameObject.name}] trong Scene.\nBạn có muốn chọn đối tượng hiện tại không?",
                    "Chọn đối tượng cũ",
                    "Hủy"
                );
                if (replace) {
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing.gameObject);
                }
                return;
            }

            // 1. Tạo Root Canvas riêng biệt với Sort Order cao nhất
            GameObject canvasGo = new GameObject("[StartupFlowCanvas]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameStartupFlowUI));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Startup Flow Canvas");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Luôn hiển thị đè lên trên tất cả HUD và Popup

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var flowUI = canvasGo.GetComponent<GameStartupFlowUI>();

            // ========================================================
            // 2. PANEL 1: MIYUKI STUDIO SPLASH
            // ========================================================
            GameObject studioPanel = CreateStretchPanel("Panel_StudioSplash", canvasGo, new Color(0.05f, 0.07f, 0.11f, 1f));
            var studioGroup = studioPanel.AddComponent<CanvasGroup>();

            // Container nội dung ở giữa màn hình
            GameObject studioContent = CreateRect("Studio_Content", studioPanel);
            var studioContentRect = studioContent.GetComponent<RectTransform>();
            studioContentRect.anchorMin = new Vector2(0.5f, 0.5f);
            studioContentRect.anchorMax = new Vector2(0.5f, 0.5f);
            studioContentRect.pivot = new Vector2(0.5f, 0.5f);
            studioContentRect.sizeDelta = new Vector2(700, 500);

            // Placeholder Icon / Logo Studio
            GameObject logoPlaceholder = CreateRect("Image_StudioLogo_Placeholder", studioContent);
            var logoRect = logoPlaceholder.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.7f);
            logoRect.anchorMax = new Vector2(0.5f, 0.7f);
            logoRect.sizeDelta = new Vector2(180, 180);
            var logoImg = logoPlaceholder.AddComponent<Image>();
            logoImg.color = new Color(0.95f, 0.75f, 0.28f, 1f); // Màu vàng gold sang trọng

            // Text Tên Studio
            GameObject studioTextGo = CreateRect("Text_StudioName", studioContent);
            var studioTextRect = studioTextGo.GetComponent<RectTransform>();
            studioTextRect.anchorMin = new Vector2(0.5f, 0.35f);
            studioTextRect.anchorMax = new Vector2(0.5f, 0.35f);
            studioTextRect.sizeDelta = new Vector2(700, 80);
            var studioTmp = studioTextGo.AddComponent<TextMeshProUGUI>();
            studioTmp.text = "MIYUKI STUDIO";
            studioTmp.fontSize = 52;
            studioTmp.fontStyle = FontStyles.Bold;
            studioTmp.alignment = TextAlignmentOptions.Center;
            studioTmp.color = new Color(1f, 0.95f, 0.85f, 1f);

            // Text Phụ Đề
            GameObject subTextGo = CreateRect("Text_Subtitle", studioContent);
            var subTextRect = subTextGo.GetComponent<RectTransform>();
            subTextRect.anchorMin = new Vector2(0.5f, 0.18f);
            subTextRect.anchorMax = new Vector2(0.5f, 0.18f);
            subTextRect.sizeDelta = new Vector2(600, 50);
            var subTmp = subTextGo.AddComponent<TextMeshProUGUI>();
            subTmp.text = "— G A M E S —";
            subTmp.fontSize = 24;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(0.65f, 0.72f, 0.82f, 0.9f);

            // Gợi ý cho designer
            GameObject hintTextGo = CreateRect("Text_DesignerNote", studioContent);
            var hintRect = hintTextGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, -0.1f);
            hintRect.anchorMax = new Vector2(0.5f, -0.1f);
            hintRect.sizeDelta = new Vector2(600, 40);
            var hintTmp = hintTextGo.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "(Em thoải mái thay Logo, Font chữ và thiết kế lại Panel này theo ý muốn nhé!)";
            hintTmp.fontSize = 18;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.color = new Color(0.5f, 0.6f, 0.7f, 0.5f);

            // ========================================================
            // 3. PANEL 2: LOADING SCREEN (Tông màu xanh lá cây / ngọc lục bảo đẹp mắt)
            // ========================================================
            Color greenThemeColor = new Color(0.08f, 0.22f, 0.14f, 1f);
            GameObject loadingPanel = CreateStretchPanel("Panel_LoadingScreen", canvasGo, greenThemeColor);
            var loadingGroup = loadingPanel.AddComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;
            loadingPanel.SetActive(false);

            // Tiêu đề game ở giữa trên
            GameObject titleContainer = CreateRect("Title_Container", loadingPanel);
            var titleRect = titleContainer.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.65f);
            titleRect.anchorMax = new Vector2(0.5f, 0.65f);
            titleRect.sizeDelta = new Vector2(800, 200);

            GameObject titleTextGo = CreateRect("Text_GameTitle", titleContainer);
            var titleTextRect = titleTextGo.GetComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0.5f, 0.6f);
            titleTextRect.anchorMax = new Vector2(0.5f, 0.6f);
            titleTextRect.sizeDelta = new Vector2(800, 90);
            var titleTmp = titleTextGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "MINEMART";
            titleTmp.fontSize = 68;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.28f, 0.85f, 0.52f, 1f); // Xanh ngọc lục bảo

            GameObject subTitleTextGo = CreateRect("Text_GameSubtitle", titleContainer);
            var subTitleTextRect = subTitleTextGo.GetComponent<RectTransform>();
            subTitleTextRect.anchorMin = new Vector2(0.5f, 0.2f);
            subTitleTextRect.anchorMax = new Vector2(0.5f, 0.2f);
            subTitleTextRect.sizeDelta = new Vector2(800, 45);
            var subTitleTmp = subTitleTextGo.AddComponent<TextMeshProUGUI>();
            subTitleTmp.text = "Idle Mining & Shop Tycoon";
            subTitleTmp.fontSize = 24;
            subTitleTmp.alignment = TextAlignmentOptions.Center;
            subTitleTmp.color = new Color(0.8f, 0.88f, 0.95f, 0.85f);

            // Container chứa thanh Loading ở phía dưới
            GameObject bottomContainer = CreateRect("Bottom_LoadingContainer", loadingPanel);
            var bottomRect = bottomContainer.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0.22f);
            bottomRect.anchorMax = new Vector2(0.5f, 0.22f);
            bottomRect.sizeDelta = new Vector2(860, 260);

            // Thanh Slider Loading Bar
            GameObject sliderGo = CreateRect("Slider_LoadingBar", bottomContainer);
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(800, 32);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;

            // Background Slider
            GameObject sliderBg = CreateRect("Background", sliderGo);
            var bgRect = sliderBg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = sliderBg.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.16f, 0.24f, 1f);

            // Fill Area
            GameObject fillArea = CreateRect("Fill Area", sliderGo);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            // Fill Image
            GameObject fillObj = CreateRect("Fill", fillArea);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.24f, 0.78f, 0.48f, 1f); // Xanh emerald năng động
            slider.fillRect = fillRect;

            // Text Phần Trăm (nằm ngay trên thanh slider)
            GameObject percentTextGo = CreateRect("Text_Percentage", bottomContainer);
            var percentRect = percentTextGo.GetComponent<RectTransform>();
            percentRect.anchorMin = new Vector2(0.5f, 0.75f);
            percentRect.anchorMax = new Vector2(0.5f, 0.75f);
            percentRect.sizeDelta = new Vector2(400, 45);
            var percentTmp = percentTextGo.AddComponent<TextMeshProUGUI>();
            percentTmp.text = "0%";
            percentTmp.fontSize = 32;
            percentTmp.fontStyle = FontStyles.Bold;
            percentTmp.alignment = TextAlignmentOptions.Center;
            percentTmp.color = Color.white;

            // Text Trạng Thái Nạp (nằm ngay dưới thanh slider)
            GameObject statusTextGo = CreateRect("Text_Status", bottomContainer);
            var statusRect = statusTextGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.22f);
            statusRect.anchorMax = new Vector2(0.5f, 0.22f);
            statusRect.sizeDelta = new Vector2(760, 40);
            var statusTmp = statusTextGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Đang tải dữ liệu...";
            statusTmp.fontSize = 22;
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.color = new Color(0.75f, 0.82f, 0.92f, 0.9f);

            // Text Tips Mẹo Chơi (ở dưới cùng màn hình)
            GameObject tipTextGo = CreateRect("Text_Tip", loadingPanel);
            var tipRect = tipTextGo.GetComponent<RectTransform>();
            tipRect.anchorMin = new Vector2(0.5f, 0.08f);
            tipRect.anchorMax = new Vector2(0.5f, 0.08f);
            tipRect.sizeDelta = new Vector2(900, 70);
            var tipTmp = tipTextGo.AddComponent<TextMeshProUGUI>();
            tipTmp.text = "💡 Mẹo: Nâng cấp trạm mỏ sẽ giúp tăng mạnh giá bán quặng và tốc độ khai thác!";
            tipTmp.fontSize = 20;
            tipTmp.alignment = TextAlignmentOptions.Center;
            tipTmp.color = new Color(0.6f, 0.68f, 0.78f, 0.85f);
            tipTmp.textWrappingMode = TextWrappingModes.Normal;

            // ========================================================
            // 4. KẾT NỐI TOÀN BỘ REFERENCES VÀO GameStartupFlowUI
            // ========================================================
            SerializedObject so = new SerializedObject(flowUI);
            so.FindProperty("_studioSplashGroup").objectReferenceValue = studioGroup;
            so.FindProperty("_loadingScreenGroup").objectReferenceValue = loadingGroup;
            so.FindProperty("_loadingBar").objectReferenceValue = slider;
            so.FindProperty("_percentageText").objectReferenceValue = percentTmp;
            so.FindProperty("_statusText").objectReferenceValue = statusTmp;
            so.FindProperty("_tipText").objectReferenceValue = tipTmp;
            so.ApplyModifiedProperties();

            // Chọn Canvas vừa tạo trong Hierarchy
            Selection.activeGameObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);

            Debug.Log("[StartupFlowEditorCreator] ✅ Đã tạo thành công Canvas Startup Flow với đầy đủ Panel Studio và Loading Screen!");
        }

        [MenuItem("MineMart/UI/Đổi Màu Panel Loading Sang Xanh Lá Cây", false, 31)]
        [MenuItem("GameObject/MineMart UI/Đổi Màu Panel Loading Sang Xanh Lá Cây", false, 31)]
        public static void SetLoadingPanelColorToGreen() {
            GameObject target = GameObject.Find("Panel_LoadingScreen");
            if (target == null) {
                var flowUI = Object.FindAnyObjectByType<GameStartupFlowUI>();
                if (flowUI != null) {
                    var child = flowUI.transform.Find("Panel_LoadingScreen");
                    if (child != null) target = child.gameObject;
                }
            }

            if (target != null) {
                var img = target.GetComponent<Image>();
                if (img != null) {
                    Undo.RecordObject(img, "Change Loading Panel Color To Green");
                    img.color = new Color(0.08f, 0.22f, 0.14f, 1f); // #143824 Màu xanh lá cây sang trọng
                    EditorUtility.SetDirty(img);
                    Debug.Log("[StartupFlowEditorCreator] 🌿 Đã cập nhật màu nền của Panel_LoadingScreen sang Xanh Lá Cây sang trọng!");
                    EditorUtility.DisplayDialog("Thành công", "Đã cập nhật màu nền của Panel_LoadingScreen sang màu Xanh Lá Cây sang trọng!", "OK");
                    return;
                }
            }

            EditorUtility.DisplayDialog("Không tìm thấy", "Chưa tìm thấy GameObject 'Panel_LoadingScreen' trong Scene hiện tại. Hãy tạo khung Canvas trước nhé!", "OK");
        }

        private static GameObject CreateStretchPanel(string name, GameObject parent, Color bgColor) {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent.transform, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var img = panel.GetComponent<Image>();
            img.color = bgColor;
            return panel;
        }

        private static GameObject CreateRect(string name, GameObject parent) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
