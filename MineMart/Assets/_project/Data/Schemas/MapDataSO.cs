using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Data {
    /// <summary>
    /// Các loại nâng cấp toàn cục cấp độ Bản đồ (Map Upgrades / Shop Upgrades).
    /// </summary>
    public enum MapUpgradeType {
        AddWorkers = 0,          // Kích hoạt thêm nhân viên (Active thêm worker từ pool)
        AddCustomers = 1,        // Tăng giới hạn số lượng khách hàng tối đa trong cửa hàng
        MultiplyBlockPrice = 2,  // Nhân giá bán block lên X lần (cho 1 mỏ hoặc toàn bộ mỏ)
        IncreaseAISpeed = 3,     // Tăng % tốc độ di chuyển của Thợ và Người chơi
        ReduceMiningTime = 4     // Giảm thời gian đào của 1 mỏ đi X giây
    }

    /// <summary>
    /// Phạm vi tác động của nâng cấp bản đồ.
    /// </summary>
    public enum MapUpgradeScope {
        SingleStall = 0, // Chỉ áp dụng cho 1 mỏ cụ thể được chỉ định
        AllStalls = 1    // Áp dụng đồng loạt cho tất cả mỏ trên Map
    }

    /// <summary>
    /// Cấu hình kinh tế của một trạm mỏ trên một Bản đồ cụ thể.
    /// Giúp một loại mỏ (vd: Mỏ Gỗ) có thể là mỏ khởi đầu ở map này, nhưng là mỏ cấp cao ở map khác.
    /// </summary>
    [Serializable]
    public class StallConfigOnMap {
        // Hệ số toán học cố định chuẩn Idle Tycoon:
        public const float UPGRADE_COST_MULTIPLIER = 1.15f; // Hệ số vàng hàm mũ 1.15 (+15% giá nâng cấp mỗi cấp)
        public const float PRICE_GROWTH_RATE = 0.1f;        // Tăng trưởng bậc tuyến tính +10% giá gốc mỗi cấp

        [Tooltip("Loại mỏ (Mỏ Đất, Mỏ Gỗ, Mỏ Đá...)")]
        [SerializeField] private StallDataSO _stallData;

        [Tooltip("Prefab 3D của trạm mỏ (chứa mô hình, collider và MiningStation) để spawn ra ô đất khi người chơi mua")]
        [SerializeField] private GameObject _stallPrefab;

        [Tooltip("Giá vàng để mua/mở khóa mỏ này trên Map này (0 = mỏ mở miễn phí ban đầu)")]
        [SerializeField] private double _buyPrice = 0;

        [Tooltip("Giá bán gốc của 1 block do mỏ này làm ra trên Map này (V_0)")]
        [SerializeField] private double _blockBaseValue = 3;

        [Tooltip("Thời gian đào gốc (giây) của 1 block tại mỏ này trên Map này")]
        [SerializeField] private float _baseCraftTime = 1.0f;

        [Tooltip("Giá nâng cấp mỏ ở Cấp 1 (C_0)")]
        [SerializeField] private double _baseUpgradePrice = 6;

        [Tooltip("Cấp độ tối đa của mỏ này trên Map (mặc định: 100)")]
        [SerializeField] private int _maxLevel = 100;

        public StallDataSO StallData => _stallData;
        public GameObject StallPrefab => _stallPrefab;
        public double BuyPrice => _buyPrice;
        public double BlockBaseValue => _blockBaseValue;
        public float BaseCraftTime => _baseCraftTime > 0 ? _baseCraftTime : 1.0f;
        public double BaseUpgradePrice => _baseUpgradePrice > 0 ? _baseUpgradePrice : 6;
        public int MaxLevel => _maxLevel > 0 ? _maxLevel : 100;
        public float UpgradeCostMultiplier => UPGRADE_COST_MULTIPLIER;
        public float PriceIncreasePerLevel => PRICE_GROWTH_RATE;
    }

    /// <summary>
    /// Cấu hình một hạng mục nâng cấp toàn cục trên Bản đồ.
    /// Thiết kế trực quan, phân tách rõ ràng theo từng loại nâng cấp.
    /// </summary>
    [Serializable]
    public class MapUpgradeConfig {
        [Tooltip("Mã định danh duy nhất của nâng cấp (vd: upgrade_worker, upgrade_speed)")]
        [SerializeField] private string _upgradeId;

        [Tooltip("Tên hiển thị trên giao diện Shop nâng cấp")]
        [SerializeField] private string _displayName;

        [Tooltip("Icon đại diện cho nâng cấp")]
        [SerializeField] private Sprite _icon;

        [TextArea(2, 3)]
        [Tooltip("Mô tả công dụng của nâng cấp")]
        [SerializeField] private string _description;

        [Tooltip("Loại nâng cấp")]
        [SerializeField] private MapUpgradeType _upgradeType;

        [Tooltip("Phạm vi tác động: 1 mỏ cụ thể hay tất cả mỏ trên Map")]
        [SerializeField] private MapUpgradeScope _targetScope = MapUpgradeScope.AllStalls;

        [Tooltip("Mỏ mục tiêu (khi chọn SingleStall)")]
        [SerializeField] private StallDataSO _targetStall;

        [Tooltip("Loại nhân viên được kích hoạt (Dành cho AddWorkers: Miner, Runner, DirectSeller)")]
        [SerializeField] private WorkerRole _workerRole = WorkerRole.Miner;

        [Tooltip("Số lượng Thợ được kích hoạt thêm mỗi cấp (Dành cho AddWorkers, vd: 1 = active 1 thợ, 2 = active 2 thợ một lúc)")]
        [SerializeField] private int _workersAddedPerLevel = 1;

        [Tooltip("Prefab Thợ tùy biến riêng cho thẻ nâng cấp này. Để trống thì lấy Prefab từ MapManager.")]
        [SerializeField] private GameObject _workerPrefab;

        [Tooltip("Số lượng khách tăng thêm mỗi cấp (Dành cho AddCustomers, vd: 1, 2, 5 khách...)")]
        [SerializeField] private int _customerBonusPerLevel = 1;

        [Tooltip("Có kích hoạt thêm bàn ngoài trời (Outdoor Table) khi mua nâng cấp này không?")]
        [SerializeField] private bool _activateOutdoorTable = false;

        [Tooltip("Chỉ số bàn ngoài trời cụ thể cần bật (-1 = tự động kích hoạt bàn tiếp theo theo thứ tự)")]
        [SerializeField] private int _outdoorTableIndex = -1;

        [Tooltip("Số lượng bàn ngoài trời sẽ kích hoạt khi mua thẻ nâng cấp này (vd: 0 = không thêm bàn, 1 = thêm 1 bàn...).")]
        [SerializeField] private int _outdoorTablesToActivate = 0;

        [Tooltip("Hệ số nhân giá bán block (Dành cho MultiplyBlockPrice, vd: 2.0 = x2 giá)")]
        [SerializeField] private float _priceMultiplierValue = 2.0f;

        [Tooltip("% Tốc độ di chuyển tăng thêm mỗi cấp (Dành cho IncreaseAISpeed, vd: 20 = +20%)")]
        [SerializeField] private float _speedBonusPercent = 20f;

        [Tooltip("Thời gian đào giảm đi (giây) mỗi cấp (Dành cho ReduceMiningTime, vd: 0.2s)")]
        [SerializeField] private float _miningTimeReduction = 0.2f;

        [Tooltip("Giá vàng cố định để mua thẻ nâng cấp này")]
        [SerializeField] private double _price = 100;

        public string UpgradeId => _upgradeId;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public MapUpgradeType UpgradeType => _upgradeType;
        public MapUpgradeScope TargetScope => _targetScope;
        public StallDataSO TargetStall => _targetStall;
        public WorkerRole WorkerRole => _workerRole;
        public GameObject WorkerPrefab => _workerPrefab;

        public int WorkersAddedPerLevel => _workersAddedPerLevel > 0 ? _workersAddedPerLevel : 1;
        public int CustomerBonusPerLevel => _customerBonusPerLevel > 0 ? _customerBonusPerLevel : 1;
        public bool ActivateOutdoorTable => _activateOutdoorTable;
        public int OutdoorTableIndex => _outdoorTableIndex;
        public int OutdoorTablesToActivate => _outdoorTablesToActivate;
        public float PriceMultiplierValue => _priceMultiplierValue > 0 ? _priceMultiplierValue : 2.0f;
        public float SpeedBonusPercent => _speedBonusPercent > 0 ? _speedBonusPercent : 20f;
        public float MiningTimeReduction => _miningTimeReduction > 0 ? _miningTimeReduction : 0.2f;

        public double Price => _price >= 0 ? _price : 0;
        public int MaxLevel => 1; // Mỗi thẻ nâng cấp mua 1 lần duy nhất

        /// <summary>
        /// Lấy chi phí vàng để mua nâng cấp này (nếu chưa mua trả về Price, đã mua trả về 0).
        /// </summary>
        public double GetCostForNextLevel(int currentLevel) {
            return currentLevel <= 0 ? Price : 0;
        }
    }

    /// <summary>
    /// Schema cấu hình toàn bộ một Bản đồ (Map).
    /// Chứa danh sách các mỏ hiện diện trên Map kèm bài toán kinh tế, và các nâng cấp toàn cục của Map.
    /// </summary>
    [CreateAssetMenu(fileName = "Map_", menuName = "MineMart/Data/Map Data")]
    public class MapDataSO : ScriptableObject {
        [Header("Map Identity")]
        [SerializeField] private string _mapId = "map_01";
        [SerializeField] private string _mapName = "Đồng Bằng Khởi Nguyên";

        [Header("Map Audio (BGM)")]
        [Tooltip("Nhạc nền riêng của Bản đồ này. Sẽ tự động phát khi bước vào Map.")]
        [SerializeField] private AudioClip _mapBgmClip;

        [Header("Stalls on this Map")]
        [Tooltip("Danh sách các mỏ xuất hiện trên Map này kèm chỉ số kinh tế")]
        [SerializeField] private List<StallConfigOnMap> _stalls = new List<StallConfigOnMap>();

        [Header("Map Global Upgrades")]
        [Tooltip("Danh sách các loại nâng cấp toàn cục áp dụng cho Map này")]
        [SerializeField] private List<MapUpgradeConfig> _upgrades = new List<MapUpgradeConfig>();

        public string MapId => _mapId;
        public string MapName => _mapName;
        public AudioClip MapBgmClip => _mapBgmClip;
        public IReadOnlyList<StallConfigOnMap> Stalls => _stalls;
        public IReadOnlyList<MapUpgradeConfig> Upgrades => _upgrades;

        /// <summary>
        /// Tìm cấu hình kinh tế của một loại mỏ trên Map này.
        /// </summary>
        public StallConfigOnMap FindStallConfig(StallDataSO stallData) {
            if (stallData == null || _stalls == null) return null;
            for (int i = 0; i < _stalls.Count; i++) {
                if (_stalls[i] != null && _stalls[i].StallData == stallData) {
                    return _stalls[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Tìm cấu hình kinh tế mỏ theo loại block sản xuất.
        /// </summary>
        public StallConfigOnMap FindStallConfigByBlock(BlockDataSO block) {
            if (block == null || _stalls == null) return null;
            for (int i = 0; i < _stalls.Count; i++) {
                if (_stalls[i] != null && _stalls[i].StallData != null && _stalls[i].StallData.ProducedBlock == block) {
                    return _stalls[i];
                }
            }
            return null;
        }
    }
}
