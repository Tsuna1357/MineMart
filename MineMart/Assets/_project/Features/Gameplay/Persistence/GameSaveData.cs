using System;
using System.Collections.Generic;

namespace MineMart.Gameplay.Persistence {
    /// <summary>
    /// Bản ghi lưu trữ cấp độ của một trạm mỏ khai thác nguyên liệu trên bản đồ.
    /// </summary>
    [Serializable]
    public class StationSaveEntry {
        public string stationId;
        public int level;
        public string plotId;

        public StationSaveEntry() { }

        public StationSaveEntry(string stationId, int level, string plotId = null) {
            this.stationId = stationId;
            this.level = level;
            this.plotId = plotId;
        }
    }

    /// <summary>
    /// Bản ghi lưu trữ cấp độ của một nâng cấp Map (Shop Upgrades).
    /// </summary>
    [Serializable]
    public class MapUpgradeSaveEntry {
        public string upgradeId;
        public int level;

        public MapUpgradeSaveEntry() { }

        public MapUpgradeSaveEntry(string upgradeId, int level) {
            this.upgradeId = upgradeId;
            this.level = level;
        }
    }

    /// <summary>
    /// Bản ghi lưu trữ trạng thái của một nấc nâng cấp ngọc lục bảo EUGT toàn cục (Global).
    /// </summary>
    [Serializable]
    public class EugtUpgradeSaveEntry {
        public string upgradeId;
        public bool isUnlocked;
        public int level;

        public EugtUpgradeSaveEntry() { }

        public EugtUpgradeSaveEntry(string upgradeId, bool isUnlocked, int level) {
            this.upgradeId = upgradeId;
            this.isUnlocked = isUnlocked;
            this.level = level;
        }
    }

    /// <summary>
    /// Bản ghi lưu trữ số lượng nhân viên theo từng loại trên Map.
    /// </summary>
    [Serializable]
    public class WorkerCountSaveEntry {
        public string role; // "Miner", "Runner", "DirectSeller"
        public int count;

        public WorkerCountSaveEntry() { }

        public WorkerCountSaveEntry(string role, int count) {
            this.role = role;
            this.count = count;
        }
    }

    /// <summary>
    /// Tiến trình của riêng một Bản đồ (Map Progress).
    /// Đảm bảo phân biệt độc lập 100% giữa các map:
    /// Tiền vàng (Gold), Cấp độ mỏ (Stations), Nâng cấp bản đồ (Map Upgrades), và Nhân viên (Workers).
    /// </summary>
    [Serializable]
    public class MapProgressSaveEntry {
        public string mapId = "map_01";
        public double gold = 0;
        public int totalWorkers = 0;
        public List<WorkerCountSaveEntry> workers = new List<WorkerCountSaveEntry>();
        public List<StationSaveEntry> stations = new List<StationSaveEntry>();
        public List<MapUpgradeSaveEntry> mapUpgrades = new List<MapUpgradeSaveEntry>();

        public MapProgressSaveEntry() { }

        public MapProgressSaveEntry(string mapId) {
            this.mapId = mapId;
        }
    }

    /// <summary>
    /// Khuôn mẫu dữ liệu gốc lưu trữ toàn bộ tiến trình game dạng JSON Offline.
    /// Phân biệt rõ ràng:
    /// 1. Tiến trình cục bộ từng Map (Gold, Stations, Map Upgrades).
    /// 2. Tiến trình toàn cục (Emerald, EUGT Upgrades, Cài đặt Game).
    /// </summary>
    [Serializable]
    public class GameSaveData {
        public string currentActiveMapId = "map_01";
        
        // Tiến trình toàn cục (Account-Wide / Global)
        public int emerald = 0;
        public List<EugtUpgradeSaveEntry> eugtUpgrades = new List<EugtUpgradeSaveEntry>();

        // Tiến trình độc lập theo từng Map
        public List<MapProgressSaveEntry> mapsProgress = new List<MapProgressSaveEntry>();

        // Mốc thời gian lưu (Unix epoch timestamp - giây)
        public long lastSaveTimestamp = 0;

        // Cài đặt trò chơi
        public bool isJoystickEnabled = true;
        public bool isHapticsEnabled = true;
        public bool isMusicEnabled = true;
        public bool isSfxEnabled = true;

        // Legacy fields để tương thích ngược nếu đọc file save cũ
        public double gold = 0;
        public List<StationSaveEntry> stations = new List<StationSaveEntry>();

        /// <summary>
        /// Lấy hoặc tạo mới bản ghi tiến trình cho một Map cụ thể.
        /// </summary>
        public MapProgressSaveEntry GetOrCreateMapProgress(string mapId) {
            if (string.IsNullOrEmpty(mapId)) mapId = "map_01";
            if (mapsProgress == null) mapsProgress = new List<MapProgressSaveEntry>();

            for (int i = 0; i < mapsProgress.Count; i++) {
                if (mapsProgress[i] != null && mapsProgress[i].mapId == mapId) {
                    return mapsProgress[i];
                }
            }

            var newEntry = new MapProgressSaveEntry(mapId);
            mapsProgress.Add(newEntry);
            return newEntry;
        }
    }
}
