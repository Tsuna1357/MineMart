using MineMart.UI.Binders;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.Editor {
    public static class MapUpgradeUIEditorCreator {
        [MenuItem("GameObject/MineMart UI/Tạo Map Upgrade Panel (Trượt Từ Dưới Lên)", false, 20)]
        public static void CreatePanelInScene(MenuCommand menuCommand) {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) {
                GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 1. Root Container (Bao trùm toàn màn hình để chứa Backdrop Button)
            GameObject rootWrapper = new GameObject("MapUpgradePanel_Wrapper", typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(rootWrapper, canvas.gameObject);
            Undo.RegisterCreatedObjectUndo(rootWrapper, "Create Map Upgrade Panel");

            var wrapperRect = rootWrapper.GetComponent<RectTransform>();
            wrapperRect.anchorMin = Vector2.zero;
            wrapperRect.anchorMax = Vector2.one;
            wrapperRect.sizeDelta = Vector2.zero;
            wrapperRect.anchoredPosition = Vector2.zero;

            // 2. Backdrop Button (Bấm vào vùng mờ xung quanh để đóng bảng)
            GameObject backdropGo = new GameObject("BackdropButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(rootWrapper.transform, false);
            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.sizeDelta = Vector2.zero;
            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.45f); // Lớp mờ đen nhẹ
            var backdropBtn = backdropGo.GetComponent<Button>();

            // 3. Panel chính (Nằm ở đáy, sẽ trượt từ dưới lên)
            GameObject panelGo = new GameObject("MapUpgradePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(MapUpgradePanelUI));
            panelGo.transform.SetParent(rootWrapper.transform, false);

            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(500f, 680f);
            panelRect.anchoredPosition = new Vector2(0f, 40f); // Vị trí mở cố định cách đáy 40px

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.13f, 0.98f); // Nền tối bo tròn thanh lịch

            var canvasGroup = panelGo.GetComponent<CanvasGroup>();

            // 4. Header: Tiêu đề
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            titleRect.sizeDelta = new Vector2(-100f, 36f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "NÂNG CẤP BẢN ĐỒ";
            titleTmp.fontSize = 19f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // 5. Header: Nút X Đóng
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-14f, -12f);
            closeRect.sizeDelta = new Vector2(36f, 36f);

            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.24f, 0.28f, 0.36f, 0.9f);
            var closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            var closeTextRect = closeTextGo.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            var closeTmp = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTmp.text = "✕";
            closeTmp.fontSize = 20f;
            closeTmp.fontStyle = FontStyles.Bold;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // 6. ScrollView cuộn dọc
            GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.sizeDelta = new Vector2(-24f, -74f);
            scrollRect.anchoredPosition = new Vector2(0f, -14f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;

            // 7. Viewport
            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0f, 1f);

            // 8. Content (Danh sách thẻ nâng cấp xếp dọc)
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 10, 16);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            // 9. Dòng chữ thông báo rỗng
            GameObject emptyNoticeGo = new GameObject("EmptyNoticeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyNoticeGo.transform.SetParent(panelGo.transform, false);
            var emptyNoticeRect = emptyNoticeGo.GetComponent<RectTransform>();
            emptyNoticeRect.anchorMin = new Vector2(0.1f, 0.3f);
            emptyNoticeRect.anchorMax = new Vector2(0.9f, 0.6f);
            emptyNoticeRect.sizeDelta = Vector2.zero;
            var emptyNoticeTmp = emptyNoticeGo.GetComponent<TextMeshProUGUI>();
            emptyNoticeTmp.text = "Bản đồ này hiện không có hạng mục nâng cấp!";
            emptyNoticeTmp.fontSize = 16f;
            emptyNoticeTmp.alignment = TextAlignmentOptions.Center;
            emptyNoticeTmp.color = new Color(0.75f, 0.8f, 0.9f, 0.8f);
            emptyNoticeGo.SetActive(false);

            // 10. Gán SerializedField vào MapUpgradePanelUI
            var panelUI = panelGo.GetComponent<MapUpgradePanelUI>();
            var serialized = new SerializedObject(panelUI);
            serialized.FindProperty("_panelRect").objectReferenceValue = panelRect;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serialized.FindProperty("_titleText").objectReferenceValue = titleTmp;
            serialized.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            serialized.FindProperty("_backdropButton").objectReferenceValue = backdropBtn;
            serialized.FindProperty("_scrollRect").objectReferenceValue = scroll;
            serialized.FindProperty("_cardsContainer").objectReferenceValue = contentGo.transform;
            serialized.FindProperty("_emptyNoticeText").objectReferenceValue = emptyNoticeTmp;
            serialized.FindProperty("_targetOpenY").floatValue = 40f;
            serialized.ApplyModifiedProperties();

            // Tự động kiểm tra và gán Prefab nếu đã tạo
            string cardPrefabPath = "Assets/_project/UI/Prefabs/MapUpgradeCard_Prefab.prefab";
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cardPrefabPath);
            if (cardPrefab != null) {
                serialized.Update();
                serialized.FindProperty("_upgradeCardPrefab").objectReferenceValue = cardPrefab;
                serialized.ApplyModifiedProperties();
            }

            Selection.activeGameObject = panelGo;
            Debug.Log("<color=#10B981>[MineMart UI]</color> Đã tạo thành công MapUpgradePanelUI dạng trượt từ dưới lên trong Scene!");
        }

        [MenuItem("GameObject/MineMart UI/Tạo Map Upgrade Card Prefab", false, 21)]
        public static GameObject CreateCardPrefabAsset() {
            string folderPath = "Assets/_project/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder("Assets/_project/UI/Prefabs")) {
                if (!AssetDatabase.IsValidFolder("Assets/_project/UI")) {
                    AssetDatabase.CreateFolder("Assets/_project", "UI");
                }
                AssetDatabase.CreateFolder("Assets/_project/UI", "Prefabs");
            }

            string prefabPath = $"{folderPath}/MapUpgradeCard_Prefab.prefab";

            // Tạo GameObject tạm
            GameObject cardGo = new GameObject("MapUpgradeCard_Prefab", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(MapUpgradeCardUI));
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(460f, 105f);

            var layoutElement = cardGo.GetComponent<LayoutElement>();
            layoutElement.minHeight = 105f;
            layoutElement.preferredHeight = 105f;

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.14f, 0.16f, 0.22f, 0.98f);

            // 1. Icon bên trái
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.03f, 0.5f);
            iconRect.anchorMax = new Vector2(0.03f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(64f, 64f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = Color.white;

            // 2. Tên nâng cấp
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.19f, 0.52f);
            titleRect.anchorMax = new Vector2(0.68f, 0.92f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Thêm Thợ Khai Thác";
            titleTmp.fontSize = 17f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = Color.white;

            // 3. Mô tả công dụng
            GameObject descGo = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(cardGo.transform, false);
            var descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.19f, 0.10f);
            descRect.anchorMax = new Vector2(0.68f, 0.52f);
            descRect.sizeDelta = Vector2.zero;
            var descTmp = descGo.GetComponent<TextMeshProUGUI>();
            descTmp.text = "+1 Thợ khai thác hỗ trợ đào khoáng sản";
            descTmp.fontSize = 12f;
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.color = new Color(0.72f, 0.78f, 0.88f);

            // 4. Nút Mua (Buy Button)
            GameObject btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.70f, 0.18f);
            btnRect.anchorMax = new Vector2(0.97f, 0.82f);
            btnRect.sizeDelta = Vector2.zero;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.1f, 0.75f, 0.35f, 1f);
            var buyBtn = btnGo.GetComponent<Button>();

            // 5. Chữ giá tiền trên nút
            GameObject costGo = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
            costGo.transform.SetParent(btnGo.transform, false);
            var costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = Vector2.one;
            costRect.sizeDelta = Vector2.zero;
            var costTmp = costGo.GetComponent<TextMeshProUGUI>();
            costTmp.text = "100 Vàng";
            costTmp.fontSize = 13f;
            costTmp.fontStyle = FontStyles.Bold;
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.color = Color.white;

            // 6. Gán tham chiếu
            var cardUI = cardGo.GetComponent<MapUpgradeCardUI>();
            var serialized = new SerializedObject(cardUI);
            serialized.FindProperty("_iconImage").objectReferenceValue = iconImg;
            serialized.FindProperty("_titleText").objectReferenceValue = titleTmp;
            serialized.FindProperty("_descriptionText").objectReferenceValue = descTmp;
            serialized.FindProperty("_buyButton").objectReferenceValue = buyBtn;
            serialized.FindProperty("_buttonBackground").objectReferenceValue = btnImg;
            serialized.FindProperty("_costText").objectReferenceValue = costTmp;
            serialized.ApplyModifiedProperties();

            // Lưu thành Prefab Asset
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardGo, prefabPath);
            Object.DestroyImmediate(cardGo);

            // Gán luôn vào MapUpgradePanelUI trong scene nếu có
            var panelUI = Object.FindFirstObjectByType<MapUpgradePanelUI>();
            if (panelUI != null) {
                var serializedPanel = new SerializedObject(panelUI);
                serializedPanel.FindProperty("_upgradeCardPrefab").objectReferenceValue = savedPrefab;
                serializedPanel.ApplyModifiedProperties();
                Debug.Log("<color=#10B981>[MineMart UI]</color> Đã tự động gán MapUpgradeCard_Prefab vào MapUpgradePanelUI trong Scene!");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log($"<color=#10B981>[MineMart UI]</color> Đã tạo thành công Prefab thẻ nâng cấp tại: {prefabPath}");
            return savedPrefab;
        }
    }
}
