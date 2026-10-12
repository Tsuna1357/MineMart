using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Data {
    /// <summary>
    /// Định nghĩa trạng thái của một cấp độ trong máy trạng thái cấp độ (Upgrade Level State Machine).
    /// Định nghĩa rõ ràng cho từng level: Cần bao nhiêu Gem để lên level này, và cộng vào chỉ số bao nhiêu.
    /// </summary>
    [Serializable]
    public class UpgradeLevelState {
        [Tooltip("Cấp độ của nâng cấp (1, 2, 3...)")]
        public int level = 1;

        [Tooltip("Chi phí Gem cần để nâng cấp lên cấp độ này")]
        public int costGem = 5;

        [Tooltip("Chỉ số cộng thêm vào Stat tại cấp độ này")]
        public float addValue = 0.05f;
    }

    /// <summary>
    /// Schema cấu hình Nâng Cấp Bằng Ngọc Lục Bảo (Emerald Upgrade Data).
    /// Quản lý máy trạng thái cấp độ (_levelStates): Định nghĩa từng level cost bao nhiêu Gem,
    /// và cộng vào chỉ số (TargetStat) bao nhiêu. Hoàn toàn không chứa công thức base hay bonus/level hardcode.
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_TipChance", menuName = "MineMart/Data/Emerald Upgrade Data")]
    public class EmeraldUpgradeDataSO : ScriptableObject {
        [Header("Identity")]
        [Tooltip("ID định danh của nâng cấp (ví dụ: EUGT_TipChance, EUGT_MoveSpeed...)")]
        [SerializeField] private string _upgradeId = "EUGT_TipChance";

        [Tooltip("Tên hiển thị trên bảng nâng cấp")]
        [SerializeField] private string _displayName = "Tỉ Lệ Rơi Tip";

        [Tooltip("Mô tả hiệu ứng nâng cấp")]
        [TextArea(2, 4)]
        [SerializeField] private string _description = "Tăng tỉ lệ khách hàng để lại tiền tip sau khi nhận hàng tại quầy.";

        [Tooltip("Icon emoji đại diện (💰, 👟, ⛏️, 🎒...)")]
        [SerializeField] private string _iconSymbol = "💰";

        [Tooltip("Sprite icon tùy chọn (nếu có)")]
        [SerializeField] private Sprite _customIcon;

        [Header("Target Stat (Chỉ số thuần túy được cộng vào)")]
        [Tooltip("Data chỉ số gốc được nâng cấp (ví dụ Stat_TipChance). Nâng cấp sẽ tự áp dụng modifier vào đây.")]
        [SerializeField] private FloatStatSO _targetStat;

        [Header("Level State Machine (Định Nghĩa Chi Tiết Từng Cấp Độ)")]
        [Tooltip("Danh sách các trạng thái cấp độ: Mỗi level định nghĩa rõ ràng tốn bao nhiêu Gem và cộng vào chỉ số bao nhiêu")]
        [SerializeField] private List<UpgradeLevelState> _levelStates = new List<UpgradeLevelState>();

        public string UpgradeId => _upgradeId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public string IconSymbol => _iconSymbol;
        public Sprite CustomIcon => _customIcon;
        public FloatStatSO TargetStat => _targetStat;
        public IReadOnlyList<UpgradeLevelState> LevelStates => _levelStates;

        [System.NonSerialized] private int _runtimeLevel = 1;
        [System.NonSerialized] private bool _runtimeUnlocked = false;

        /// <summary>
        /// Cấp độ hiện tại trong phiên chơi (chỉ lưu trong RAM, không lưu ngầm PlayerPrefs).
        /// </summary>
        public int CurrentLevel {
            get => _runtimeLevel;
            set => _runtimeLevel = Mathf.Max(1, value);
        }

        /// <summary>
        /// Trạng thái đã mở khóa trong phiên chơi (chỉ lưu trong RAM, không lưu ngầm PlayerPrefs).
        /// </summary>
        public bool IsUnlocked {
            get => _runtimeUnlocked;
            set => _runtimeUnlocked = value;
        }

        public void SetRuntimeState(bool unlocked, int level) {
            _runtimeUnlocked = unlocked;
            _runtimeLevel = Mathf.Max(1, level);
        }

        /// <summary>
        /// Cấp độ tối đa của nâng cấp được xác định trực tiếp từ số lượng trạng thái level được định nghĩa.
        /// </summary>
        public int MaxLevel => _levelStates != null && _levelStates.Count > 0 ? _levelStates.Count : 1;

        /// <summary>
        /// Lấy chi phí Gem cần để nâng cấp từ cấp hiện tại lên cấp tiếp theo.
        /// </summary>
        public int GetUpgradeCost(int currentLevel) {
            if (currentLevel >= MaxLevel || _levelStates == null || _levelStates.Count == 0) return 0;

            int targetLevel = currentLevel + 1;
            for (int i = 0; i < _levelStates.Count; i++) {
                if (_levelStates[i] != null && _levelStates[i].level == targetLevel) {
                    return _levelStates[i].costGem;
                }
            }

            // Fallback theo index nếu level không khớp số thứ tự
            if (currentLevel < _levelStates.Count && _levelStates[currentLevel] != null) {
                return _levelStates[currentLevel].costGem;
            }

            return 0;
        }

        /// <summary>
        /// Lấy tổng chỉ số cộng thêm tích lũy của tất cả các cấp độ từ 1 đến cấp hiện tại.
        /// </summary>
        public float GetBonusAtLevel(int level) {
            if (_levelStates == null || level <= 0) return 0f;

            float total = 0f;
            for (int i = 0; i < _levelStates.Count; i++) {
                UpgradeLevelState state = _levelStates[i];
                if (state != null && state.level <= level) {
                    total += state.addValue;
                }
            }
            return total;
        }

        /// <summary>
        /// Lấy thông tin cấu hình của một cấp độ cụ thể.
        /// </summary>
        public UpgradeLevelState GetLevelState(int level) {
            if (_levelStates == null) return null;
            for (int i = 0; i < _levelStates.Count; i++) {
                if (_levelStates[i] != null && _levelStates[i].level == level) {
                    return _levelStates[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Áp dụng chỉ số cộng thêm vào TargetStat (gán Modifier tương ứng với CurrentLevel hoặc level chỉ định).
        /// </summary>
        public void ApplyToTargetStat(int level = -1) {
            if (_targetStat == null) return;

            if (level == 0 || (level < 0 && !IsUnlocked)) {
                _targetStat.RemoveModifier(_upgradeId);
                return;
            }

            int targetLevel = level > 0 ? level : CurrentLevel;
            float bonus = GetBonusAtLevel(targetLevel);
            _targetStat.SetModifier(_upgradeId, bonus);
        }
    }
}
