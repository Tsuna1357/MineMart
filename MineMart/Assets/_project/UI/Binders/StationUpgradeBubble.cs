using System;
using MineMart.Core.Events;
using MineMart.Core.Math;
using MineMart.Economy;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Bong bóng Nâng Cấp Quầy Mỏ (Station Upgrade Bubble) hiển thị trên Canvas 2D,
    /// tự động bám theo vị trí 3D trên đầu của trạm mỏ (WorldToScreenPoint).
    /// Mở ra khi người chơi chạm nhẹ vào Quầy Mỏ hoặc Ô Plot chứa mỏ đó.
    /// </summary>
    public class StationUpgradeBubble : MonoBehaviour {
        public static StationUpgradeBubble Instance { get; private set; }

        [Header("Canvas Group (Auto Added)")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Text Displays (TextMeshPro)")]
        [SerializeField] private TextMeshProUGUI _stationNameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _benefitText;
        [SerializeField] private TextMeshProUGUI _costText;

        [Header("Buttons & Visuals")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Image _buttonBackground;

        [Header("Colors")]
        [SerializeField] private Color _canAffordColor = new Color(0.1f, 0.75f, 0.35f, 1f);     // Xanh lá sáng khi đủ tiền
        [SerializeField] private Color _cannotAffordColor = new Color(0.45f, 0.5f, 0.55f, 1f); // Xám khi thiếu tiền

        [Header("Positioning Settings")]
        [Tooltip("Độ cao nhô lên phía trên khối mỏ (mặc định: 2.2m)")]
        [SerializeField] private float _yOffset = 2.2f;

        [Header("Event Channels (Optional)")]
        [SerializeField] private GoldChangedEventChannelSO _goldChangedChannel;

        private RectTransform _rectTransform;
        private Camera _mainCamera;
        private MiningStation _currentStation;

        public bool IsShowing => _canvasGroup != null && _canvasGroup.alpha > 0.01f;
        public MiningStation CurrentStation => _currentStation;

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
            // Nút đóng: Tối thiểu 54x54 px (chuẩn chạm ngón tay trên màn hình di động)
            if (_closeButton != null) {
                var rt = _closeButton.GetComponent<RectTransform>();
                if (rt != null) {
                    Vector2 sz = rt.sizeDelta;
                    if (sz.x < 54f || sz.y < 54f) {
                        rt.sizeDelta = new Vector2(Mathf.Max(54f, sz.x), Mathf.Max(54f, sz.y));
                    }
                }
            }

            // Nút Nâng cấp: Tối thiểu 200x56 px (dễ bấm, chữ và icon rõ ràng trên điện thoại)
            if (_upgradeButton != null) {
                var rt = _upgradeButton.GetComponent<RectTransform>();
                if (rt != null) {
                    Vector2 sz = rt.sizeDelta;
                    if (sz.x < 200f || sz.y < 56f) {
                        rt.sizeDelta = new Vector2(Mathf.Max(200f, sz.x), Mathf.Max(56f, sz.y));
                    }
                }
            }
        }

        private void AutoFindReferences() {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Tự động tìm các Text con theo từ khóa
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts) {
                string lower = t.gameObject.name.ToLower();
                if (_stationNameText == null && (lower.Contains("name") || lower.Contains("title") || lower.Contains("ten"))) _stationNameText = t;
                else if (_levelText == null && (lower.Contains("level") || lower.Contains("cap"))) _levelText = t;
                else if (_costText == null && (lower.Contains("cost") || lower.Contains("price") || lower.Contains("coin") || lower.Contains("gold") || lower.Contains("gia"))) _costText = t;
                else if (_benefitText == null && (lower.Contains("benefit") || lower.Contains("stat") || lower.Contains("desc") || lower.Contains("info") || lower.Contains("loi"))) _benefitText = t;
            }

            // Fallback nếu người dùng đặt tên mặc định (ví dụ Text (TMP) (1), (2)...)
            foreach (var t in allTexts) {
                if (t == _stationNameText || t == _levelText || t == _costText || t == _benefitText) continue;
                if (_stationNameText == null) _stationNameText = t;
                else if (_levelText == null) _levelText = t;
                else if (_benefitText == null) _benefitText = t;
                else if (_costText == null) _costText = t;
            }

            // Tự động tìm các Nút
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons) {
                string lower = b.gameObject.name.ToLower();
                if (lower.Contains("close") || lower.Contains("exit") || lower.Contains("cancel") || lower == "x" || lower.Contains("dong")) {
                    if (_closeButton == null) _closeButton = b;
                } else if (_upgradeButton == null) {
                    _upgradeButton = b;
                    if (_buttonBackground == null) _buttonBackground = b.GetComponent<Image>();
                }
            }

            // Fallback cho UpgradeButton nếu còn nút chưa gán
            if (_upgradeButton == null) {
                foreach (var b in allButtons) {
                    if (b != _closeButton) {
                        _upgradeButton = b;
                        if (_buttonBackground == null) _buttonBackground = b.GetComponent<Image>();
                        break;
                    }
                }
            }
        }

        private void OnEnable() {
            WorkstationPlacementController.OnStationTapped += HandleStationTapped;
            WorkstationPlacementController.OnEmptyPlotTapped += HandleEmptyPlotTapped;
            WorkstationPlacementController.OnBackgroundTapped += HandleBackgroundTapped;

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged += HandleGoldChanged;
            }

            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(HandleUpgradeClick);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);

            // Mặc định ẩn bằng CanvasGroup để GameObject vẫn active và nhận event
            Hide();
        }

        private void OnDisable() {
            WorkstationPlacementController.OnStationTapped -= HandleStationTapped;
            WorkstationPlacementController.OnEmptyPlotTapped -= HandleEmptyPlotTapped;
            WorkstationPlacementController.OnBackgroundTapped -= HandleBackgroundTapped;

            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged -= HandleGoldChanged;
            }

            if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(HandleUpgradeClick);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        }

        private Canvas _parentCanvas;

        private void LateUpdate() {
            if (_currentStation == null || !IsShowing) return;

            UpdateScreenPosition();
            RefreshAffordability();
        }

        private Vector3 GetTargetWorldPosition() {
            if (_currentStation == null) return Vector3.zero;

            WorkstationPlot plot = _currentStation.CurrentPlot;
            if (plot == null) {
                plot = _currentStation.GetComponentInParent<WorkstationPlot>();
            }
            if (plot == null) {
                plot = WorkstationPlot.FindNearestPlot(_currentStation.transform.position, 4f);
            }

            Vector3 centerPos = plot != null ? plot.transform.position : _currentStation.transform.position;
            return centerPos + Vector3.up * _yOffset;
        }

        private void UpdateScreenPosition() {
            if (_currentStation == null) return;

            if (_mainCamera == null) {
                _mainCamera = Camera.main;
                if (_mainCamera == null) {
                    _mainCamera = FindFirstObjectByType<Camera>();
                    if (_mainCamera == null) return;
                }
            }

            // Đồng bộ vị trí bong bóng bám theo tâm của Plot (hoặc trạm mỏ)
            Vector3 worldPos = GetTargetWorldPosition();
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Nếu đối tượng nằm phía sau camera (z <= 0) thì ẩn tạm thời
            if (screenPos.z <= 0) {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            // Khi đối tượng quay lại trước camera thì hiện lại
            if (_canvasGroup != null && _canvasGroup.alpha < 0.01f && _currentStation != null) {
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
                // Tự động kẹp mép màn hình (Screen Padding Clamping) để bong bóng không bao giờ bị cắt viền
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
        /// Mở bong bóng nâng cấp cho trạm mỏ được chọn.
        /// </summary>
        public void Show(MiningStation station) {
            if (station == null || !station.IsUnlocked) return;

            // Chặn mở nếu bất kỳ bảng UI toàn màn hình nào đang mở
            if (EmeraldUpgradeTreePanelUI.Instance != null && EmeraldUpgradeTreePanelUI.Instance.IsOpen) return;
            if (SettingsDialogUI.Instance != null && SettingsDialogUI.Instance.IsOpen) return;
            if (MapUpgradePanelUI.Instance != null && MapUpgradePanelUI.Instance.IsOpen) {
                MapUpgradePanelUI.Instance.Close();
            }

            if (PlotStallShopBubble.Instance != null && PlotStallShopBubble.Instance.IsShowing) {
                PlotStallShopBubble.Instance.Hide();
            }

            _currentStation = station;

            if (!gameObject.activeSelf) {
                gameObject.SetActive(true);
            }

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            transform.SetAsLastSibling();
            UpdateScreenPosition();
            RefreshUI();
            PunchScaleEffect();

            Debug.Log($"<color=#10B981>[StationUpgradeBubble]</color> Đã mở bong bóng cho: {station.name}");
        }

        /// <summary>
        /// Đóng bong bóng nâng cấp.
        /// </summary>
        public void Hide() {
            _currentStation = null;
            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void HandleBackgroundTapped() {
            if (IsShowing) {
                Hide();
            }
        }

        private void HandleEmptyPlotTapped(WorkstationPlot plot) {
            if (IsShowing) {
                Hide();
            }
        }

        private void HandleStationTapped(MiningStation station) {
            if (station == null) return;

            // Nếu bảng nâng cấp map, cây nâng cấp ngọc, hoặc cài đặt đang mở -> không mở bong bóng trạm
            if (MapUpgradePanelUI.Instance != null && MapUpgradePanelUI.Instance.IsOpen) return;
            if (EmeraldUpgradeTreePanelUI.Instance != null && EmeraldUpgradeTreePanelUI.Instance.IsOpen) return;
            if (SettingsDialogUI.Instance != null && SettingsDialogUI.Instance.IsOpen) return;

            // Nếu chạm lại đúng mỏ đang mở -> đóng lại; nếu mỏ khác -> mở mỏ mới
            if (IsShowing && _currentStation == station) {
                Hide();
            } else {
                Show(station);
            }
        }

        private void HandleGoldChanged(double newBalance) {
            if (IsShowing) {
                RefreshAffordability();
            }
        }

        private void HandleUpgradeClick() {
            if (_currentStation == null || _currentStation.IsMaxLevel) return;

            int oldLevel = _currentStation.CurrentLevel;
            double cost = _currentStation.GetUpgradeCost();
            if (EconomyManager.Instance != null && EconomyManager.Instance.TrySpendGold(cost)) {
                _currentStation.Upgrade();
                if (SaveManager.Instance != null) {
                    SaveManager.Instance.SaveGame();
                }
                RefreshUI();
                PunchScaleEffect();

                // Kiểm tra nếu vừa chạm mốc Milestone đột phá doanh thu
                if (_currentStation.CurrentProfitMultiplier > _currentStation.CalculateMilestoneMultiplierForLevel(oldLevel)) {
                    Debug.Log($"<color=#F59E0B>[Milestone]</color> 🏆 Quầy {_currentStation.name} vừa đạt mốc Cấp {_currentStation.CurrentLevel}! Hệ số nhân doanh thu: x{_currentStation.CurrentProfitMultiplier:F0}");
                } else {
                    Debug.Log($"<color=#10B981>[Upgrade]</color> Nâng cấp thành công {_currentStation.name} lên Cấp {_currentStation.CurrentLevel}!");
                }
            } else {
                Debug.LogWarning("[Upgrade] Không đủ Vàng để nâng cấp mỏ!");
            }
        }

        private void RefreshUI() {
            if (_currentStation == null) return;

            // Tên quầy
            if (_stationNameText != null) {
                string stallName = _currentStation.StationData != null ? _currentStation.StationData.StallName : _currentStation.gameObject.name;
                _stationNameText.text = stallName.ToUpper();
            }

            // Cấp độ
            if (_levelText != null) {
                if (_currentStation.IsMaxLevel) {
                    _levelText.text = $"CẤP {_currentStation.CurrentLevel} (TỐI ĐA)";
                } else {
                    _levelText.text = $"CẤP {_currentStation.CurrentLevel} -> {_currentStation.CurrentLevel + 1}";
                }
            }

            // Thông tin lợi ích nâng cấp kèm mốc Milestone
            if (_benefitText != null) {
                if (_currentStation.IsMaxLevel) {
                    _benefitText.text = $"Giá: {_currentStation.GetFinalSellPrice():F0} Vàng | Thợ: {_currentStation.MaxSlots} Slot";
                } else {
                    double currPrice = _currentStation.GetFinalSellPrice();
                    double nextPrice = _currentStation.GetPriceAtLevel(_currentStation.CurrentLevel + 1);

                    string milestoneInfo = "";
                    if (_currentStation.TryGetNextMilestone(out var nextRule, out int levelsLeft)) {
                        if (levelsLeft == 1) {
                            milestoneInfo = $" [Mốc Lv {nextRule.TargetLevel}: x{nextRule.ProfitMultiplier:F0}!]";
                        } else {
                            milestoneInfo = $" [Mốc Lv {nextRule.TargetLevel}: -{levelsLeft}]";
                        }
                    }

                    _benefitText.text = $"Giá: {currPrice:F0} -> {nextPrice:F0} Vàng{milestoneInfo} | Thợ: {_currentStation.MaxSlots} Slot";
                }
            }

            // Chi phí
            if (_costText != null) {
                if (_currentStation.IsMaxLevel) {
                    _costText.text = "MAX";
                } else {
                    _costText.text = NumberFormatter.Format(_currentStation.GetUpgradeCost()) + " Vàng";
                }
            }

            RefreshAffordability();
        }

        private void RefreshAffordability() {
            if (_currentStation == null) return;

            if (_currentStation.IsMaxLevel) {
                if (_upgradeButton != null) _upgradeButton.interactable = false;
                if (_buttonBackground != null) _buttonBackground.color = _cannotAffordColor;
                return;
            }

            double cost = _currentStation.GetUpgradeCost();
            bool canAfford = EconomyManager.Instance != null && EconomyManager.Instance.CurrentGold >= cost;

            if (_upgradeButton != null) _upgradeButton.interactable = canAfford;
            if (_buttonBackground != null) {
                _buttonBackground.color = canAfford ? _canAffordColor : _cannotAffordColor;
            }
        }

        private void PunchScaleEffect() {
            transform.localScale = Vector3.one * 1.15f;
            CancelInvoke(nameof(ResetScale));
            Invoke(nameof(ResetScale), 0.1f);
        }

        private void ResetScale() {
            transform.localScale = Vector3.one;
        }
    }
}
