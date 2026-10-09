using System;
using System.Collections.Generic;
using System.Linq;
using MineMart.Data;
using MineMart.Gameplay.Map;
using MineMart.Gameplay.Mining;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Customers {
    /// <summary>
    /// Giám đốc Khách hàng: Quản lý lượng khách, skin đa dạng và menu mặt hàng đã mở khóa.
    /// Áp dụng mô hình Object Pooling: Tiền khởi tạo (pre-instantiate) toàn bộ Prefab skin
    /// theo số lượng _maxInstancePerPrefab ngay khi nạp Scene, tái sử dụng Zero GC Alloc.
    /// </summary>
    public class CustomerManager : MonoBehaviour {
        public static CustomerManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Số lượng khách tối đa cùng lúc trong cửa hàng (có thể nâng cấp qua bản đồ / kỹ năng)")]
        [SerializeField] private int _maxCustomers = 3;

        [Tooltip("Khoảng thời gian (giây) giữa các lần sinh khách mới")]
        [SerializeField] private float _spawnInterval = 3f;

        [Tooltip("Số lượng instance tối đa cho mỗi loại Prefab khách được tạo sẵn khi khởi chạy Scene (Pool Size per Prefab)")]
        [SerializeField] private int _maxInstancePerPrefab = 3;

        [Header("Spawn Points")]
        [Tooltip("Danh sách các điểm sinh khách ở ngoài cửa")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Customer Skins")]
        [Tooltip("Danh sách các Prefab skin của khách hàng để lựa chọn ngẫu nhiên")]
        [SerializeField] private List<GameObject> _customerPrefabs = new List<GameObject>();

        // Tương thích ngược nếu Unity Scene cũ còn lưu trường đơn _customerPrefab
        [HideInInspector]
        [SerializeField] private GameObject _customerPrefab;

        // Quản lý Object Pool theo từng Prefab skin
        private readonly Dictionary<GameObject, List<CustomerAgent>> _prefabPoolMap = new Dictionary<GameObject, List<CustomerAgent>>();
        private readonly List<GameObject> _activePrefabKeys = new List<GameObject>();
        private Transform _poolContainer;

        private List<CustomerAgent> _activeCustomers = new List<CustomerAgent>();
        private List<BlockDataSO> _unlockedBlocks = new List<BlockDataSO>();
        private float _spawnTimer = 0f;

        public IReadOnlyList<BlockDataSO> UnlockedBlocks => _unlockedBlocks;
        public int MaxCustomers {
            get => _maxCustomers;
            set => _maxCustomers = Mathf.Max(1, value);
        }

        public void AddMaxCustomers(int count) {
            _maxCustomers = Mathf.Max(1, _maxCustomers + count);
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
                return;
            }

            // Tương thích ngược: Nếu _customerPrefabs rỗng nhưng scene cũ có _customerPrefab
            if ((_customerPrefabs == null || _customerPrefabs.Count == 0) && _customerPrefab != null) {
                if (_customerPrefabs == null) _customerPrefabs = new List<GameObject>();
                _customerPrefabs.Add(_customerPrefab);
            }

            InitializeCustomerPool();
        }

        private void OnValidate() {
            if ((_customerPrefabs == null || _customerPrefabs.Count == 0) && _customerPrefab != null) {
                if (_customerPrefabs == null) _customerPrefabs = new List<GameObject>();
                _customerPrefabs.Add(_customerPrefab);
            }
        }

        /// <summary>
        /// Khởi tạo trước toàn bộ các instance cho từng loại skin theo _maxInstancePerPrefab.
        /// </summary>
        private void InitializeCustomerPool() {
            if (_customerPrefabs == null || _customerPrefabs.Count == 0) return;

            if (_poolContainer == null) {
                GameObject containerObj = new GameObject("[CustomerPool]");
                containerObj.transform.SetParent(transform);
                _poolContainer = containerObj.transform;
            }

            _prefabPoolMap.Clear();
            _activePrefabKeys.Clear();

            int safeMaxPerPrefab = Mathf.Max(1, _maxInstancePerPrefab);

            for (int p = 0; p < _customerPrefabs.Count; p++) {
                GameObject prefab = _customerPrefabs[p];
                if (prefab == null) continue;

                if (!_prefabPoolMap.ContainsKey(prefab)) {
                    _prefabPoolMap[prefab] = new List<CustomerAgent>();
                    _activePrefabKeys.Add(prefab);
                }

                for (int i = 0; i < safeMaxPerPrefab; i++) {
                    GameObject instance = Instantiate(prefab, _poolContainer);
                    instance.name = $"{prefab.name}_Pool_{i + 1}";

                    CustomerAgent agent = SetupCustomerInstance(instance);
                    instance.SetActive(false);
                    _prefabPoolMap[prefab].Add(agent);
                }
            }

            int totalPooled = _prefabPoolMap.Values.Sum(list => list.Count);
            Debug.Log($"<color=#10B981>[CustomerManager]</color> Khởi tạo Customer Pool thành công: {totalPooled} khách ({_activePrefabKeys.Count} loại skin x {safeMaxPerPrefab} instance/skin).");
        }

        private void Start() {
            RefreshUnlockedBlocks();

            // 1. Lắng nghe sự kiện mở khóa hoặc lên cấp toàn cục từ bất kỳ MiningStation nào
            MiningStation.OnAnyStationUnlocked += HandleAnyStationUnlocked;
            MiningStation.OnAnyStationLevelChanged += HandleAnyStationLevelChanged;

            // 2. Lắng nghe trực tiếp từ MapManager khi có quầy mỏ mới được mua và cắm vào plot
            if (MapManager.Instance != null) {
                MapManager.Instance.OnStallPurchasedAndSpawned += HandleStallSpawned;
            }

            // 3. Đăng ký các trạm đã có sẵn từ trước trong Scene
            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null) {
                    station.OnLevelChanged += OnStationLevelChanged;
                }
            }
        }

        private void OnDestroy() {
            MiningStation.OnAnyStationUnlocked -= HandleAnyStationUnlocked;
            MiningStation.OnAnyStationLevelChanged -= HandleAnyStationLevelChanged;

            if (MapManager.Instance != null) {
                MapManager.Instance.OnStallPurchasedAndSpawned -= HandleStallSpawned;
            }

            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null) {
                    station.OnLevelChanged -= OnStationLevelChanged;
                }
            }
        }

        private void HandleStallSpawned(WorkstationPlot plot, MiningStation station) {
            Debug.Log($"<color=#10B981>[CustomerManager]</color> Nhận diện quầy mỏ mới: {station?.name}. Cập nhật Menu bán hàng...");
            RefreshUnlockedBlocks();

            if (station != null) {
                station.OnLevelChanged -= OnStationLevelChanged;
                station.OnLevelChanged += OnStationLevelChanged;
            }

            // Mua mỏ đầu tiên: Kích hoạt sinh khách ngay lập tức không cần chờ đếm timer!
            if (_unlockedBlocks.Count > 0 && _activeCustomers.Count == 0) {
                _spawnTimer = _spawnInterval;
            }
        }

        private void HandleAnyStationUnlocked(MiningStation station) {
            RefreshUnlockedBlocks();
            if (_unlockedBlocks.Count > 0 && _activeCustomers.Count == 0) {
                _spawnTimer = _spawnInterval;
            }
        }

        private void HandleAnyStationLevelChanged(MiningStation station) {
            RefreshUnlockedBlocks();
        }

        private void OnStationLevelChanged(int newLevel) {
            RefreshUnlockedBlocks();
        }

        /// <summary>
        /// Quét lại toàn bộ các mỏ đang hoạt động và cập nhật danh sách Menu.
        /// </summary>
        public void RefreshUnlockedBlocks() {
            int previousCount = _unlockedBlocks.Count;
            _unlockedBlocks.Clear();

            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null && station.IsUnlocked && station.ProducedBlock != null) {
                    if (!_unlockedBlocks.Contains(station.ProducedBlock)) {
                        _unlockedBlocks.Add(station.ProducedBlock);
                    }
                }
            }

            // Nếu vừa chuyển từ 0 sang có mặt hàng bán, kích hoạt khách ngay
            if (previousCount == 0 && _unlockedBlocks.Count > 0) {
                Debug.Log($"<color=#38BDF8>[CustomerManager]</color> Cửa hàng đã có món đầu tiên ({_unlockedBlocks[0].DisplayName})! Bắt đầu đón khách!");
                _spawnTimer = _spawnInterval;
            }
        }

        /// <summary>
        /// Xin 1 món ăn ngẫu nhiên từ danh sách những món đang bán trong tiệm.
        /// </summary>
        public BlockDataSO GetRandomUnlockedBlock() {
            if (_unlockedBlocks.Count == 0) return null;
            return _unlockedBlocks[UnityEngine.Random.Range(0, _unlockedBlocks.Count)];
        }

        private void Update() {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return;
            if (_activePrefabKeys.Count == 0) return;

            // Cơ chế tự phục hồi: Nếu menu đang rỗng, quét lại xem người chơi đã mua mỏ nào chưa
            if (_unlockedBlocks.Count == 0) {
                RefreshUnlockedBlocks();
                if (_unlockedBlocks.Count == 0) return; // Nếu vẫn chưa có mỏ nào thì tiếp tục chờ
            }

            if (_activeCustomers.Count < _maxCustomers) {
                _spawnTimer += Time.deltaTime;
                if (_spawnTimer >= _spawnInterval) {
                    _spawnTimer = 0f;
                    SpawnCustomer();
                }
            }
        }

        private void SpawnCustomer() {
            Transform spawnPoint = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
            if (spawnPoint == null) return;

            CustomerAgent agent = GetAvailableCustomer();
            if (agent == null) {
                // Tạm thời toàn bộ khách trong Pool đang bận (đang mua hoặc đang đi ra cửa)
                // Đặt lại timer gần kề để thử lại sau 0.5s ngay khi có khách ra tới cửa
                _spawnTimer = _spawnInterval - 0.5f;
                return;
            }

            _activeCustomers.Add(agent);
            agent.OnLeaveStore -= HandleCustomerLeave;
            agent.OnLeaveStore += HandleCustomerLeave;

            agent.Spawn(spawnPoint.position, spawnPoint.rotation);
            Debug.Log($"<color=#38BDF8>[CustomerManager]</color> Đã xuất phát 1 khách hàng từ Pool ({_activeCustomers.Count}/{_maxCustomers}) - Skin: {agent.gameObject.name}");
        }

        /// <summary>
        /// Chọn ngẫu nhiên một skin và lấy một instance đang rảnh từ Pool.
        /// </summary>
        private CustomerAgent GetAvailableCustomer() {
            if (_activePrefabKeys.Count == 0) return null;

            // 1. Tạo danh sách ngẫu nhiên thứ tự các skin
            List<GameObject> shuffledSkins = new List<GameObject>(_activePrefabKeys);
            for (int i = shuffledSkins.Count - 1; i > 0; i--) {
                int r = UnityEngine.Random.Range(0, i + 1);
                GameObject temp = shuffledSkins[i];
                shuffledSkins[i] = shuffledSkins[r];
                shuffledSkins[r] = temp;
            }

            // 2. Tìm skin đầu tiên có instance đang rảnh (inactive)
            for (int s = 0; s < shuffledSkins.Count; s++) {
                GameObject prefab = shuffledSkins[s];
                if (_prefabPoolMap.TryGetValue(prefab, out List<CustomerAgent> poolList)) {
                    for (int i = 0; i < poolList.Count; i++) {
                        CustomerAgent candidate = poolList[i];
                        if (candidate != null && !candidate.gameObject.activeInHierarchy) {
                            return candidate;
                        }
                    }
                }
            }

            // 3. Dự phòng trường hợp _maxCustomers được nâng cấp lớn hơn tổng pool có sẵn:
            // Tự động sinh thêm 1 instance vào pool của skin ngẫu nhiên để không bị nghẽn game
            if (_activeCustomers.Count < _maxCustomers) {
                GameObject randomPrefab = _activePrefabKeys[UnityEngine.Random.Range(0, _activePrefabKeys.Count)];
                GameObject extraObj = Instantiate(randomPrefab, _poolContainer);
                extraObj.name = $"{randomPrefab.name}_Pool_Extra_{_prefabPoolMap[randomPrefab].Count + 1}";

                CustomerAgent extraAgent = SetupCustomerInstance(extraObj);
                extraObj.SetActive(false);
                _prefabPoolMap[randomPrefab].Add(extraAgent);
                return extraAgent;
            }

            return null;
        }

        /// <summary>
        /// Khởi tạo và thiết lập chuẩn cho mỗi GameObject khách:
        /// - Dọn sạch triệt để bất kỳ component CustomerAgent hoặc NavMeshAgent nào bị add nhầm vào child của Prefab
        /// - Đảm bảo CustomerAgent và NavMeshAgent duy nhất nằm trên ROOT của GameObject
        /// - Đánh dấu agent thuộc Object Pool để ngăn chặn chạy tự động trong Start()
        /// </summary>
        private CustomerAgent SetupCustomerInstance(GameObject instance) {
            if (instance == null) return null;

            // Xóa bỏ triệt để các component AI / NavMesh bị gắn nhầm trên GameObject con của prefab
            CustomerAgent[] childAgents = instance.GetComponentsInChildren<CustomerAgent>(true);
            for (int c = 0; c < childAgents.Length; c++) {
                if (childAgents[c] != null && childAgents[c].gameObject != instance) {
                    DestroyImmediate(childAgents[c]);
                }
            }

            NavMeshAgent[] childNavs = instance.GetComponentsInChildren<NavMeshAgent>(true);
            for (int n = 0; n < childNavs.Length; n++) {
                if (childNavs[n] != null && childNavs[n].gameObject != instance) {
                    DestroyImmediate(childNavs[n]);
                }
            }

            CustomerAgent agent = instance.GetComponent<CustomerAgent>();
            if (agent == null) {
                agent = instance.AddComponent<CustomerAgent>();
            }

            agent.MarkAsPooled();
            return agent;
        }

        private void HandleCustomerLeave(CustomerAgent agent) {
            if (agent != null) {
                _activeCustomers.Remove(agent);
                agent.OnLeaveStore -= HandleCustomerLeave;
            }
        }
    }
}
