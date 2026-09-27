using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using MineMart.Economy;
using MineMart.Gameplay.Mining;

namespace MineMart.Gameplay.Persistence
{
    /// <summary>
    /// Bộ não điều hành lưu trữ và khôi phục dữ liệu game dạng JSON Offline.
    /// Tự động lưu định kỳ mỗi 30 giây, khi ẩn hoặc thoát game.
    /// Tự động tính toán thu nhập ngoại tuyến (AFK Welcome-Back) khi khởi động lại game.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

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

        // Sự kiện thông báo khi nhận thưởng tiền AFK: (số tiền thưởng, số giây vắng mặt hợp lệ)
        public event Action<double, float> OnAfkEarningsGranted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }

        private void Start()
        {
            ScanMiningStationsIfEmpty();
            LoadGame();
        }

        private void Update()
        {
            if (!_autoSaveEnabled) return;

            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= AUTO_SAVE_INTERVAL)
            {
                _autoSaveTimer = 0f;
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }

        private void ScanMiningStationsIfEmpty()
        {
            if (_miningStations == null || _miningStations.Count == 0)
            {
                MiningStation[] foundStations = FindObjectsByType<MiningStation>();
                _miningStations = new List<MiningStation>(foundStations);
            }
        }

        /// <summary>
        /// Thu thập toàn bộ trạng thái hiện tại và ghi xuống file JSON.
        /// </summary>
        public void SaveGame()
        {
            try
            {
                ScanMiningStationsIfEmpty();

                GameSaveData saveData = new GameSaveData();

                // 1. Lưu số dư ví tiền
                if (EconomyManager.Instance != null)
                {
                    saveData.gold = EconomyManager.Instance.CurrentGold;
                }

                // 2. Lưu cấp độ từng trạm mỏ
                for (int i = 0; i < _miningStations.Count; i++)
                {
                    MiningStation station = _miningStations[i];
                    if (station == null) continue;

                    string stationId = (station.StationData != null && !string.IsNullOrEmpty(station.StationData.StallId))
                        ? station.StationData.StallId
                        : station.gameObject.name;

                    saveData.stations.Add(new StationSaveEntry(stationId, station.CurrentLevel));
                }

                // 3. Ghi mốc thời gian lưu
                saveData.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // 4. Tuần tự hóa JSON và ghi đĩa
                string json = JsonUtility.ToJson(saveData, prettyPrint: true);
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Lỗi khi lưu game: {ex.Message}");
            }
        }

        /// <summary>
        /// Đọc file JSON và nạp lại trạng thái cho toàn bộ hệ thống.
        /// </summary>
        public void LoadGame()
        {
            if (!File.Exists(_saveFilePath))
            {
                // Chưa có file save (Người chơi mới chơi lần đầu)
                return;
            }

            try
            {
                string json = File.ReadAllText(_saveFilePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                if (saveData == null) return;

                // 1. Nạp lại số dư ví tiền
                if (EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.SetGold(saveData.gold);
                }

                // 2. Nạp lại cấp độ từng trạm mỏ
                Dictionary<string, int> stationLevelMap = new Dictionary<string, int>();
                if (saveData.stations != null)
                {
                    for (int i = 0; i < saveData.stations.Count; i++)
                    {
                        StationSaveEntry entry = saveData.stations[i];
                        if (entry != null && !string.IsNullOrEmpty(entry.stationId))
                        {
                            stationLevelMap[entry.stationId] = entry.level;
                        }
                    }
                }

                for (int i = 0; i < _miningStations.Count; i++)
                {
                    MiningStation station = _miningStations[i];
                    if (station == null) continue;

                    string stationId = (station.StationData != null && !string.IsNullOrEmpty(station.StationData.StallId))
                        ? station.StationData.StallId
                        : station.gameObject.name;

                    if (stationLevelMap.TryGetValue(stationId, out int savedLevel))
                    {
                        station.SetLevel(savedLevel);
                    }
                }

                // 3. Tính toán tiền tích lũy ngoại tuyến (AFK Earnings)
                ProcessAfkEarnings(saveData.lastSaveTimestamp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Lỗi khi tải game: {ex.Message}");
            }
        }

        /// <summary>
        /// Tính toán và cộng thưởng tiền vắng mặt theo công thức trong GDD Mục 4.3.
        /// </summary>
        private void ProcessAfkEarnings(long lastSaveTimestamp)
        {
            if (lastSaveTimestamp <= 0) return;

            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double secondsOffline = currentTimestamp - lastSaveTimestamp;

            if (secondsOffline <= 5) return; // Vắng mặt quá ngắn (dưới 5 giây) thì bỏ qua

            // Giới hạn thời gian tích lũy tối đa 2 giờ (7200 giây)
            float validOfflineSeconds = Mathf.Min((float)secondsOffline, MAX_AFK_SECONDS);

            // Tính tổng công suất tạo tiền mỗi giây của tất cả các mỏ đang mở khóa: Revenue / CraftTime
            double totalPerSecondProduction = 0;
            for (int i = 0; i < _miningStations.Count; i++)
            {
                MiningStation station = _miningStations[i];
                if (station != null && station.IsUnlocked)
                {
                    float craftTime = station.GetBaseCraftTime();
                    if (craftTime > 0)
                    {
                        totalPerSecondProduction += (station.GetFinalSellPrice() / craftTime);
                    }
                }
            }

            if (totalPerSecondProduction <= 0) return;

            // Áp dụng hệ số 0.5 theo công thức cân bằng GDD Mục 4.3
            double afkEarnings = validOfflineSeconds * totalPerSecondProduction * 0.5;

            if (afkEarnings > 0 && EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddGold(afkEarnings);
                OnAfkEarningsGranted?.Invoke(afkEarnings, validOfflineSeconds);
            }
        }

        /// <summary>
        /// Xóa file savegame để khôi phục trạng thái ban đầu phục vụ kiểm thử.
        /// </summary>
        public void DeleteSave()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    File.Delete(_saveFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Lỗi khi xóa file save: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
