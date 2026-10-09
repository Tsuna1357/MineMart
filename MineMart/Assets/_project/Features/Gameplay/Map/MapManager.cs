using System;
using System.Collections.Generic;
using UnityEngine;
using MineMart.Data;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Player;
using MineMart.Gameplay.Staff;
using MineMart.Feel;

namespace MineMart.Gameplay.Map {
    /// <summary>
    /// Cấu hình liên kết giữa cấp độ nâng cấp khách hàng và vật thể trên Scene (như Bàn ngoài trời).
    /// </summary>
    [Serializable]
    public class CustomerVisualBinding {
        [Tooltip("GameObject trên Scene (vd: Bàn ngoài trời, ghế ngồi, quầy phụ...).")]
        public GameObject targetObject;

        [Tooltip("Nếu tích chọn (true): Sẽ tự động SetActive(true) khi đạt tới cấp độ nâng cấp tương ứng. Nếu false hoặc targetObject để null: Bỏ qua.")]
        public bool setActive = true;
    }

    /// <summary>
    /// Quản lý tổng thể Bản đồ và Hệ thống Nâng cấp Toàn cục (Map Manager & Global Upgrades).
    /// Chịu trách nhiệm:
    /// 1. Cung cấp dữ liệu kinh tế (Giá mở mỏ, giá block, thời gian đào gốc, giá nâng cấp) từ MapDataSO cho từng MiningStation.
    /// 2. Điều phối 5 loại nâng cấp Map: Thêm thợ, Thêm khách, Nhân giá block, Tăng % tốc độ chạy AI/Player, Giảm thời gian đào.
    /// </summary>
    public class MapManager : MonoBehaviour {
        public static MapManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Hồ sơ dữ liệu bản đồ hiện tại")]
        [SerializeField] private MapDataSO _currentMapData;

        [Header("Worker Management by Role (AddWorkers Upgrade)")]
        [Tooltip("Danh sách Thợ Đào (Miner) trên Scene (đang ẩn). Kích hoạt khi mua nâng cấp AddWorkers loại Miner.")]
        [SerializeField] private List<GameObject> _minerWorkers = new List<GameObject>();

        [Tooltip("Danh sách Thợ Vận Chuyển (Runner) trên Scene (đang ẩn). Kích hoạt khi mua nâng cấp AddWorkers loại Runner.")]
        [SerializeField] private List<GameObject> _runnerWorkers = new List<GameObject>();

        [Tooltip("Danh sách Thợ Bán Trực Tiếp (DirectSeller) trên Scene (đang ẩn). Kích hoạt khi mua nâng cấp AddWorkers loại DirectSeller.")]
        [SerializeField] private List<GameObject> _directSellerWorkers = new List<GameObject>();

        // Giữ lại trường cũ để tự động chuyển đổi an toàn cho Scene cũ trong OnValidate
        [SerializeField, HideInInspector] private List<GameObject> _workerPool = new List<GameObject>();

        [Header("Customer Outdoor Tables / Props (AddCustomers Upgrade)")]
        [Tooltip("Danh sách GameObject bàn ngoài trời / vật thể Scene tương ứng theo từng cấp nâng cấp khách hàng (Phần tử 0 = Cấp 1, Phần tử 1 = Cấp 2...). Nếu để trống (null) hoặc setActive = false thì bỏ qua.")]
        [SerializeField] private List<CustomerVisualBinding> _customerOutdoorTables = new List<CustomerVisualBinding>();

        // Lưu trữ cấp độ hiện tại của từng loại nâng cấp Map (UpgradeId -> Level)
        private readonly Dictionary<string, int> _upgradeLevels = new Dictionary<string, int>();

        // Sự kiện thông báo khi một nâng cấp Map được mua thành công
        public event Action<MapUpgradeConfig, int> OnUpgradePurchased;

        public MapDataSO CurrentMapData {
            get => _currentMapData;
            set {
                _currentMapData = value;
                ApplyInitialMapSettings();
            }
        }

        public IReadOnlyList<GameObject> MinerWorkers => _minerWorkers;
        public IReadOnlyList<GameObject> RunnerWorkers => _runnerWorkers;
        public IReadOnlyList<GameObject> DirectSellerWorkers => _directSellerWorkers;

