using System.Collections.Generic;
using System.Linq;
using MineMart.Data;
using MineMart.Gameplay.Mining;
using UnityEngine;

namespace MineMart.Gameplay.Customers {
    /// <summary>
    /// Giám đốc Khách hàng: Quản lý lượng khách và danh sách các mặt hàng đã mở khóa.
    /// Tự động cập nhật danh sách Menu khi có mỏ mới được mở khóa.
    /// Quyết định việc sinh ra khách mới nếu chưa vượt quá giới hạn (MaxCustomers).
    /// </summary>
    public class CustomerManager : MonoBehaviour {
        public static CustomerManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Số lượng khách tối đa trong cửa hàng (có thể nâng cấp qua cây kỹ năng)")]
        [SerializeField] private int _maxCustomers = 3;

        [Tooltip("Khoảng thời gian (giây) giữa các lần sinh khách mới")]
        [SerializeField] private float _spawnInterval = 3f;

        [Header("Spawn Points")]
        [Tooltip("Danh sách các điểm đẻ khách ở ngoài cửa")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("References")]
        [Tooltip("Prefab của khách hàng AI")]
        [SerializeField] private GameObject _customerPrefab;

        private List<CustomerAgent> _activeCustomers = new List<CustomerAgent>();
        private List<BlockDataSO> _unlockedBlocks = new List<BlockDataSO>();
        private float _spawnTimer = 0f;

        public IReadOnlyList<BlockDataSO> UnlockedBlocks => _unlockedBlocks;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        private void Start() {
            RefreshUnlockedBlocks();

            // Lắng nghe sự kiện lên cấp từ tất cả các trạm mỏ để cập nhật Menu
            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null) {
                    station.OnLevelChanged += OnStationLevelChanged;
                }
            }
        }

        private void OnDestroy() {
            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null) {
                    station.OnLevelChanged -= OnStationLevelChanged;
                }
            }
        }

        private void OnStationLevelChanged(int newLevel) {
            RefreshUnlockedBlocks();
        }

        /// <summary>
        /// Quét lại toàn bộ các mỏ đang hoạt động và cập nhật danh sách Menu.
        /// </summary>
        public void RefreshUnlockedBlocks() {
            _unlockedBlocks.Clear();
            for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                var station = MiningStation.AllStations[i];
                if (station != null && station.IsUnlocked && station.ProducedBlock != null) {
                    if (!_unlockedBlocks.Contains(station.ProducedBlock)) {
                        _unlockedBlocks.Add(station.ProducedBlock);
                    }
                }
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
            if (_spawnPoints == null || _spawnPoints.Length == 0 || _customerPrefab == null) return;

            // Nếu tiệm chưa bán gì thì không có khách tới
            if (_unlockedBlocks.Count == 0) return;

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

            GameObject obj = Instantiate(_customerPrefab, spawnPoint.position, spawnPoint.rotation);
            CustomerAgent agent = obj.GetComponent<CustomerAgent>();

            if (agent != null) {
                _activeCustomers.Add(agent);
                agent.OnLeaveStore += HandleCustomerLeave;
            }
        }

        private void HandleCustomerLeave(CustomerAgent agent) {
            if (agent != null) {
                _activeCustomers.Remove(agent);
                agent.OnLeaveStore -= HandleCustomerLeave;
            }
        }
    }
}
