using System;
using System.Collections.Generic;
using MineMart.Data;
using MineMart.Gameplay.Orders;
using MineMart.Economy;
using MineMart.Feel;
using UnityEngine;

namespace MineMart.Gameplay.Mining {
    /// <summary>
    /// Bản ghi cấu hình của từng slot khai thác tại quầy mỏ.
    /// </summary>
    [Serializable]
    public class MiningSlot {
        [Tooltip("Số thứ tự của slot (0 = Slot gốc, 1 = Slot 2, ...)")]
        public int SlotIndex;

        [Tooltip("Cấp độ mỏ yêu cầu để mở khóa slot này")]
        public int UnlockLevel = 1;

        [Tooltip("Điểm neo nơi thợ/người chơi đứng đào cho slot này")]
        public Transform MinerAnchor;

        [Tooltip("Khối hiển thị 3D cho slot này")]
        public GameObject VisualRoot;

        [NonSerialized] public object OccupyingWorker;
        public bool IsOccupied => OccupyingWorker != null;
        [NonSerialized] public float SavedCraftTimer = 0f;
    }

    /// <summary>
    /// Cấu hình điểm neo tùy chỉnh cho từng slot phụ (Slot 2, Slot 3...).
    /// Cho phép nhà thiết kế tự do đặt vị trí quặng và chỗ đứng đào theo bất kỳ hình dạng nào.
    /// </summary>
    [Serializable]
    public class CustomSlotAnchor {
        [Tooltip("Điểm neo nơi thợ/người chơi đứng đào cho slot này")]
        public Transform MinerAnchor;

        [Tooltip("Khối hiển thị 3D cho slot này")]
        public GameObject VisualRoot;
    }

    /// <summary>
    /// Quản lý Trạm Khai Thác Nguyên Liệu (Mỏ Đất, Mỏ Gỗ, Mỏ Đá...).
    /// Trạm là nơi đặt khối tài nguyên Voxel để thợ đứng đào.
    /// Quản lý Cấp độ (Level 0 = Chưa mở -> Bấm mở khóa -> Level 1), 
    /// mốc Milestone doanh thu, và vị trí thợ đứng làm việc.
    /// Hỗ trợ mở khóa nhiều slot khai thác song song theo tiến trình cấp độ.
    /// </summary>
    public class MiningStation : MonoBehaviour {
        [Header("Configuration")]
        [Tooltip("Hồ sơ cấu hình của trạm mỏ (StallDataSO đã cấu hình trong .asset)")]
        [SerializeField] private StallDataSO _stationData;

        [Header("Scene Anchors")]
        [Tooltip("Điểm neo nơi thợ đứng cầm cuốc đào khối tài nguyên này")]
        [SerializeField] private Transform _minerAnchor;

        [Tooltip("GameObject chứa hình ảnh 3D của khối tài nguyên (ẩn khi chưa mở khóa)")]
        [SerializeField] private GameObject _visualRoot;

        [Header("Layout Settings")]
        [Tooltip("Tùy biến vị trí tự do cho các slot phụ (Slot 2, Slot 3...). Nếu danh sách này để trống, code sẽ tự động dàn hàng ngang theo Slot Spacing.")]
        [SerializeField] private List<CustomSlotAnchor> _customSlotAnchors = new List<CustomSlotAnchor>();

        [Tooltip("Khoảng cách giữa các khối quặng và vị trí đứng khi tự động sinh thêm slot (mặc định: 1.2m)")]
        [SerializeField] private Vector3 _slotSpacing = new Vector3(1.2f, 0f, 0f);

        private readonly List<MiningSlot> _slots = new List<MiningSlot>();

        [Header("Plot Layout")]
        [Tooltip("Ô quy hoạch hiện tại mà trạm mỏ đang được cắm vào")]
        [SerializeField] private WorkstationPlot _currentPlot;

        private bool _isInitialized = false;

        // =========================================================================
        // HỆ SỐ TOÁN HỌC CỐ ĐỊNH CHUẨN IDLE TYCOON (KHÔNG THAY ĐỔI)
        // =========================================================================
        public const double UPGRADE_COST_MULTIPLIER = 1.15; // Hệ số vàng 1.15: Cost(L) = round(C_0 * 1.15^(L-1))
        public const double PRICE_GROWTH_RATE = 0.1;        // Tăng trưởng bậc tuyến tính +10%: Value(L) = V_0 * (1 + 0.1 * (L-1))

        [Header("Slots & Progression")]
        [Tooltip("Cấp độ hiện tại của trạm mỏ (0 = Chưa mở khóa, 1 = Đã mở khóa sẵn sàng khai thác)")]
        [SerializeField] private int _currentLevel = 1;

        [Tooltip("Danh sách các mốc cấp độ mở khóa slot phụ (vd: [5, 10] tức là Lv 1 có 1 slot, Lv 5 mở thêm Slot 2, Lv 10 mở thêm Slot 3).")]
        [SerializeField] private List<int> _slotUnlockLevels = new List<int>() { 5, 10 };

        [Header("Milestone Breakthroughs (Multipliers x2, x4...)")]
        [Tooltip("Danh sách mốc Milestone nhân doanh thu cấu hình riêng cho mỏ này khi đạt cấp chỉ định (vd: Lv 10, 25, 50, 75).")]
        [SerializeField] private List<MilestoneRuleSO> _milestoneRules = new List<MilestoneRuleSO>();

        // Dữ liệu kinh tế được nạp tự động từ MapDataSO (qua MapManager):
        private double _buyPrice = 0;
        private double _blockBaseValue = 3;
        private float _baseCraftTime = 1.0f;
        private double _baseUpgradePrice = 6;

        public double BuyPrice {
            get => _buyPrice;
            set => _buyPrice = value;
        }

        public double BaseUpgradePrice {
            get => _baseUpgradePrice > 0 ? _baseUpgradePrice : 6;
            set => _baseUpgradePrice = value;
        }

        public double UpgradeCostMultiplier => UPGRADE_COST_MULTIPLIER;
        public double PriceIncreasePerLevel => PRICE_GROWTH_RATE;

        /// <summary>
        /// Cấp độ tối đa của mỏ này được quản lý và cấu hình tập trung từ MapManager / MapDataSO.
        /// </summary>
        public int MaxLevel {
            get {
                if (Map.MapManager.Instance != null && _stationData != null) {
                    return Map.MapManager.Instance.GetStallMaxLevel(_stationData);
                }
                return 100;
            }
        }

        /// <summary>
        /// Nạp và ghi đè cấu hình kinh tế của trạm mỏ từ MapDataSO (qua MapManager).
        /// </summary>
        public void ApplyMapEconomyConfig(StallConfigOnMap config) {
            if (config == null) return;
            if (_stationData == null && config.StallData != null) {
                _stationData = config.StallData;
            }
            _buyPrice = config.BuyPrice;
            _blockBaseValue = config.BlockBaseValue;
            _baseCraftTime = config.BaseCraftTime;
            _baseUpgradePrice = config.BaseUpgradePrice;

            RecalculateMilestoneMultipliers();
        }

