using System;
using System.Collections.Generic;

namespace MineMart.Gameplay.Persistence
{
    /// <summary>
    /// Bản ghi lưu trữ cấp độ của một trạm mỏ khai thác nguyên liệu.
    /// </summary>
    [Serializable]
    public class StationSaveEntry
    {
        public string stationId;
        public int level;

        public StationSaveEntry() {}

        public StationSaveEntry(string stationId, int level)
        {
            this.stationId = stationId;
            this.level = level;
        }
    }

    /// <summary>
    /// Khuôn mẫu dữ liệu gốc lưu trữ toàn bộ tiến trình màn chơi dạng JSON.
    /// Đáp ứng tiêu chí 100% Offline Persistence cho bản Single-Screen MVP.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public double gold = 0;
        public List<StationSaveEntry> stations = new List<StationSaveEntry>();
        public long lastSaveTimestamp = 0; // Unix epoch timestamp (giây)
        public bool isJoystickEnabled = true;
        public bool isHapticsEnabled = true;
    }
}