        /// <summary>
        /// Danh sách tổng hợp toàn bộ thợ trên Scene (dành cho tương thích ngược).
        /// </summary>
        public IReadOnlyList<GameObject> WorkerPool {
            get {
                var combined = new List<GameObject>(_minerWorkers.Count + _runnerWorkers.Count + _directSellerWorkers.Count);
                combined.AddRange(_minerWorkers);
                combined.AddRange(_runnerWorkers);
                combined.AddRange(_directSellerWorkers);
                return combined;
            }
        }

        public IReadOnlyList<CustomerVisualBinding> CustomerOutdoorTables => _customerOutdoorTables;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
                return;
            }

            InitializeWorkerPool();
            UpdateCustomerVisualsActivation();
        }

        private void Start() {
            ApplyInitialMapSettings();
            EnsureMapCameraController();
        }

        private void EnsureMapCameraController() {
            if (MapCameraController.Instance == null) {
                Camera cam = Camera.main;
                if (cam != null && cam.GetComponent<MapCameraController>() == null) {
                    cam.gameObject.AddComponent<MapCameraController>();
                }
            }
        }

        private void InitializeWorkerPool() {
            UpdateWorkerPoolActivation();
        }

        // Sự kiện thông báo khi một trạm mỏ được mua và spawn thành công lên ô đất
        public event Action<WorkstationPlot, MiningStation> OnStallPurchasedAndSpawned;

        /// <summary>
        /// Đồng bộ cấu hình kinh tế từ MapDataSO vào tất cả các trạm mỏ đang có trên Scene.
        /// </summary>
        public void ApplyInitialMapSettings() {
            if (_currentMapData == null) return;

            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                MiningStation station = MiningStation.AllStations[i];
                if (station != null && station.StationData != null) {
                    StallConfigOnMap config = _currentMapData.FindStallConfig(station.StationData);
                    if (config != null) {
                        station.ApplyMapEconomyConfig(config);
                    }
                }
            }
        }

        #region Dynamic Stall Purchase & Spawning on Plots

        /// <summary>
        /// Lấy danh sách các mỏ được cấu hình cho Map này nhưng CHƯA ĐƯỢC MUA / chưa xuất hiện trên bất kỳ ô đất nào.
        /// Dùng để hiển thị lên Bong bóng Menu khi người chơi bấm vào một ô đất trống.
        /// </summary>
        public List<StallConfigOnMap> GetUnpurchasedStalls() {
            var result = new List<StallConfigOnMap>();
            if (_currentMapData == null || _currentMapData.Stalls == null) return result;

            // Thu thập các loại mỏ đã có mặt trên các ô đất hoặc trên Scene
            var activeStallTypes = new HashSet<StallDataSO>();
            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var st = MiningStation.AllStations[i];
                if (st != null && st.StationData != null) {
                    activeStallTypes.Add(st.StationData);
                }
            }

            for (int i = 0; i < _currentMapData.Stalls.Count; i++) {
                var cfg = _currentMapData.Stalls[i];
                if (cfg != null && cfg.StallData != null && !activeStallTypes.Contains(cfg.StallData)) {
                    result.Add(cfg);
                }
            }
            return result;
        }

        /// <summary>
        /// Sinh ra (Spawn) Prefab của trạm mỏ và cắm chuẩn xác vào ô đất quy hoạch (WorkstationPlot).
        /// </summary>
        public MiningStation SpawnStallAtPlot(StallConfigOnMap stallConfig, WorkstationPlot plot) {
            if (stallConfig == null || plot == null) return null;

            if (stallConfig.StallPrefab == null) {
                Debug.LogWarning($"[MapManager] Không thể spawn mỏ {stallConfig.StallData?.StallName} vì chưa gán StallPrefab trong MapDataSO!");
                return null;
            }

            GameObject instance = Instantiate(stallConfig.StallPrefab, plot.StationTargetPosition, plot.StationTargetRotation);
            instance.name = stallConfig.StallData != null ? stallConfig.StallData.StallName : "Stall";

            MiningStation station = instance.GetComponent<MiningStation>();
            if (station == null) {
                station = instance.GetComponentInChildren<MiningStation>();
            }

            if (station != null) {
                station.ApplyMapEconomyConfig(stallConfig);
                plot.SnapStation(station, false);
                station.TryUnlock(); // Đặt thành Level 1 để sẵn sàng cho thợ đào

                // Hiệu ứng rơi từ trên cao xuống vị trí ô plot (Drop Bounce)
                DropBounceEffect.Play(station.transform, plot.StationTargetPosition, 4.0f, 0.48f);

                OnStallPurchasedAndSpawned?.Invoke(plot, station);
            }

            return station;
        }

        /// <summary>
        /// Mua và sinh trạm mỏ lên ô đất trống kèm kiểm tra trừ tiền vàng.
        /// </summary>
        public bool TryBuyAndSpawnStall(StallConfigOnMap stallConfig, WorkstationPlot plot, ref double playerGold) {
            if (stallConfig == null || plot == null || !plot.IsEmpty) return false;

            double cost = stallConfig.BuyPrice;
            if (playerGold < cost) return false;

            playerGold -= cost;
            MiningStation station = SpawnStallAtPlot(stallConfig, plot);
            return station != null;
        }

        #endregion

        #region Economic Queries (Stall & Block Values)

        /// <summary>
        /// Lấy cấu hình kinh tế của trạm mỏ trên Map này.
        /// </summary>
        public StallConfigOnMap GetStallConfig(StallDataSO stallData) {
            if (_currentMapData == null || stallData == null) return null;
            return _currentMapData.FindStallConfig(stallData);
        }

        /// <summary>
        /// Lấy cấp độ tối đa của trạm mỏ trên Map này.
        /// </summary>
        public int GetStallMaxLevel(StallDataSO stallData) {
            if (_currentMapData != null && stallData != null) {
                StallConfigOnMap cfg = _currentMapData.FindStallConfig(stallData);
                if (cfg != null) return cfg.MaxLevel;
            }
            return 100;
        }

        /// <summary>
        /// Lấy giá bán gốc của block trên Map này.
        /// </summary>
        public double GetBlockBaseValue(BlockDataSO block) {
            if (_currentMapData != null && block != null) {
                StallConfigOnMap cfg = _currentMapData.FindStallConfigByBlock(block);
                if (cfg != null) return cfg.BlockBaseValue;
            }
            return 3.0; // Fallback an toàn nếu chưa gán MapData
        }

        /// <summary>
        /// Lấy thời gian đào gốc (giây) của trạm mỏ trên Map này (chưa trừ nâng cấp giảm giờ).
        /// </summary>
        public float GetStallBaseCraftTime(StallDataSO stallData) {
            if (_currentMapData != null && stallData != null) {
                StallConfigOnMap cfg = _currentMapData.FindStallConfig(stallData);
                if (cfg != null) return cfg.BaseCraftTime;
            }
            return 1.0f;
        }

        #endregion

        #region Map Upgrades State & Queries

        /// <summary>
        /// Lấy cấp độ hiện tại của một nâng cấp Map theo UpgradeId.
        /// </summary>
        public int GetUpgradeLevel(string upgradeId) {
            if (string.IsNullOrEmpty(upgradeId)) return 0;
            return _upgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        /// <summary>
        /// Lấy cấp độ hiện tại của một cấu hình nâng cấp.
        /// </summary>
        public int GetUpgradeLevel(MapUpgradeConfig upgrade) {
            if (upgrade == null) return 0;
            return GetUpgradeLevel(upgrade.UpgradeId);
        }

        /// <summary>
        /// Đặt cấp độ cho một nâng cấp (dùng khi load savegame).
        /// </summary>
        public void SetUpgradeLevel(string upgradeId, int level) {
            if (string.IsNullOrEmpty(upgradeId)) return;
            _upgradeLevels[upgradeId] = Mathf.Max(0, level);
            ApplyUpgradeEffectsImmediately();
        }

        /// <summary>
        /// Lấy chi phí vàng để nâng cấp lên cấp tiếp theo.
        /// </summary>
        public double GetUpgradeCost(MapUpgradeConfig upgrade) {
            if (upgrade == null) return 0;
            int currentLevel = GetUpgradeLevel(upgrade);
            return upgrade.GetCostForNextLevel(currentLevel);
        }

        /// <summary>
        /// Kiểm tra xem người chơi có đủ tiền mua nâng cấp này không.
        /// </summary>
        public bool CanAffordUpgrade(MapUpgradeConfig upgrade, double currentGold) {
            if (upgrade == null) return false;
            int currentLevel = GetUpgradeLevel(upgrade);
            if (currentLevel >= upgrade.MaxLevel) return false;
            return currentGold >= upgrade.GetCostForNextLevel(currentLevel);
        }

        /// <summary>
        /// Thực hiện mua nâng cấp Map và kích hoạt hiệu ứng tương ứng.
        /// </summary>
        public bool TryBuyUpgrade(MapUpgradeConfig upgrade, ref double playerGold) {
            if (upgrade == null) return false;
            int currentLevel = GetUpgradeLevel(upgrade);
            if (currentLevel >= upgrade.MaxLevel) return false;

            double cost = upgrade.GetCostForNextLevel(currentLevel);
            if (playerGold < cost) return false;

            playerGold -= cost;
            int newLevel = currentLevel + 1;
            _upgradeLevels[upgrade.UpgradeId] = newLevel;

            // Kích hoạt hiệu ứng của nâng cấp này
            ExecuteUpgradeEffect(upgrade, newLevel);
            OnUpgradePurchased?.Invoke(upgrade, newLevel);
            return true;
        }

        /// <summary>
        /// Thực hiện mua nâng cấp Map không trừ tiền trực tiếp (dùng khi trừ tiền qua CurrencyManager bên ngoài).
        /// </summary>
        public bool ApplyUpgradeLevelUp(MapUpgradeConfig upgrade) {
            if (upgrade == null) return false;
            int currentLevel = GetUpgradeLevel(upgrade);
            if (currentLevel >= upgrade.MaxLevel) return false;

            int newLevel = currentLevel + 1;
            _upgradeLevels[upgrade.UpgradeId] = newLevel;

            ExecuteUpgradeEffect(upgrade, newLevel);
            OnUpgradePurchased?.Invoke(upgrade, newLevel);
            return true;
        }

        #endregion

        #region Upgrade Effects Execution

        private void ExecuteUpgradeEffect(MapUpgradeConfig upgrade, int newLevel) {
            if (upgrade == null) return;

            switch (upgrade.UpgradeType) {
                case MapUpgradeType.AddWorkers:
                    // Kích hoạt thêm thợ từ worker pool
                    UpdateWorkerPoolActivation();
                    break;

                case MapUpgradeType.AddCustomers:
                    // Tăng giới hạn số lượng khách tối đa
                    if (CustomerManager.Instance != null) {
                        int addedCount = upgrade.CustomerBonusPerLevel;
                        CustomerManager.Instance.AddMaxCustomers(addedCount);
                    }
                    UpdateCustomerVisualsActivation();
                    break;

                case MapUpgradeType.IncreaseAISpeed:
                    // Cập nhật tốc độ di chuyển cho Player và Workers
                    RefreshAllMovementSpeeds();
                    break;

                case MapUpgradeType.MultiplyBlockPrice:
                case MapUpgradeType.ReduceMiningTime:
                    // Được áp dụng trực tiếp qua các hàm truy vấn thời gian thực GetBlockPriceMultiplier và GetMiningTimeReduction
                    break;
            }
        }

        private void ApplyUpgradeEffectsImmediately() {
            UpdateWorkerPoolActivation();
            UpdateCustomerVisualsActivation();
            RefreshAllMovementSpeeds();
        }

        /// <summary>
        /// Lấy số lượng thợ đã được mở khóa theo từng vai trò cụ thể từ các nâng cấp Map.
        /// </summary>
        public int GetUnlockedWorkersCount(WorkerRole role) {
            if (_currentMapData == null || _currentMapData.Upgrades == null) return 0;
            int count = 0;
            for (int i = 0; i < _currentMapData.Upgrades.Count; i++) {
                var up = _currentMapData.Upgrades[i];
                if (up != null && up.UpgradeType == MapUpgradeType.AddWorkers && up.WorkerRole == role) {
                    int lvl = GetUpgradeLevel(up);
                    count += lvl * up.WorkersAddedPerLevel;
                }
            }
            return count;
        }

        /// <summary>
        /// Tổng số lượng thợ đã mở khóa trên toàn bộ các vai trò.
        /// </summary>
        public int GetTotalUnlockedWorkersCount() {
            return GetUnlockedWorkersCount(WorkerRole.Miner)
                 + GetUnlockedWorkersCount(WorkerRole.Runner)
                 + GetUnlockedWorkersCount(WorkerRole.DirectSeller);
        }

        private void UpdateWorkerPoolActivation() {
            UpdateRolePoolActivation(_minerWorkers, WorkerRole.Miner);
            UpdateRolePoolActivation(_runnerWorkers, WorkerRole.Runner);
            UpdateRolePoolActivation(_directSellerWorkers, WorkerRole.DirectSeller);
        }

        private void UpdateRolePoolActivation(List<GameObject> pool, WorkerRole role) {
            if (pool == null) return;
            int unlockedCount = GetUnlockedWorkersCount(role);

            for (int i = 0; i < pool.Count; i++) {
                GameObject workerObj = pool[i];
                if (workerObj != null) {
                    bool shouldBeActive = i < unlockedCount;
                    bool wasActive = workerObj.activeSelf;

                    // Đảm bảo WorkerAgent trên GameObject có đúng Role tương ứng với danh sách
                    WorkerAgent agent = workerObj.GetComponent<WorkerAgent>();
                    if (agent != null && agent.Role != role) {
                        agent.SetRole(role);
                    }

                    workerObj.SetActive(shouldBeActive);

                    // Nếu thợ vừa được kích hoạt mới trong khi đang chơi -> hiệu ứng rơi từ trên trời xuống
                    if (shouldBeActive && !wasActive && Application.isPlaying) {
                        DropBounceEffect.Play(workerObj.transform, workerObj.transform.position, 3.5f, 0.45f);
                    }
                }
            }
        }

        private void OnValidate() {
            // Tự động phân loại nếu Scene cũ đã kéo sẵn GameObject vào _workerPool
            if (_workerPool != null && _workerPool.Count > 0 &&
                _minerWorkers.Count == 0 && _runnerWorkers.Count == 0 && _directSellerWorkers.Count == 0) {
                for (int i = 0; i < _workerPool.Count; i++) {
                    GameObject obj = _workerPool[i];
                    if (obj == null) continue;
                    WorkerAgent agent = obj.GetComponent<WorkerAgent>();
                    if (agent != null) {
                        switch (agent.Role) {
                            case WorkerRole.Miner: _minerWorkers.Add(obj); break;
                            case WorkerRole.Runner: _runnerWorkers.Add(obj); break;
                            case WorkerRole.DirectSeller: _directSellerWorkers.Add(obj); break;
                        }
                    } else {
                        _minerWorkers.Add(obj);
                    }
                }
            }
        }

        private int GetTotalCustomerUpgradeLevel() {
            if (_currentMapData == null) return 0;
            int totalLvl = 0;
            for (int i = 0; i < _currentMapData.Upgrades.Count; i++) {
                var up = _currentMapData.Upgrades[i];
                if (up != null && up.UpgradeType == MapUpgradeType.AddCustomers) {
                    totalLvl += GetUpgradeLevel(up);
                }
            }
            return totalLvl;
        }

        /// <summary>
        /// Kích hoạt SetActive(true) các bàn ngoài trời / vật thể Scene tương ứng theo cấp nâng cấp khách hàng.
        /// </summary>
        private void UpdateCustomerVisualsActivation() {
            int customerLevel = GetTotalCustomerUpgradeLevel();
            for (int i = 0; i < _customerOutdoorTables.Count; i++) {
                var binding = _customerOutdoorTables[i];
                if (binding != null && binding.targetObject != null) {
                    // Nếu setActive = true: Active khi cấp độ >= (i + 1). Nếu false: bỏ qua.
                    bool shouldBeActive = binding.setActive && (i < customerLevel);
                    bool wasActive = binding.targetObject.activeSelf;
                    binding.targetObject.SetActive(shouldBeActive);

                    // Nếu bàn ghế/vật thể vừa được kích hoạt mới trong khi đang chơi -> hiệu ứng rơi từ trên trời xuống
                    if (shouldBeActive && !wasActive && Application.isPlaying) {
                        DropBounceEffect.Play(binding.targetObject.transform, binding.targetObject.transform.position, 3.5f, 0.45f);
                    }
                }
            }
        }

        #endregion

        #region Multipliers & Modifiers Calculations

        /// <summary>
        /// Tính toán hệ số nhân giá bán block cho một trạm mỏ cụ thể (Upgrade 3: MultiplyBlockPrice).
        /// Hỗ trợ cả 2 trường hợp: Nâng cấp cho 1 mỏ cụ thể hoặc nâng cấp cho Tất cả mỏ trên Map.
        /// </summary>
        public double GetBlockPriceMultiplier(StallDataSO stallData) {
            if (_currentMapData == null || stallData == null) return 1.0;

            double multiplier = 1.0;
            for (int i = 0; i < _currentMapData.Upgrades.Count; i++) {
                MapUpgradeConfig up = _currentMapData.Upgrades[i];
                if (up == null || up.UpgradeType != MapUpgradeType.MultiplyBlockPrice) continue;

                int level = GetUpgradeLevel(up);
                if (level <= 0) continue;

                bool applies = false;
                if (up.TargetScope == MapUpgradeScope.AllStalls) {
                    applies = true;
                } else if (up.TargetScope == MapUpgradeScope.SingleStall && up.TargetStall == stallData) {
                    applies = true;
                }

                if (applies) {
                    multiplier *= Math.Pow(up.PriceMultiplierValue > 0 ? up.PriceMultiplierValue : 1.0, level);
                }
            }
            return multiplier;
        }

        /// <summary>
        /// Tính toán số giây được giảm trừ vào thời gian đào gốc của mỏ này (Upgrade 5: ReduceMiningTime).
        /// </summary>
        public float GetMiningTimeReduction(StallDataSO stallData) {
            if (_currentMapData == null || stallData == null) return 0f;

            float reduction = 0f;
            for (int i = 0; i < _currentMapData.Upgrades.Count; i++) {
                MapUpgradeConfig up = _currentMapData.Upgrades[i];
                if (up == null || up.UpgradeType != MapUpgradeType.ReduceMiningTime) continue;

                int level = GetUpgradeLevel(up);
                if (level <= 0) continue;

                if (up.TargetStall == stallData) {
                    reduction += up.MiningTimeReduction * level;
                }
            }
            return reduction;
        }

        /// <summary>
        /// Tổng % tốc độ chạy cộng thêm cho Player và Workers (Upgrade 4: IncreaseAISpeed).
        /// Trả về số thập phân (vd: 0.2 = +20% tốc độ).
        /// </summary>
        public float AISpeedBonusPercent {
            get {
                if (_currentMapData == null) return 0f;
                float bonus = 0f;
                for (int i = 0; i < _currentMapData.Upgrades.Count; i++) {
                    MapUpgradeConfig up = _currentMapData.Upgrades[i];
                    if (up != null && up.UpgradeType == MapUpgradeType.IncreaseAISpeed) {
                        int level = GetUpgradeLevel(up);
                        bonus += (up.SpeedBonusPercent / 100f) * level;
                    }
                }
                return bonus;
            }
        }

        /// <summary>
        /// Cập nhật lại tốc độ di chuyển thực tế cho Người chơi và toàn bộ Thợ mỏ.
        /// </summary>
        public void RefreshAllMovementSpeeds() {
            float bonus = AISpeedBonusPercent;

            if (PlayerController.Instance != null) {
                PlayerController.Instance.ApplySpeedBonus(bonus);
            }

            for (int i = 0; i < WorkerAgent.AllWorkers.Count; i++) {
                WorkerAgent worker = WorkerAgent.AllWorkers[i];
                if (worker != null) {
                    worker.ApplySpeedBonus(bonus);
                }
            }
        }

        #endregion
    }
}