        [Header("Order-Driven Mining")]
        [Tooltip("Số lượng đơn hàng đang chờ mỏ này khai thác")]
        [SerializeField] private int _pendingOrders = 0;

        [Tooltip("Bắt buộc phải có order từ khách mới cho phép khai thác (mặc định = false để đào tự do trong giai đoạn test)")]
        [SerializeField] private bool _requireOrderToMine = false;

    /// <summary>
    /// Hướng tràn/dâng đầy của thanh tiến trình khai thác.
    /// </summary>
    public enum ProgressBarFillDirection {
        VerticalY = 0,     // Dâng từ dưới lên trên (Bottom to Top)
        HorizontalX = 1,   // Tràn từ trái sang phải (Left to Right)
        DepthZ = 2,        // Tràn từ sau ra trước (Back to Front)
        ScaleUniform = 3   // Phóng to đều cả 3 chiều theo tỉ lệ 0 -> 1
    }

    [Header("3D Mining Progress Visual (Thanh Tiến Trình Hình Cube hoặc Model Tùy Chọn)")]
        [Tooltip("Prefab thanh tiến trình riêng do bạn tự cài đặt model 3D vào. Nếu prefab có object con mang tên 'Fill', 'Bar', hoặc 'Progress', script sẽ tự động điều khiển phần đó để dâng đầy theo tiến độ. Nếu để trống, sẽ dùng 2 khối Cube mặc định.")]
        [SerializeField] private GameObject _customProgressBarPrefab;

        [Tooltip("Prefab riêng cho phần ruột/mực dâng lên (Fill Model). Dùng khi bạn chỉ muốn thay ruột bằng model riêng.")]
        [SerializeField] private GameObject _customProgressFillPrefab;

        [Tooltip("Prefab riêng cho phần khung/vỏ bọc ngoài (Container / Frame).")]
        [SerializeField] private GameObject _customProgressContainerPrefab;

        [Tooltip("Hướng dâng đầy của thanh tiến trình (mặc định: VerticalY - dâng từ dưới lên)")]
        [SerializeField] private ProgressBarFillDirection _fillDirection = ProgressBarFillDirection.VerticalY;

        [Tooltip("Giữ nguyên Material/Texture gốc của Model tùy biến của bạn thay vì tự động đổi màu")]
        [SerializeField] private bool _keepCustomModelMaterials = true;

        [Tooltip("Tự động xoay mặt thanh tiến trình hướng thẳng về Camera chính")]
        [SerializeField] private bool _billboardToCamera = false;

        [Tooltip("Độ cao của thanh tiến trình so với mặt đất (mặc định: 1.6m)")]
        [SerializeField] private float _progressVisualHeight = 1.6f;

        [Tooltip("Kích thước của khối Cube tiến trình (mặc định: 0.35m)")]
        [SerializeField] private float _progressCubeSize = 0.35f;

        [Tooltip("Cho phép hiển thị thanh tiến trình 3D khi có người đào")]
        [SerializeField] private bool _showProgressVisual = true;

        [Tooltip("Sử dụng màu xanh tươi sáng rực rỡ (Bright Emerald) để dễ nhìn nhất")]
        [SerializeField] private bool _useVibrantGreenColor = true;

        [Tooltip("Màu sắc xanh tươi sáng nổi bật cho thanh tiến trình")]
        [SerializeField] private Color _vibrantProgressColor = new Color(0.15f, 0.98f, 0.45f, 1f);

        [Header("Thanh Chỉnh Độ Mờ Tiến Trình (Opacity Sliders)")]
        [Tooltip("Thanh trượt chỉnh độ mờ tổng thể của thanh tiến trình (0 = trong suốt hoàn toàn, 1 = đậm đặc rõ nét)")]
        [Range(0f, 1f)]
        [SerializeField] private float _progressOpacity = 0.5f;

        [Tooltip("Thanh trượt chỉnh độ mờ khung viền kính ngoài (Container Frame)")]
        [Range(0f, 1f)]
        [SerializeField] private float _containerOpacity = 0.25f;

        [Tooltip("Thanh trượt chỉnh độ mờ ruột tiến trình dâng lên bên trong (Fill Cube)")]
        [Range(0f, 1f)]
        [SerializeField] private float _fillOpacity = 0.8f;

        private float _currentCraftTimer = 0f;
        private GameObject _progressVisualRoot;
        private GameObject _progressContainerCube;
        private GameObject _progressFillCube;
        private Material _fillMaterial;
        private Material _containerMaterial;

        // Quản lý biến cho Model tùy chọn (Custom Progress Bar)
        private GameObject _customProgressBarInstance;
        private Transform _customFillTransform;
        private Vector3 _customFillInitialScale = Vector3.one;
        private Vector3 _customFillInitialLocalPos = Vector3.zero;
        private bool _isUsingCustomVisual = false;

        public float ProgressOpacity {
            get => _progressOpacity;
            set {
                _progressOpacity = Mathf.Clamp01(value);
                ApplyProgressVisualColors();
            }
        }

        public float ContainerOpacity {
            get => _containerOpacity;
            set {
                _containerOpacity = Mathf.Clamp01(value);
                ApplyProgressVisualColors();
            }
        }

        public float FillOpacity {
            get => _fillOpacity;
            set {
                _fillOpacity = Mathf.Clamp01(value);
                ApplyProgressVisualColors();
            }
        }

        public float CurrentCraftTimer => _currentCraftTimer;
        public float MiningProgress => GetBaseCraftTime() > 0 ? Mathf.Clamp01(_currentCraftTimer / GetBaseCraftTime()) : 0f;

        private double _currentProfitMultiplier = 1.0;

        // Sự kiện thông báo khi trạm lên cấp hoặc đạt mốc milestone
        public event Action<int> OnLevelChanged;
        public event Action<MilestoneRuleSO> OnMilestoneReached;

        // Sự kiện tĩnh toàn cục (Global Static Events) khi có bất kỳ trạm nào mở khóa hoặc lên cấp
        public static event Action<MiningStation> OnAnyStationUnlocked;
        public static event Action<MiningStation> OnAnyStationLevelChanged;

        public StallDataSO StationData => _stationData;
        public BlockDataSO ProducedBlock => _stationData != null ? _stationData.ProducedBlock : null;
        public IReadOnlyList<MiningSlot> Slots => _slots;
        public Transform MinerAnchor => (_slots.Count > 0 && _slots[0].MinerAnchor != null) ? _slots[0].MinerAnchor : (_minerAnchor != null ? _minerAnchor : transform);

        public int CurrentLevel => _currentLevel;
        public bool IsMaxLevel => _currentLevel >= MaxLevel;
        public bool IsUnlocked => _currentLevel > 0;
        public double CurrentProfitMultiplier => _currentProfitMultiplier;
        public int PendingOrders => _pendingOrders;
        public bool RequireOrderToMine => _requireOrderToMine;
        public IReadOnlyList<int> SlotUnlockLevels => GetConfiguredUnlockLevels();

