using System;
using System.Collections.Generic;
using System.IO;
using MineMart.Economy;
using MineMart.Gameplay.Map;
using MineMart.Gameplay.Mining;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MineMart.Gameplay.Persistence {
    /// <summary>
    /// Bộ não điều hành lưu trữ và khôi phục dữ liệu game dạng JSON Offline.
    /// Phân biệt độc lập rõ ràng:
    /// - Tiến trình theo từng Map (Gold, Cấp độ mỏ, Cấp độ nâng cấp Map).
    /// - Tiến trình toàn cục (Emerald, Cây nâng cấp EUGT, Cài đặt).
    /// Tự động lưu định kỳ mỗi 30 giây, khi pause hoặc quit game.
    /// Tự động tính toán thu nhập ngoại tuyến (AFK Welcome-Back) và hiển thị Popup Claim khi khởi động lại game.
    /// </summary>
    public class SaveManager : MonoBehaviour {
        private static SaveManager _instance;
        public static SaveManager Instance {
            get {
                if (_instance == null) {
                    _instance = FindAnyObjectByType<SaveManager>();
                    if (_instance == null) {
                        GameObject go = new GameObject("[SaveManager]");
                        _instance = go.AddComponent<SaveManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap() {
            var instance = Instance;
        }

        private const string SAVE_FILE_NAME = "savegame.json";
        private const float AUTO_SAVE_INTERVAL = 30f;
        private const float MAX_AFK_SECONDS = 7200f; // Tối đa 2 giờ vắng mặt

        [Header("Scene References")]
        [Tooltip("Danh sách các trạm mỏ trong scene cần lưu cấp độ. Để trống thì hệ thống sẽ tự động quét.")]
        [SerializeField] private List<MiningStation> _miningStations = new List<MiningStation>();

        [Header("Settings")]
        [SerializeField] private bool _autoSaveEnabled = true;

        private float _autoSaveTimer = 0f;
        private string _saveFilePath;

        private double _pendingAfkEarnings = 0;
        private float _pendingAfkSeconds = 0;

        public bool HasPendingAfkEarnings => _pendingAfkEarnings > 0;
        public double PendingAfkEarnings => _pendingAfkEarnings;
        public float PendingAfkSeconds => _pendingAfkSeconds;

        public void ClearPendingAfkEarnings() {
            _pendingAfkEarnings = 0;
            _pendingAfkSeconds = 0;
        }

        // Callbacks từ EUGT để lưu/nạp mà không tạo phụ thuộc chéo vào UI
        public static Func<List<EugtUpgradeSaveEntry>> OnCollectEugtSaveData;
        public static Action<List<EugtUpgradeSaveEntry>> OnApplyEugtSaveData;
        public static Action OnResetEugtSaveData;

        // Sự kiện thông báo khi tính xong tiền AFK: (số tiền cơ bản, số giây vắng mặt hợp lệ)
        public event Action<double, float> OnAfkEarningsGranted;

        private void Awake() {
            if (_instance == null) {
                _instance = this;
            } else if (_instance != this) {
                Destroy(gameObject);
                return;
            }

            _saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }

        private void Start() {
            ScanMiningStationsIfEmpty();
            LoadGame();
        }

        private void Update() {
            if (!_autoSaveEnabled) return;

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= AUTO_SAVE_INTERVAL) {
                _autoSaveTimer = 0f;
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus) {
            if (pauseStatus) {
                SaveGame();
            }
        }

        private void OnApplicationQuit() {
            SaveGame();
        }

        private void ScanMiningStationsIfEmpty() {
            if (_miningStations == null || _miningStations.Count == 0) {
                MiningStation[] foundStations = FindObjectsByType<MiningStation>();
                _miningStations = new List<MiningStation>(foundStations);
            }
        }

        private string GetCurrentMapId() {
            if (MapManager.Instance != null && MapManager.Instance.CurrentMapData != null) {
                return MapManager.Instance.CurrentMapData.MapId;
            }
            if (EconomyManager.Instance != null && !string.IsNullOrEmpty(EconomyManager.Instance.ActiveMapId)) {
                return EconomyManager.Instance.ActiveMapId;
            }
            return "map_01";
        }

        /// <summary>
        /// Đọc file save hiện tại từ đĩa (nếu có) để bảo toàn tiến trình của các Map khác khi ghi đĩa.
        /// </summary>
        private GameSaveData ReadExistingSaveDataOrNew() {
            if (File.Exists(_saveFilePath)) {
                try {
                    string json = File.ReadAllText(_saveFilePath);
                    GameSaveData existing = JsonUtility.FromJson<GameSaveData>(json);
                    if (existing != null) return existing;
                } catch (Exception ex) {
                    Debug.LogWarning($"[SaveManager] Không thể đọc save hiện có, tạo mới: {ex.Message}");
                }
            }
            return new GameSaveData();
        }

        /// <summary>
        /// Thu thập toàn bộ trạng thái hiện tại và ghi xuống file JSON.
        /// Đảm bảo phân biệt độc lập Gold và Map Upgrades cho từng Map cụ thể.
        /// </summary>
        public void SaveGame() {
            try {
                ScanMiningStationsIfEmpty();

                GameSaveData saveData = ReadExistingSaveDataOrNew();
                string currentMapId = GetCurrentMapId();
                saveData.currentActiveMapId = currentMapId;

                // 1. Lưu tiến trình riêng biệt cho Map hiện tại (Gold, Stations, Map Upgrades)
                MapProgressSaveEntry mapEntry = saveData.GetOrCreateMapProgress(currentMapId);

                if (EconomyManager.Instance != null) {
                    mapEntry.gold = EconomyManager.Instance.CurrentGold;
                }

                mapEntry.stations.Clear();
                for (int i = 0; i < _miningStations.Count; i++) {
                    MiningStation station = _miningStations[i];
                    if (station == null) continue;

                    string stationId = (station.StationData != null && !string.IsNullOrEmpty(station.StationData.StallId))
                        ? station.StationData.StallId
                        : station.gameObject.name;

                    string plotId = (station.CurrentPlot != null) ? station.CurrentPlot.PlotId : null;
                    mapEntry.stations.Add(new StationSaveEntry(stationId, station.CurrentLevel, plotId));
                }

                mapEntry.mapUpgrades.Clear();
                if (MapManager.Instance != null) {
                    var mapLevels = MapManager.Instance.GetAllUpgradeLevels();
                    foreach (var kvp in mapLevels) {
                        mapEntry.mapUpgrades.Add(new MapUpgradeSaveEntry(kvp.Key, kvp.Value));
                    }
                    mapEntry.totalWorkers = MapManager.Instance.GetTotalSpawnedWorkers();
                    mapEntry.workers = MapManager.Instance.GetWorkersCountSaveData();
                }

                // 2. Lưu tiến trình toàn cục (Global: Emerald, EUGT)
                if (HardCurrencyManager.Instance != null) {
                    saveData.emerald = HardCurrencyManager.Instance.CurrentEmerald;
                }

                if (OnCollectEugtSaveData != null) {
                    saveData.eugtUpgrades = OnCollectEugtSaveData.Invoke();
                }

                // 3. Ghi mốc thời gian lưu
                saveData.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // 4. Tuần tự hóa JSON và ghi đĩa
                string json = JsonUtility.ToJson(saveData, prettyPrint: true);
                File.WriteAllText(_saveFilePath, json);
            } catch (Exception ex) {
                Debug.LogError($"[SaveManager] Lỗi khi lưu game: {ex.Message}");
            }
        }

        /// <summary>
        /// Đọc file JSON và nạp lại trạng thái cho toàn bộ hệ thống.
        /// Nạp Gold và Map Upgrades đúng theo Map hiện tại.
        /// </summary>
        public void LoadGame() {
            if (!File.Exists(_saveFilePath)) {
                // Người chơi mới chơi lần đầu
                return;
            }

            try {
                string json = File.ReadAllText(_saveFilePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                if (saveData == null) return;

                string currentMapId = GetCurrentMapId();

                // 1. Nạp tiến trình toàn cục (Emerald & EUGT)
                if (HardCurrencyManager.Instance != null) {
                    HardCurrencyManager.Instance.SetEmerald(saveData.emerald);
                }

                if (OnApplyEugtSaveData != null && saveData.eugtUpgrades != null) {
                    OnApplyEugtSaveData.Invoke(saveData.eugtUpgrades);
                }

                // 2. Tìm bản ghi tiến trình riêng của Map hiện tại
                MapProgressSaveEntry mapEntry = null;
                if (saveData.mapsProgress != null) {
                    for (int i = 0; i < saveData.mapsProgress.Count; i++) {
                        if (saveData.mapsProgress[i] != null && saveData.mapsProgress[i].mapId == currentMapId) {
                            mapEntry = saveData.mapsProgress[i];
                            break;
                        }
                    }
                }

                // Tương thích ngược: Nếu save file cũ chưa phân chia map
                if (mapEntry == null && (saveData.gold > 0 || (saveData.stations != null && saveData.stations.Count > 0))) {
                    mapEntry = new MapProgressSaveEntry(currentMapId) {
                        gold = saveData.gold,
                        stations = saveData.stations
                    };
                }

                if (mapEntry != null) {
                    // Nạp số dư tiền vàng của Map này
                    if (EconomyManager.Instance != null) {
                        EconomyManager.Instance.SetGold(mapEntry.gold);
                    }

                    // Nạp các nâng cấp toàn cục của Map này
                    if (MapManager.Instance != null && mapEntry.mapUpgrades != null) {
                        Dictionary<string, int> upgradeMap = new Dictionary<string, int>();
                        for (int i = 0; i < mapEntry.mapUpgrades.Count; i++) {
                            var u = mapEntry.mapUpgrades[i];
                            if (u != null && !string.IsNullOrEmpty(u.upgradeId)) {
                                upgradeMap[u.upgradeId] = u.level;
                            }
                        }
                        MapManager.Instance.LoadUpgradeLevels(upgradeMap);
                    }

                    // Nạp cấp độ và vị trí ô quy hoạch của từng mỏ trên Map này
                    Dictionary<string, StationSaveEntry> stationEntryMap = new Dictionary<string, StationSaveEntry>();
                    if (mapEntry.stations != null) {
                        for (int i = 0; i < mapEntry.stations.Count; i++) {
                            StationSaveEntry entry = mapEntry.stations[i];
                            if (entry != null && !string.IsNullOrEmpty(entry.stationId)) {
                                stationEntryMap[entry.stationId] = entry;
                            }
                        }
                    }

                    for (int i = 0; i < _miningStations.Count; i++) {
                        MiningStation station = _miningStations[i];
                        if (station == null) continue;

                        string stationId = (station.StationData != null && !string.IsNullOrEmpty(station.StationData.StallId))
                            ? station.StationData.StallId
                            : station.gameObject.name;

                        if (stationEntryMap.TryGetValue(stationId, out StationSaveEntry entry)) {
                            station.SetLevel(entry.level);

                            if (!string.IsNullOrEmpty(entry.plotId)) {
                                WorkstationPlot plot = WorkstationPlot.FindPlotById(entry.plotId);
                                if (plot != null) {
                                    plot.SnapStation(station, false);
                                }
                            }
                        }
                    }
                }

                // 3. Tính toán tiền tích lũy ngoại tuyến (AFK Welcome Back)
                ProcessAfkEarnings(saveData.lastSaveTimestamp);
            } catch (Exception ex) {
                Debug.LogError($"[SaveManager] Lỗi khi tải game: {ex.Message}");
            }
        }

        /// <summary>
        /// Tính toán tiền tích lũy khi off game quay lại theo công thức:
        /// afk cash = (trung bình cộng giá của các stall đã mở bán) x (thời gian afk off game / trung bình cộng thời gian đào khoáng sản)
        /// Và kích hoạt hiển thị Popup AFK Claim chào mừng người chơi quay trở lại.
        /// </summary>
        private void ProcessAfkEarnings(long lastSaveTimestamp) {
            if (lastSaveTimestamp <= 0) return;

            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double secondsOffline = currentTimestamp - lastSaveTimestamp;

            if (secondsOffline <= 5) return; // Vắng mặt dưới 5 giây thì bỏ qua

            // Giới hạn thời gian tích lũy tối đa (mặc định 2 giờ = 7200 giây)
            float validOfflineSeconds = Mathf.Min((float)secondsOffline, MAX_AFK_SECONDS);

            // 1. Thu thập tất cả các stall (mỏ) đang mở bán trên Map hiện tại
            double totalPrice = 0;
            float totalCraftTime = 0;
            int unlockedCount = 0;

            for (int i = 0; i < _miningStations.Count; i++) {
                MiningStation station = _miningStations[i];
                if (station != null && station.IsUnlocked) {
                    totalPrice += station.GetFinalSellPrice();
                    totalCraftTime += station.GetBaseCraftTime();
                    unlockedCount++;
                }
            }

            if (unlockedCount == 0 || totalCraftTime <= 0) return;

            // 2. Tính trung bình cộng giá bán và trung bình cộng thời gian đào
            double avgPrice = totalPrice / unlockedCount;
            float avgCraftTime = totalCraftTime / unlockedCount;

            // 3. Áp dụng công thức: afk cash = avgPrice * (validOfflineSeconds / avgCraftTime)
            double afkEarnings = Math.Round(avgPrice * (validOfflineSeconds / avgCraftTime), 0);

            if (afkEarnings <= 0) return;

            // 4. Lưu lại tiền AFK chờ nhận và phát sự kiện thông báo cho UI Popup
            _pendingAfkEarnings = afkEarnings;
            _pendingAfkSeconds = validOfflineSeconds;
            OnAfkEarningsGranted?.Invoke(afkEarnings, validOfflineSeconds);
        }

        /// <summary>
        /// Xóa sạch file savegame và reset toàn bộ tiến trình game, sau đó tải lại Scene.
        /// Được gọi từ nút Đặt Lại Tiến Trình trong menu Cài Đặt (SettingsDialogUI).
        /// </summary>
        public void ResetAllProgressAndRestart() {
            try {
                // 1. Xóa file save JSON
                if (File.Exists(_saveFilePath)) {
                    File.Delete(_saveFilePath);
                }

                // 2. Xóa PlayerPrefs phòng ngừa
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();

                // 3. Reset các Manager trong RAM
                if (EconomyManager.Instance != null) {
                    EconomyManager.Instance.SetGold(0);
                }

                if (HardCurrencyManager.Instance != null) {
                    HardCurrencyManager.Instance.SetEmerald(0);
                }

                if (MapManager.Instance != null) {
                    MapManager.Instance.ResetAllUpgrades();
                }

                OnResetEugtSaveData?.Invoke();

                Debug.Log("[SaveManager] Đã xóa toàn bộ dữ liệu lưu trữ và reset tiến trình thành công!");

                // 4. Reload Scene hiện tại để tái khởi tạo thế giới sạch
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            } catch (Exception ex) {
                Debug.LogError($"[SaveManager] Lỗi khi reset tiến trình: {ex.Message}");
            }
        }

        /// <summary>
        /// Xóa file savegame đơn thuần.
        /// </summary>
        public void DeleteSave() {
            try {
                if (File.Exists(_saveFilePath)) {
                    File.Delete(_saveFilePath);
                }
            } catch (Exception ex) {
                Debug.LogError($"[SaveManager] Lỗi khi xóa file save: {ex.Message}");
            }
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }
    }
}
