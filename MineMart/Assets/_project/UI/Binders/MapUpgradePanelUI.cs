using System;
using System.Collections;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Economy;
using MineMart.Gameplay.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Bảng Giao Diện Nâng Cấp Bản Đồ (Map Upgrade Sliding Panel).
    /// Thiết kế dạng Bottom Sheet Drawer:
    /// - Khi người chơi bấm nút UGMbtn (Bottom Bar), bảng trượt mượt mà từ dưới màn hình lên.
    /// - Khi bấm dấu X (Close Button) hoặc bấm vùng mờ bên ngoài, bảng trượt ngược xuống ra khỏi màn hình.
    /// - Danh sách nâng cấp cuộn dọc (Vertical ScrollRect) hiển thị các thẻ Prefab tái sử dụng (Object Pooling).
    /// </summary>
    public class MapUpgradePanelUI : MonoBehaviour {
        public static MapUpgradePanelUI Instance { get; private set; }

        [Header("Panel Animation Settings")]
        [Tooltip("RectTransform của khung bảng chính thực hiện hiệu ứng trượt")]
        [SerializeField] private RectTransform _panelRect;

        [Tooltip("CanvasGroup điều khiển độ trong suốt và chặn raycast")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Thời gian trượt (giây)")]
        [SerializeField] private float _slideDuration = 0.32f;

        [Tooltip("Vị trí Y khi bảng mở hoàn toàn (mặc định: 0)")]
        [SerializeField] private float _targetOpenY = 0f;

        [Header("Header & Navigation")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton; // Tùy chọn: bấm vùng mờ xung quanh để đóng

        [Header("Vertical Scroll View")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _cardsContainer;
        [SerializeField] private GameObject _upgradeCardPrefab;
        [SerializeField] private TextMeshProUGUI _emptyNoticeText;

        [Header("Event Channels")]
        [SerializeField] private GoldChangedEventChannelSO _goldChangedChannel;

        private readonly List<MapUpgradeCardUI> _cardPool = new List<MapUpgradeCardUI>();
        private Coroutine _slideCoroutine;
        private bool _isOpen = false;
        private float _hiddenY = -1200f;

        public bool IsOpen => _isOpen;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            }

            if (_panelRect == null) {
                _panelRect = GetComponent<RectTransform>();
            }

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            AutoFindReferences();
            CalculateHiddenPosition();
        }

        private void Start() {
            // Tự động kết nối với nút UGMbtn từ MainCanvasHud
            if (MainCanvasHud.Instance != null) {
                MainCanvasHud.Instance.OnUGMClicked += Toggle;
            }

            // Mặc định ban đầu giấu panel xuống dưới màn hình ngay lập tức (không chạy animation)
            SnapToHidden();
        }

        private void OnEnable() {
            if (_closeButton != null) {
                _closeButton.onClick.RemoveListener(Close);
                _closeButton.onClick.AddListener(Close);
            }

            if (_backdropButton != null) {
                _backdropButton.onClick.RemoveListener(Close);
                _backdropButton.onClick.AddListener(Close);
            }

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged += HandleGoldChanged;
            }

            if (MapManager.Instance != null) {
                MapManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
            }
        }

        private void OnDisable() {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
            if (_backdropButton != null) _backdropButton.onClick.RemoveListener(Close);

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged -= HandleGoldChanged;
            }

            if (MapManager.Instance != null) {
                MapManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
            }
        }

        private void OnDestroy() {
            if (MainCanvasHud.Instance != null) {
                MainCanvasHud.Instance.OnUGMClicked -= Toggle;
            }
        }

        private void CalculateHiddenPosition() {
            if (_panelRect != null) {
                float height = _panelRect.rect.height;
                if (height < 200f) height = 750f;
                _hiddenY = -height - 120f;
            } else {
                _hiddenY = -1200f;
            }
        }

        public void SnapToHidden() {
            _isOpen = false;
            CalculateHiddenPosition();

            if (_panelRect != null) {
                Vector2 pos = _panelRect.anchoredPosition;
                pos.y = _hiddenY;
                _panelRect.anchoredPosition = pos;
            }

            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_backdropButton != null) {
                _backdropButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Chuyển đổi trạng thái Đóng/Mở của bảng.
        /// </summary>
        public void Toggle() {
            if (_isOpen) {
                Close();
            } else {
                Open();
            }
        }

        /// <summary>
        /// Mở bảng: Trượt mượt mà từ đáy màn hình lên trên.
        /// </summary>
        public void Open() {
            if (_isOpen) return;
            _isOpen = true;

            // Đóng ngay lập tức các popup shop mỏ và nâng cấp trạm để tránh xung đột UI và đè model 3D
            if (PlotStallShopBubble.Instance != null && PlotStallShopBubble.Instance.IsShowing) {
                PlotStallShopBubble.Instance.Hide();
            }
            if (StationUpgradeBubble.Instance != null && StationUpgradeBubble.Instance.IsShowing) {
                StationUpgradeBubble.Instance.Hide();
            }

            PopulateUpgradeCards();

            if (_canvasGroup != null) {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_backdropButton != null) {
                _backdropButton.gameObject.SetActive(true);
            }

            transform.SetAsLastSibling();

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(_targetOpenY, 1f));

            Debug.Log("<color=#38BDF8>[MapUpgradePanelUI]</color> Đang kéo bảng nâng cấp map từ dưới lên!");
        }

        /// <summary>
        /// Đóng bảng: Trượt mượt mà tụt xuống dưới đáy màn hình và giấu đi.
        /// </summary>
        public void Close() {
            if (!_isOpen) return;
            _isOpen = false;

            if (_backdropButton != null) {
                _backdropButton.gameObject.SetActive(false);
            }

            if (_canvasGroup != null) {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            CalculateHiddenPosition();

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(_hiddenY, 0f));

            Debug.Log("<color=#38BDF8>[MapUpgradePanelUI]</color> Đang trượt bảng nâng cấp map xuống khỏi màn hình!");
        }

        private IEnumerator SlideRoutine(float targetY, float targetAlpha) {
            if (_panelRect == null) yield break;

            Vector2 startPos = _panelRect.anchoredPosition;
            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            float elapsed = 0f;

            while (elapsed < _slideDuration) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideDuration);

                // Hàm nội suy EaseOutCubic giúp chuyển động mượt và tự nhiên
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                Vector2 currentPos = startPos;
                currentPos.y = Mathf.Lerp(startPos.y, targetY, ease);
                _panelRect.anchoredPosition = currentPos;

                if (_canvasGroup != null) {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, ease);
                }

                yield return null;
            }

            Vector2 finalPos = _panelRect.anchoredPosition;
            finalPos.y = targetY;
            _panelRect.anchoredPosition = finalPos;

            if (_canvasGroup != null) {
                _canvasGroup.alpha = targetAlpha;
            }

            _slideCoroutine = null;
        }

        /// <summary>
        /// Nạp toàn bộ danh sách nâng cấp của Map hiện tại từ MapManager.
        /// Tái sử dụng các thẻ đã sinh (Object Pooling) để đạt hiệu năng tối đa.
        /// </summary>
        private void PopulateUpgradeCards() {
            if (_titleText != null) {
                string mapName = MapManager.Instance != null && MapManager.Instance.CurrentMapData != null
                    ? MapManager.Instance.CurrentMapData.MapName
                    : "BẢN ĐỒ";
                _titleText.text = $"NÂNG CẤP — {mapName.ToUpper()}";
            }

            List<MapUpgradeConfig> sortedUpgrades = new List<MapUpgradeConfig>();
            if (MapManager.Instance != null && MapManager.Instance.CurrentMapData != null && MapManager.Instance.CurrentMapData.Upgrades != null) {
                var rawList = MapManager.Instance.CurrentMapData.Upgrades;
                for (int i = 0; i < rawList.Count; i++) {
                    var u = rawList[i];
                    if (u == null) continue;

                    // CHỈ HIỂN THỊ CÁC NÂNG CẤP CHƯA MUA: Nâng cấp đã mua sẽ biến mất hoàn toàn khỏi danh sách
                    int currentLevel = MapManager.Instance.GetUpgradeLevel(u);
                    if (currentLevel < u.MaxLevel) {
                        sortedUpgrades.Add(u);
                    }
                }
            }

            // Tự động sắp xếp danh sách theo thứ tự tăng dần của ID (Hỗ trợ cả số như 01, 02, 10 hoặc chữ)
            sortedUpgrades.Sort((a, b) => CompareUpgradeIds(a?.UpgradeId, b?.UpgradeId));

            if (sortedUpgrades.Count == 0) {
                if (_emptyNoticeText != null) {
                    _emptyNoticeText.gameObject.SetActive(true);
                    _emptyNoticeText.text = "Bạn đã hoàn thành tất cả nâng cấp của bản đồ!";
                }
                if (_scrollRect != null) _scrollRect.gameObject.SetActive(false);

                for (int i = 0; i < _cardPool.Count; i++) {
                    if (_cardPool[i] != null) _cardPool[i].gameObject.SetActive(false);
                }
                return;
            }

            if (_emptyNoticeText != null) _emptyNoticeText.gameObject.SetActive(false);
            if (_scrollRect != null) {
                _scrollRect.gameObject.SetActive(true);
                _scrollRect.verticalNormalizedPosition = 1f; // Cuộn lên trên cùng
            }

            Transform container = _cardsContainer != null ? _cardsContainer : transform;

            for (int i = 0; i < sortedUpgrades.Count; i++) {
                MapUpgradeConfig cfg = sortedUpgrades[i];
                if (cfg == null) continue;

                MapUpgradeCardUI cardUI;
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
                    if (!cardUI.gameObject.activeSelf) cardUI.gameObject.SetActive(true);

                    bool isPurchased = MapManager.Instance != null && MapManager.Instance.GetUpgradeLevel(cfg) >= cfg.MaxLevel;
                    cardUI.Bind(cfg, isPurchased, OnBuyUpgradeClicked);
                }
            }

            // Ẩn các card dư thừa trong pool
            for (int j = sortedUpgrades.Count; j < _cardPool.Count; j++) {
                if (_cardPool[j] != null && _cardPool[j].gameObject.activeSelf) {
                    _cardPool[j].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// So sánh 2 mã ID nâng cấp thông minh:
        /// - Nếu ID là số (vd: "01", "02", "10") -> so sánh theo giá trị số tự nhiên.
        /// - Nếu ID có chứa số (vd: "upgrade_1", "upgrade_10") -> "upgrade_2" sẽ đứng trước "upgrade_10".
        /// - Fallback: So sánh chuỗi không phân biệt hoa thường.
        /// </summary>
        private static int CompareUpgradeIds(string a, string b) {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
            if (string.IsNullOrEmpty(a)) return 1;
            if (string.IsNullOrEmpty(b)) return -1;

            if (int.TryParse(a, out int numA) && int.TryParse(b, out int numB)) {
                return numA.CompareTo(numB);
            }

            int extractedA = ExtractLeadingOrTrailingNumber(a);
            int extractedB = ExtractLeadingOrTrailingNumber(b);
            if (extractedA != -1 && extractedB != -1) {
                int numCompare = extractedA.CompareTo(extractedB);
                if (numCompare != 0) return numCompare;
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractLeadingOrTrailingNumber(string str) {
            if (string.IsNullOrEmpty(str)) return -1;
            var match = System.Text.RegularExpressions.Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int result)) {
                return result;
            }
            return -1;
        }

        private MapUpgradeCardUI CreateCardInstance(Transform container) {
            if (_upgradeCardPrefab != null) {
                GameObject cardGo = Instantiate(_upgradeCardPrefab, container);
                var cardUI = cardGo.GetComponent<MapUpgradeCardUI>();
                if (cardUI == null) {
                    cardUI = cardGo.AddComponent<MapUpgradeCardUI>();
                    cardUI.AutoFindReferences();
                }
                return cardUI;
            }

            // Fallback: Tạo thẻ gọn gàng nếu chưa gán prefab
            return CreateDefaultCardUI(container);
        }

        private MapUpgradeCardUI CreateDefaultCardUI(Transform container) {
            GameObject cardGo = new GameObject("MapUpgradeCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(MapUpgradeCardUI));
            cardGo.transform.SetParent(container, false);

            var rect = cardGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(440f, 100f);

            var layout = cardGo.GetComponent<LayoutElement>();
            layout.minHeight = 100f;
            layout.preferredHeight = 100f;

            var bg = cardGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.2f, 0.96f);

            // Tên nâng cấp
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.55f);
            titleRect.anchorMax = new Vector2(0.66f, 0.92f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.fontSize = 17f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.color = Color.white;

            // Mô tả
            GameObject descGo = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(cardGo.transform, false);
            var descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.04f, 0.12f);
            descRect.anchorMax = new Vector2(0.66f, 0.52f);
            descRect.sizeDelta = Vector2.zero;
            var descTmp = descGo.GetComponent<TextMeshProUGUI>();
            descTmp.fontSize = 12f;
            descTmp.alignment = TextAlignmentOptions.TopLeft;
            descTmp.color = new Color(0.75f, 0.8f, 0.9f);

            // Nút Mua
            GameObject btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.70f, 0.18f);
            btnRect.anchorMax = new Vector2(0.96f, 0.82f);
            btnRect.sizeDelta = Vector2.zero;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.1f, 0.75f, 0.35f, 1f);
            var buyBtn = btnGo.GetComponent<Button>();

            // Chữ giá tiền
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

            var cardUI = cardGo.GetComponent<MapUpgradeCardUI>();
            cardUI.AutoFindReferences();
            return cardUI;
        }

        private void OnBuyUpgradeClicked(MapUpgradeConfig cfg) {
            if (cfg == null) return;

            if (MapManager.Instance == null) return;
            if (MapManager.Instance.GetUpgradeLevel(cfg) >= cfg.MaxLevel) {
                Debug.LogWarning($"[MapUpgradePanelUI] Nâng cấp {cfg.DisplayName} đã được mua tối đa rồi!");
                return;
            }

            double cost = cfg.Price;
            if (cost > 0 && EconomyManager.Instance != null) {
                if (!EconomyManager.Instance.TrySpendGold(cost)) {
                    Debug.LogWarning($"[MapUpgradePanelUI] Không đủ vàng để mua {cfg.DisplayName} (Cần {cost} Vàng)!");
                    return;
                }
            }

            // Kích hoạt nâng cấp qua MapManager (tự động áp dụng hiệu ứng sinh thợ, thêm khách, tăng tốc...)
            MapManager.Instance.ApplyUpgradeLevelUp(cfg);
            Debug.Log($"<color=#10B981>[MapUpgradePanelUI]</color> Mua thành công nâng cấp: {cfg.DisplayName}!");

            // Cập nhật lại giao diện ngay
            PopulateUpgradeCards();
        }

        private void HandleUpgradePurchased(MapUpgradeConfig cfg, int newLevel) {
            // Khi bất kỳ nâng cấp nào được mua, lập tức làm mới danh sách để thẻ đó biến mất ngay
            PopulateUpgradeCards();
        }

        private void HandleGoldChanged(double newBalance) {
            if (_isOpen) {
                for (int i = 0; i < _cardPool.Count; i++) {
                    if (_cardPool[i] != null && _cardPool[i].gameObject.activeSelf) {
                        _cardPool[i].RefreshAffordability();
                    }
                }
            }
        }

        private void AutoFindReferences() {
            if (_titleText == null) {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts) {
                    string lower = t.gameObject.name.ToLower();
                    if (lower.Contains("title") || lower.Contains("header")) {
                        _titleText = t;
                        break;
                    }
                }
            }

            if (_closeButton == null) {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons) {
                    string lower = b.gameObject.name.ToLower();
                    if (lower.Contains("close") || lower.Contains("exit") || lower == "x" || lower.Contains("dong")) {
                        _closeButton = b;
                        break;
                    }
                }
            }

            if (_scrollRect == null) {
                _scrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (_scrollRect != null && _cardsContainer == null) {
                _cardsContainer = _scrollRect.content != null ? _scrollRect.content : _scrollRect.transform;
            }
        }
    }
}
