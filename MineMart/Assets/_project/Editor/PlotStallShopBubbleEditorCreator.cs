using MineMart.UI.Binders;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.Editor {
    public static class PlotStallShopBubbleEditorCreator {
        [MenuItem("GameObject/MineMart UI/Tạo Plot Stall Shop Bubble (Cuộn Ngang)", false, 10)]
        public static void CreateBubbleInScene(MenuCommand menuCommand) {
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

            // 1. Root Bubble
            GameObject bubbleRoot = new GameObject("PlotStallShopBubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(PlotStallShopBubble));
            GameObjectUtility.SetParentAndAlign(bubbleRoot, canvas.gameObject);
            Undo.RegisterCreatedObjectUndo(bubbleRoot, "Create Plot Stall Shop Bubble");

            var rootRect = bubbleRoot.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(440f, 230f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            var rootImg = bubbleRoot.GetComponent<Image>();
            rootImg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f); // Nền tối bo tròn thanh lịch

            var canvasGroup = bubbleRoot.GetComponent<CanvasGroup>();

            // 2. Header: Title
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(bubbleRoot.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-80f, 32f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "CHỌN QUẦY MỎ";
            titleTmp.fontSize = 18f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // 3. Header: Close Button
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(bubbleRoot.transform, false);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -8f);
            closeRect.sizeDelta = new Vector2(34f, 34f);

            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.25f, 0.28f, 0.35f, 0.8f);
            var closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            var closeTextRect = closeTextGo.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            var closeTmp = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTmp.text = "✕";
            closeTmp.fontSize = 18f;
            closeTmp.fontStyle = FontStyles.Bold;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // 4. ScrollRect (Vùng cuộn ngang)
            GameObject scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(bubbleRoot.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.sizeDelta = new Vector2(-20f, -60f); // chừa lề trên dưới
            scrollRect.anchoredPosition = new Vector2(0f, -12f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;

            // 5. Viewport (Khung nhìn cắt viền)
            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0f, 1f);

            // 6. Content (Nơi chứa các thẻ mỏ)
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false; // Để false để thẻ tự do giữ nguyên chiều cao (ví dụ 200px)
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            // 7. Empty Notice Text
            GameObject noticeGo = new GameObject("EmptyNoticeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            noticeGo.transform.SetParent(bubbleRoot.transform, false);
            var noticeRect = noticeGo.GetComponent<RectTransform>();
            noticeRect.anchorMin = new Vector2(0.1f, 0.2f);
            noticeRect.anchorMax = new Vector2(0.9f, 0.7f);
            noticeRect.sizeDelta = Vector2.zero;
            var noticeTmp = noticeGo.GetComponent<TextMeshProUGUI>();
            noticeTmp.text = "Tất cả các mỏ trên bản đồ đã được xây dựng!";
            noticeTmp.fontSize = 15f;
            noticeTmp.alignment = TextAlignmentOptions.Center;
            noticeTmp.color = new Color(0.8f, 0.85f, 0.9f, 0.8f);
            noticeGo.SetActive(false);

            // 8. Tự động gán SerializedField vào PlotStallShopBubble
            var bubble = bubbleRoot.GetComponent<PlotStallShopBubble>();
            var serializedBubble = new SerializedObject(bubble);
            serializedBubble.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedBubble.FindProperty("_titleText").objectReferenceValue = titleTmp;
            serializedBubble.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            serializedBubble.FindProperty("_scrollRect").objectReferenceValue = scroll;
            serializedBubble.FindProperty("_cardsContainer").objectReferenceValue = contentGo.transform;
            serializedBubble.FindProperty("_emptyNoticeText").objectReferenceValue = noticeTmp;
            serializedBubble.ApplyModifiedProperties();

            Selection.activeGameObject = bubbleRoot;
            Debug.Log("<color=#10B981>[MineMart UI]</color> Đã tạo thành công PlotStallShopBubble với ScrollRect cuộn ngang hoàn chỉnh!");
        }

        [MenuItem("GameObject/MineMart UI/Tạo Stall Card Prefab (Asset Thẻ Mỏ)", false, 11)]
        public static GameObject CreateCardPrefabAsset() {
            string folderPath = "Assets/_project/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder("Assets/_project/UI/Prefabs")) {
                if (!AssetDatabase.IsValidFolder("Assets/_project/UI")) {
                    AssetDatabase.CreateFolder("Assets/_project", "UI");
                }
                AssetDatabase.CreateFolder("Assets/_project/UI", "Prefabs");
            }

            string prefabPath = $"{folderPath}/StallCard_Prefab.prefab";

            // Tạo GameObject tạm để biến thành Prefab kèm LayoutElement
            GameObject cardGo = new GameObject("StallCard_Prefab", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(PlotStallCardUI));
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(170f, 150f);

            var layoutElement = cardGo.GetComponent<LayoutElement>();
            layoutElement.minWidth = 170f;
            layoutElement.preferredWidth = 170f;
            layoutElement.minHeight = 150f;
            layoutElement.preferredHeight = 150f;

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);

            // 1. Tên Quầy Mỏ
            GameObject titleGo = new GameObject("StallNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.74f);
            titleRect.anchorMax = new Vector2(0.95f, 0.96f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Mỏ Gỗ";
            titleTmp.fontSize = 15f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // 2. Điểm neo Preview Block 3D
            GameObject holderGo = new GameObject("BlockPreviewHolder", typeof(RectTransform));
            holderGo.transform.SetParent(cardGo.transform, false);
            var holderRect = holderGo.GetComponent<RectTransform>();
            holderRect.anchorMin = new Vector2(0.5f, 0.52f);
            holderRect.anchorMax = new Vector2(0.5f, 0.52f);
            holderRect.sizeDelta = new Vector2(50f, 50f);
            holderRect.anchoredPosition = Vector2.zero;

            // 3. Thông tin Block (Tên block hiển thị nhỏ)
            GameObject infoGo = new GameObject("BlockInfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            infoGo.transform.SetParent(cardGo.transform, false);
            var infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.05f, 0.30f);
            infoRect.anchorMax = new Vector2(0.95f, 0.44f);
            infoRect.sizeDelta = Vector2.zero;
            var infoTmp = infoGo.GetComponent<TextMeshProUGUI>();
            infoTmp.text = "Khối Gỗ";
            infoTmp.fontSize = 11f;
            infoTmp.alignment = TextAlignmentOptions.Center;
            infoTmp.color = new Color(0.72f, 0.8f, 0.92f);

            // 4. Nút Mua (Buy Button)
            GameObject btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.1f, 0.06f);
            btnRect.anchorMax = new Vector2(0.9f, 0.26f);
            btnRect.sizeDelta = Vector2.zero;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.1f, 0.75f, 0.35f, 1f); // Màu xanh lá sáng đủ tiền
            var buyBtn = btnGo.GetComponent<Button>();

            // 5. Giá tiền trên nút
            GameObject costGo = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
            costGo.transform.SetParent(btnGo.transform, false);
            var costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = Vector2.one;
            costRect.sizeDelta = Vector2.zero;
            var costTmp = costGo.GetComponent<TextMeshProUGUI>();
            costTmp.text = "50 Vàng";
            costTmp.fontSize = 13f;
            costTmp.fontStyle = FontStyles.Bold;
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.color = Color.white;

            // 6. Gắn dây vào component PlotStallCardUI
            var cardUI = cardGo.GetComponent<PlotStallCardUI>();
            var serializedCard = new SerializedObject(cardUI);
            serializedCard.FindProperty("_stallNameText").objectReferenceValue = titleTmp;
            serializedCard.FindProperty("_blockPreviewHolder").objectReferenceValue = holderGo.transform;
            serializedCard.FindProperty("_blockInfoText").objectReferenceValue = infoTmp;
            serializedCard.FindProperty("_costText").objectReferenceValue = costTmp;
            serializedCard.FindProperty("_buyButton").objectReferenceValue = buyBtn;
            serializedCard.FindProperty("_buttonBackground").objectReferenceValue = btnImg;
            serializedCard.ApplyModifiedProperties();

            // Lưu thành Prefab Asset
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardGo, prefabPath);
            Object.DestroyImmediate(cardGo);

            // Nếu trong scene đang có PlotStallShopBubble thì gán luôn vào
            var bubble = Object.FindFirstObjectByType<PlotStallShopBubble>();
            if (bubble != null) {
                var serializedBubble = new SerializedObject(bubble);
                serializedBubble.FindProperty("_stallCardPrefab").objectReferenceValue = savedPrefab;
                serializedBubble.ApplyModifiedProperties();
                Debug.Log($"<color=#10B981>[MineMart UI]</color> Đã tự động gán StallCard_Prefab vào PlotStallShopBubble trong Scene!");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log($"<color=#10B981>[MineMart UI]</color> Đã tạo Prefab thẻ mỏ thành công tại: {prefabPath}");
            return savedPrefab;
        }
    }
}
