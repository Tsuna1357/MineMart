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

        // Quản lý thời gian & thanh tiến trình riêng biệt cho từng slot
        [NonSerialized] public float CurrentCraftTimer = 0f;
        [NonSerialized] public GameObject ProgressVisualRoot;
        [NonSerialized] public GameObject ProgressContainerCube;
        [NonSerialized] public GameObject ProgressFillCube;
        [NonSerialized] public GameObject CustomProgressBarInstance;
        [NonSerialized] public Transform CustomFillTransform;
        [NonSerialized] public Vector3 CustomFillInitialScale = Vector3.one;
        [NonSerialized] public Vector3 CustomFillInitialLocalPos = Vector3.zero;
        [NonSerialized] public Material ContainerMaterial;
        [NonSerialized] public Material FillMaterial;

        // Phục hồi và tạo hiệu ứng rung nảy đàn hồi (Game Feel)
        [NonSerialized] public Vector3 InitialVisualScale = Vector3.one;
        [NonSerialized] public Vector3 InitialVisualLocalPos = Vector3.zero;
        [NonSerialized] public bool HasSavedVisualTransform = false;
        [NonSerialized] public float HitWobbleCooldown = 0f;
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
            if (config.StallData != null) {
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

        [Header("Block Mining Feel Settings (Tốc Độ Nhún Của Stall)")]
        [Tooltip("Tốc độ / tần số nhún của khối stall khi đang đào (mặc định 4.5: nhún chậm rãi từ từ)")]
        [SerializeField] private float _miningWobbleSpeed = 4.5f;

        [Tooltip("Độ co giãn nhún của khối stall khi đào (mặc định 0.025: nhún nhẹ nhàng)")]
        [SerializeField] private float _miningWobbleIntensity = 0.025f;

        [Tooltip("Thời gian hiệu ứng rơi nảy khối mới khi đào xong (giây - mặc định 0.75s: rơi chậm rãi từ từ)")]
        [SerializeField] private float _blockRespawnDuration = 0.75f;

        [Tooltip("Độ cao rơi của khối mới khi đào xong (mét)")]
        [SerializeField] private float _blockRespawnDropHeight = 1.2f;

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
        [NonSerialized] private bool _isInitializingSlots = false;

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
                        UpdateSlotProgressVisual(_slots[i]);
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
        /// đồng thời bảo lưu 100% tiến độ đào dở dang của từng slot và giữ nguyên thanh tiến trình hiển thị.
        /// </summary>
        public void InterruptAllMiners() {
            // 1. Bảo lưu tiến độ đào hiện tại của toàn bộ các slot (nếu đang đào dở) trước khi giải phóng thợ
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null) {
                    if (slot.CurrentCraftTimer > 0f) {
                        slot.SavedCraftTimer = slot.CurrentCraftTimer;
                    } else if (slot.SavedCraftTimer > 0f) {
                        slot.CurrentCraftTimer = slot.SavedCraftTimer;
                    }
                }
            }

            if (_currentCraftTimer > 0f && _slots.Count > 0 && _slots[0].CurrentCraftTimer <= 0f) {
                _slots[0].CurrentCraftTimer = _currentCraftTimer;
                _slots[0].SavedCraftTimer = _currentCraftTimer;
            }

            // 2. Ngắt người chơi (nếu đang đào trạm này)
            if (Player.PlayerController.Instance != null) {
                float saved = Player.PlayerController.Instance.CancelMiningIfTarget(this);
                if (saved > 0f) {
                    SaveTimerToWorkerSlot(Player.PlayerController.Instance, saved);
                }
            }

            // 3. Ngắt toàn bộ công nhân đang đào trạm này
            for (int w = 0; w < Staff.WorkerAgent.AllWorkers.Count; w++) {
                var worker = Staff.WorkerAgent.AllWorkers[w];
                if (worker != null) {
                    float saved = worker.CancelMiningIfTarget(this);
                    if (saved > 0f) {
                        SaveTimerToWorkerSlot(worker, saved);
                    }
                }
            }

            // 4. Giải phóng cờ chiếm giữ thợ nhưng GIỮ NGUYÊN tiến độ dở dang và cập nhật thanh tiến trình
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null) {
                    slot.OccupyingWorker = null;
                    UpdateSlotProgressVisual(slot);
                }
            }
            UpdateProgressVisual();
        }

        private void SaveTimerToWorkerSlot(object worker, float timer) {
            if (timer <= 0f) return; // Tuyệt đối không ghi đè giá trị 0 lên slot đang có tiến độ!
            MiningSlot slot = GetSlotForWorker(worker);
            if (slot != null) {
                slot.SavedCraftTimer = timer;
                slot.CurrentCraftTimer = timer;
                UpdateSlotProgressVisual(slot);
            }
            if (timer > _currentCraftTimer) {
                _currentCraftTimer = timer;
            }
        }

        /// <summary>
        /// Lấy lại tiến độ đào dở dang đã được bảo lưu của slot này (nếu có).
        /// </summary>
        public float GetSavedCraftTimer(object worker) {
            MiningSlot slot = GetSlotForWorker(worker);
            if (slot != null) {
                if (slot.SavedCraftTimer > 0f) {
                    float t = slot.SavedCraftTimer;
                    slot.CurrentCraftTimer = t;
                    UpdateSlotProgressVisual(slot);
                    return t;
                }
                if (slot.CurrentCraftTimer > 0f) {
                    return slot.CurrentCraftTimer;
                }
            }

            if (_currentCraftTimer > 0f) {
                return _currentCraftTimer;
            }
            return 0f;
        }

        /// <summary>
        /// Lấy slot mà worker/player đang đứng làm việc.
        /// </summary>
        public MiningSlot GetSlotForWorker(object worker) {
            if (worker != null) {
                for (int i = 0; i < _slots.Count; i++) {
                    if (_slots[i].OccupyingWorker == worker) {
                        return _slots[i];
                    }
                }
            }
            return _slots.Count > 0 ? _slots[0] : null;
        }

        /// <summary>
        /// Lấy tiến độ đào (0 -> 1) của slot tương ứng với worker/player này.
        /// </summary>
        public float GetMiningProgress(object worker) {
            MiningSlot slot = GetSlotForWorker(worker);
            float baseTime = GetBaseCraftTime();
            if (slot != null && baseTime > 0f) {
                return Mathf.Clamp01(slot.CurrentCraftTimer / baseTime);
            }
            return MiningProgress;
        }

        /// <summary>
        /// Cộng dồn tiến trình khai thác của mỏ theo deltaTime và cập nhật hiển thị thanh khối Cube 3D.
        /// Trả về true nếu khối block đã hoàn tất khai thác (đạt 100% thời gian đào).
        /// </summary>
        /// <summary>
        /// Cộng dồn tiến trình khai thác của slot tương ứng theo deltaTime và cập nhật hiển thị thanh khối Cube 3D.
        /// Trả về true nếu khối block đã hoàn tất khai thác (đạt 100% thời gian đào).
        /// </summary>
        public bool AddMiningProgress(object worker, float deltaTime) {
            MiningSlot targetSlot = GetSlotForWorker(worker);
            if (targetSlot == null && _slots.Count > 0) {
                targetSlot = _slots[0];
            }
            if (targetSlot == null) return false;

            float baseTime = GetBaseCraftTime();
            targetSlot.CurrentCraftTimer += deltaTime;
            _currentCraftTimer = targetSlot.CurrentCraftTimer;

            UpdateSlotProgressVisual(targetSlot);
            ApplyHitWobble(targetSlot);

            if (targetSlot.CurrentCraftTimer >= baseTime) {
                targetSlot.CurrentCraftTimer = 0f;
                targetSlot.SavedCraftTimer = 0f;
                _currentCraftTimer = 0f;
                UpdateSlotProgressVisual(targetSlot);
                TriggerBlockBreakAndRespawn(targetSlot);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fallback cho các script gọi cũ không truyền worker.
        /// </summary>
        public bool AddMiningProgress(float deltaTime) {
            return AddMiningProgress(null, deltaTime);
        }

        /// <summary>
        /// Xóa sạch tiến độ đào dở dang và cập nhật lại thanh Cube.
        /// </summary>
        public void ResetMiningProgress(object worker = null) {
            if (worker != null) {
                MiningSlot slot = GetSlotForWorker(worker);
                if (slot != null) {
                    slot.CurrentCraftTimer = 0f;
                    slot.SavedCraftTimer = 0f;
                    UpdateSlotProgressVisual(slot);
                }
            } else {
                for (int i = 0; i < _slots.Count; i++) {
                    _slots[i].CurrentCraftTimer = 0f;
                    _slots[i].SavedCraftTimer = 0f;
                    UpdateSlotProgressVisual(_slots[i]);
                }
            }
            _currentCraftTimer = 0f;
        }

        // =========================================================================
        // BLOCK MINING FEEDBACK (RUNG LẮC KHI ĐÀO & RƠI NẢY KHI HOÀN THÀNH)
        // =========================================================================

        /// <summary>
        /// Tìm GameObject con hiển thị model 3D khối block của Slot 0.
        /// Ngăn chặn việc gán nhầm VisualRoot vào chính GameObject gốc (gây kéo theo toàn bộ stall con nhảy lên).
        /// </summary>
        public GameObject FindSlot0VisualChild() {
            if (_visualRoot != null && _visualRoot != gameObject) {
                return _visualRoot;
            }

            HashSet<GameObject> customVisuals = new HashSet<GameObject>();
            if (_customSlotAnchors != null) {
                foreach (var csa in _customSlotAnchors) {
                    if (csa != null && csa.VisualRoot != null) {
                        customVisuals.Add(csa.VisualRoot);
                    }
                }
            }

            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);
                GameObject childGo = child.gameObject;

                if (customVisuals.Contains(childGo)) continue;
                if (_minerAnchor != null && (child == _minerAnchor || child.IsChildOf(_minerAnchor))) continue;
                if (child.name.StartsWith("MiningProgressVisual")) continue;

                // Nếu có Renderer hoặc MeshFilter
                if (childGo.GetComponentInChildren<Renderer>(true) != null || childGo.GetComponentInChildren<MeshFilter>(true) != null) {
                    return childGo;
                }
            }

            return null;
        }

        private void ApplyHitWobble(MiningSlot slot) {
            if (slot == null || slot.VisualRoot == null) return;

            // Nếu slot 0 vô tình trỏ vào chính root GameObject, đổi sang child model để không làm rung toàn bộ quầy mỏ
            if (slot.VisualRoot == gameObject) {
                GameObject child = FindSlot0VisualChild();
                if (child != null) {
                    slot.VisualRoot = child;
                    slot.HasSavedVisualTransform = false;
                }
            }

            if (!slot.HasSavedVisualTransform) {
                slot.InitialVisualScale = slot.VisualRoot.transform.localScale;
                slot.InitialVisualLocalPos = slot.VisualRoot.transform.localPosition;
                slot.HasSavedVisualTransform = true;
            }

            slot.HitWobbleCooldown = 0.25f;

            // Nhún chậm rãi, nhẹ nhàng, êm dịu (không rung giật)
            float wobble = Mathf.Sin(Time.time * _miningWobbleSpeed) * _miningWobbleIntensity;

            slot.VisualRoot.transform.localScale = new Vector3(
                slot.InitialVisualScale.x * (1f + wobble),
                slot.InitialVisualScale.y * (1f - wobble * 1.2f),
                slot.InitialVisualScale.z * (1f + wobble)
            );
        }

        private void TriggerBlockBreakAndRespawn(MiningSlot slot) {
            if (slot == null || slot.VisualRoot == null) return;

            // Đảm bảo không bao giờ bounce root GameObject (tránh kéo tất cả các stall clone nhảy theo)
            if (slot.VisualRoot == gameObject) {
                GameObject child = FindSlot0VisualChild();
                if (child != null) {
                    slot.VisualRoot = child;
                    slot.HasSavedVisualTransform = false;
                } else {
                    return; // Không bounce root GameObject
                }
            }

            if (!slot.HasSavedVisualTransform) {
                slot.InitialVisualScale = slot.VisualRoot.transform.localScale;
                slot.InitialVisualLocalPos = slot.VisualRoot.transform.localPosition;
                slot.HasSavedVisualTransform = true;
            }

            // Phục hồi scale chuẩn trước khi chạy animation rơi nảy
            slot.VisualRoot.transform.localScale = slot.InitialVisualScale;

            // Hiệu ứng rơi khối block mới từ trên trời xuống chậm rãi, từ từ, êm ái
            Vector3 targetPos = slot.InitialVisualLocalPos;
            DropBounceEffect.PlayLocal(slot.VisualRoot.transform, targetPos, _blockRespawnDropHeight, _blockRespawnDuration);
        }

        // =========================================================================
        // 3D PROGRESS VISUAL SYSTEM (HỖ TRỢ CẢ MODEL TÙY CHỈNH LẪN CUBE MẶC ĐỊNH)
        // =========================================================================

        private void EnsureProgressVisualSetup() {
            for (int i = 0; i < _slots.Count; i++) {
                EnsureSlotProgressVisual(_slots[i]);
            }
        }

        private void UpdateSlotProgressBarPosition(MiningSlot slot) {
            if (slot == null || slot.ProgressVisualRoot == null) return;

            // Định vị thanh tiến trình nằm chính xác TRÊN KHỐI BLOCK (VisualRoot), không phải trên vị trí thợ đứng
            Vector3 blockLocalPos;
            if (slot.VisualRoot != null && slot.VisualRoot != gameObject) {
                blockLocalPos = transform.InverseTransformPoint(slot.VisualRoot.transform.position);
            } else {
                GameObject slot0Child = FindSlot0VisualChild();
                if (slot0Child != null) {
                    blockLocalPos = transform.InverseTransformPoint(slot0Child.transform.position);
                } else if (slot.MinerAnchor != null) {
                    blockLocalPos = transform.InverseTransformPoint(slot.MinerAnchor.position);
                } else {
                    blockLocalPos = _slotSpacing * slot.SlotIndex;
                }
            }

            slot.ProgressVisualRoot.transform.localPosition = new Vector3(blockLocalPos.x, _progressVisualHeight, blockLocalPos.z);
        }

        private void EnsureSlotProgressVisual(MiningSlot slot) {
            if (slot == null || slot.ProgressVisualRoot != null) return;

            // 1. Tạo Root đặt tại độ cao mong muốn ngay phía trên vị trí đứng đào (MinerAnchor)
            slot.ProgressVisualRoot = new GameObject($"MiningProgressVisual_Slot_{slot.SlotIndex}");
            slot.ProgressVisualRoot.transform.SetParent(transform, false);

            UpdateSlotProgressBarPosition(slot);
            slot.ProgressVisualRoot.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);

            bool hasCustomPrefab = _customProgressBarPrefab != null || _customProgressFillPrefab != null || _customProgressContainerPrefab != null;

            if (hasCustomPrefab) {
                _isUsingCustomVisual = true;

                if (_customProgressBarPrefab != null) {
                    slot.CustomProgressBarInstance = Instantiate(_customProgressBarPrefab, slot.ProgressVisualRoot.transform, false);
                    slot.CustomProgressBarInstance.name = $"CustomProgressBar_Slot_{slot.SlotIndex}";
                    slot.CustomProgressBarInstance.transform.localPosition = Vector3.zero;

                    Collider[] colliders = slot.CustomProgressBarInstance.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < colliders.Length; i++) {
                        Destroy(colliders[i]);
                    }

                    slot.CustomFillTransform = FindFillChildTransform(slot.CustomProgressBarInstance.transform);
                    if (slot.CustomFillTransform != null) {
                        slot.CustomFillInitialScale = slot.CustomFillTransform.localScale;
                        slot.CustomFillInitialLocalPos = slot.CustomFillTransform.localPosition;
                    }
                } else {
                    if (_customProgressContainerPrefab != null) {
                        var containerInstance = Instantiate(_customProgressContainerPrefab, slot.ProgressVisualRoot.transform, false);
                        containerInstance.name = $"CustomContainer_Slot_{slot.SlotIndex}";
                        containerInstance.transform.localPosition = Vector3.zero;
                        Collider[] cols = containerInstance.GetComponentsInChildren<Collider>(true);
                        for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
                    }

                    if (_customProgressFillPrefab != null) {
                        var fillInstance = Instantiate(_customProgressFillPrefab, slot.ProgressVisualRoot.transform, false);
                        fillInstance.name = $"CustomFill_Slot_{slot.SlotIndex}";
                        fillInstance.transform.localPosition = Vector3.zero;
                        Collider[] cols = fillInstance.GetComponentsInChildren<Collider>(true);
                        for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);

                        slot.CustomFillTransform = fillInstance.transform;
                        slot.CustomFillInitialScale = slot.CustomFillTransform.localScale;
                        slot.CustomFillInitialLocalPos = slot.CustomFillTransform.localPosition;
                    }
                }

                slot.ProgressVisualRoot.SetActive(false);

                if (slot.SlotIndex == 0) {
                    _progressVisualRoot = slot.ProgressVisualRoot;
                    _customProgressBarInstance = slot.CustomProgressBarInstance;
                    _customFillTransform = slot.CustomFillTransform;
                }
                return;
            }

            _isUsingCustomVisual = false;

            Shader unlitShader = Shader.Find("Sprites/Default") 
                              ?? Shader.Find("Universal Render Pipeline/Unlit") 
                              ?? Shader.Find("Standard");

            // 3. Khung Container ngoài
            slot.ProgressContainerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.ProgressContainerCube.name = $"ContainerFrame_Slot_{slot.SlotIndex}";
            slot.ProgressContainerCube.transform.SetParent(slot.ProgressVisualRoot.transform, false);
            slot.ProgressContainerCube.transform.localPosition = Vector3.zero;
            slot.ProgressContainerCube.transform.localScale = Vector3.one * _progressCubeSize;

            var colContainer = slot.ProgressContainerCube.GetComponent<Collider>();
            if (colContainer != null) Destroy(colContainer);

            var mrContainer = slot.ProgressContainerCube.GetComponent<MeshRenderer>();
            if (mrContainer != null) {
                mrContainer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrContainer.receiveShadows = false;
                float effContainerAlpha = Mathf.Clamp01(_containerOpacity * _progressOpacity);
                slot.ContainerMaterial = CreateTransparentMaterial(unlitShader, new Color(0.88f, 0.96f, 1.0f, effContainerAlpha), 3001);
                mrContainer.material = slot.ContainerMaterial;
            }

            // 4. Khối Mực Điền Đầy (Fill Cube)
            slot.ProgressFillCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.ProgressFillCube.name = $"ProgressFill_Slot_{slot.SlotIndex}";
            slot.ProgressFillCube.transform.SetParent(slot.ProgressVisualRoot.transform, false);

            var colFill = slot.ProgressFillCube.GetComponent<Collider>();
            if (colFill != null) Destroy(colFill);

            var mrFill = slot.ProgressFillCube.GetComponent<MeshRenderer>();
            if (mrFill != null) {
                mrFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mrFill.receiveShadows = false;
                Color fillCol = GetBlockColor();
                fillCol.a = Mathf.Clamp01(_fillOpacity * _progressOpacity);
                slot.FillMaterial = CreateTransparentMaterial(unlitShader, fillCol, 3000);
                mrFill.material = slot.FillMaterial;
            }

            slot.ProgressVisualRoot.SetActive(false);

            if (slot.SlotIndex == 0) {
                _progressVisualRoot = slot.ProgressVisualRoot;
                _progressContainerCube = slot.ProgressContainerCube;
                _progressFillCube = slot.ProgressFillCube;
                _containerMaterial = slot.ContainerMaterial;
                _fillMaterial = slot.FillMaterial;
            }
        }

        private Transform FindFillChildTransform(Transform root) {
            if (root == null) return null;
            string[] keywords = new string[] { "fill", "bar", "progress", "slider", "amount", "inner" };
            Transform found = SearchTransformRecursive(root, keywords);
            if (found != null && found != root) return found;

            for (int i = 0; i < root.childCount; i++) {
                Transform child = root.GetChild(i);
                if (child.GetComponent<Renderer>() != null) return child;
            }

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

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
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
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot == null) continue;

                if (slot.ContainerMaterial != null) {
                    float effectiveContainerAlpha = Mathf.Clamp01(_containerOpacity * _progressOpacity);
                    Color containerCol = new Color(0.88f, 0.96f, 1.0f, effectiveContainerAlpha);
                    SetMaterialColor(slot.ContainerMaterial, containerCol);
                }

                if (slot.FillMaterial != null) {
                    float effectiveFillAlpha = Mathf.Clamp01(_fillOpacity * _progressOpacity);
                    Color fillCol = GetBlockColor();
                    fillCol.a = effectiveFillAlpha;
                    SetMaterialColor(slot.FillMaterial, fillCol);
                }
            }

            if (_containerMaterial != null) {
                float effectiveContainerAlpha = Mathf.Clamp01(_containerOpacity * _progressOpacity);
                SetMaterialColor(_containerMaterial, new Color(0.88f, 0.96f, 1.0f, effectiveContainerAlpha));
            }

            if (_fillMaterial != null) {
                float effectiveFillAlpha = Mathf.Clamp01(_fillOpacity * _progressOpacity);
                Color fillCol = GetBlockColor();
                fillCol.a = effectiveFillAlpha;
                SetMaterialColor(_fillMaterial, fillCol);
            }
        }

        public void UpdateProgressVisual() {
            for (int i = 0; i < _slots.Count; i++) {
                UpdateSlotProgressVisual(_slots[i]);
            }
        }

        public void UpdateSlotProgressVisual(MiningSlot slot) {
            if (slot == null) return;
            if (!_showProgressVisual) {
                if (slot.ProgressVisualRoot != null && slot.ProgressVisualRoot.activeSelf) {
                    slot.ProgressVisualRoot.SetActive(false);
                }
                return;
            }

            EnsureSlotProgressVisual(slot);
            if (slot.ProgressVisualRoot == null) return;
            UpdateSlotProgressBarPosition(slot);

            float baseTime = GetBaseCraftTime();
            float progress = baseTime > 0 ? Mathf.Clamp01(slot.CurrentCraftTimer / baseTime) : 0f;
            bool isMiningInProgress = progress > 0.001f;
            bool isAnyoneMining = slot.IsOccupied;

            if (!isMiningInProgress && !isAnyoneMining) {
                if (slot.ProgressVisualRoot.activeSelf) slot.ProgressVisualRoot.SetActive(false);
                return;
            }

            if (!slot.ProgressVisualRoot.activeSelf) {
                slot.ProgressVisualRoot.SetActive(true);
            }

            // === A. TRƯỜNG HỢP MODEL TÙY CHỌN ===
            bool hasCustomPrefab = _customProgressBarPrefab != null || _customProgressFillPrefab != null || _customProgressContainerPrefab != null;
            if (hasCustomPrefab) {
                if (slot.CustomFillTransform != null) {
                    if (progress > 0.005f) {
                        if (!slot.CustomFillTransform.gameObject.activeSelf) slot.CustomFillTransform.gameObject.SetActive(true);

                        float p = Mathf.Clamp01(progress);
                        switch (_fillDirection) {
                            case ProgressBarFillDirection.VerticalY: {
                                float bottomY = slot.CustomFillInitialLocalPos.y - slot.CustomFillInitialScale.y * 0.5f;
                                float curHeight = slot.CustomFillInitialScale.y * p;
                                slot.CustomFillTransform.localPosition = new Vector3(
                                    slot.CustomFillInitialLocalPos.x,
                                    bottomY + curHeight * 0.5f,
                                    slot.CustomFillInitialLocalPos.z
                                );
                                slot.CustomFillTransform.localScale = new Vector3(
                                    slot.CustomFillInitialScale.x,
                                    Mathf.Max(0.0001f, curHeight),
                                    slot.CustomFillInitialScale.z
                                );
                                break;
                            }
                            case ProgressBarFillDirection.HorizontalX: {
                                float leftX = slot.CustomFillInitialLocalPos.x - slot.CustomFillInitialScale.x * 0.5f;
                                float curWidth = slot.CustomFillInitialScale.x * p;
                                slot.CustomFillTransform.localPosition = new Vector3(
                                    leftX + curWidth * 0.5f,
                                    slot.CustomFillInitialLocalPos.y,
                                    slot.CustomFillInitialLocalPos.z
                                );
                                slot.CustomFillTransform.localScale = new Vector3(
                                    Mathf.Max(0.0001f, curWidth),
                                    slot.CustomFillInitialScale.y,
                                    slot.CustomFillInitialScale.z
                                );
                                break;
                            }
                            case ProgressBarFillDirection.DepthZ: {
                                float backZ = slot.CustomFillInitialLocalPos.z - slot.CustomFillInitialScale.z * 0.5f;
                                float curDepth = slot.CustomFillInitialScale.z * p;
                                slot.CustomFillTransform.localPosition = new Vector3(
                                    slot.CustomFillInitialLocalPos.x,
                                    slot.CustomFillInitialLocalPos.y,
                                    backZ + curDepth * 0.5f
                                );
                                slot.CustomFillTransform.localScale = new Vector3(
                                    slot.CustomFillInitialScale.x,
                                    slot.CustomFillInitialScale.y,
                                    Mathf.Max(0.0001f, curDepth)
                                );
                                break;
                            }
                            case ProgressBarFillDirection.ScaleUniform: {
                                slot.CustomFillTransform.localScale = slot.CustomFillInitialScale * p;
                                break;
                            }
                        }
                    } else {
                        if (slot.CustomFillTransform.gameObject.activeSelf) slot.CustomFillTransform.gameObject.SetActive(false);
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

            if (slot.ProgressFillCube != null) {
                if (progress > 0.005f) {
                    if (!slot.ProgressFillCube.activeSelf) slot.ProgressFillCube.SetActive(true);
                    slot.ProgressFillCube.transform.localPosition = new Vector3(0f, bottomYCube + currentHeight * 0.5f, 0f);
                    slot.ProgressFillCube.transform.localScale = new Vector3(innerSize * 0.98f, Mathf.Max(0.001f, currentHeight), innerSize * 0.98f);
                } else {
                    if (slot.ProgressFillCube.activeSelf) slot.ProgressFillCube.SetActive(false);
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
                if (id.Contains("dirt") || id.Contains("dat")) return new Color(0.45f, 0.92f, 0.35f);
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

        private Camera _mainCamera;

        private void Update() {
            // Hồi phục êm dịu scale của các block về kích thước gốc sau khi nhún
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot == null || slot.VisualRoot == null || !slot.HasSavedVisualTransform) continue;

                if (slot.HitWobbleCooldown > 0f) {
                    slot.HitWobbleCooldown -= Time.deltaTime;
                } else {
                    if (slot.VisualRoot.transform.localScale != slot.InitialVisualScale) {
                        slot.VisualRoot.transform.localScale = Vector3.Lerp(
                            slot.VisualRoot.transform.localScale,
                            slot.InitialVisualScale,
                            Time.deltaTime * 5f // Hồi phục chậm rãi, mượt mà
                        );
                    }
                }
            }
        }

        private void LateUpdate() {
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null && slot.ProgressVisualRoot != null && slot.ProgressVisualRoot.activeSelf) {
                    if (_billboardToCamera) {
                        if (_mainCamera == null) _mainCamera = Camera.main;
                        if (_mainCamera != null) {
                            slot.ProgressVisualRoot.transform.rotation = _mainCamera.transform.rotation;
                        }
                    } else {
                        slot.ProgressVisualRoot.transform.rotation = Quaternion.Euler(20f, 45f, 0f);
                    }
                }
            }
        }

        private void OnDestroy() {
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null) {
                    if (slot.ContainerMaterial != null) Destroy(slot.ContainerMaterial);
                    if (slot.FillMaterial != null) Destroy(slot.FillMaterial);
                }
            }
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
            if (_isInitializingSlots) return;
            _isInitializingSlots = true;
            try {
                if (_minerAnchor == null) {
                    Transform foundAnchor = transform.Find("MinerAnchor");
                    if (foundAnchor != null) _minerAnchor = foundAnchor;
                }

            if (_visualRoot == null) {
                _visualRoot = gameObject;
            }

            IReadOnlyList<int> unlockLevels = GetConfiguredUnlockLevels();

            // Đảm bảo Slot 0 luôn tồn tại (Slot gốc mở ở Level 1)
            GameObject slot0Visual = _visualRoot;
            if (slot0Visual == null || slot0Visual == gameObject) {
                GameObject childVisual = FindSlot0VisualChild();
                if (childVisual != null) slot0Visual = childVisual;
                else slot0Visual = gameObject;
            }

            if (_slots.Count == 0) {
                _slots.Add(new MiningSlot {
                    SlotIndex = 0,
                    UnlockLevel = 1,
                    MinerAnchor = _minerAnchor,
                    VisualRoot = slot0Visual
                });
            } else {
                if (_slots[0].MinerAnchor == null) _slots[0].MinerAnchor = _minerAnchor;
                if (_slots[0].VisualRoot == null || _slots[0].VisualRoot == gameObject) _slots[0].VisualRoot = slot0Visual;
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
                            // Kiểm tra an toàn: Nếu _visualRoot trỏ vào chính nó, trỏ vào GameObject cha/tổ tiên,
                            // hoặc chứa bất kỳ component MiningStation nào -> TUYỆT ĐỐI KHÔNG Instantiate trực tiếp
                            // để ngăn ngừa vòng lặp đệ quy vô tận (StackOverflow/Freeze Unity)!
                            bool isSelfOrAncestorOrHasStation = _visualRoot == gameObject 
                                || transform.IsChildOf(_visualRoot.transform)
                                || _visualRoot.GetComponentInChildren<MiningStation>(true) != null;

                            if (isSelfOrAncestorOrHasStation) {
                                // Tự động tạo GameObject con chứa Mesh thay vì Instantiate cả GameObject cha
                                visualClone = new GameObject($"VisualRoot_{slotIndex + 1}");
                                visualClone.transform.SetParent(transform, false);
                                visualClone.transform.localPosition = _slotSpacing * slotIndex;
                                visualClone.transform.localRotation = Quaternion.identity;
                                visualClone.transform.localScale = Vector3.one;

                                var mf = _visualRoot.GetComponentInChildren<MeshFilter>(true) ?? GetComponent<MeshFilter>();
                                if (mf != null) {
                                    var cloneMf = visualClone.AddComponent<MeshFilter>();
                                    cloneMf.sharedMesh = mf.sharedMesh;
                                }
                                var mr = _visualRoot.GetComponentInChildren<MeshRenderer>(true) ?? GetComponent<MeshRenderer>();
                                if (mr != null) {
                                    var cloneMr = visualClone.AddComponent<MeshRenderer>();
                                    cloneMr.sharedMaterials = mr.sharedMaterials;
                                }
                            } else {
                                visualClone = Instantiate(_visualRoot, transform);
                                visualClone.name = $"VisualRoot_{slotIndex + 1}";
                                visualClone.transform.localPosition = _visualRoot.transform.localPosition + _slotSpacing * slotIndex;
                                visualClone.transform.localRotation = _visualRoot.transform.localRotation;

                                // Dọn dẹp an toàn: nếu vô tình clone có chứa script MiningStation thì xóa ngay
                                var stationOnClone = visualClone.GetComponentInChildren<MiningStation>(true);
                                if (stationOnClone != null) {
                                    DestroyImmediate(stationOnClone);
                                }
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

            // Ghi nhớ scale và position ban đầu của toàn bộ VisualRoot để phục hồi sau rung lắc / rơi nảy
            for (int i = 0; i < _slots.Count; i++) {
                MiningSlot slot = _slots[i];
                if (slot != null && slot.VisualRoot != null && !slot.HasSavedVisualTransform) {
                    slot.InitialVisualScale = slot.VisualRoot.transform.localScale;
                    slot.InitialVisualLocalPos = slot.VisualRoot.transform.localPosition;
                    slot.HasSavedVisualTransform = true;
                }
            }
            } finally {
                _isInitializingSlots = false;
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
            if (_currentLevel > 0) {
                RecalculateMilestoneMultipliers();
                UpdateVisualState();
                OnAnyStationUnlocked?.Invoke(this);
                OnAnyStationLevelChanged?.Invoke(this);
                return true;
            }

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

        /// <summary>
        /// Lấy cấp độ của mốc Milestone đã đạt gần nhất (hoặc 0 nếu chưa đạt mốc nào).
        /// </summary>
        public int GetPreviousMilestoneLevel() {
            var milestones = ActiveMilestones;
            if (milestones == null || milestones.Count == 0) return 0;

            int prevLevel = 0;
            for (int i = 0; i < milestones.Count; i++) {
                var rule = milestones[i];
                if (rule != null && rule.TargetLevel <= _currentLevel) {
                    if (rule.TargetLevel > prevLevel) {
                        prevLevel = rule.TargetLevel;
                    }
                }
            }
            return prevLevel;
        }

        /// <summary>
        /// Lấy tiến độ từ mốc trước đến mốc kế tiếp (từ 0.0f đến 1.0f) để hiển thị thanh tiến trình ngang.
        /// </summary>
        public float GetMilestoneProgress() {
            if (IsMaxLevel) return 1.0f;
            if (!TryGetNextMilestone(out var nextRule, out _)) return 1.0f;

            int prevLevel = GetPreviousMilestoneLevel();
            int span = nextRule.TargetLevel - prevLevel;
            if (span <= 0) return 1.0f;

            int currentInSegment = _currentLevel - prevLevel;
            return Mathf.Clamp01((float)currentInSegment / span);
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
