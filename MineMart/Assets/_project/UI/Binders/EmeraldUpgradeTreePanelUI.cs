using System;
using System.Collections;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Economy;
using MineMart.Gameplay.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Bản ghi cấu hình từng nấc mở khóa trong Cây Nâng Cấp EUGT.
    /// Cho phép nhà thiết kế thiết lập: Nấc này mở khóa nâng cấp nào (EmeraldUpgradeDataSO)
    /// và giá Gem cần để mở khóa nấc đó.
    /// </summary>
    [Serializable]
    public class EmeraldUpgradeSlotConfig {
        [Tooltip("Dữ liệu cấu hình nâng cấp (EmeraldUpgradeDataSO)")]
        public EmeraldUpgradeDataSO upgradeData;

        [Tooltip("Số Gem cần để mở khóa ô nâng cấp này")]
        public int unlockCostGem = 10;
    }

    /// <summary>
    /// Bản ghi định nghĩa một node nâng cấp trong cây Emerald Upgrade Tree (EUGT) - Legacy tương thích ngược.
    /// </summary>
    [Serializable]
    public class EmeraldUpgradeNode {
        public string id;                   // ID định danh duy nhất (EUGT_01, EUGT_02...)
        public string displayName;          // Tên hiển thị (Giày Thần Tốc, Cuốc Khai Thác...)
        public string description;          // Mô tả tác dụng buff
        public string iconSymbol;           // Biểu tượng icon đại diện (👟, ⛏️, 🎒, 💰, ⚡, 🛡️, 💎...)
        public Sprite customIcon;           // Icon sprite tùy chọn nếu có
        public int maxLevel = 10;           // Cấp tối đa
        public int unlockCostGem = 10;      // Giá Gem để mở khóa ô này lần đầu
        public int baseUpgradeCostGem = 5;  // Giá Gem nâng cấp ở Lv.1
        public float costMultiplier = 1.35f;// Hệ số tăng giá Gem mỗi cấp

        public int GetUpgradeCost(int currentLevel) {
            if (currentLevel >= maxLevel) return 0;
            return Mathf.RoundToInt(baseUpgradeCostGem * Mathf.Pow(costMultiplier, currentLevel));
        }
    }

    /// <summary>
    /// Bảng Giao Diện Nâng Cấp Cây Ngọc Lục Bảo (Emerald Upgrade Tree - EUGT Panel).
    /// - Khi bấm EUGTbtn ở Bottom Bar, bảng trượt từ dưới màn hình lên.
    /// - Chứa ScrollRect vuốt lên vuốt xuống.
    /// - Các ô nâng cấp chỉ hiển thị Icon và Level, ô chưa mở khóa hiện Móc Khóa 🔒.
    /// - Bấm vào ô đã mở khóa sẽ mở bảng chi tiết nâng cấp (Detail Popup) để nâng cấp bằng Gem.
    /// - Danh sách các nấc mở khóa (_unlockSlots) quy định giá tiền Gem và nâng cấp tương ứng.
    /// - Dưới cùng có nút "Unlock Next Upgrade" mở khóa nâng cấp tiếp theo theo thứ tự danh sách.
    /// - Hỗ trợ tính toán chỉ số động thông qua FloatStatSO.
    /// </summary>
    public class EmeraldUpgradeTreePanelUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        public static EmeraldUpgradeTreePanelUI Instance { get; private set; }

        [Header("Animation & Containers")]
        [SerializeField] private GameObject _rootWrapper;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _slideDuration = 0.28f;
        [SerializeField] private float _targetOpenY = 0f;

        [Header("Header & Close")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _capacityText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton;

        [Header("Unlock Slots Configuration (Danh Sách Các Nấc Mở Khóa)")]
        [Tooltip("Danh sách các nấc mở khóa của cây: Mỗi nấc quy định nâng cấp đó là gì (EmeraldUpgradeDataSO) và giá Gem mở khóa nấc đó")]
        [SerializeField] private List<EmeraldUpgradeSlotConfig> _unlockSlots = new List<EmeraldUpgradeSlotConfig>();

        [Header("Scroll View & Items")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _itemsContainer;
        [Tooltip("Prefab thẻ nâng cấp hình vuông (EmeraldUpgradeNodeItemUI). Để trống sẽ tự tạo runtime.")]
        [SerializeField] private EmeraldUpgradeNodeItemUI _nodeItemPrefab;

        [Header("Bottom Action")]
        [SerializeField] private Button _unlockNextBtn;
        [SerializeField] private TextMeshProUGUI _unlockNextText;

        [Header("Upgrade Detail Popup")]
        [SerializeField] private GameObject _detailPopupRoot;
        [SerializeField] private TextMeshProUGUI _detailTitleText;
        [SerializeField] private TextMeshProUGUI _detailDescText;
        [SerializeField] private TextMeshProUGUI _detailLevelText;
        [SerializeField] private TextMeshProUGUI _detailIconText;
        [SerializeField] private Button _detailUpgradeBtn;
        [SerializeField] private TextMeshProUGUI _detailUpgradeBtnText;
        [SerializeField] private Button _detailCloseBtn;

        [Header("Event Channel")]
        [SerializeField] private EmeraldChangedEventChannelSO _emeraldChangedChannel;

        // Legacy Nodes dự phòng nếu chưa gán UnlockSlots
        [SerializeField] private List<EmeraldUpgradeNode> _upgradeNodes = new List<EmeraldUpgradeNode>();

        private readonly List<GameObject> _itemCards = new List<GameObject>();
        private EmeraldUpgradeDataSO _selectedUpgradeData;
        private EmeraldUpgradeNode _selectedNode;
        private Coroutine _slideCoroutine;
        private bool _isOpen = false;
        private float _hiddenY = -1200f;

        // Quản lý vuốt kéo cử chỉ (Swipe / Drag Gesture)
        private bool _isDragging = false;
        private float _dragPointerStartY;
        private float _dragPanelStartY;
        private float _lastPointerY;
        private float _lastDragTime;
        private float _dragVelocityY;

        public bool IsOpen => _isOpen;
        public IReadOnlyList<EmeraldUpgradeSlotConfig> UnlockSlots => _unlockSlots;
        public int TotalSlotsCount => _unlockSlots != null && _unlockSlots.Count > 0 ? _unlockSlots.Count : (_upgradeNodes != null ? _upgradeNodes.Count : 0);

        // Trạng thái nâng cấp thuần túy lưu trong RAM suốt phiên chơi (không tự tiện lưu PlayerPrefs)
        private readonly Dictionary<string, bool> _runtimeUnlockedState = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> _runtimeLevelState = new Dictionary<string, int>();

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else if (Instance != this) {
                Destroy(gameObject);
                return;
            }

            // Đăng ký callbacks lưu/nạp/reset với SaveManager
            SaveManager.OnCollectEugtSaveData = GetSaveData;
            SaveManager.OnApplyEugtSaveData = LoadSaveData;
            SaveManager.OnResetEugtSaveData = ResetAllEugtUpgrades;

            // Dọn sạch mọi dữ liệu PlayerPrefs cũ nếu trước đó từng lỡ lưu
            if (_unlockSlots != null) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null) {
                        PlayerPrefs.DeleteKey("MineMart_EUGT_Unlocked_" + slot.upgradeData.UpgradeId);
                        PlayerPrefs.DeleteKey("MineMart_EUGT_Level_" + slot.upgradeData.UpgradeId);
                    }
                }
            }
            if (_upgradeNodes != null) {
                for (int i = 0; i < _upgradeNodes.Count; i++) {
                    if (_upgradeNodes[i] != null) {
                        PlayerPrefs.DeleteKey("MineMart_EUGT_Unlocked_" + _upgradeNodes[i].id);
                        PlayerPrefs.DeleteKey("MineMart_EUGT_Level_" + _upgradeNodes[i].id);
                    }
                }
            }
            PlayerPrefs.Save();

            if (_panelRect == null) _panelRect = GetComponent<RectTransform>();
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            EnsureItemsContainerGridLayout();
            AutoFindReferences();
            CalculateHiddenPosition();
        }

        private void Start() {
            SnapToHidden();
            ApplyAllUpgradesToStats();
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
            if (_unlockNextBtn != null) {
                _unlockNextBtn.onClick.RemoveListener(OnUnlockNextClicked);
                _unlockNextBtn.onClick.AddListener(OnUnlockNextClicked);
            }
            if (_detailCloseBtn != null) {
                _detailCloseBtn.onClick.RemoveListener(CloseDetailPopup);
                _detailCloseBtn.onClick.AddListener(CloseDetailPopup);
            }
            if (_detailUpgradeBtn != null) {
                _detailUpgradeBtn.onClick.RemoveListener(OnBuyUpgradeInDetailClicked);
                _detailUpgradeBtn.onClick.AddListener(OnBuyUpgradeInDetailClicked);
            }

            if (_emeraldChangedChannel != null) {
                _emeraldChangedChannel.OnEmeraldChanged += HandleEmeraldChanged;
            }
        }

        private void OnDisable() {
            if (_emeraldChangedChannel != null) {
                _emeraldChangedChannel.OnEmeraldChanged -= HandleEmeraldChanged;
            }
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
            if (SaveManager.OnCollectEugtSaveData == (Func<List<EugtUpgradeSaveEntry>>)GetSaveData) {
                SaveManager.OnCollectEugtSaveData = null;
            }
            if (SaveManager.OnApplyEugtSaveData == (Action<List<EugtUpgradeSaveEntry>>)LoadSaveData) {
                SaveManager.OnApplyEugtSaveData = null;
            }
            if (SaveManager.OnResetEugtSaveData == (Action)ResetAllEugtUpgrades) {
                SaveManager.OnResetEugtSaveData = null;
            }
        }

        /// <summary>
        /// Tự động chuyển đổi VerticalLayoutGroup sang GridLayoutGroup 2 cột nếu phát hiện container đang dùng layout cũ.
        /// </summary>
        private void EnsureItemsContainerGridLayout() {
            if (_itemsContainer == null) return;

            var verticalLayout = _itemsContainer.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout != null) {
                DestroyImmediate(verticalLayout);
            }

            var grid = _itemsContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) {
                grid = _itemsContainer.gameObject.AddComponent<GridLayoutGroup>();
            }

            if (grid != null) {
                Vector2 targetSquareCellSize = new Vector2(310f, 310f);
                if (_nodeItemPrefab != null) {
                    var prefabRect = _nodeItemPrefab.GetComponent<RectTransform>();
                    if (prefabRect != null && prefabRect.sizeDelta.x > 0 && prefabRect.sizeDelta.y > 0) {
                        // Tôn trọng kích thước vuông của prefab do người dùng thiết kế
                        float side = prefabRect.sizeDelta.x;
                        targetSquareCellSize = new Vector2(side, side);
                    }
                }
                grid.cellSize = targetSquareCellSize;
                grid.spacing = new Vector2(16f, 16f);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
            }
        }

        private bool _hasInitializedOpenPosition = false;

        private void CalculateHiddenPosition() {
            // Giữ nguyên 100% vị trí mở (_targetOpenY) do người dùng tự căn chỉnh trong Editor/Scene
            if (!_hasInitializedOpenPosition && _panelRect != null) {
                _targetOpenY = _panelRect.anchoredPosition.y;
                _hasInitializedOpenPosition = true;
            }

            float canvasHeight = 1600f;
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) {
                RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
                if (canvasRect != null && canvasRect.rect.height > 0) {
                    canvasHeight = canvasRect.rect.height;
                }
            }

            // Tọa độ ẩn (_hiddenY) chỉ cần hạ xuống đủ sâu để lọt ra ngoài mép dưới màn hình
            _hiddenY = _targetOpenY - canvasHeight - 150f;
        }

        public void Toggle() {
            if (_isOpen) Close();
            else Open();
        }

        public void Open() {
            _isOpen = true;
            CalculateHiddenPosition();
            if (_rootWrapper != null) _rootWrapper.SetActive(true);
            gameObject.SetActive(true);

            // Đóng các popup khác nếu đang mở
            if (PlotStallShopBubble.Instance != null && PlotStallShopBubble.Instance.IsShowing) {
                PlotStallShopBubble.Instance.Hide();
            }
            if (StationUpgradeBubble.Instance != null && StationUpgradeBubble.Instance.IsShowing) {
                StationUpgradeBubble.Instance.Hide();
            }
            if (MapUpgradePanelUI.Instance != null && MapUpgradePanelUI.Instance.IsOpen) {
                MapUpgradePanelUI.Instance.Close();
            }

            if (_canvasGroup != null) {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            CloseDetailPopup();
            RefreshAllViews();

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(AnimateSlide(_hiddenY, _targetOpenY, 0f, 1f));
        }

        public void Close() {
            if (!_isOpen) return;
            _isOpen = false;
            CloseDetailPopup();

            if (_canvasGroup != null) {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(AnimateSlide(_panelRect.anchoredPosition.y, _hiddenY, _canvasGroup != null ? _canvasGroup.alpha : 1f, 0f, () => {
                if (_rootWrapper != null) _rootWrapper.SetActive(false);
            }));
        }

        public void SnapToHidden() {
            _isOpen = false;
            CloseDetailPopup();
            CalculateHiddenPosition();
            if (_panelRect != null) {
                _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, _hiddenY);
            }
            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            if (_rootWrapper != null) {
                _rootWrapper.SetActive(false);
            }
        }

        #region Drag & Swipe Gesture (Kéo vuốt lên / xuống)

        public void OnBeginDrag(PointerEventData eventData) {
            if (!_isOpen) return;
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _isDragging = true;
            _dragPointerStartY = eventData.position.y;
            _dragPanelStartY = _panelRect != null ? _panelRect.anchoredPosition.y : _targetOpenY;
            _lastPointerY = eventData.position.y;
            _lastDragTime = Time.unscaledTime;
            _dragVelocityY = 0f;
        }

        public void OnDrag(PointerEventData eventData) {
            if (!_isOpen || !_isDragging || _panelRect == null) return;

            float scaleFactor = GetCanvasScaleFactor();
            float deltaCanvasY = (eventData.position.y - _dragPointerStartY) / scaleFactor;
            float newY = _dragPanelStartY + deltaCanvasY;

            // Kéo lên trên vượt quá vị trí mở: thêm lực cản đàn hồi cao su (rubber-band)
            if (newY > _targetOpenY) {
                float over = newY - _targetOpenY;
                newY = _targetOpenY + over * 0.25f;
            }

            _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, newY);

            // Tính vận tốc kéo
            float dt = Time.unscaledTime - _lastDragTime;
            if (dt > 0.001f) {
                _dragVelocityY = ((eventData.position.y - _lastPointerY) / scaleFactor) / dt;
                _lastPointerY = eventData.position.y;
                _lastDragTime = Time.unscaledTime;
            }

            // Đồng bộ độ mờ backdrop
            if (_canvasGroup != null) {
                float totalDistance = Mathf.Abs(_targetOpenY - _hiddenY);
                if (totalDistance > 1f) {
                    float currentProgress = Mathf.Clamp01((newY - _hiddenY) / totalDistance);
                    _canvasGroup.alpha = currentProgress;
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData) {
            if (!_isDragging) return;
            _isDragging = false;

            if (_panelRect == null) return;

            float currentY = _panelRect.anchoredPosition.y;
            float pulledDown = _targetOpenY - currentY;

            // Nếu người chơi kéo xuống hơn 120px HOẶC vẩy mạnh xuống dưới (velocity < -600px/s)
            if (pulledDown > 120f || _dragVelocityY < -600f) {
                Close();
            } else {
                // Thả tay nhẹ hoặc vuốt lên -> tự nảy mượt mà về vị trí mở
                if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
                _slideCoroutine = StartCoroutine(AnimateSlide(currentY, _targetOpenY, _canvasGroup != null ? _canvasGroup.alpha : 1f, 1f));
            }
        }

        private float GetCanvasScaleFactor() {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.scaleFactor > 0f) {
                return rootCanvas.scaleFactor;
            }
            return 1f;
        }

        #endregion

        private IEnumerator AnimateSlide(float startY, float endY, float startAlpha, float endAlpha, Action onComplete = null) {
            float elapsed = 0f;
            while (elapsed < _slideDuration) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideDuration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (_panelRect != null) {
                    float currentY = Mathf.Lerp(startY, endY, ease);
                    _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, currentY);
                }
                if (_canvasGroup != null) {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, ease);
                }
                yield return null;
            }

            if (_panelRect != null) _panelRect.anchoredPosition = new Vector2(_panelRect.anchoredPosition.x, endY);
            if (_canvasGroup != null) _canvasGroup.alpha = endAlpha;

            onComplete?.Invoke();
            _slideCoroutine = null;
        }

        public bool IsUpgradeUnlocked(string id) {
            if (string.IsNullOrEmpty(id)) return false;
            return _runtimeUnlockedState.TryGetValue(id, out bool unlocked) && unlocked;
        }

        public int GetUpgradeLevel(string id) {
            if (string.IsNullOrEmpty(id)) return 1;
            return _runtimeLevelState.TryGetValue(id, out int level) ? level : 1;
        }

        public void SetUpgradeUnlocked(string id, bool unlocked) {
            if (string.IsNullOrEmpty(id)) return;
            _runtimeUnlockedState[id] = unlocked;
            SyncDataSOState(id);
        }

        public void SetUpgradeLevel(string id, int level) {
            if (string.IsNullOrEmpty(id)) return;
            _runtimeLevelState[id] = level;
            SyncDataSOState(id);
        }

        private void SyncDataSOState(string id) {
            if (_unlockSlots != null) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null && slot.upgradeData.UpgradeId == id) {
                        bool unlocked = IsUpgradeUnlocked(id);
                        int level = GetUpgradeLevel(id);
                        slot.upgradeData.SetRuntimeState(unlocked, level);
                        break;
                    }
                }
            }
        }

        // Tương thích ngược với IsNodeUnlocked
        public bool IsNodeUnlocked(string id) => IsUpgradeUnlocked(id);
        public int GetNodeLevel(string id) => GetUpgradeLevel(id);
        public void SetNodeUnlocked(string id, bool unlocked) => SetUpgradeUnlocked(id, unlocked);
        public void SetNodeLevel(string id, int level) => SetUpgradeLevel(id, level);

        /// <summary>
        /// Thu thập toàn bộ trạng thái mở khóa và cấp độ của Cây Nâng Cấp EUGT để lưu game.
        /// </summary>
        public List<EugtUpgradeSaveEntry> GetSaveData() {
            var list = new List<EugtUpgradeSaveEntry>();
            if (_unlockSlots != null) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null) {
                        string id = slot.upgradeData.UpgradeId;
                        list.Add(new EugtUpgradeSaveEntry(id, IsUpgradeUnlocked(id), GetUpgradeLevel(id)));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Nạp lại trạng thái mở khóa và cấp độ EUGT từ file save.
        /// </summary>
        public void LoadSaveData(List<EugtUpgradeSaveEntry> savedList) {
            if (savedList == null) return;
            for (int i = 0; i < savedList.Count; i++) {
                var entry = savedList[i];
                if (entry != null && !string.IsNullOrEmpty(entry.upgradeId)) {
                    SetUpgradeUnlocked(entry.upgradeId, entry.isUnlocked);
                    SetUpgradeLevel(entry.upgradeId, entry.level);
                }
            }
            ApplyAllUpgradesToStats();
            RefreshAllViews();
        }

        /// <summary>
        /// Đặt lại toàn bộ nâng cấp EUGT về trạng thái ban đầu (khóa hết, cấp 1) phục vụ chức năng Reset Tiến Trình.
        /// </summary>
        public void ResetAllEugtUpgrades() {
            _runtimeUnlockedState.Clear();
            _runtimeLevelState.Clear();
            if (_unlockSlots != null) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null) {
                        slot.upgradeData.SetRuntimeState(false, 1);
                        slot.upgradeData.ApplyToTargetStat(0);
                    }
                }
            }
            RefreshAllViews();
        }

        /// <summary>
        /// Đồng bộ và áp dụng tất cả các nấc nâng cấp đã mở khóa vào TargetStat tương ứng.
        /// </summary>
        public void ApplyAllUpgradesToStats() {
            if (_unlockSlots != null) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null) {
                        if (IsUpgradeUnlocked(slot.upgradeData.UpgradeId)) {
                            int level = GetUpgradeLevel(slot.upgradeData.UpgradeId);
                            slot.upgradeData.ApplyToTargetStat(level);
                        } else {
                            slot.upgradeData.ApplyToTargetStat(0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lấy giá trị hiệu dụng hiện tại của một FloatStatSO (BaseValue + Modifiers tích lũy).
        /// </summary>
        public float GetEffectiveFloatStat(FloatStatSO stat, float fallbackBase = 0.1f) {
            if (stat == null) return fallbackBase;
            return stat.CurrentValue;
        }

        /// <summary>
        /// Truy vấn tĩnh lấy giá trị hiện tại của bất kỳ FloatStatSO nào (dùng cho Tip, Speed, v.v.).
        /// </summary>
        public static float GetFloatStat(FloatStatSO stat, float fallbackBase = 0.1f) {
            if (stat != null) {
                return stat.CurrentValue;
            }
            return fallbackBase;
        }

        /// <summary>
        /// Làm mới toàn bộ UI danh sách và nút mở khóa dưới cùng.
        /// </summary>
        public void RefreshAllViews() {
            PopulateCards();
            RefreshCapacityAndUnlockButton();
            if (_selectedUpgradeData != null && _detailPopupRoot != null && _detailPopupRoot.activeSelf) {
                RefreshDetailPopupView(_selectedUpgradeData);
            } else if (_selectedNode != null && _detailPopupRoot != null && _detailPopupRoot.activeSelf) {
                RefreshDetailPopupView(_selectedNode);
            }
        }

        private void PopulateCards() {
            if (_itemsContainer == null) return;

            for (int i = 0; i < _itemCards.Count; i++) {
                if (_itemCards[i] != null) Destroy(_itemCards[i]);
            }
            _itemCards.Clear();

            // 1. Ưu tiên hiển thị theo Unlock Slots (kiến trúc mới)
            if (_unlockSlots != null && _unlockSlots.Count > 0) {
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    EmeraldUpgradeSlotConfig slot = _unlockSlots[i];
                    if (slot == null || slot.upgradeData == null) continue;

                    EmeraldUpgradeDataSO data = slot.upgradeData;
                    bool unlocked = IsUpgradeUnlocked(data.UpgradeId);
                    int level = GetUpgradeLevel(data.UpgradeId);

                    if (_nodeItemPrefab != null) {
                        EmeraldUpgradeNodeItemUI cardItem = Instantiate(_nodeItemPrefab, _itemsContainer);
                        cardItem.name = "NodeCard_" + data.UpgradeId;
                        cardItem.Setup(data, unlocked, level, OnDataCardClicked);
                        _itemCards.Add(cardItem.gameObject);
                    } else {
                        GameObject cardGo = CreateSquareDataCardUI(_itemsContainer, data, unlocked, level);
                        _itemCards.Add(cardGo);
                    }
                }
                return;
            }

            // 2. Dự phòng hiển thị theo Legacy Nodes (nếu chưa gán Slots)
            if (_upgradeNodes != null && _upgradeNodes.Count > 0) {
                for (int i = 0; i < _upgradeNodes.Count; i++) {
                    EmeraldUpgradeNode node = _upgradeNodes[i];
                    bool unlocked = IsNodeUnlocked(node.id);
                    int level = GetNodeLevel(node.id);

                    if (_nodeItemPrefab != null) {
                        EmeraldUpgradeNodeItemUI cardItem = Instantiate(_nodeItemPrefab, _itemsContainer);
                        cardItem.name = "NodeCard_" + node.id;
                        cardItem.Setup(node, unlocked, level, OnCardClicked);
                        _itemCards.Add(cardItem.gameObject);
                    } else {
                        GameObject cardGo = CreateSquareNodeCardUI(_itemsContainer, node, unlocked, level);
                        _itemCards.Add(cardGo);
                    }
                }
                return;
            }

            // Thông báo nếu trống
            GameObject noticeGo = new GameObject("EmptyNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
            noticeGo.transform.SetParent(_itemsContainer, false);
            var noticeRect = noticeGo.GetComponent<RectTransform>();
            noticeRect.sizeDelta = new Vector2(400f, 120f);
            var tmp = noticeGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "Chưa có nấc nâng cấp nào trong danh sách Unlock Slots.\nHãy kéo thả EmeraldUpgradeDataSO vào Panel_EUGT!";
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.75f, 0.85f, 0.8f);
            _itemCards.Add(noticeGo);
        }

        private GameObject CreateSquareDataCardUI(Transform parent, EmeraldUpgradeDataSO data, bool unlocked, int level) {
            bool isMax = level >= data.MaxLevel;

            GameObject cardGo = new GameObject("NodeCard_" + data.UpgradeId, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(parent, false);
            var rect = cardGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 210f);

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = unlocked ? new Color(0.14f, 0.18f, 0.25f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.85f);

            var btn = cardGo.GetComponent<Button>();
            btn.onClick.AddListener(() => OnDataCardClicked(data));

            // Badge Level
            GameObject levelBadgeGo = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            levelBadgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = levelBadgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 1f);
            badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -12f);
            badgeRect.sizeDelta = new Vector2(140f, 32f);

            var badgeImg = levelBadgeGo.GetComponent<Image>();
            badgeImg.color = unlocked ? (isMax ? new Color(0.92f, 0.70f, 0.15f, 1f) : new Color(0.12f, 0.75f, 0.45f, 1f)) : new Color(0.20f, 0.22f, 0.28f, 0.8f);

            GameObject badgeTextGo = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextGo.transform.SetParent(levelBadgeGo.transform, false);
            var badgeTextRect = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;

            var badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = unlocked ? (isMax ? "MAX" : $"Lv. {level}") : "🔒 KHÓA";
            badgeTmp.fontSize = 13f;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.color = Color.white;

            // Icon Box
            GameObject iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            iconBoxGo.transform.SetParent(cardGo.transform, false);
            var iconBoxRect = iconBoxGo.GetComponent<RectTransform>();
            iconBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconBoxRect.pivot = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchoredPosition = new Vector2(0f, 6f);
            iconBoxRect.sizeDelta = new Vector2(76f, 76f);

            var iconBoxImg = iconBoxGo.GetComponent<Image>();
            iconBoxImg.color = unlocked ? new Color(0.18f, 0.24f, 0.35f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f);

            GameObject iconTextGo = new GameObject("IconText", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconTextGo.transform.SetParent(iconBoxGo.transform, false);
            var iconTextRect = iconTextGo.GetComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.sizeDelta = Vector2.zero;

            var iconTmp = iconTextGo.GetComponent<TextMeshProUGUI>();
            iconTmp.text = unlocked ? data.IconSymbol : "🔒";
            iconTmp.fontSize = 38f;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = unlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);

            // Name Text
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0f);
            nameRect.anchorMax = new Vector2(0.95f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 14f);
            nameRect.sizeDelta = new Vector2(0f, 40f);

            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = unlocked ? data.DisplayName : "???";
            nameTmp.fontSize = 14f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = unlocked ? Color.white : new Color(0.55f, 0.6f, 0.7f);

            return cardGo;
        }

        private GameObject CreateSquareNodeCardUI(Transform parent, EmeraldUpgradeNode node, bool unlocked, int level) {
            bool isMax = level >= node.maxLevel;

            GameObject cardGo = new GameObject("NodeCard_" + node.id, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(parent, false);
            var rect = cardGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 210f);

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = unlocked ? new Color(0.14f, 0.18f, 0.25f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.85f);

            var btn = cardGo.GetComponent<Button>();
            btn.onClick.AddListener(() => OnCardClicked(node));

            GameObject levelBadgeGo = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            levelBadgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = levelBadgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 1f);
            badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -12f);
            badgeRect.sizeDelta = new Vector2(140f, 32f);

            var badgeImg = levelBadgeGo.GetComponent<Image>();
            badgeImg.color = unlocked ? (isMax ? new Color(0.92f, 0.70f, 0.15f, 1f) : new Color(0.12f, 0.75f, 0.45f, 1f)) : new Color(0.20f, 0.22f, 0.28f, 0.8f);

            GameObject badgeTextGo = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextGo.transform.SetParent(levelBadgeGo.transform, false);
            var badgeTextRect = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.sizeDelta = Vector2.zero;

            var badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = unlocked ? (isMax ? "MAX" : $"Lv. {level}") : "🔒 KHÓA";
            badgeTmp.fontSize = 13f;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.color = Color.white;

            GameObject iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            iconBoxGo.transform.SetParent(cardGo.transform, false);
            var iconBoxRect = iconBoxGo.GetComponent<RectTransform>();
            iconBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconBoxRect.pivot = new Vector2(0.5f, 0.5f);
            iconBoxRect.anchoredPosition = new Vector2(0f, 6f);
            iconBoxRect.sizeDelta = new Vector2(76f, 76f);

            var iconBoxImg = iconBoxGo.GetComponent<Image>();
            iconBoxImg.color = unlocked ? new Color(0.18f, 0.24f, 0.35f, 1f) : new Color(0.12f, 0.14f, 0.18f, 1f);

            GameObject iconTextGo = new GameObject("IconText", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconTextGo.transform.SetParent(iconBoxGo.transform, false);
            var iconTextRect = iconTextGo.GetComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.sizeDelta = Vector2.zero;

            var iconTmp = iconTextGo.GetComponent<TextMeshProUGUI>();
            iconTmp.text = unlocked ? node.iconSymbol : "🔒";
            iconTmp.fontSize = 38f;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = unlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);

            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0f);
            nameRect.anchorMax = new Vector2(0.95f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 14f);
            nameRect.sizeDelta = new Vector2(0f, 40f);

            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = unlocked ? node.displayName : "???";
            nameTmp.fontSize = 14f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = unlocked ? Color.white : new Color(0.55f, 0.6f, 0.7f);

            return cardGo;
        }

        private void OnDataCardClicked(EmeraldUpgradeDataSO data) {
            if (data == null) return;
            if (!IsUpgradeUnlocked(data.UpgradeId)) {
                Debug.Log("<color=#f39c12>[EUGT]</color> Ô nâng cấp này đang bị khóa bí ẩn! Hãy mở khóa nấc tiếp theo ở bên dưới.");
                return;
            }
            OpenDetailPopup(data);
        }

        private void OnCardClicked(EmeraldUpgradeNode node) {
            if (node == null) return;
            if (!IsNodeUnlocked(node.id)) {
                Debug.Log("<color=#f39c12>[EUGT]</color> Ô nâng cấp này đang bị khóa bí ẩn! Hãy mở khóa nấc tiếp theo ở bên dưới.");
                return;
            }
            OpenDetailPopup(node);
        }

        public void OpenDetailPopup(EmeraldUpgradeDataSO data) {
            _selectedUpgradeData = data;
            _selectedNode = null;
            if (_detailPopupRoot == null) return;

            _detailPopupRoot.SetActive(true);
            RefreshDetailPopupView(data);
        }

        public void OpenDetailPopup(EmeraldUpgradeNode node) {
            _selectedNode = node;
            _selectedUpgradeData = null;
            if (_detailPopupRoot == null) return;

            _detailPopupRoot.SetActive(true);
            RefreshDetailPopupView(node);
        }

        public void CloseDetailPopup() {
            if (_detailPopupRoot != null) {
                _detailPopupRoot.SetActive(false);
            }
            _selectedUpgradeData = null;
            _selectedNode = null;
        }

        private void RefreshDetailPopupView(EmeraldUpgradeDataSO data) {
            if (data == null) return;

            int level = GetUpgradeLevel(data.UpgradeId);
            bool isMax = level >= data.MaxLevel;
            int cost = data.GetUpgradeCost(level);
            int currentEmerald = HardCurrencyManager.Instance != null ? HardCurrencyManager.Instance.CurrentEmerald : 0;
            bool canAfford = currentEmerald >= cost;

            if (_detailTitleText != null) _detailTitleText.text = data.DisplayName;
            if (_detailIconText != null) _detailIconText.text = data.IconSymbol;
            if (_detailDescText != null) _detailDescText.text = data.Description;

            if (_detailLevelText != null) {
                _detailLevelText.text = isMax 
                    ? $"Cấp độ: <color=#f1c40f>MAX (Lv.{data.MaxLevel})</color>" 
                    : $"Cấp độ: <color=#2ecc71>Lv.{level}</color>  ➔  <color=#3498db>Lv.{level + 1}</color>";
            }

            if (_detailUpgradeBtn != null) {
                _detailUpgradeBtn.interactable = !isMax && canAfford;
            }

            if (_detailUpgradeBtnText != null) {
                if (isMax) {
                    _detailUpgradeBtnText.text = "ĐÃ ĐẠT CẤP TỐI ĐA";
                } else {
                    string costColor = canAfford ? "#ffffff" : "#ff6b6b";
                    _detailUpgradeBtnText.text = $"NÂNG CẤP (<color={costColor}>{cost} 💎</color>)";
                }
            }
        }

        private void RefreshDetailPopupView(EmeraldUpgradeNode node) {
            if (node == null) return;

            int level = GetNodeLevel(node.id);
            bool isMax = level >= node.maxLevel;
            int cost = node.GetUpgradeCost(level);
            int currentEmerald = HardCurrencyManager.Instance != null ? HardCurrencyManager.Instance.CurrentEmerald : 0;
            bool canAfford = currentEmerald >= cost;

            if (_detailTitleText != null) _detailTitleText.text = node.displayName;
            if (_detailIconText != null) _detailIconText.text = node.iconSymbol;
            if (_detailDescText != null) _detailDescText.text = node.description;

            if (_detailLevelText != null) {
                _detailLevelText.text = isMax 
                    ? $"Cấp độ: <color=#f1c40f>MAX (Lv.{node.maxLevel})</color>" 
                    : $"Cấp độ: <color=#2ecc71>Lv.{level}</color>  ➔  <color=#3498db>Lv.{level + 1}</color>";
            }

            if (_detailUpgradeBtn != null) {
                _detailUpgradeBtn.interactable = !isMax && canAfford;
            }

            if (_detailUpgradeBtnText != null) {
                if (isMax) {
                    _detailUpgradeBtnText.text = "ĐÃ ĐẠT CẤP TỐI ĐA";
                } else {
                    string costColor = canAfford ? "#ffffff" : "#ff6b6b";
                    _detailUpgradeBtnText.text = $"NÂNG CẤP (<color={costColor}>{cost} 💎</color>)";
                }
            }
        }

        private void OnBuyUpgradeInDetailClicked() {
            if (_selectedUpgradeData != null) {
                int level = GetUpgradeLevel(_selectedUpgradeData.UpgradeId);
                if (level >= _selectedUpgradeData.MaxLevel) return;

                int cost = _selectedUpgradeData.GetUpgradeCost(level);
                if (HardCurrencyManager.Instance == null || !HardCurrencyManager.Instance.TrySpendEmerald(cost)) {
                    Debug.LogWarning($"[EUGT] Không đủ Gem để nâng cấp {_selectedUpgradeData.DisplayName} (Cần {cost} Gem)!");
                    return;
                }

                int newLevel = level + 1;
                SetUpgradeLevel(_selectedUpgradeData.UpgradeId, newLevel);
                _selectedUpgradeData.ApplyToTargetStat(newLevel);
                Debug.Log($"<color=#2ecc71>[EUGT]</color> Nâng cấp thành công {_selectedUpgradeData.DisplayName} lên Lv.{newLevel}!");

                RefreshAllViews();
                return;
            }

            if (_selectedNode != null) {
                int level = GetNodeLevel(_selectedNode.id);
                if (level >= _selectedNode.maxLevel) return;

                int cost = _selectedNode.GetUpgradeCost(level);
                if (HardCurrencyManager.Instance == null || !HardCurrencyManager.Instance.TrySpendEmerald(cost)) {
                    Debug.LogWarning($"[EUGT] Không đủ Gem để nâng cấp {_selectedNode.displayName} (Cần {cost} Gem)!");
                    return;
                }

                int newLevel = level + 1;
                SetNodeLevel(_selectedNode.id, newLevel);
                Debug.Log($"<color=#2ecc71>[EUGT]</color> Nâng cấp thành công {_selectedNode.displayName} lên Lv.{newLevel}!");

                RefreshAllViews();
            }
        }

        private void RefreshCapacityAndUnlockButton() {
            // 1. Kiểm tra danh sách Unlock Slots
            if (_unlockSlots != null && _unlockSlots.Count > 0) {
                int unlockedCount = 0;
                EmeraldUpgradeSlotConfig nextLockedSlot = null;

                for (int i = 0; i < _unlockSlots.Count; i++) {
                    EmeraldUpgradeSlotConfig slot = _unlockSlots[i];
                    if (slot == null || slot.upgradeData == null) continue;

                    if (IsUpgradeUnlocked(slot.upgradeData.UpgradeId)) {
                        unlockedCount++;
                    } else if (nextLockedSlot == null) {
                        nextLockedSlot = slot;
                    }
                }

                if (_capacityText != null) {
                    _capacityText.text = $"Số lượng nâng cấp: <color=#2ecc71>{unlockedCount}</color> / {_unlockSlots.Count}";
                }

                if (_unlockNextBtn != null && _unlockNextText != null) {
                    if (nextLockedSlot != null) {
                        _unlockNextBtn.gameObject.SetActive(true);
                        int currentEmerald = HardCurrencyManager.Instance != null ? HardCurrencyManager.Instance.CurrentEmerald : 0;
                        bool canAfford = currentEmerald >= nextLockedSlot.unlockCostGem;

                        _unlockNextBtn.interactable = canAfford;
                        string costColor = canAfford ? "#ffffff" : "#ff6b6b";
                        _unlockNextText.text = $"MỞ KHÓA NÂNG CẤP TIẾP THEO (<color={costColor}>{nextLockedSlot.unlockCostGem} 💎</color>)";
                    } else {
                        _unlockNextBtn.interactable = false;
                        _unlockNextText.text = "ĐÃ MỞ KHÓA TẤT CẢ NÂNG CẤP";
                    }
                }
                return;
            }

            // 2. Dự phòng Legacy Nodes
            int legacyUnlocked = 0;
            EmeraldUpgradeNode nextLegacyNode = null;
            if (_upgradeNodes != null) {
                for (int i = 0; i < _upgradeNodes.Count; i++) {
                    var n = _upgradeNodes[i];
                    if (IsNodeUnlocked(n.id)) legacyUnlocked++;
                    else if (nextLegacyNode == null) nextLegacyNode = n;
                }
            }

            if (_capacityText != null) {
                _capacityText.text = $"Số lượng nâng cấp: <color=#2ecc71>{legacyUnlocked}</color> / {(_upgradeNodes != null ? _upgradeNodes.Count : 0)}";
            }

            if (_unlockNextBtn != null && _unlockNextText != null) {
                if (nextLegacyNode != null) {
                    _unlockNextBtn.gameObject.SetActive(true);
                    int currentEmerald = HardCurrencyManager.Instance != null ? HardCurrencyManager.Instance.CurrentEmerald : 0;
                    bool canAfford = currentEmerald >= nextLegacyNode.unlockCostGem;
                    _unlockNextBtn.interactable = canAfford;
                    string costColor = canAfford ? "#ffffff" : "#ff6b6b";
                    _unlockNextText.text = $"MỞ KHÓA NÂNG CẤP TIẾP THEO (<color={costColor}>{nextLegacyNode.unlockCostGem} 💎</color>)";
                } else {
                    _unlockNextBtn.interactable = false;
                    _unlockNextText.text = "ĐÃ MỞ KHÓA TẤT CẢ NÂNG CẤP";
                }
            }
        }

        private void OnUnlockNextClicked() {
            // 1. Mở khóa theo Unlock Slots
            if (_unlockSlots != null && _unlockSlots.Count > 0) {
                EmeraldUpgradeSlotConfig nextLockedSlot = null;
                for (int i = 0; i < _unlockSlots.Count; i++) {
                    var slot = _unlockSlots[i];
                    if (slot != null && slot.upgradeData != null && !IsUpgradeUnlocked(slot.upgradeData.UpgradeId)) {
                        nextLockedSlot = slot;
                        break;
                    }
                }

                if (nextLockedSlot == null) {
                    Debug.LogWarning("[EUGT] Đã mở khóa hết tất cả các nâng cấp trong danh sách!");
                    return;
                }

                int cost = nextLockedSlot.unlockCostGem;
                if (HardCurrencyManager.Instance == null || !HardCurrencyManager.Instance.TrySpendEmerald(cost)) {
                    Debug.LogWarning($"[EUGT] Không đủ Gem để mở khóa {nextLockedSlot.upgradeData.DisplayName} (Cần {cost} Gem)!");
                    return;
                }

                SetUpgradeUnlocked(nextLockedSlot.upgradeData.UpgradeId, true);
                SetUpgradeLevel(nextLockedSlot.upgradeData.UpgradeId, 1);
                nextLockedSlot.upgradeData.ApplyToTargetStat(1);
                Debug.Log($"<color=#10B981>[EUGT]</color> Mở khóa thành công nâng cấp kế tiếp: {nextLockedSlot.upgradeData.DisplayName} ({nextLockedSlot.upgradeData.UpgradeId})!");

                RefreshAllViews();
                return;
            }

            // 2. Mở khóa theo Legacy Nodes
            if (_upgradeNodes != null) {
                EmeraldUpgradeNode nextLockedNode = null;
                for (int i = 0; i < _upgradeNodes.Count; i++) {
                    var n = _upgradeNodes[i];
                    if (!IsNodeUnlocked(n.id)) {
                        nextLockedNode = n;
                        break;
                    }
                }

                if (nextLockedNode == null) return;
                int cost = nextLockedNode.unlockCostGem;
                if (HardCurrencyManager.Instance == null || !HardCurrencyManager.Instance.TrySpendEmerald(cost)) return;

                SetNodeUnlocked(nextLockedNode.id, true);
                SetNodeLevel(nextLockedNode.id, 1);
                Debug.Log($"<color=#10B981>[EUGT]</color> Mở khóa thành công nâng cấp kế tiếp: {nextLockedNode.displayName}!");

                RefreshAllViews();
            }
        }

        private void HandleEmeraldChanged(int newBalance) {
            if (_isOpen) {
                RefreshCapacityAndUnlockButton();
                if (_selectedUpgradeData != null && _detailPopupRoot != null && _detailPopupRoot.activeSelf) {
                    RefreshDetailPopupView(_selectedUpgradeData);
                } else if (_selectedNode != null && _detailPopupRoot != null && _detailPopupRoot.activeSelf) {
                    RefreshDetailPopupView(_selectedNode);
                }
            }
        }

        private void AutoFindReferences() {
            if (_emeraldChangedChannel == null) {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EmeraldChangedEventChannelSO Event_EmeraldChanged");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _emeraldChangedChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<EmeraldChangedEventChannelSO>(path);
                }
#endif
            }

            // Gắn component drag handle lên HeaderBar để người chơi có thể chạm vào thanh tiêu đề để vuốt kéo
            Transform headerTrans = transform.Find("HeaderBar");
            if (headerTrans != null && headerTrans.GetComponent<EUGTDragHandle>() == null) {
                var handle = headerTrans.gameObject.AddComponent<EUGTDragHandle>();
                handle.SetTargetPanel(this);
            }
        }

        public static EmeraldUpgradeTreePanelUI GetOrCreateInScene() {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<EmeraldUpgradeTreePanelUI>(FindObjectsInactive.Include);
            if (existing != null) {
                Instance = existing;
                return existing;
            }

            Canvas targetCanvas = null;
            if (MainCanvasHud.Instance != null) {
                targetCanvas = MainCanvasHud.Instance.GetComponentInParent<Canvas>();
            }
            if (targetCanvas == null) {
                var allCanvas = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                foreach (var c in allCanvas) {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.name.Contains("Canvas") || c.name.Contains("UI")) {
                        targetCanvas = c;
                        break;
                    }
                }
            }
            if (targetCanvas == null) {
                targetCanvas = FindAnyObjectByType<Canvas>();
            }

            if (targetCanvas == null) {
                Debug.LogWarning("[EUGT] Không tìm thấy Canvas trong Scene để tạo EUGT Panel!");
                return null;
            }

            return CreateDefaultPanelUI(targetCanvas.transform);
        }

        private static EmeraldUpgradeTreePanelUI CreateDefaultPanelUI(Transform canvasTransform) {
            GameObject rootWrapper = new GameObject("EmeraldUpgradeTree_Drawer", typeof(RectTransform));
            rootWrapper.transform.SetParent(canvasTransform, false);
            var rootRect = rootWrapper.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            GameObject backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(rootWrapper.transform, false);
            var backRect = backdropGo.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.sizeDelta = Vector2.zero;

            var backImg = backdropGo.GetComponent<Image>();
            backImg.color = new Color(0f, 0f, 0f, 0.65f);
            var backBtn = backdropGo.GetComponent<Button>();

            GameObject panelGo = new GameObject("Panel_EUGT", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(EmeraldUpgradeTreePanelUI));
            panelGo.transform.SetParent(rootWrapper.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(0f, 750f);
            panelRect.anchoredPosition = new Vector2(0f, 0f);

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);
            var canvasGroup = panelGo.GetComponent<CanvasGroup>();
            var script = panelGo.GetComponent<EmeraldUpgradeTreePanelUI>();

            // Header Bar
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

            GameObject capGo = new GameObject("CapacityText", typeof(RectTransform), typeof(TextMeshProUGUI));
            capGo.transform.SetParent(headerGo.transform, false);
            var capRect = capGo.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0.05f, 0.05f);
            capRect.anchorMax = new Vector2(0.85f, 0.48f);
            capRect.sizeDelta = Vector2.zero;
            var capTmp = capGo.GetComponent<TextMeshProUGUI>();
            capTmp.text = "Số lượng nâng cấp: 0 / 0";
            capTmp.fontSize = 13f;
            capTmp.alignment = TextAlignmentOptions.MidlineLeft;
            capTmp.color = new Color(0.75f, 0.85f, 0.95f);

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

            // Bottom Bar
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

            // Scroll View
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

            GameObject viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewGo.transform.SetParent(scrollGo.transform, false);
            var viewRect = viewGo.GetComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.sizeDelta = Vector2.zero;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 600f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210f, 210f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewRect;
            scrollRect.content = contentRect;

            // Detail Popup
            GameObject detailRootGo = new GameObject("DetailPopup_Root", typeof(RectTransform));
            detailRootGo.transform.SetParent(rootWrapper.transform, false);
            var detailRootRect = detailRootGo.GetComponent<RectTransform>();
            detailRootRect.anchorMin = Vector2.zero;
            detailRootRect.anchorMax = Vector2.one;
            detailRootRect.sizeDelta = Vector2.zero;

            GameObject detailBackGo = new GameObject("PopupBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            detailBackGo.transform.SetParent(detailRootGo.transform, false);
            var detailBackRect = detailBackGo.GetComponent<RectTransform>();
            detailBackRect.anchorMin = Vector2.zero;
            detailBackRect.anchorMax = Vector2.one;
            detailBackRect.sizeDelta = Vector2.zero;
            var detailBackImg = detailBackGo.GetComponent<Image>();
            detailBackImg.color = new Color(0f, 0f, 0f, 0.7f);
            var detailBackBtn = detailBackGo.GetComponent<Button>();

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

            SetField(script, "_rootWrapper", rootWrapper);
            SetField(script, "_panelRect", panelRect);
            SetField(script, "_canvasGroup", canvasGroup);
            SetField(script, "_titleText", titleTmp);
            SetField(script, "_capacityText", capTmp);
            SetField(script, "_closeButton", closeBtn);
            SetField(script, "_backdropButton", backBtn);
            SetField(script, "_scrollRect", scrollRect);
            SetField(script, "_itemsContainer", contentGo.transform);
            SetField(script, "_unlockNextBtn", unlBtn);
            SetField(script, "_unlockNextText", unlTxtTmp);

            SetField(script, "_detailPopupRoot", detailRootGo);
            SetField(script, "_detailTitleText", dTitleTmp);
            SetField(script, "_detailDescText", dDescTmp);
            SetField(script, "_detailLevelText", dLevelTmp);
            SetField(script, "_detailIconText", dIconTmp);
            SetField(script, "_detailUpgradeBtn", dUpBtn);
            SetField(script, "_detailUpgradeBtnText", dUpTxtTmp);
            SetField(script, "_detailCloseBtn", dCloseBtn);

            detailBackBtn.onClick.AddListener(script.CloseDetailPopup);
            script.SnapToHidden();

            return script;
        }

        private static void SetField(object target, string fieldName, object value) {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) {
                field.SetValue(target, value);
            }
        }
    }

    /// <summary>
    /// Component hỗ trợ bắt sự kiện kéo vuốt trên thanh HeaderBar hoặc DragHandle và chuyển tiếp về EmeraldUpgradeTreePanelUI.
    /// </summary>
    public class EUGTDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
        [SerializeField] private EmeraldUpgradeTreePanelUI _targetPanel;

        private void Awake() {
            if (_targetPanel == null) {
                _targetPanel = GetComponentInParent<EmeraldUpgradeTreePanelUI>();
            }
        }

        public void SetTargetPanel(EmeraldUpgradeTreePanelUI panel) {
            _targetPanel = panel;
        }

        public void OnBeginDrag(PointerEventData eventData) {
            var panel = _targetPanel != null ? _targetPanel : EmeraldUpgradeTreePanelUI.Instance;
            if (panel != null) panel.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData) {
            var panel = _targetPanel != null ? _targetPanel : EmeraldUpgradeTreePanelUI.Instance;
            if (panel != null) panel.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) {
            var panel = _targetPanel != null ? _targetPanel : EmeraldUpgradeTreePanelUI.Instance;
            if (panel != null) panel.OnEndDrag(eventData);
        }
    }
}
