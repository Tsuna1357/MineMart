using System;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Core.Math;
using MineMart.Data;
using MineMart.Economy;
using MineMart.Gameplay.Map;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Bong bóng Cửa hàng Quầy Mỏ trên Ô Đất Trống (Plot Stall Shop Bubble).
    /// Hiển thị trên Canvas 2D bám theo tọa độ 3D của ô đất trống (WorldToScreenPoint).
    /// Cho phép người chơi cuộn ngang danh sách các mỏ chưa mua trên bản đồ và bấm chọn mua.
    /// </summary>
    public class PlotStallShopBubble : MonoBehaviour {
        public static PlotStallShopBubble Instance { get; private set; }

        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Header Displays")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _closeButton;

        [Header("Horizontal Scroll View")]
        [Tooltip("ScrollRect dùng để cuộn ngang danh sách mỏ")]
        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("Transform chứa các thẻ mỏ (Content của ScrollRect)")]
        [SerializeField] private Transform _cardsContainer;

        [Tooltip("Prefab của từng thẻ mỏ (Tùy chọn: nếu để trống code sẽ tự động dựng thẻ fallback đẹp mắt)")]
        [SerializeField] private GameObject _stallCardPrefab;

        [Tooltip("Dòng chữ thông báo khi đã mua hết tất cả các mỏ trên Map này")]
        [SerializeField] private TextMeshProUGUI _emptyNoticeText;

        [Header("Visual Colors")]
        [SerializeField] private Color _canAffordColor = new Color(0.1f, 0.75f, 0.35f, 1f);     // Xanh lá sáng khi đủ tiền
        [SerializeField] private Color _cannotAffordColor = new Color(0.45f, 0.5f, 0.55f, 1f); // Xám khi thiếu tiền

        [Header("Positioning Settings")]
        [Tooltip("Độ cao nhô lên phía trên tâm ô đất (mặc định: 2.2m)")]
        [SerializeField] private float _yOffset = 2.2f;

        [Header("Event Channels")]
        [SerializeField] private GoldChangedEventChannelSO _goldChangedChannel;

        private RectTransform _rectTransform;
        private Camera _mainCamera;
        private Canvas _parentCanvas;
        private WorkstationPlot _currentPlot;
        private readonly List<PlotStallCardUI> _cardPool = new List<PlotStallCardUI>();

        public bool IsShowing => _canvasGroup != null && _canvasGroup.alpha > 0.01f;
        public WorkstationPlot CurrentPlot => _currentPlot;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            }

            _rectTransform = GetComponent<RectTransform>();
            _mainCamera = Camera.main;

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            AutoFindReferences();
            EnsureButtonHitboxes();
        }

        private void EnsureButtonHitboxes() {
            if (_closeButton != null) {
                var rt = _closeButton.GetComponent<RectTransform>();
                if (rt != null) {
                    Vector2 sz = rt.sizeDelta;
                    if (sz.x < 54f || sz.y < 54f) {
                        rt.sizeDelta = new Vector2(Mathf.Max(54f, sz.x), Mathf.Max(54f, sz.y));
                    }
                }
            }
        }

        private void AutoFindReferences() {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_scrollRect == null) {
                _scrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (_scrollRect != null && _cardsContainer == null) {
                _cardsContainer = _scrollRect.content != null ? _scrollRect.content : _scrollRect.transform;
            }

            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts) {
                string lower = t.gameObject.name.ToLower();
                if (_titleText == null && (lower.Contains("title") || lower.Contains("header") || lower.Contains("tieu"))) _titleText = t;
                else if (_emptyNoticeText == null && (lower.Contains("empty") || lower.Contains("none") || lower.Contains("het") || lower.Contains("notice"))) _emptyNoticeText = t;
            }

            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons) {
                string lower = b.gameObject.name.ToLower();
                if (_closeButton == null && (lower.Contains("close") || lower.Contains("exit") || lower.Contains("cancel") || lower == "x" || lower.Contains("dong"))) {
                    _closeButton = b;
                    break;
                }
            }
        }

        private void OnEnable() {
            WorkstationPlacementController.OnEmptyPlotTapped += HandleEmptyPlotTapped;
            WorkstationPlacementController.OnStationTapped += HandleStationTapped;
            WorkstationPlacementController.OnBackgroundTapped += HandleBackgroundTapped;

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged += HandleGoldChanged;
            }

            if (_closeButton != null) {
                _closeButton.onClick.AddListener(Hide);
            }

            // Mặc định ẩn bằng CanvasGroup và tắt thẻ con, GameObject cha vẫn active nhận event
            Hide();
        }

        private void OnDisable() {
            WorkstationPlacementController.OnEmptyPlotTapped -= HandleEmptyPlotTapped;
            WorkstationPlacementController.OnStationTapped -= HandleStationTapped;
            WorkstationPlacementController.OnBackgroundTapped -= HandleBackgroundTapped;

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged -= HandleGoldChanged;
            }

            if (_closeButton != null) {
                _closeButton.onClick.RemoveListener(Hide);
            }
        }

        private void LateUpdate() {
            if (_currentPlot == null || !IsShowing) return;

            UpdateScreenPosition();
        }

        private Vector3 GetTargetWorldPosition() {
            if (_currentPlot == null) return Vector3.zero;
            Transform anchor = _currentPlot.SnapAnchor;
            Vector3 centerPos = anchor != null ? anchor.position : _currentPlot.transform.position;
            return centerPos + Vector3.up * _yOffset;
        }

        private void UpdateScreenPosition() {
            if (_currentPlot == null) return;

            if (_mainCamera == null) {
                _mainCamera = Camera.main;
                if (_mainCamera == null) {
                    _mainCamera = FindFirstObjectByType<Camera>();
                    if (_mainCamera == null) return;
                }
            }

            Vector3 worldPos = GetTargetWorldPosition();
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Nếu đối tượng nằm phía sau camera (z <= 0) thì ẩn tạm thời
            if (screenPos.z <= 0) {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            // Khi đối tượng quay lại trước camera thì hiện lại
            if (_canvasGroup != null && _canvasGroup.alpha < 0.01f && _currentPlot != null) {
                _canvasGroup.alpha = 1f;
            }

            if (_parentCanvas == null) {
                _parentCanvas = GetComponentInParent<Canvas>();
            }

            RectTransform parentRect = _rectTransform.parent as RectTransform;
            Camera uiCamera = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (_parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : _mainCamera)
                : null;

            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCamera, out Vector2 localPoint)) {
                // Tự động kẹp mép màn hình (Screen Padding Clamping)
                float scaleX = Mathf.Abs(_rectTransform.localScale.x);
                float scaleY = Mathf.Abs(_rectTransform.localScale.y);
                if (scaleX < 0.01f) scaleX = 1f;
                if (scaleY < 0.01f) scaleY = 1f;

                float halfW = _rectTransform.rect.width * 0.5f * scaleX;
                float halfH = _rectTransform.rect.height * 0.5f * scaleY;
                const float padding = 16f;

                float minX = parentRect.rect.xMin + halfW + padding;
                float maxX = parentRect.rect.xMax - halfW - padding;
                float minY = parentRect.rect.yMin + halfH + padding;
                float maxY = parentRect.rect.yMax - halfH - padding;

                if (minX <= maxX) localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
                if (minY <= maxY) localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

                _rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            } else {
                _rectTransform.position = screenPos;
            }
        }

        /// <summary>
        /// Mở bong bóng chọn quầy mỏ cho một ô đất trống.
        /// </summary>
        public void Show(WorkstationPlot plot) {
            if (plot == null || !plot.IsEmpty) return;

            // Chặn mở nếu bất kỳ bảng UI toàn màn hình nào đang mở
            if (EmeraldUpgradeTreePanelUI.Instance != null && EmeraldUpgradeTreePanelUI.Instance.IsOpen) return;
            if (SettingsDialogUI.Instance != null && SettingsDialogUI.Instance.IsOpen) return;
            if (MapUpgradePanelUI.Instance != null && MapUpgradePanelUI.Instance.IsOpen) {
                MapUpgradePanelUI.Instance.Close();
            }

            // Đóng bong bóng nâng cấp mỏ nếu đang mở
            if (StationUpgradeBubble.Instance != null && StationUpgradeBubble.Instance.IsShowing) {
                StationUpgradeBubble.Instance.Hide();
            }

            _currentPlot = plot;

            if (_scrollRect != null && !_scrollRect.gameObject.activeSelf) {
                _scrollRect.gameObject.SetActive(true);
            }
            if (_cardsContainer != null && !_cardsContainer.gameObject.activeSelf) {
                _cardsContainer.gameObject.SetActive(true);
            }

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            transform.SetAsLastSibling();
            PopulateStallCards();
            UpdateScreenPosition();
            PunchScaleEffect();

            Debug.Log($"<color=#38BDF8>[PlotStallShopBubble]</color> Đã mở danh sách mỏ cho: {plot.name}");
        }

        /// <summary>
        /// Đóng bong bóng cửa hàng mỏ và dọn dẹp sạch sẽ toàn bộ model 3D.
        /// </summary>
        public void Hide() {
            _currentPlot = null;

            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            // Dọn dẹp sạch sẽ 100% tất cả model 3D của thẻ trong pool
            for (int i = 0; i < _cardPool.Count; i++) {
                if (_cardPool[i] != null) {
                    _cardPool[i].Cleanup3DModel();
                    _cardPool[i].gameObject.SetActive(false);
                }
            }

            if (_cardsContainer != null && _cardsContainer.gameObject.activeSelf) {
                _cardsContainer.gameObject.SetActive(false);
            }

            if (_scrollRect != null && _scrollRect.gameObject.activeSelf) {
                _scrollRect.gameObject.SetActive(false);
            }
        }

        private void HandleEmptyPlotTapped(WorkstationPlot plot) {
            if (plot == null) return;

            // Nếu bảng nâng cấp map, cây nâng cấp ngọc, hoặc cài đặt đang mở -> không mở shop ô đất
            if (MapUpgradePanelUI.Instance != null && MapUpgradePanelUI.Instance.IsOpen) return;
            if (EmeraldUpgradeTreePanelUI.Instance != null && EmeraldUpgradeTreePanelUI.Instance.IsOpen) return;
            if (SettingsDialogUI.Instance != null && SettingsDialogUI.Instance.IsOpen) return;

            // Nếu bấm lại đúng ô đang mở -> đóng; nếu bấm ô khác -> mở ô đó
            if (IsShowing && _currentPlot == plot) {
                Hide();
            } else {
                Show(plot);
            }
        }

        private void HandleStationTapped(MiningStation station) {
            // Khi người chơi chạm vào mỏ đã có -> đóng bảng chọn mỏ trống
            if (IsShowing) {
                Hide();
            }
        }

        private void HandleBackgroundTapped() {
            if (IsShowing) {
                Hide();
            }
        }

        private void HandleGoldChanged(double newBalance) {
            if (IsShowing) {
                RefreshAffordability();
            }
        }

        private void RefreshAffordability() {
            for (int i = 0; i < _cardPool.Count; i++) {
                if (_cardPool[i] != null && _cardPool[i].gameObject.activeSelf) {
                    _cardPool[i].RefreshAffordability();
                }
            }
        }

        /// <summary>
        /// Nạp danh sách các quầy mỏ từ MapManager vào khung cuộn ngang.
        /// Sử dụng Object Pooling: Tái sử dụng các thẻ đã tạo, tuyệt đối không Destroy/Instantiate liên tục!
        /// </summary>
        private void PopulateStallCards() {
            if (_titleText != null) {
                _titleText.text = _currentPlot != null ? $"CHỌN MỎ — {_currentPlot.PlotName}" : "CHỌN QUẦY MỎ";
            }

            // Đảm bảo HorizontalLayoutGroup không ép bóp méo chiều cao của thẻ con
            if (_cardsContainer != null) {
                var layout = _cardsContainer.GetComponent<HorizontalLayoutGroup>();
                if (layout != null) {
                    layout.childControlHeight = false;
                    layout.childForceExpandHeight = false;
                    layout.childAlignment = TextAnchor.MiddleCenter;
                }
            }

            // Lấy danh sách mỏ chưa mua trên bản đồ từ MapManager
            List<StallConfigOnMap> unpurchased = MapManager.Instance != null
                ? MapManager.Instance.GetUnpurchasedStalls()
                : new List<StallConfigOnMap>();

            if (unpurchased == null || unpurchased.Count == 0) {
                if (_emptyNoticeText != null) {
                    _emptyNoticeText.gameObject.SetActive(true);
                    _emptyNoticeText.text = "Tất cả các mỏ trên bản đồ đã được xây dựng!";
                }
                if (_scrollRect != null) {
                    _scrollRect.gameObject.SetActive(false);
                }

                // Ẩn toàn bộ thẻ trong pool khi hết mỏ
                for (int i = 0; i < _cardPool.Count; i++) {
                    if (_cardPool[i] != null) _cardPool[i].gameObject.SetActive(false);
                }
                return;
            }

            if (_emptyNoticeText != null) {
                _emptyNoticeText.gameObject.SetActive(false);
            }
            if (_scrollRect != null) {
                _scrollRect.gameObject.SetActive(true);
                _scrollRect.horizontalNormalizedPosition = 0f; // Reset về đầu danh sách
            }

            Transform container = _cardsContainer != null ? _cardsContainer : transform;
            float maxCardHeight = 150f;

            // 1. TÁI SỬ DỤNG HOẶC SINH THÊM THẺ THEO NHU CẦU (OBJECT POOLING)
            for (int i = 0; i < unpurchased.Count; i++) {
                StallConfigOnMap cfg = unpurchased[i];
                if (cfg == null) continue;

                PlotStallCardUI cardUI;
                if (i < _cardPool.Count) {
                    cardUI = _cardPool[i];
                    if (cardUI == null) {
                        cardUI = CreateCardInstance(container);
                        _cardPool[i] = cardUI;
                    }
                } else {
                    cardUI = CreateCardInstance(container);
                    _cardPool.Add(cardUI);
                }

                if (cardUI != null) {
                    if (!cardUI.gameObject.activeSelf) {
                        cardUI.gameObject.SetActive(true);
                    }
                    cardUI.Bind(cfg, _canAffordColor, _cannotAffordColor, OnBuyStallClicked);

                    var cardRect = cardUI.GetComponent<RectTransform>();
                    if (cardRect != null && cardRect.sizeDelta.y > maxCardHeight) {
                        maxCardHeight = cardRect.sizeDelta.y;
                    }
                }
            }

            // 2. ẨN CÁC THẺ DƯ THỪA TRONG POOL (KHÔNG DESTROY)
            for (int j = unpurchased.Count; j < _cardPool.Count; j++) {
                if (_cardPool[j] != null && _cardPool[j].gameObject.activeSelf) {
                    _cardPool[j].gameObject.SetActive(false);
                }
            }

            // 3. Tự động mở rộng chiều cao Bubble nếu thẻ có kích thước lớn hơn chuẩn cũ (ví dụ 200x200)
            if (_rectTransform != null) {
                float requiredBubbleHeight = Mathf.Max(240f, maxCardHeight + 85f);
                if (_rectTransform.sizeDelta.y < requiredBubbleHeight) {
                    _rectTransform.sizeDelta = new Vector2(_rectTransform.sizeDelta.x, requiredBubbleHeight);
                }
            }
        }

        private PlotStallCardUI CreateCardInstance(Transform container) {
            if (_stallCardPrefab != null) {
                GameObject cardGo = Instantiate(_stallCardPrefab, container);
                var cardUI = cardGo.GetComponent<PlotStallCardUI>();
                if (cardUI == null) {
                    cardUI = cardGo.AddComponent<PlotStallCardUI>();
                    cardUI.AutoFindReferences();
                }
                return cardUI;
            }

            // Fallback: Tự động dựng Card gọn gàng bằng mã nguồn nếu chưa gán prefab
            return CreateDefaultCardUI(container);
        }

        private PlotStallCardUI CreateDefaultCardUI(Transform container) {
            GameObject cardGo = new GameObject("StallCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(PlotStallCardUI));
            cardGo.transform.SetParent(container, false);

            var rect = cardGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170f, 150f);

            var layoutElem = cardGo.GetComponent<LayoutElement>();
            layoutElem.minWidth = 170f;
            layoutElem.preferredWidth = 170f;
            layoutElem.minHeight = 150f;
            layoutElem.preferredHeight = 150f;

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f); // Nền xám đen bo thẻ sang trọng

            // Tiêu đề Tên mỏ
            GameObject titleGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.72f);
            titleRect.anchorMax = new Vector2(1f, 0.96f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.fontSize = 15f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // Điểm neo Preview Block 3D
            GameObject holderGo = new GameObject("BlockPreviewHolder", typeof(RectTransform));
            holderGo.transform.SetParent(cardGo.transform, false);
            var holderRect = holderGo.GetComponent<RectTransform>();
            holderRect.anchorMin = new Vector2(0.5f, 0.52f);
            holderRect.anchorMax = new Vector2(0.5f, 0.52f);
            holderRect.sizeDelta = new Vector2(50f, 50f);
            holderRect.anchoredPosition = Vector2.zero;

            // Thông tin block sản xuất (phụ đề nhỏ)
            GameObject infoGo = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            infoGo.transform.SetParent(cardGo.transform, false);
            var infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0.32f);
            infoRect.anchorMax = new Vector2(1f, 0.46f);
            infoRect.sizeDelta = Vector2.zero;
            var infoTmp = infoGo.GetComponent<TextMeshProUGUI>();
            infoTmp.fontSize = 11f;
            infoTmp.alignment = TextAlignmentOptions.Center;
            infoTmp.color = new Color(0.75f, 0.8f, 0.9f);

            // Nút Mua (Buy Button)
            GameObject btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.1f, 0.06f);
            btnRect.anchorMax = new Vector2(0.9f, 0.28f);
            btnRect.sizeDelta = Vector2.zero;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = _canAffordColor;
            var btn = btnGo.GetComponent<Button>();

            // Chữ trên nút mua
            GameObject costGo = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
            costGo.transform.SetParent(btnGo.transform, false);
            var costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = Vector2.one;
            costRect.sizeDelta = Vector2.zero;
            var costTmp = costGo.GetComponent<TextMeshProUGUI>();
            costTmp.fontSize = 13f;
            costTmp.fontStyle = FontStyles.Bold;
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.color = Color.white;

            var cardUI = cardGo.GetComponent<PlotStallCardUI>();
            cardUI.AutoFindReferences();
            return cardUI;
        }

        private void OnBuyStallClicked(StallConfigOnMap cfg) {
            if (cfg == null || _currentPlot == null || !_currentPlot.IsEmpty) return;

            double cost = cfg.BuyPrice;
            if (cost > 0 && EconomyManager.Instance != null) {
                if (!EconomyManager.Instance.TrySpendGold(cost)) {
                    Debug.LogWarning("[PlotStallShopBubble] Không đủ tiền vàng để mua mỏ!");
                    return;
                }
            }

            // Gọi MapManager sinh ra mỏ mới cắm vào Plot (đã tích hợp hiệu ứng rơi từ trên cao xuống)
            if (MapManager.Instance != null) {
                MapManager.Instance.SpawnStallAtPlot(cfg, _currentPlot);
            }

            // Lưu game ngay lập tức để bảo toàn quầy vừa mua
            if (SaveManager.Instance != null) {
                SaveManager.Instance.SaveGame();
            }

            Debug.Log($"<color=#10B981>[PlotStallShopBubble]</color> Đã mua và xây dựng thành công mỏ: {cfg.StallData?.StallName} trên {_currentPlot.name}");

            // Đóng bong bóng sau khi mua thành công
            Hide();
        }

        private void PunchScaleEffect() {
            transform.localScale = Vector3.one * 1.12f;
            CancelInvoke(nameof(ResetScale));
            Invoke(nameof(ResetScale), 0.12f);
        }

        private void ResetScale() {
            transform.localScale = Vector3.one;
        }
    }
}