        public WorkstationPlot CurrentPlot {
            get => _currentPlot;
            set => _currentPlot = value;
        }

        /// <summary>
        /// Tổng số slot đã mở khóa ở cấp độ hiện tại.
        /// </summary>
        public int MaxSlots {
            get {
                if (!IsUnlocked) return 0;
                int count = 0;
                for (int i = 0; i < _slots.Count; i++) {
                    if (_currentLevel >= _slots[i].UnlockLevel) count++;
                }
                return count > 0 ? count : (IsUnlocked ? 1 : 0);
            }
        }

        /// <summary>
        /// Số slot hiện đang có người/công nhân chiếm giữ.
        /// </summary>
        public int OccupiedSlotCount {
            get {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++) {
                    if (_slots[i].IsOccupied) count++;
                }
                return count;
            }
        }

        private bool _isBeingDragged = false;
        public bool IsBeingDragged {
            get => _isBeingDragged;
            set {
                _isBeingDragged = value;
                if (_isBeingDragged) {
                    InterruptAllMiners();
                }
            }
        }

        public bool HasAvailableSlot => IsUnlocked && !_isBeingDragged && OccupiedSlotCount < MaxSlots;
        public bool IsOccupied => !HasAvailableSlot;

        /// <summary>
        /// Lấy điểm neo MinerAnchor phù hợp nhất cho công nhân/người chơi này.
        /// </summary>
        public Transform GetMinerAnchorFor(object worker) {
            if (worker != null) {
                for (int i = 0; i < _slots.Count; i++) {
                    if (_slots[i].OccupyingWorker == worker && _slots[i].MinerAnchor != null) {
                        return _slots[i].MinerAnchor;
                    }
                }
            }

            // Nếu chưa claim, tìm anchor slot trống đầu tiên
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (IsUnlocked && _currentLevel >= slot.UnlockLevel && !slot.IsOccupied && slot.MinerAnchor != null) {
                    return slot.MinerAnchor;
                }
            }

