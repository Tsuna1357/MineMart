using System;
using System.Collections;
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
        [SerializeField] private TextMeshProUGUI _blockPriceText;
        [SerializeField] private TextMeshProUGUI _costText;

        [Header("Milestone Progress Bar (Horizontal)")]
        [Tooltip("Thanh tiến trình ngang thể hiện khoảng cách tới mốc Milestone tiếp theo (Item 5 trong FutureUpdateNote)")]
        [SerializeField] private Slider _milestoneSlider;
        [SerializeField] private Image _milestoneFillImage;
        [SerializeField] private RectTransform _milestoneBarContainer;
        [SerializeField] private Color _milestoneFillColor = new Color(0.98f, 0.78f, 0.16f, 1f); // Vàng Gold rực rỡ
        [SerializeField] private Color _milestoneBgColor = new Color(0.08f, 0.12f, 0.18f, 0.88f); // Nền xám đen bo góc

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

        private float _currentFill = -1f;
        private Coroutine _fillAnimRoutine;
        private Coroutine _barPunchRoutine;

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
                else if (_blockPriceText == null && (lower.Contains("sell") || lower.Contains("blockprice") || lower.Contains("giaban"))) _blockPriceText = t;
                else if (_benefitText == null && (lower.Contains("benefit") || lower.Contains("stat") || lower.Contains("desc") || lower.Contains("info") || lower.Contains("loi"))) _benefitText = t;
            }

            // Fallback nếu người dùng đặt tên mặc định (ví dụ Text (TMP) (1), (2)...)
            foreach (var t in allTexts) {
                if (t == _stationNameText || t == _levelText || t == _costText || t == _benefitText || t == _blockPriceText) continue;
                if (_stationNameText == null) _stationNameText = t;
                else if (_levelText == null) _levelText = t;
                else if (_benefitText == null) _benefitText = t;
                else if (_costText == null) _costText = t;
            }

            if (_blockPriceText == null && _benefitText != null) {
                _blockPriceText = _benefitText;
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

            // Tự động tìm Slider hoặc Image fill của Milestone Progress Bar
            if (_milestoneSlider == null) {
                _milestoneSlider = GetComponentInChildren<Slider>(true);
                if (_milestoneSlider != null) {
                    _milestoneBarContainer = _milestoneSlider.GetComponent<RectTransform>();
                    if (_milestoneFillImage == null && _milestoneSlider.fillRect != null) {
                        _milestoneFillImage = _milestoneSlider.fillRect.GetComponent<Image>();
                    }
                }
            }

            if (_milestoneBarContainer == null || _milestoneFillImage == null) {
                var allImages = GetComponentsInChildren<Image>(true);
                foreach (var img in allImages) {
                    string lower = img.gameObject.name.ToLower();
                    if (lower.Contains("milestone") || lower.Contains("progress") || lower.Contains("fillbar")) {
                        if (img.gameObject.name.Equals("Fill", StringComparison.OrdinalIgnoreCase)) {
                            _milestoneFillImage = img;
                            _milestoneBarContainer = img.transform.parent as RectTransform;
                            break;
                        } else if (_milestoneBarContainer == null) {
                            _milestoneBarContainer = img.rectTransform;
                        }
                    }
                }
            }
        }

        public void SetupSliderOnContainer(GameObject barRoot) {
            if (barRoot == null) return;

            var barRect = barRoot.GetComponent<RectTransform>();
            _milestoneBarContainer = barRect;

            // 1. Đảm bảo Background Image
            var bgImg = barRoot.GetComponent<Image>();
            if (bgImg == null) {
                bgImg = barRoot.AddComponent<Image>();
                bgImg.color = _milestoneBgColor;
            } else if (bgImg.color != Color.white) {
                _milestoneBgColor = bgImg.color;
            }

            // 2. Đảm bảo component Slider
            var slider = barRoot.GetComponent<Slider>();
            if (slider == null) {
                slider = barRoot.AddComponent<Slider>();
            }

            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // 3. Tìm hoặc tạo Fill
            RectTransform fillRect = null;
            Image fillImg = _milestoneFillImage;

            if (fillImg == null) {
                var fillTr = barRoot.transform.Find("Fill");
                if (fillTr == null) {
                    fillTr = barRoot.transform.Find("Fill Area/Fill");
                }
                if (fillTr != null) {
                    fillImg = fillTr.GetComponent<Image>();
                    fillRect = fillTr as RectTransform;
                }
            } else {
                fillRect = fillImg.rectTransform;
            }

            if (fillImg == null) {
                GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(barRoot.transform, false);
#if UNITY_EDITOR
                if (!Application.isPlaying) {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(fillGo, "Create Fill Image");
                }
#endif
                fillRect = fillGo.GetComponent<RectTransform>();
                fillImg = fillGo.GetComponent<Image>();
                fillImg.color = _milestoneFillColor;
            } else if (fillImg.color != Color.white) {
                _milestoneFillColor = fillImg.color;
            }

            // Cấu hình Fill RectTransform:
            // Dùng Image.Type.Simple để tránh triệt để lỗi mesh của Unity UI khi sprite null
            fillImg.type = Image.Type.Simple;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;

            _milestoneSlider = slider;
            _milestoneFillImage = fillImg;
        }

        public void ApplyConfiguredColors() {
            if (_milestoneFillImage != null) {
                _milestoneFillImage.color = _milestoneFillColor;
            }
            if (_milestoneBarContainer != null) {
                var bg = _milestoneBarContainer.GetComponent<Image>();
                if (bg != null) {
                    bg.color = _milestoneBgColor;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            ApplyConfiguredColors();
        }
#endif

        private void EnsureMilestoneProgressBar() {
            if (_milestoneSlider != null && _milestoneFillImage != null && _milestoneBarContainer != null) return;

            // 1. Tìm Slider
            if (_milestoneSlider == null) {
                _milestoneSlider = GetComponentInChildren<Slider>(true);
            }

            if (_milestoneSlider != null) {
                _milestoneBarContainer = _milestoneSlider.GetComponent<RectTransform>();
                if (_milestoneFillImage == null && _milestoneSlider.fillRect != null) {
                    _milestoneFillImage = _milestoneSlider.fillRect.GetComponent<Image>();
                }
                return;
            }

            // 2. Nếu đã có Container trong Scene (ví dụ Milestone_ProgressBar) nhưng chưa có Slider
            if (_milestoneBarContainer != null) {
                SetupSliderOnContainer(_milestoneBarContainer.gameObject);
                return;
            }

            // Tìm theo tên
            var foundTr = transform.Find("Milestone_ProgressBar");
            if (foundTr == null) {
                var allTr = GetComponentsInChildren<Transform>(true);
                foreach (var tr in allTr) {
                    if (tr.gameObject.name.ToLower().Contains("milestone")) {
                        foundTr = tr;
                        break;
                    }
                }
            }

            if (foundTr != null) {
                SetupSliderOnContainer(foundTr.gameObject);
                return;
            }

            // 3. Tự động tạo thanh tiến trình ngang hiện đại nếu trong Scene chưa có sẵn
            Transform hostParent = transform;
            if (_levelText != null && _levelText.transform.parent != null) {
                hostParent = _levelText.transform.parent;
            }

            GameObject barRoot = new GameObject("Milestone_ProgressBar", typeof(RectTransform), typeof(Image));
            barRoot.transform.SetParent(hostParent, false);

            var barRect = barRoot.GetComponent<RectTransform>();
            _milestoneBarContainer = barRect;

            var le = barRoot.AddComponent<LayoutElement>();
            le.minHeight = 8f;
            le.preferredHeight = 10f;
            le.flexibleWidth = 1f;

            var priceTmp = _blockPriceText != null ? _blockPriceText : _benefitText;
            if (priceTmp != null) {
                barRect.SetSiblingIndex(priceTmp.transform.GetSiblingIndex() + 1);
                barRect.anchoredPosition = priceTmp.rectTransform.anchoredPosition + new Vector2(0f, -22f);
                float width = Mathf.Max(180f, priceTmp.rectTransform.sizeDelta.x);
                barRect.sizeDelta = new Vector2(width, 10f);
            } else if (_levelText != null) {
                barRect.SetSiblingIndex(_levelText.transform.GetSiblingIndex() + 1);
                barRect.anchoredPosition = _levelText.rectTransform.anchoredPosition + new Vector2(0f, -20f);
                barRect.sizeDelta = new Vector2(200f, 10f);
            } else {
                barRect.anchoredPosition = Vector2.zero;
                barRect.sizeDelta = new Vector2(200f, 10f);
            }

            SetupSliderOnContainer(barRoot);
        }

#if UNITY_EDITOR
        [ContextMenu("Tạo / Đồng Bộ Thanh Tiến Trình (Milestone Bar) trong Scene")]
        public void CreateMilestoneBarInEditor() {
            // 1. Tìm xem trong Scene / Bubble đã có Milestone_ProgressBar hay chưa
            Transform existingBar = null;
            if (_milestoneBarContainer != null) {
                existingBar = _milestoneBarContainer;
            } else {
                var found = transform.Find("Milestone_ProgressBar");
                if (found == null) {
                    var allTransforms = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in allTransforms) {
                        if (tr.gameObject.name.ToLower().Contains("milestone")) {
                            existingBar = tr;
                            break;
                        }
                    }
                } else {
                    existingBar = found;
                }
            }

            GameObject barRoot;
            if (existingBar != null) {
                barRoot = existingBar.gameObject;
                UnityEditor.Undo.RecordObject(barRoot, "Sync Milestone Slider");
            } else {
                Transform hostParent = transform;
                if (_levelText != null && _levelText.transform.parent != null) {
                    hostParent = _levelText.transform.parent;
                }

                barRoot = new GameObject("Milestone_ProgressBar", typeof(RectTransform), typeof(Image));
                barRoot.transform.SetParent(hostParent, false);
                UnityEditor.Undo.RegisterCreatedObjectUndo(barRoot, "Create Milestone Progress Bar");

                var le = barRoot.AddComponent<LayoutElement>();
                le.minHeight = 8f;
                le.preferredHeight = 10f;
                le.flexibleWidth = 1f;

                var barRect = barRoot.GetComponent<RectTransform>();
                var priceTmp = _blockPriceText != null ? _blockPriceText : _benefitText;
                if (priceTmp != null) {
                    barRect.SetSiblingIndex(priceTmp.transform.GetSiblingIndex() + 1);
                    barRect.anchoredPosition = priceTmp.rectTransform.anchoredPosition + new Vector2(0f, -22f);
                    float width = Mathf.Max(180f, priceTmp.rectTransform.sizeDelta.x);
                    barRect.sizeDelta = new Vector2(width, 10f);
                } else if (_levelText != null) {
                    barRect.SetSiblingIndex(_levelText.transform.GetSiblingIndex() + 1);
                    barRect.anchoredPosition = _levelText.rectTransform.anchoredPosition + new Vector2(0f, -20f);
                    barRect.sizeDelta = new Vector2(200f, 10f);
                } else {
                    barRect.anchoredPosition = Vector2.zero;
                    barRect.sizeDelta = new Vector2(200f, 10f);
                }
            }

            SetupSliderOnContainer(barRoot);
            ApplyConfiguredColors();

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.Selection.activeGameObject = barRoot;
            UnityEditor.EditorGUIUtility.PingObject(barRoot);
            Debug.Log("<color=#10B981>[StationUpgradeBubble]</color> Đã tạo / đồng bộ Slider cho 'Milestone_ProgressBar'! Slot 'Milestone Slider' đã được liên kết tự động.");
        }
#endif

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
                    _mainCamera = FindAnyObjectByType<Camera>();
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
            _currentFill = -1f; // Đánh dấu để cập nhật tức thì giá trị thanh tiến trình của mỏ mới

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
            ApplyConfiguredColors();
            UpdateScreenPosition();
            RefreshUI(instantProgressBar: true);
            PunchScaleEffect();

            Debug.Log($"<color=#10B981>[StationUpgradeBubble]</color> Đã mở bong bóng cho: {station.name}");
        }

        /// <summary>
        /// Đóng bong bóng nâng cấp.
        /// </summary>
        public void Hide() {
            _currentStation = null;
            _currentFill = -1f;
            if (_fillAnimRoutine != null) {
                StopCoroutine(_fillAnimRoutine);
                _fillAnimRoutine = null;
            }
            if (_barPunchRoutine != null) {
                StopCoroutine(_barPunchRoutine);
                _barPunchRoutine = null;
            }

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
                RefreshUI(instantProgressBar: false);
                PunchScaleEffect();

                // Kiểm tra nếu vừa chạm mốc Milestone đột phá doanh thu
                if (_currentStation.CurrentProfitMultiplier > _currentStation.CalculateMilestoneMultiplierForLevel(oldLevel)) {
                    PunchMilestoneBar();
                    Debug.Log($"<color=#F59E0B>[Milestone]</color> 🏆 Quầy {_currentStation.name} vừa đạt mốc Cấp {_currentStation.CurrentLevel}! Hệ số nhân doanh thu: x{_currentStation.CurrentProfitMultiplier:F0}");
                } else {
                    Debug.Log($"<color=#10B981>[Upgrade]</color> Nâng cấp thành công {_currentStation.name} lên Cấp {_currentStation.CurrentLevel}!");
                }
            } else {
                Debug.LogWarning("[Upgrade] Không đủ Vàng để nâng cấp mỏ!");
            }
        }

        private void RefreshUI(bool instantProgressBar = false) {
            if (_currentStation == null) return;

            // 1. Tên quầy
            if (_stationNameText != null) {
                string stallName = _currentStation.StationData != null ? _currentStation.StationData.StallName : _currentStation.gameObject.name;
                _stationNameText.text = stallName.ToUpper();
            }

            // 2. Cấp độ: Chỉ hiển thị cấp hiện tại (theo Item 5 trong FutureUpdateNote)
            if (_levelText != null) {
                if (_currentStation.IsMaxLevel) {
                    _levelText.text = $"CẤP {_currentStation.CurrentLevel} (TỐI ĐA)";
                } else {
                    _levelText.text = $"CẤP {_currentStation.CurrentLevel}";
                }
            }

            // 3. Hiển thị giá tiền bán của block (gọn gàng, chính xác theo cấp mỏ & map)
            var priceTmp = _blockPriceText != null ? _blockPriceText : _benefitText;
            if (priceTmp != null) {
                if (!priceTmp.gameObject.activeSelf) {
                    priceTmp.gameObject.SetActive(true);
                }
                double sellPrice = _currentStation.GetFinalSellPrice();
                priceTmp.text = $"Giá bán: {NumberFormatter.Format(sellPrice)} Vàng";
            }

            // 4. Cập nhật thanh tiến trình ngang mốc Milestone
            UpdateProgressBar(instantProgressBar);

            // 5. Chi phí nâng cấp
            if (_costText != null) {
                if (_currentStation.IsMaxLevel) {
                    _costText.text = "MAX";
                } else {
                    _costText.text = NumberFormatter.Format(_currentStation.GetUpgradeCost()) + " Vàng";
                }
            }

            RefreshAffordability();
        }

        private void SetProgressBarFill(float fill) {
            fill = Mathf.Clamp01(fill);
            _currentFill = fill;

            if (_milestoneFillImage != null) {
                _milestoneFillImage.color = _milestoneFillColor;

                if (_milestoneFillImage.sprite != null) {
                    if (_milestoneFillImage.type != Image.Type.Filled) {
                        _milestoneFillImage.type = Image.Type.Filled;
                        _milestoneFillImage.fillMethod = Image.FillMethod.Horizontal;
                        _milestoneFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    }
                    _milestoneFillImage.fillAmount = fill;
                } else {
                    // Không có sprite: DÙNG Image.Type.Simple VÀ CO DÃN BẰNG RectTransform.anchorMax
                    // Tránh triệt để bug của Unity UI khi Image.Type.Filled không có Sprite sẽ vẽ 100% hình chữ nhật
                    _milestoneFillImage.type = Image.Type.Simple;
                    RectTransform fillRt = _milestoneFillImage.rectTransform;
                    fillRt.anchorMin = new Vector2(0f, 0f);
                    fillRt.anchorMax = new Vector2(fill, 1f);
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;
                }
            }

            if (_milestoneSlider != null) {
                _milestoneSlider.minValue = 0f;
                _milestoneSlider.maxValue = 1f;
                _milestoneSlider.value = fill;
            }
        }

        private void UpdateProgressBar(bool instant) {
            if (_currentStation == null) return;
            EnsureMilestoneProgressBar();

            float targetFill = _currentStation.GetMilestoneProgress();

            if (instant || _currentFill < 0f) {
                if (_fillAnimRoutine != null) {
                    StopCoroutine(_fillAnimRoutine);
                    _fillAnimRoutine = null;
                }
                SetProgressBarFill(targetFill);
            } else {
                if (_fillAnimRoutine != null) {
                    StopCoroutine(_fillAnimRoutine);
                }
                _fillAnimRoutine = StartCoroutine(AnimateProgressFillRoutine(_currentFill, targetFill));
            }
        }

        private IEnumerator AnimateProgressFillRoutine(float fromFill, float toFill) {
            float duration = 0.22f;
            float elapsed = 0f;

            // Nếu vừa vượt mốc Milestone (toFill bé hơn fromFill do sang chặng mốc mới bắt đầu lại từ 0):
            // Cho thanh chạy đầy 1.0f trước, nháy nhẹ rồi mới reset từ 0.0f lên toFill
            if (toFill < fromFill && fromFill > 0.7f) {
                while (elapsed < duration * 0.5f) {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                    SetProgressBarFill(Mathf.Lerp(fromFill, 1.0f, t));
                    yield return null;
                }
                SetProgressBarFill(1.0f);
                PunchMilestoneBar();
                yield return new WaitForSecondsRealtime(0.06f);
                fromFill = 0f;
                elapsed = 0f;
            }

            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                SetProgressBarFill(Mathf.Lerp(fromFill, toFill, smoothT));
                yield return null;
            }

            SetProgressBarFill(toFill);
            _fillAnimRoutine = null;
        }

        private void PunchMilestoneBar() {
            if (_milestoneBarContainer != null) {
                if (_barPunchRoutine != null) StopCoroutine(_barPunchRoutine);
                _barPunchRoutine = StartCoroutine(PunchBarRoutine(_milestoneBarContainer));
            }
        }

        private IEnumerator PunchBarRoutine(RectTransform target) {
            float dur = 0.18f;
            float el = 0f;
            while (el < dur) {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / dur);
                float s = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                if (target != null) target.localScale = Vector3.one * s;
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
            _barPunchRoutine = null;
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
