using MineMart.Core.Events;
using MineMart.UI.Binders;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ sinh giao diện Bảng Cây Nâng Cấp Ngọc Lục Bảo (Emerald Upgrade Tree - EUGT Panel)
    /// và Prefab Thẻ Nâng Cấp Hình Vuông (2 ô mỗi hàng, cuộn dọc)
    /// trực tiếp vào Scene/Project chỉ với 1 cú click để Designer/Developer tự do điều chỉnh trong Edit Mode.
    /// </summary>
    public static class EmeraldUpgradeTreeEditorCreator {
        [MenuItem("GameObject/MineMart UI/Tạo EUGT Panel (Cây Nâng Cấp Ngọc Lục Bảo)", false, 23)]
        [MenuItem("Tools/MineMart/Tạo EUGT Panel Vào Scene", false, 23)]
        public static void CreateEUGTPanelInScene(MenuCommand menuCommand) {
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

            // Kiểm tra và tạo Prefab ô vuông nếu chưa có
            string prefabPath = "Assets/_project/UI/Prefabs/EUGT_NodeCard_Square_Prefab.prefab";
            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (cardPrefab == null) {
                cardPrefab = CreateNodeCardPrefabAsset();
            }

            // Kiểm tra nếu đã có sẵn trong Scene thì xóa cái cũ đi để tạo lại mới sạch sẽ
            var existingDrawer = canvas.transform.Find("EmeraldUpgradeTree_Drawer");
            if (existingDrawer != null) {
                Undo.DestroyObjectImmediate(existingDrawer.gameObject);
            }

            // 1. Root Wrapper (Bao phủ toàn màn hình)
            GameObject rootWrapper = new GameObject("EmeraldUpgradeTree_Drawer", typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(rootWrapper, canvas.gameObject);
            Undo.RegisterCreatedObjectUndo(rootWrapper, "Tạo EUGT Panel");

            var rootRect = rootWrapper.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;

            // 2. Backdrop Button (Bấm ra ngoài để đóng)
            GameObject backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(rootWrapper.transform, false);
            var backRect = backdropGo.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.sizeDelta = Vector2.zero;
            var backImg = backdropGo.GetComponent<Image>();
            backImg.color = new Color(0f, 0f, 0f, 0.65f);
            var backBtn = backdropGo.GetComponent<Button>();

            // 3. Panel EUGT chính (Đặt ở vị trí mở sẵn 0,0 để em nhìn thấy và điều chỉnh ngay trong Edit Mode)
            GameObject panelGo = new GameObject("Panel_EUGT", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(EmeraldUpgradeTreePanelUI));
            panelGo.transform.SetParent(rootWrapper.transform, false);

            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(480f, 750f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);
            var canvasGroup = panelGo.GetComponent<CanvasGroup>();
            var script = panelGo.GetComponent<EmeraldUpgradeTreePanelUI>();

            // 4. Header Bar
            GameObject headerGo = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            headerGo.transform.SetParent(panelGo.transform, false);
            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 75f);
            var headerImg = headerGo.GetComponent<Image>();
            headerImg.color = new Color(0.13f, 0.16f, 0.22f, 1f);

            // Tiêu đề
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.45f);
            titleRect.anchorMax = new Vector2(0.85f, 0.95f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "💎 NÂNG CẤP NGỌC LỤC BẢO";
            titleTmp.fontSize = 20f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = new Color(0.2f, 1.0f, 0.55f);

            // Số lượng nâng cấp (Capacity)
            GameObject capGo = new GameObject("CapacityText", typeof(RectTransform), typeof(TextMeshProUGUI));
            capGo.transform.SetParent(headerGo.transform, false);
            var capRect = capGo.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0.05f, 0.05f);
            capRect.anchorMax = new Vector2(0.85f, 0.48f);
            capRect.sizeDelta = Vector2.zero;
            var capTmp = capGo.GetComponent<TextMeshProUGUI>();
            capTmp.text = "Số lượng nâng cấp: 0 / 8";
            capTmp.fontSize = 13f;
            capTmp.alignment = TextAlignmentOptions.MidlineLeft;
            capTmp.color = new Color(0.75f, 0.85f, 0.95f);

            // Nút Đóng (CloseBtn)
            GameObject closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(headerGo.transform, false);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-16f, 0f);
            closeRect.sizeDelta = new Vector2(40f, 40f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.25f, 0.28f, 0.38f, 1f);
            var closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTxtGo = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            var closeTxtRect = closeTxtGo.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.sizeDelta = Vector2.zero;
            var closeTxtTmp = closeTxtGo.GetComponent<TextMeshProUGUI>();
            closeTxtTmp.text = "✕";
            closeTxtTmp.fontSize = 20f;
            closeTxtTmp.alignment = TextAlignmentOptions.Center;
            closeTxtTmp.color = Color.white;

            // 5. Bottom Bar chứa nút Unlock Next Upgrade
            GameObject bottomBarGo = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            bottomBarGo.transform.SetParent(panelGo.transform, false);
            var btmRect = bottomBarGo.GetComponent<RectTransform>();
            btmRect.anchorMin = new Vector2(0f, 0f);
            btmRect.anchorMax = new Vector2(1f, 0f);
            btmRect.pivot = new Vector2(0.5f, 0f);
            btmRect.anchoredPosition = Vector2.zero;
            btmRect.sizeDelta = new Vector2(0f, 85f);
            var btmImg = bottomBarGo.GetComponent<Image>();
            btmImg.color = new Color(0.12f, 0.14f, 0.19f, 1f);

            GameObject unlockBtnGo = new GameObject("UnlockNextBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            unlockBtnGo.transform.SetParent(bottomBarGo.transform, false);
            var unlRect = unlockBtnGo.GetComponent<RectTransform>();
            unlRect.anchorMin = new Vector2(0.06f, 0.15f);
            unlRect.anchorMax = new Vector2(0.94f, 0.85f);
            unlRect.sizeDelta = Vector2.zero;
            var unlImg = unlockBtnGo.GetComponent<Image>();
            unlImg.color = new Color(0.12f, 0.75f, 0.45f, 1f);
            var unlBtn = unlockBtnGo.GetComponent<Button>();

            GameObject unlTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            unlTxtGo.transform.SetParent(unlockBtnGo.transform, false);
            var unlTxtRect = unlTxtGo.GetComponent<RectTransform>();
            unlTxtRect.anchorMin = Vector2.zero;
            unlTxtRect.anchorMax = Vector2.one;
            unlTxtRect.sizeDelta = Vector2.zero;
            var unlTxtTmp = unlTxtGo.GetComponent<TextMeshProUGUI>();
            unlTxtTmp.text = "MỞ KHÓA NÂNG CẤP TIẾP THEO (10 💎)";
            unlTxtTmp.fontSize = 16f;
            unlTxtTmp.fontStyle = FontStyles.Bold;
            unlTxtTmp.alignment = TextAlignmentOptions.Center;
            unlTxtTmp.color = Color.white;

            // 6. Scroll View ở giữa
            GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRectT = scrollGo.GetComponent<RectTransform>();
            scrollRectT.anchorMin = new Vector2(0f, 0f);
            scrollRectT.anchorMax = new Vector2(1f, 1f);
            scrollRectT.offsetMin = new Vector2(0f, 85f);
            scrollRectT.offsetMax = new Vector2(0f, -75f);

            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // Viewport
            GameObject viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewGo.transform.SetParent(scrollGo.transform, false);
            var viewRect = viewGo.GetComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.sizeDelta = Vector2.zero;

            // Content: Lưới 2 cột (GridLayoutGroup) mỗi hàng 2 prefab hình vuông rồi mới cuộn xuống
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 600f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210f, 210f); // Kích thước hình vuông
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2; // Cố định 2 cột mỗi hàng
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewRect;
            scrollRect.content = contentRect;

            // 7. Detail Popup (Bảng nâng cấp chi tiết)
            GameObject detailRootGo = new GameObject("DetailPopup_Root", typeof(RectTransform));
            detailRootGo.transform.SetParent(rootWrapper.transform, false);
            var detailRootRect = detailRootGo.GetComponent<RectTransform>();
            detailRootRect.anchorMin = Vector2.zero;
            detailRootRect.anchorMax = Vector2.one;
            detailRootRect.sizeDelta = Vector2.zero;

            // Backdrop Popup
            GameObject detailBackGo = new GameObject("PopupBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            detailBackGo.transform.SetParent(detailRootGo.transform, false);
            var detailBackRect = detailBackGo.GetComponent<RectTransform>();
            detailBackRect.anchorMin = Vector2.zero;
            detailBackRect.anchorMax = Vector2.one;
            detailBackRect.sizeDelta = Vector2.zero;
            var detailBackImg = detailBackGo.GetComponent<Image>();
            detailBackImg.color = new Color(0f, 0f, 0f, 0.7f);
            var detailBackBtn = detailBackGo.GetComponent<Button>();

            // Khung Card Popup
            GameObject detailCardGo = new GameObject("DetailCard", typeof(RectTransform), typeof(Image));
            detailCardGo.transform.SetParent(detailRootGo.transform, false);
            var detailCardRect = detailCardGo.GetComponent<RectTransform>();
            detailCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailCardRect.pivot = new Vector2(0.5f, 0.5f);
            detailCardRect.sizeDelta = new Vector2(400f, 380f);
            detailCardRect.anchoredPosition = Vector2.zero;

            var detailCardImg = detailCardGo.GetComponent<Image>();
            detailCardImg.color = new Color(0.13f, 0.16f, 0.22f, 1f);

            // Icon to trong popup
            GameObject dIconGo = new GameObject("DetailIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
            dIconGo.transform.SetParent(detailCardGo.transform, false);
            var dIconRect = dIconGo.GetComponent<RectTransform>();
            dIconRect.anchorMin = new Vector2(0.5f, 1f);
            dIconRect.anchorMax = new Vector2(0.5f, 1f);
            dIconRect.pivot = new Vector2(0.5f, 1f);
            dIconRect.anchoredPosition = new Vector2(0f, -24f);
            dIconRect.sizeDelta = new Vector2(80f, 60f);
            var dIconTmp = dIconGo.GetComponent<TextMeshProUGUI>();
            dIconTmp.fontSize = 48f;
            dIconTmp.alignment = TextAlignmentOptions.Center;

            // Tên nâng cấp
            GameObject dTitleGo = new GameObject("DetailTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            dTitleGo.transform.SetParent(detailCardGo.transform, false);
            var dTitleRect = dTitleGo.GetComponent<RectTransform>();
            dTitleRect.anchorMin = new Vector2(0.5f, 1f);
            dTitleRect.anchorMax = new Vector2(0.5f, 1f);
            dTitleRect.pivot = new Vector2(0.5f, 1f);
            dTitleRect.anchoredPosition = new Vector2(0f, -90f);
            dTitleRect.sizeDelta = new Vector2(360f, 36f);
            var dTitleTmp = dTitleGo.GetComponent<TextMeshProUGUI>();
            dTitleTmp.fontSize = 20f;
            dTitleTmp.fontStyle = FontStyles.Bold;
            dTitleTmp.alignment = TextAlignmentOptions.Center;
            dTitleTmp.color = Color.white;

            // Level text
            GameObject dLevelGo = new GameObject("DetailLevel", typeof(RectTransform), typeof(TextMeshProUGUI));
            dLevelGo.transform.SetParent(detailCardGo.transform, false);
            var dLevelRect = dLevelGo.GetComponent<RectTransform>();
            dLevelRect.anchorMin = new Vector2(0.5f, 1f);
            dLevelRect.anchorMax = new Vector2(0.5f, 1f);
            dLevelRect.pivot = new Vector2(0.5f, 1f);
            dLevelRect.anchoredPosition = new Vector2(0f, -126f);
            dLevelRect.sizeDelta = new Vector2(360f, 28f);
            var dLevelTmp = dLevelGo.GetComponent<TextMeshProUGUI>();
            dLevelTmp.fontSize = 15f;
            dLevelTmp.alignment = TextAlignmentOptions.Center;
            dLevelTmp.color = Color.white;

            // Mô tả
            GameObject dDescGo = new GameObject("DetailDesc", typeof(RectTransform), typeof(TextMeshProUGUI));
            dDescGo.transform.SetParent(detailCardGo.transform, false);
            var dDescRect = dDescGo.GetComponent<RectTransform>();
            dDescRect.anchorMin = new Vector2(0.5f, 1f);
            dDescRect.anchorMax = new Vector2(0.5f, 1f);
            dDescRect.pivot = new Vector2(0.5f, 1f);
            dDescRect.anchoredPosition = new Vector2(0f, -165f);
            dDescRect.sizeDelta = new Vector2(340f, 80f);
            var dDescTmp = dDescGo.GetComponent<TextMeshProUGUI>();
            dDescTmp.fontSize = 14f;
            dDescTmp.alignment = TextAlignmentOptions.Center;
            dDescTmp.color = new Color(0.8f, 0.85f, 0.95f);

            // Nút Nâng Cấp
            GameObject dUpgradeBtnGo = new GameObject("DetailUpgradeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            dUpgradeBtnGo.transform.SetParent(detailCardGo.transform, false);
            var dUpRect = dUpgradeBtnGo.GetComponent<RectTransform>();
            dUpRect.anchorMin = new Vector2(0.5f, 0f);
            dUpRect.anchorMax = new Vector2(0.5f, 0f);
            dUpRect.pivot = new Vector2(0.5f, 0f);
            dUpRect.anchoredPosition = new Vector2(0f, 24f);
            dUpRect.sizeDelta = new Vector2(280f, 54f);
            var dUpImg = dUpgradeBtnGo.GetComponent<Image>();
            dUpImg.color = new Color(0.1f, 0.75f, 0.42f, 1f);
            var dUpBtn = dUpgradeBtnGo.GetComponent<Button>();

            GameObject dUpTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            dUpTxtGo.transform.SetParent(dUpgradeBtnGo.transform, false);
            var dUpTxtRect = dUpTxtGo.GetComponent<RectTransform>();
            dUpTxtRect.anchorMin = Vector2.zero;
            dUpTxtRect.anchorMax = Vector2.one;
            dUpTxtRect.sizeDelta = Vector2.zero;
            var dUpTxtTmp = dUpTxtGo.GetComponent<TextMeshProUGUI>();
            dUpTxtTmp.text = "NÂNG CẤP (10 💎)";
            dUpTxtTmp.fontSize = 16f;
            dUpTxtTmp.fontStyle = FontStyles.Bold;
            dUpTxtTmp.alignment = TextAlignmentOptions.Center;
            dUpTxtTmp.color = Color.white;

            // Nút Đóng Popup (✕)
            GameObject dCloseBtnGo = new GameObject("PopupCloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            dCloseBtnGo.transform.SetParent(detailCardGo.transform, false);
            var dCloseRect = dCloseBtnGo.GetComponent<RectTransform>();
            dCloseRect.anchorMin = new Vector2(1f, 1f);
            dCloseRect.anchorMax = new Vector2(1f, 1f);
            dCloseRect.pivot = new Vector2(1f, 1f);
            dCloseRect.anchoredPosition = new Vector2(-12f, -12f);
            dCloseRect.sizeDelta = new Vector2(34f, 34f);
            var dCloseImg = dCloseBtnGo.GetComponent<Image>();
            dCloseImg.color = new Color(0.25f, 0.28f, 0.38f, 1f);
            var dCloseBtn = dCloseBtnGo.GetComponent<Button>();

            GameObject dCloseTxtGo = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
            dCloseTxtGo.transform.SetParent(dCloseBtnGo.transform, false);
            var dCloseTxtRect = dCloseTxtGo.GetComponent<RectTransform>();
            dCloseTxtRect.anchorMin = Vector2.zero;
            dCloseTxtRect.anchorMax = Vector2.one;
            dCloseTxtRect.sizeDelta = Vector2.zero;
            var dCloseTxtTmp = dCloseTxtGo.GetComponent<TextMeshProUGUI>();
            dCloseTxtTmp.text = "✕";
            dCloseTxtTmp.fontSize = 18f;
            dCloseTxtTmp.alignment = TextAlignmentOptions.Center;
            dCloseTxtTmp.color = Color.white;

            detailRootGo.SetActive(false);

            // Tìm kênh sự kiện EmeraldChanged
            EmeraldChangedEventChannelSO emeraldChannel = null;
            string[] guids = AssetDatabase.FindAssets("t:EmeraldChangedEventChannelSO Event_EmeraldChanged");
            if (guids != null && guids.Length > 0) {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                emeraldChannel = AssetDatabase.LoadAssetAtPath<EmeraldChangedEventChannelSO>(path);
            }

            // Gán SerializedProperties qua SerializedObject
            SerializedObject so = new SerializedObject(script);
            so.FindProperty("_rootWrapper").objectReferenceValue = rootWrapper;
            so.FindProperty("_panelRect").objectReferenceValue = panelRect;
            so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_titleText").objectReferenceValue = titleTmp;
            so.FindProperty("_capacityText").objectReferenceValue = capTmp;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("_backdropButton").objectReferenceValue = backBtn;
            so.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("_itemsContainer").objectReferenceValue = contentGo.transform;
            so.FindProperty("_unlockNextBtn").objectReferenceValue = unlBtn;
            so.FindProperty("_unlockNextText").objectReferenceValue = unlTxtTmp;

            // Gán Prefab item vuông
            if (cardPrefab != null) {
                var itemComponent = cardPrefab.GetComponent<EmeraldUpgradeNodeItemUI>();
                if (itemComponent != null) {
                    so.FindProperty("_nodeItemPrefab").objectReferenceValue = itemComponent;
                }
            }

            so.FindProperty("_detailPopupRoot").objectReferenceValue = detailRootGo;
            so.FindProperty("_detailTitleText").objectReferenceValue = dTitleTmp;
            so.FindProperty("_detailDescText").objectReferenceValue = dDescTmp;
            so.FindProperty("_detailLevelText").objectReferenceValue = dLevelTmp;
            so.FindProperty("_detailIconText").objectReferenceValue = dIconTmp;
            so.FindProperty("_detailUpgradeBtn").objectReferenceValue = dUpBtn;
            so.FindProperty("_detailUpgradeBtnText").objectReferenceValue = dUpTxtTmp;
            so.FindProperty("_detailCloseBtn").objectReferenceValue = dCloseBtn;

            if (emeraldChannel != null) {
                so.FindProperty("_emeraldChangedChannel").objectReferenceValue = emeraldChannel;
            }

            // Danh sách nâng cấp rỗng mặc định để người dùng tự do thêm trên Inspector
            so.FindProperty("_upgradeNodes").ClearArray();
            so.FindProperty("_maxUpgradesCapacity").intValue = 8;
            so.ApplyModifiedProperties();

            // Chọn ngay Panel_EUGT để Inspector mở sẵn cho em điều chỉnh
            Selection.activeGameObject = panelGo;
            EditorGUIUtility.PingObject(panelGo);

            Debug.Log("<color=#10B981>[MineMart UI]</color> ✅ Đã tạo thành công EUGT Panel trên Hierarchy với lưới 2 cột hình vuông! Prefab thẻ nâng cấp đã được gán sẵn tại: Assets/_project/UI/Prefabs/EUGT_NodeCard_Square_Prefab.prefab");
        }

        [MenuItem("GameObject/MineMart UI/Tạo EUGT Node Card Prefab (Hình Vuông)", false, 24)]
        [MenuItem("Tools/MineMart/Tạo EUGT Node Card Prefab (Hình Vuông)", false, 24)]
        public static GameObject CreateNodeCardPrefabAsset() {
            string folderPath = "Assets/_project/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder("Assets/_project/UI/Prefabs")) {
                if (!AssetDatabase.IsValidFolder("Assets/_project/UI")) {
                    AssetDatabase.CreateFolder("Assets/_project", "UI");
                }
                AssetDatabase.CreateFolder("Assets/_project/UI", "Prefabs");
            }

            string prefabPath = $"{folderPath}/EUGT_NodeCard_Square_Prefab.prefab";

            // Tạo GameObject tạm hình vuông 210x210
            GameObject cardGo = new GameObject("EUGT_NodeCard_Square_Prefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EmeraldUpgradeNodeItemUI));
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(210f, 210f);

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.14f, 0.18f, 0.25f, 0.95f);
            var cardBtn = cardGo.GetComponent<Button>();

            // 1. Badge Level ở phía trên
            GameObject levelBadgeGo = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            levelBadgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = levelBadgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 1f);
            badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -12f);
            badgeRect.sizeDelta = new Vector2(140f, 32f);

            var badgeImg = levelBadgeGo.GetComponent<Image>();
            badgeImg.color = new Color(0.12f, 0.75f, 0.45f, 1f);

            GameObject badgeTextGo = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextGo.transform.SetParent(levelBadgeGo.transform, false);
            var badgeTextRect = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;

            var badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = "Lv. 1";
            badgeTmp.fontSize = 13f;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.color = Color.white;

            // 2. Icon Box ở giữa (76x76)
            GameObject iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            iconBoxGo.transform.SetParent(cardGo.transform, false);
            var iconBoxRect = iconBoxGo.GetComponent<RectTransform>();
            iconBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconBoxRect.pivot = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchoredPosition = new Vector2(0f, 6f);
            iconBoxRect.sizeDelta = new Vector2(76f, 76f);

            var iconBoxImg = iconBoxGo.GetComponent<Image>();
            iconBoxImg.color = new Color(0.18f, 0.24f, 0.35f, 1f);

            GameObject iconTextGo = new GameObject("IconText", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconTextGo.transform.SetParent(iconBoxGo.transform, false);
            var iconTextRect = iconTextGo.GetComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.sizeDelta = Vector2.zero;

            var iconTmp = iconTextGo.GetComponent<TextMeshProUGUI>();
            iconTmp.text = "💎";
            iconTmp.fontSize = 38f;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = Color.white;

            // Image sprite tùy chọn (ẩn mặc định nếu dùng icon text emoji)
            GameObject iconSpriteGo = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
            iconSpriteGo.transform.SetParent(iconBoxGo.transform, false);
            var iconSpriteRect = iconSpriteGo.GetComponent<RectTransform>();
            iconSpriteRect.anchorMin = Vector2.zero;
            iconSpriteRect.anchorMax = Vector2.one;
            iconSpriteRect.sizeDelta = Vector2.zero;
            var iconSpriteImg = iconSpriteGo.GetComponent<Image>();
            iconSpriteImg.color = Color.white;
            iconSpriteGo.SetActive(false);

            // 3. Tên Nâng Cấp ở phía dưới
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0f);
            nameRect.anchorMax = new Vector2(0.95f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 14f);
            nameRect.sizeDelta = new Vector2(0f, 40f);

            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = "Tên Nâng Cấp";
            nameTmp.fontSize = 14f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.white;

            // 4. Khóa Overlay (Lock Overlay)
            GameObject lockOverlayGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
            lockOverlayGo.transform.SetParent(cardGo.transform, false);
            var lockRect = lockOverlayGo.GetComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero;
            lockRect.anchorMax = Vector2.one;
            lockRect.sizeDelta = Vector2.zero;
            var lockImg = lockOverlayGo.GetComponent<Image>();
            lockImg.color = new Color(0f, 0f, 0f, 0.45f);
            lockOverlayGo.SetActive(false); // Mặc định mở

            // Gán SerializedProperties vào EmeraldUpgradeNodeItemUI
            var itemScript = cardGo.GetComponent<EmeraldUpgradeNodeItemUI>();
            var soItem = new SerializedObject(itemScript);
            soItem.FindProperty("_bgImage").objectReferenceValue = bgImg;
            soItem.FindProperty("_cardButton").objectReferenceValue = cardBtn;
            soItem.FindProperty("_iconImage").objectReferenceValue = iconSpriteImg;
            soItem.FindProperty("_iconText").objectReferenceValue = iconTmp;
            soItem.FindProperty("_nameText").objectReferenceValue = nameTmp;
            soItem.FindProperty("_levelBadgeText").objectReferenceValue = badgeTmp;
            soItem.FindProperty("_levelBadgeBg").objectReferenceValue = badgeImg;
            soItem.FindProperty("_lockOverlay").objectReferenceValue = lockOverlayGo;
            soItem.ApplyModifiedProperties();

            // Lưu thành Prefab Asset
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardGo, prefabPath);
            Object.DestroyImmediate(cardGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#10B981>[MineMart UI]</color> ✅ Đã tạo Prefab Thẻ Nâng Cấp Hình Vuông tại: {prefabPath}");
            return savedPrefab;
        }
    }
}