            return MinerAnchor;
        }

        /// <summary>
        /// Chiếm 1 slot khai thác trống tại trạm này và trả về Anchor tương ứng.
        /// </summary>
        public Transform ClaimSlot(object worker) {
            if (worker != null) {
                for (int i = 0; i < _slots.Count; i++) {
                    if (_slots[i].OccupyingWorker == worker) {
                        return _slots[i].MinerAnchor != null ? _slots[i].MinerAnchor : MinerAnchor;
                    }
                }

                for (int i = 0; i < _slots.Count; i++) {
                    MiningSlot slot = _slots[i];
                    if (IsUnlocked && _currentLevel >= slot.UnlockLevel && !slot.IsOccupied) {
                        slot.OccupyingWorker = worker;
                        return slot.MinerAnchor != null ? slot.MinerAnchor : MinerAnchor;
                    }
                }
            }
            return GetMinerAnchorFor(worker);
        }

        /// <summary>
        /// Giải phóng slot khi công nhân/người chơi hoàn tất hoặc hủy đào.
        /// </summary>
        public void ReleaseSlot(object worker) {
            if (worker != null) {
                for (int i = 0; i < _slots.Count; i++) {
                    if (_slots[i].OccupyingWorker == worker) {
                        _slots[i].OccupyingWorker = null;
                    }
                }
            }
            UpdateProgressVisual();
        }

        [Obsolete("Sử dụng ClaimSlot(worker) và ReleaseSlot(worker) để kiểm soát từng slot chính xác, tránh giải phóng nhầm thợ khác.")]
        public void SetOccupied(bool occupied) {
            if (occupied) {
                ClaimSlot(this);
            } else {
                ReleaseSlot(this);
            }
        }

        /// <summary>
        /// Kiểm tra xem có đơn hàng nào đang chờ mỏ này sản xuất không (từ OrderManager hoặc pending orders).
        /// </summary>
        public bool HasOrderInQueue {
            get {
                if (OrderManager.Instance != null && ProducedBlock != null) {
                    return OrderManager.Instance.HasPendingOrder(ProducedBlock);
                }
                return _pendingOrders > 0;
            }
        }

        public bool NeedsMining => IsUnlocked && !_isBeingDragged && (!_requireOrderToMine || HasOrderInQueue) && HasAvailableSlot;

        /// <summary>
        /// Sự kiện phát ra khi trạm mỏ hoàn tất di chuyển và hạ cánh xuống vị trí mới.
        /// Cho phép Thợ (WorkerAgent) và Người chơi (Auto-Pilot) tự động chạy sang vị trí mới để đào tiếp.
        /// </summary>
        public static event Action<MiningStation> OnStationRelocated;

        public void NotifyPlacedDown() {
            OnStationRelocated?.Invoke(this);
        }

        /// <summary>
        /// Ngắt toàn bộ công nhân và người chơi đang đào khi trạm bị nhấc lên di chuyển,
        /// đồng thời lưu lại tiến độ đào dở của từng slot để khi đặt xuống có thể tiếp tục.
        /// </summary>
        public void InterruptAllMiners() {
            if (Player.PlayerController.Instance != null) {
                float saved = Player.PlayerController.Instance.CancelMiningIfTarget(this);
                SaveTimerToWorkerSlot(Player.PlayerController.Instance, saved);
            }

            for (int w = 0; w < Staff.WorkerAgent.AllWorkers.Count; w++) {
                var worker = Staff.WorkerAgent.AllWorkers[w];
                if (worker != null) {
                    float saved = worker.CancelMiningIfTarget(this);
                    SaveTimerToWorkerSlot(worker, saved);
                }
            }

            for (int i = 0; i < _slots.Count; i++) {
                _slots[i].OccupyingWorker = null;
            }
        }

        private void SaveTimerToWorkerSlot(object worker, float timer) {
            if (timer > _currentCraftTimer) {
                _currentCraftTimer = timer;
                UpdateProgressVisual();
            }
            if (worker == null || timer <= 0f) return;
            for (int i = 0; i < _slots.Count; i++) {
                if (_slots[i].OccupyingWorker == worker) {
                    _slots[i].SavedCraftTimer = timer;
                    return;
                }
            }
        }

        /// <summary>
        /// Lấy lại tiến độ đào dở dang đã được bảo lưu của slot này (nếu có).
        /// </summary>
        public float GetSavedCraftTimer(object worker) {
            if (_currentCraftTimer > 0f) {
                return _currentCraftTimer;
            }

            for (int i = 0; i < _slots.Count; i++) {
                if (_slots[i].SavedCraftTimer > 0f) {
                    float t = _slots[i].SavedCraftTimer;
                    _slots[i].SavedCraftTimer = 0f;
                    _currentCraftTimer = t;
                    UpdateProgressVisual();
                    return t;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Cộng dồn tiến trình khai thác của mỏ theo deltaTime và cập nhật hiển thị thanh khối Cube 3D.
        /// Trả về true nếu khối block đã hoàn tất khai thác (đạt 100% thời gian đào).
        /// </summary>
        public bool AddMiningProgress(float deltaTime) {
            float baseTime = GetBaseCraftTime();
            _currentCraftTimer += deltaTime;
            UpdateProgressVisual();

            if (_currentCraftTimer >= baseTime) {
                _currentCraftTimer = 0f;
                UpdateProgressVisual();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Xóa sạch tiến độ đào dở dang và cập nhật lại thanh Cube.
        /// </summary>
        public void ResetMiningProgress() {
            _currentCraftTimer = 0f;
            UpdateProgressVisual();
        }

        // =========================================================================
        // 3D PROGRESS VISUAL SYSTEM (HỖ TRỢ CẢ MODEL TÙY CHỈNH LẪN CUBE MẶC ĐỊNH)
        // =========================================================================

        private void EnsureProgressVisualSetup() {
            if (_progressVisualRoot != null) return;

            // 1. Tạo Root đặt tại độ cao mong muốn
            _progressVisualRoot = new GameObject("MiningProgressVisual");
            _progressVisualRoot.transform.SetParent(transform, false);
            _progressVisualRoot.transform.localPosition = new Vector3(0f, _progressVisualHeight, 0f);
            _progressVisualRoot.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);

            bool hasCustomPrefab = _customProgressBarPrefab != null || _customProgressFillPrefab != null || _customProgressContainerPrefab != null;

            if (hasCustomPrefab) {
                _isUsingCustomVisual = true;

                if (_customProgressBarPrefab != null) {
                    _customProgressBarInstance = Instantiate(_customProgressBarPrefab, _progressVisualRoot.transform, false);
                    _customProgressBarInstance.name = "CustomProgressBar";
                    _customProgressBarInstance.transform.localPosition = Vector3.zero;

                    // Gỡ toàn bộ Collider để không vướng click
                    Collider[] colliders = _customProgressBarInstance.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < colliders.Length; i++) {
                        Destroy(colliders[i]);
                    }

                    // Tự động tìm child đại diện cho phần ruột/mực Fill dâng lên
                    _customFillTransform = FindFillChildTransform(_customProgressBarInstance.transform);
                    if (_customFillTransform != null) {
                        _customFillInitialScale = _customFillTransform.localScale;
                        _customFillInitialLocalPos = _customFillTransform.localPosition;
                    }
                } else {
                    if (_customProgressContainerPrefab != null) {
                        var containerInstance = Instantiate(_customProgressContainerPrefab, _progressVisualRoot.transform, false);
                        containerInstance.name = "CustomContainer";
                        containerInstance.transform.localPosition = Vector3.zero;
                        Collider[] cols = containerInstance.GetComponentsInChildren<Collider>(true);
                        for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
                    }

                    if (_customProgressFillPrefab != null) {
                        var fillInstance = Instantiate(_customProgressFillPrefab, _progressVisualRoot.transform, false);
                        fillInstance.name = "CustomFill";
                        fillInstance.transform.localPosition = Vector3.zero;
                        Collider[] cols = fillInstance.GetComponentsInChildren<Collider>(true);
                        for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);

                        _customFillTransform = fillInstance.transform;
                        _customFillInitialScale = _customFillTransform.localScale;
                        _customFillInitialLocalPos = _customFillTransform.localPosition;
                    }
                }

                _progressVisualRoot.SetActive(false);
                return;
            }

            _isUsingCustomVisual = false;

            // 2. Tạo Shader tương thích
            Shader unlitShader = Shader.Find("Sprites/Default") 
                              ?? Shader.Find("Universal Render Pipeline/Unlit") 
                              ?? Shader.Find("Standard");

            // 3. Khung Container ngoài (khối cube viền mờ trong suốt)
            _progressContainerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _progressContainerCube.name = "ContainerFrame";
            _progressContainerCube.transform.SetParent(_progressVisualRoot.transform, false);
            _progressContainerCube.transform.localPosition = Vector3.zero;
            _progressContainerCube.transform.localScale = Vector3.one * _progressCubeSize;

            // Xóa Collider để không chặn raycast click hay va chạm
            var colContainer = _progressContainerCube.GetComponent<Collider>();
            if (colContainer != null) Destroy(colContainer);

            var mrContainer = _progressContainerCube.GetComponent<MeshRenderer>();
            if (mrContainer != null) {
                mrContainer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrContainer.receiveShadows = false;
                float effContainerAlpha = Mathf.Clamp01(_containerOpacity * _progressOpacity);
                // RenderQueue 3001 để vẽ khung kính ngoài bao bọc khối ruột bên trong
                _containerMaterial = CreateTransparentMaterial(unlitShader, new Color(0.88f, 0.96f, 1.0f, effContainerAlpha), 3001);
                mrContainer.material = _containerMaterial;
            }

            // 4. Khối Mực Điền Đầy (Fill Cube) dâng từ đáy lên
            _progressFillCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _progressFillCube.name = "ProgressFill";
            _progressFillCube.transform.SetParent(_progressVisualRoot.transform, false);

            var colFill = _progressFillCube.GetComponent<Collider>();
            if (colFill != null) Destroy(colFill);

            var mrFill = _progressFillCube.GetComponent<MeshRenderer>();
            if (mrFill != null) {
                mrFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrFill.receiveShadows = false;
                Color fillCol = GetBlockColor();
                fillCol.a = Mathf.Clamp01(_fillOpacity * _progressOpacity);
                // RenderQueue 3000 để vẽ khối mực bên trong trước
                _fillMaterial = CreateTransparentMaterial(unlitShader, fillCol, 3000);
                mrFill.material = _fillMaterial;
            }

            _progressVisualRoot.SetActive(false);
        }

        private Transform FindFillChildTransform(Transform root) {
            if (root == null) return null;
            // 1. Quét tìm object con có tên chứa từ khóa
            string[] keywords = new string[] { "fill", "bar", "progress", "slider", "amount", "inner" };
            Transform found = SearchTransformRecursive(root, keywords);
            if (found != null && found != root) return found;

            // 2. Nếu có child và không tìm ra tên keyword, ưu tiên child có Renderer
            for (int i = 0; i < root.childCount; i++) {
                Transform child = root.GetChild(i);
                if (child.GetComponent<Renderer>() != null) return child;
            }

            // 3. Fallback về child đầu tiên hoặc chính root
            return root.childCount > 0 ? root.GetChild(0) : root;
        }

        private Transform SearchTransformRecursive(Transform parent, string[] keywords) {
            for (int i = 0; i < parent.childCount; i++) {
                Transform child = parent.GetChild(i);
                string childName = child.name.ToLowerInvariant();
                for (int k = 0; k < keywords.Length; k++) {
                    if (childName.Contains(keywords[k])) {
                        return child;
                    }
                }
                Transform sub = SearchTransformRecursive(child, keywords);
                if (sub != null) return sub;
            }
            return null;
        }

        private Material CreateTransparentMaterial(Shader shader, Color initialColor, int renderQueue) {
            var mat = new Material(shader);

            // Cấu hình vật liệu Transparent tương thích chuẩn cả URP lẫn Built-in
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent trong URP
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // 0 = Alpha blend
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

            mat.renderQueue = renderQueue;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            SetMaterialColor(mat, initialColor);
            return mat;
        }

        private void SetMaterialColor(Material mat, Color col) {
            if (mat == null) return;
            mat.color = col;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        }

        public void ApplyProgressVisualColors() {
            if (_containerMaterial != null) {
                float effectiveContainerAlpha = Mathf.Clamp01(_containerOpacity * _progressOpacity);
                Color containerCol = new Color(0.88f, 0.96f, 1.0f, effectiveContainerAlpha);
                SetMaterialColor(_containerMaterial, containerCol);
            }

            if (_fillMaterial != null) {
                float effectiveFillAlpha = Mathf.Clamp01(_fillOpacity * _progressOpacity);
                Color fillCol = GetBlockColor();
                fillCol.a = effectiveFillAlpha;
                SetMaterialColor(_fillMaterial, fillCol);
            }
        }

        public void UpdateProgressVisual() {
            if (!_showProgressVisual) {
                if (_progressVisualRoot != null && _progressVisualRoot.activeSelf) {
                    _progressVisualRoot.SetActive(false);
                }
                return;
            }

            EnsureProgressVisualSetup();

            float progress = MiningProgress;
            bool isMiningInProgress = progress > 0.001f;
            bool isAnyoneMining = OccupiedSlotCount > 0;

            // Hiển thị thanh tiến trình nếu đang có người đào HOẶC đang lưu lại tiến độ đào dở dang
            if (!isMiningInProgress && !isAnyoneMining) {
                if (_progressVisualRoot.activeSelf) _progressVisualRoot.SetActive(false);
                return;
            }

            if (!_progressVisualRoot.activeSelf) {
                _progressVisualRoot.SetActive(true);
            }

            // === A. TRƯỜNG HỢP MODEL TÙY CHỌN (CUSTOM MODEL PREFAB) ===
            if (_isUsingCustomVisual) {
                if (_customFillTransform != null) {
                    if (progress > 0.005f) {
                        if (!_customFillTransform.gameObject.activeSelf) _customFillTransform.gameObject.SetActive(true);

                        float p = Mathf.Clamp01(progress);
                        switch (_fillDirection) {
                            case ProgressBarFillDirection.VerticalY: {
                                float bottomY = _customFillInitialLocalPos.y - _customFillInitialScale.y * 0.5f;
                                float curHeight = _customFillInitialScale.y * p;
                                _customFillTransform.localPosition = new Vector3(
                                    _customFillInitialLocalPos.x,
                                    bottomY + curHeight * 0.5f,
                                    _customFillInitialLocalPos.z
                                );
                                _customFillTransform.localScale = new Vector3(
                                    _customFillInitialScale.x,
                                    Mathf.Max(0.0001f, curHeight),
                                    _customFillInitialScale.z
                                );
                                break;
                            }
                            case ProgressBarFillDirection.HorizontalX: {
                                float leftX = _customFillInitialLocalPos.x - _customFillInitialScale.x * 0.5f;
                                float curWidth = _customFillInitialScale.x * p;
                                _customFillTransform.localPosition = new Vector3(
                                    leftX + curWidth * 0.5f,
                                    _customFillInitialLocalPos.y,
                                    _customFillInitialLocalPos.z
                                );
                                _customFillTransform.localScale = new Vector3(
                                    Mathf.Max(0.0001f, curWidth),
                                    _customFillInitialScale.y,
                                    _customFillInitialScale.z
                                );
                                break;
                            }
                            case ProgressBarFillDirection.DepthZ: {
                                float backZ = _customFillInitialLocalPos.z - _customFillInitialScale.z * 0.5f;
                                float curDepth = _customFillInitialScale.z * p;
                                _customFillTransform.localPosition = new Vector3(
                                    _customFillInitialLocalPos.x,
                                    _customFillInitialLocalPos.y,
                                    backZ + curDepth * 0.5f
                                );
                                _customFillTransform.localScale = new Vector3(
                                    _customFillInitialScale.x,
                                    _customFillInitialScale.y,
                                    Mathf.Max(0.0001f, curDepth)
                                );
                                break;
                            }
                            case ProgressBarFillDirection.ScaleUniform: {
                                _customFillTransform.localScale = _customFillInitialScale * p;
                                break;
                            }
                        }
                    } else {
                        if (_customFillTransform.gameObject.activeSelf) _customFillTransform.gameObject.SetActive(false);
                    }
                }

                if (!_keepCustomModelMaterials) {
                    ApplyProgressVisualColors();
                }
                return;
            }

            // === B. TRƯỜNG HỢP CUBE MẶC ĐỊNH ===
            float innerSize = _progressCubeSize * 0.94f;
            float currentHeight = innerSize * Mathf.Clamp01(progress);
            float bottomYCube = -innerSize * 0.5f;

            if (_progressFillCube != null) {
                if (progress > 0.005f) {
                    if (!_progressFillCube.activeSelf) _progressFillCube.SetActive(true);
                    _progressFillCube.transform.localPosition = new Vector3(0f, bottomYCube + currentHeight * 0.5f, 0f);
                    _progressFillCube.transform.localScale = new Vector3(innerSize * 0.98f, Mathf.Max(0.001f, currentHeight), innerSize * 0.98f);
                } else {
                    if (_progressFillCube.activeSelf) _progressFillCube.SetActive(false);
                }
            }

            ApplyProgressVisualColors();
        }

        private Color GetBlockColor() {
            if (_useVibrantGreenColor) {
                return _vibrantProgressColor;
            }

            if (ProducedBlock != null) {
                string id = (ProducedBlock.BlockId ?? ProducedBlock.name).ToLowerInvariant();
                if (id.Contains("dirt") || id.Contains("dat")) return new Color(0.45f, 0.92f, 0.35f); // xanh mầm tươi mát
                if (id.Contains("stone") || id.Contains("da") || id.Contains("cobble")) return new Color(0.35f, 0.92f, 0.85f);
                if (id.Contains("coal") || id.Contains("than")) return new Color(0.25f, 0.95f, 0.55f);
                if (id.Contains("iron") || id.Contains("sat")) return new Color(0.9f, 0.95f, 1.0f);
                if (id.Contains("gold") || id.Contains("vang")) return new Color(1.0f, 0.92f, 0.2f);
                if (id.Contains("diamond") || id.Contains("kimcuong")) return new Color(0.2f, 0.95f, 1.0f);
                if (id.Contains("emerald") || id.Contains("ngoc")) return new Color(0.15f, 1.0f, 0.45f);
                if (id.Contains("wood") || id.Contains("go") || id.Contains("log")) return new Color(0.85f, 0.65f, 0.35f);
                if (id.Contains("sand") || id.Contains("cat")) return new Color(0.98f, 0.92f, 0.65f);
                if (id.Contains("redstone")) return new Color(1.0f, 0.3f, 0.3f);
                if (id.Contains("lapis")) return new Color(0.25f, 0.55f, 1.0f);
            }
            return _vibrantProgressColor;
        }

        private void LateUpdate() {
            if (_progressVisualRoot != null && _progressVisualRoot.activeSelf) {
                if (_billboardToCamera && Camera.main != null) {
                    _progressVisualRoot.transform.rotation = Camera.main.transform.rotation;
                } else {
                    _progressVisualRoot.transform.rotation = Quaternion.Euler(20f, 45f, 0f);
                }
            }
        }

        private void OnDestroy() {
            if (_containerMaterial != null) Destroy(_containerMaterial);
            if (_fillMaterial != null) Destroy(_fillMaterial);
        }

#if UNITY_EDITOR
        private void OnValidate() {
            // Cập nhật ngay lập tức màu sắc & độ mờ khi kéo thanh trượt trong Inspector
            if (_progressVisualRoot != null) {
                ApplyProgressVisualColors();
            }

            EnsureCollider();
            if (Application.isPlaying && _slots != null && _slots.Count > 0) {
                RecalculateMilestoneMultipliers();
                UpdateVisualState();
            }
        }

        [ContextMenu("Đồng Bộ Độ Mờ Sang Tất Cả Quầy Mỏ")]
        public void SyncOpacityToAllStations() {
            foreach (var st in s_allStations) {
                if (st == null || st == this) continue;
                st._progressOpacity = this._progressOpacity;
                st._containerOpacity = this._containerOpacity;
                st._fillOpacity = this._fillOpacity;
                st.ApplyProgressVisualColors();
            }
            Debug.Log($"[MiningStation] Đã đồng bộ độ mờ (Tổng thể: {_progressOpacity}, Viền ngoài: {_containerOpacity}, Mực trong: {_fillOpacity}) sang {s_allStations.Count} quầy mỏ!");
        }
#endif

        private static readonly List<MiningStation> s_allStations = new List<MiningStation>();
        public static IReadOnlyList<MiningStation> AllStations => s_allStations;

        private void EnsureCollider() {
            var col = GetComponent<Collider>();
            if (col == null) {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1.2f, 1.2f, 1.2f);
                box.center = new Vector3(0, 0.6f, 0);
            }
        }

        private void OnEnable() {
            if (!s_allStations.Contains(this)) {
                s_allStations.Add(this);
            }
        }

        private void OnDisable() {
            s_allStations.Remove(this);
        }

        /// <summary>
        /// Thêm đơn hàng cần mỏ này sản xuất.
        /// </summary>
        public void AddPendingOrder(int count = 1) {
            _pendingOrders += Mathf.Max(1, count);
        }

        /// <summary>
        /// Tiêu thụ 1 đơn hàng khi thợ hoặc người chơi đào xong 1 block mà không dùng cơ chế Claim trước.
        /// Tự động cập nhật trạng thái đơn sang Delivering trên OrderManager.
        /// </summary>
        public void ConsumePendingOrder() {
            if (OrderManager.Instance != null && ProducedBlock != null) {
                OrderTicket ticket = OrderManager.Instance.ClaimNextPendingOrder(ProducedBlock, this);
                if (ticket != null) {
                    OrderManager.Instance.MarkDelivering(ticket);
                }
            }

            if (_pendingOrders > 0) {
                _pendingOrders--;
            }
        }

        /// <summary>
        /// Người chơi hoặc Thợ claim trước 1 đơn hàng Pending khi bắt đầu bổ cuốc đào.
        /// Việc claim ngay lập tức làm giảm PendingCount về 0 (nếu chỉ có 1 đơn), giúp mỏ khóa lại không cho đào thừa!
        /// </summary>
        public OrderTicket ClaimOrderForMining(object worker) {
            if (OrderManager.Instance != null && ProducedBlock != null) {
                return OrderManager.Instance.ClaimNextPendingOrder(ProducedBlock, worker);
            }
            return null;
        }

        /// <summary>
        /// Hoàn trả đơn hàng về trạng thái Pending nếu người chơi/thợ bỏ dở việc đào giữa chừng.
        /// </summary>
        public void ReleaseClaim(OrderTicket ticket) {
            if (OrderManager.Instance != null && ticket != null) {
                OrderManager.Instance.ReleaseClaim(ticket);
            }
        }

        /// <summary>
        /// Xác nhận đã đào xong 1 khối block, chuyển vé sang trạng thái Delivering (bưng đi giao).
        /// </summary>
        public void FinishMiningOrder(OrderTicket ticket) {
            if (OrderManager.Instance != null && ticket != null) {
                OrderManager.Instance.MarkDelivering(ticket);
            }

            if (_pendingOrders > 0) {
                _pendingOrders--;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Add 1 Pending Order")]
        private void DebugAddOrder() {
            if (OrderManager.Instance != null && ProducedBlock != null) {
                OrderManager.Instance.CreateOrder(this, ProducedBlock, GetFinalSellPrice());
            } else {
                AddPendingOrder(1);
            }
            Debug.Log($"[MiningStation] Đã thêm 1 đơn hàng vào {name}. Đơn trong OrderManager: {(OrderManager.Instance != null && ProducedBlock != null ? OrderManager.Instance.GetPendingCount(ProducedBlock) : _pendingOrders)}");
        }

        [ContextMenu("Debug: Clear Pending Orders")]
        private void DebugClearOrders() {
            _pendingOrders = 0;
            Debug.Log($"[MiningStation] Đã xóa hết đơn chờ trên {name}.");
        }
#endif

        private void Awake() {
            EnsureCollider();
            InitializeSlots();

            // Đảm bảo mỏ luôn sẵn sàng ở cấp 1 nếu tạo mới chưa gán level
            if (_currentLevel < 1) {
                _currentLevel = 1;
            }

#if UNITY_EDITOR
            if (_stationData == null) {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:StallDataSO Stall_Dirt");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _stationData = UnityEditor.AssetDatabase.LoadAssetAtPath<StallDataSO>(path);
                }
            }
#endif

            RecalculateMilestoneMultipliers();
            UpdateVisualState();
            _isInitialized = true;
        }

#if UNITY_EDITOR
        private void Reset() {
            EnsureCollider();
        }
#endif

        private void InitializeSlots() {
            if (_minerAnchor == null) {
                Transform foundAnchor = transform.Find("MinerAnchor");
                if (foundAnchor != null) _minerAnchor = foundAnchor;
            }

            if (_visualRoot == null) {
                _visualRoot = gameObject;
            }

            IReadOnlyList<int> unlockLevels = GetConfiguredUnlockLevels();

            // Đảm bảo Slot 0 luôn tồn tại (Slot gốc mở ở Level 1)
            if (_slots.Count == 0) {
                _slots.Add(new MiningSlot {
                    SlotIndex = 0,
                    UnlockLevel = 1,
                    MinerAnchor = _minerAnchor,
                    VisualRoot = _visualRoot
                });
            } else {
                if (_slots[0].MinerAnchor == null) _slots[0].MinerAnchor = _minerAnchor;
                if (_slots[0].VisualRoot == null) _slots[0].VisualRoot = _visualRoot;
                _slots[0].UnlockLevel = 1;
            }

            // Tự động tạo và liên kết các slot tiếp theo theo danh sách mốc level
            for (int k = 0; k < unlockLevels.Count; k++) {
                int slotIndex = k + 1;
                int reqLevel = unlockLevels[k];

                if (_slots.Count <= slotIndex) {
                    Transform anchor = null;
                    GameObject visual = null;

                    // 1. Ưu tiên lấy từ Custom Slot Anchors cấu hình trên Inspector
                    if (_customSlotAnchors != null && k < _customSlotAnchors.Count && _customSlotAnchors[k] != null) {
                        anchor = _customSlotAnchors[k].MinerAnchor;
                        visual = _customSlotAnchors[k].VisualRoot;
                    }

                    // 2. Tìm GameObject con có tên MinerAnchor_X nếu chưa được gán
                    if (anchor == null) {
                        Transform foundAnchor = transform.Find($"MinerAnchor_{slotIndex + 1}");
                        if (foundAnchor != null) {
                            anchor = foundAnchor;
                        } else if (_minerAnchor != null) {
                            GameObject anchorGo = new GameObject($"MinerAnchor_{slotIndex + 1}");
                            anchorGo.transform.SetParent(transform, false);
                            anchorGo.transform.localPosition = _minerAnchor.localPosition + _slotSpacing * slotIndex;
                            anchorGo.transform.localRotation = _minerAnchor.localRotation;
                            anchor = anchorGo.transform;
                        }
                    }

                    // 3. Tìm GameObject con VisualRoot_X hoặc clone khối 3D gốc
                    if (visual == null) {
                        Transform foundVisual = transform.Find($"VisualRoot_{slotIndex + 1}");
                        if (foundVisual != null) {
                            visual = foundVisual.gameObject;
                        } else if (_visualRoot != null) {
                            GameObject visualClone;
                            if (_visualRoot == gameObject) {
                                // Nếu Visual Root trỏ vào chính GameObject trạm (có MeshFilter/MeshRenderer),
                                // tự động tạo GameObject con chứa Mesh thay vì bỏ qua do trùng gameObject.
                                visualClone = new GameObject($"VisualRoot_{slotIndex + 1}");
                                visualClone.transform.SetParent(transform, false);
                                visualClone.transform.localPosition = _slotSpacing * slotIndex;
                                visualClone.transform.localRotation = Quaternion.identity;
                                visualClone.transform.localScale = Vector3.one;

                                var mf = GetComponent<MeshFilter>();
                                if (mf != null) {
                                    var cloneMf = visualClone.AddComponent<MeshFilter>();
                                    cloneMf.sharedMesh = mf.sharedMesh;
                                }
                                var mr = GetComponent<MeshRenderer>();
                                if (mr != null) {
                                    var cloneMr = visualClone.AddComponent<MeshRenderer>();
                                    cloneMr.sharedMaterials = mr.sharedMaterials;
                                }
                            } else {
                                visualClone = Instantiate(_visualRoot, transform);
                                visualClone.name = $"VisualRoot_{slotIndex + 1}";
                                visualClone.transform.localPosition = _visualRoot.transform.localPosition + _slotSpacing * slotIndex;
                                visualClone.transform.localRotation = _visualRoot.transform.localRotation;
                            }
                            visual = visualClone;
                        }
                    }

                    _slots.Add(new MiningSlot {
                        SlotIndex = slotIndex,
                        UnlockLevel = reqLevel,
                        MinerAnchor = anchor,
                        VisualRoot = visual
                    });
                } else {
                    _slots[slotIndex].UnlockLevel = reqLevel;
                }
            }
        }

        public IReadOnlyList<int> GetConfiguredUnlockLevels() {
            return _slotUnlockLevels ?? (IReadOnlyList<int>)Array.Empty<int>();
        }

        private void Start() {
            if (Map.MapManager.Instance != null && _stationData != null) {
                StallConfigOnMap cfg = Map.MapManager.Instance.GetStallConfig(_stationData);
                if (cfg != null) {
                    ApplyMapEconomyConfig(cfg);
                }
            }

            // Đảm bảo trạm mỏ luôn có ít nhất 1 đơn khởi đầu để người chơi đào được ngay khi test
            if (_pendingOrders <= 0 && _requireOrderToMine) {
                _pendingOrders = 1;
            }

            EnsureProgressVisualSetup();
            UpdateProgressVisual();
        }

        /// <summary>
        /// Lấy chi phí cần thiết để mở khóa hoặc nâng cấp lên cấp tiếp theo.
        /// </summary>
        public double GetNextUpgradeCost() {
            if (IsMaxLevel) return 0;
            return GetUpgradeCost();
        }

        /// <summary>
        /// Mở khóa trạm (chuyển từ Level 0 lên Level 1).
        /// Hỗ trợ cả trường hợp mở khóa miễn phí 0 Vàng (như Quầy Đất tân thủ).
        /// </summary>
        public bool TryUnlock() {
            if (IsUnlocked) return false;

            _currentLevel = 1;
            RecalculateMilestoneMultipliers();
            UpdateVisualState();

            OnLevelChanged?.Invoke(_currentLevel);
            OnAnyStationUnlocked?.Invoke(this);
            OnAnyStationLevelChanged?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Nâng cấp trạm lên 1 cấp mới, đồng thời kiểm tra chạm mốc Milestone nhân doanh thu
        /// và mở khóa các slot tiếp theo khi chạm ngưỡng level quy định.
        /// </summary>
        public void Upgrade() {
            if (!IsUnlocked) {
                TryUnlock();
                return;
            }

            if (IsMaxLevel) return;

            _currentLevel++;
            CheckMilestones();
            UpdateVisualState();
            OnLevelChanged?.Invoke(_currentLevel);
            OnAnyStationLevelChanged?.Invoke(this);
        }

        /// <summary>
        /// Lấy chi phí vàng để nâng cấp trạm mỏ lên cấp tiếp theo: BaseUpgradePrice * Multiplier^(Level - 1)
        /// </summary>
        public double GetUpgradeCost() {
            if (_currentLevel <= 0) return BaseUpgradePrice;
            return Math.Round(BaseUpgradePrice * Math.Pow(UpgradeCostMultiplier, _currentLevel - 1));
        }

        /// <summary>
        /// Nạp lại cấp độ trạm từ SaveManager khi load game.
        /// </summary>
        public void SetLevel(int level) {
            _currentLevel = Mathf.Clamp(level, 0, MaxLevel);
            RecalculateMilestoneMultipliers();
            UpdateVisualState();
            OnLevelChanged?.Invoke(_currentLevel);
        }

        private IReadOnlyList<MilestoneRuleSO> ActiveMilestones =>
            _milestoneRules != null ? _milestoneRules : (IReadOnlyList<MilestoneRuleSO>)Array.Empty<MilestoneRuleSO>();

        /// <summary>
        /// Tính toán giá bán của block ở một cấp độ cụ thể (kết hợp giá gốc từ Map, tăng trưởng theo level, mốc Milestone và nâng cấp Map).
        /// </summary>
        public double GetPriceAtLevel(int level) {
            if (ProducedBlock == null) return 0;

            double baseVal = _blockBaseValue > 0 ? _blockBaseValue : 3.0;
            if (Map.MapManager.Instance != null) {
                baseVal = Map.MapManager.Instance.GetBlockBaseValue(ProducedBlock);
            }

            double mult = CalculateMilestoneMultiplierForLevel(level);
            double growth = 1.0 + Math.Max(0, level - 1) * PriceIncreasePerLevel;

            double mapUpgradeMultiplier = (Map.MapManager.Instance != null && _stationData != null)
                ? Map.MapManager.Instance.GetBlockPriceMultiplier(_stationData)
                : 1.0;

            return Math.Round(baseVal * growth * mult * mapUpgradeMultiplier, 1);
        }

        /// <summary>
        /// Tính toán giá bán cuối cùng của 1 block sản xuất từ trạm này ở cấp độ hiện tại.
        /// </summary>
        public double GetFinalSellPrice() {
            return GetPriceAtLevel(_currentLevel);
        }

        /// <summary>
        /// Lấy giá bán hiện tại của loại block này dựa trên trạm mỏ đang hoạt động trên bản đồ.
        /// Dùng cho luồng thanh toán Order và nhận đơn khách hàng.
        /// </summary>
        public static double GetSellPriceForBlock(BlockDataSO block) {
            if (block == null) return 0;
            for (int i = 0; i < s_allStations.Count; i++) {
                MiningStation st = s_allStations[i];
                if (st != null && st.ProducedBlock == block && st.IsUnlocked) {
                    return st.GetFinalSellPrice();
                }
            }

            if (Map.MapManager.Instance != null) {
                return Map.MapManager.Instance.GetBlockBaseValue(block);
            }
            return 3;
        }

        /// <summary>
        /// Tính trung bình cộng giá bán của toàn bộ block đã mở bán tại shop (trên các mỏ đã mở khóa).
        /// </summary>
        public static double CalculateAverageUnlockedBlockPrice() {
            double total = 0;
            int count = 0;
            for (int i = 0; i < s_allStations.Count; i++) {
                MiningStation st = s_allStations[i];
                if (st != null && st.IsUnlocked && st.ProducedBlock != null) {
                    total += st.GetFinalSellPrice();
                    count++;
                }
            }

            if (count == 0) {
                return 3.0; // Fallback an toàn
            }

            return total / count;
        }

        /// <summary>
        /// Tính số tiền tip mặc định = Trung bình cộng giá của toàn bộ block đã mở bán nhân với 2.
        /// </summary>
        public static double CalculateTipAmount() {
            return Math.Round(CalculateAverageUnlockedBlockPrice() * 2.0);
        }

        /// <summary>
        /// Thời gian đào của block tại trạm này (đã trừ nâng cấp giảm giờ đào từ MapManager).
        /// </summary>
        public float GetBaseCraftTime() {
            float baseTime = _baseCraftTime > 0 ? _baseCraftTime : 1.0f;
            if (Map.MapManager.Instance != null && _stationData != null) {
                float mapBase = Map.MapManager.Instance.GetStallBaseCraftTime(_stationData);
                if (mapBase > 0) baseTime = mapBase;
                float reduction = Map.MapManager.Instance.GetMiningTimeReduction(_stationData);
                return Mathf.Max(0.1f, baseTime - reduction);
            }
            return Mathf.Max(0.1f, baseTime);
        }

        /// <summary>
        /// Lấy mốc Milestone tiếp theo gần nhất (ví dụ Lv 10 nhân x2).
        /// Trả về true nếu còn mốc phía trước, false nếu đã vượt qua mọi mốc.
        /// </summary>
        public bool TryGetNextMilestone(out MilestoneRuleSO nextRule, out int levelsLeft) {
            nextRule = null;
            levelsLeft = 0;
            var milestones = ActiveMilestones;
            if (milestones == null || milestones.Count == 0) return false;

            int minTarget = int.MaxValue;
            for (int i = 0; i < milestones.Count; i++) {
                MilestoneRuleSO rule = milestones[i];
                if (rule != null && rule.TargetLevel > _currentLevel) {
                    if (rule.TargetLevel < minTarget) {
                        minTarget = rule.TargetLevel;
                        nextRule = rule;
                    }
                }
            }

            if (nextRule != null) {
                levelsLeft = nextRule.TargetLevel - _currentLevel;
                return true;
            }
            return false;
        }

        public double CalculateMilestoneMultiplierForLevel(int level) {
            double mult = 1.0;
            var milestones = ActiveMilestones;
            if (milestones == null || milestones.Count == 0) return mult;

            for (int i = 0; i < milestones.Count; i++) {
                MilestoneRuleSO rule = milestones[i];
                if (rule != null && level >= rule.TargetLevel) {
                    mult *= rule.ProfitMultiplier;
                }
            }
            return mult;
        }

        private void CheckMilestones() {
            var milestones = ActiveMilestones;
            if (milestones == null || milestones.Count == 0) return;

            for (int i = 0; i < milestones.Count; i++) {
                MilestoneRuleSO rule = milestones[i];
                if (rule != null && _currentLevel == rule.TargetLevel) {
                    _currentProfitMultiplier = CalculateMilestoneMultiplierForLevel(_currentLevel);

                    // Thưởng Gem / Emerald cho người chơi khi trạm mỏ đạt mốc đột phá
                    if (rule.GemReward > 0 && HardCurrencyManager.Instance != null) {
                        HardCurrencyManager.Instance.AddEmerald(rule.GemReward);
                        Debug.Log($"<color=#2ecc71>[MiningStation]</color> Trạm '{gameObject.name}' đạt mốc Level {rule.TargetLevel}! Thưởng {rule.GemReward} Gem (Emerald).");
                    }

                    OnMilestoneReached?.Invoke(rule);
                }
            }
        }

        private void RecalculateMilestoneMultipliers() {
            _currentProfitMultiplier = CalculateMilestoneMultiplierForLevel(_currentLevel);
        }

        private void UpdateVisualState() {
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null && slot.VisualRoot != null) {
                    bool unlocked = IsUnlocked && _currentLevel >= slot.UnlockLevel;
                    bool wasActive = slot.VisualRoot.activeSelf;
                    slot.VisualRoot.SetActive(unlocked);

                    // Khi mở thêm slot khối block mới lúc nâng cấp -> hiệu ứng rơi từ trên trời xuống
                    if (unlocked && !wasActive && _isInitialized && Application.isPlaying) {
                        DropBounceEffect.PlayLocal(slot.VisualRoot.transform, slot.VisualRoot.transform.localPosition, 2.5f, 0.4f);
                    }
                }
            }
        }

        private void OnDrawGizmos() {
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot == null || slot.MinerAnchor == null) continue;

                bool unlocked = IsUnlocked && _currentLevel >= slot.UnlockLevel;
                if (i == 0) {
                    Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
                } else if (unlocked) {
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                } else {
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                }

                Gizmos.DrawWireSphere(slot.MinerAnchor.position, 0.3f);
            }
        }
    }
}
