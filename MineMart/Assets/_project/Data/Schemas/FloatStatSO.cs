using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Data {
    /// <summary>
    /// Data thuần túy định nghĩa một chỉ số dạng số thực (Float Stat), ví dụ: Tỉ lệ để lại Tip, Tốc độ chạy cơ bản...
    /// Chứa BaseValue thuần túy và quản lý các modifier được cộng vào ở runtime (từ Upgrade, Buff...).
    /// Các hệ thống Gameplay (như Checkout Slot) chỉ trỏ duy nhất đến Stat này và đọc CurrentValue.
    /// </summary>
    [CreateAssetMenu(fileName = "Stat_TipChance", menuName = "MineMart/Data/Float Stat")]
    public class FloatStatSO : ScriptableObject {
        [Tooltip("Mã định danh duy nhất của chỉ số (ví dụ: TipChance, MoveSpeed...)")]
        [SerializeField] private string _statId = "TipChance";

        [Tooltip("Tên hiển thị thân thiện")]
        [SerializeField] private string _statName = "Tỉ Lệ Để Lại Tip";

        [Tooltip("Giá trị cơ bản gốc của chỉ số (ví dụ 0.10 tức 10% cơ bản)")]
        [SerializeField] private float _baseValue = 0.10f;

        [Tooltip("Giá trị giới hạn tối thiểu")]
        [SerializeField] private float _minValue = 0f;

        [Tooltip("Giá trị giới hạn tối đa (1.0 = 100%)")]
        [SerializeField] private float _maxValue = 1f;

        // Lưu trữ các giá trị cộng thêm (Modifiers) từ các nguồn nâng cấp / buff
        private readonly Dictionary<string, float> _modifiers = new Dictionary<string, float>();

        public string StatId => _statId;
        public string StatName => _statName;
        public float BaseValue => _baseValue;
        public float MinValue => _minValue;
        public float MaxValue => _maxValue;

        /// <summary>
        /// Giá trị hiệu dụng thực tế hiện tại của chỉ số = BaseValue + Tổng các modifier cộng thêm.
        /// </summary>
        public float CurrentValue {
            get {
                float total = _baseValue;
                foreach (var kvp in _modifiers) {
                    total += kvp.Value;
                }
                return Clamp(total);
            }
        }

        /// <summary>
        /// Gán hoặc cập nhật giá trị cộng thêm từ một nguồn (ví dụ: từ UpgradeID).
        /// </summary>
        public void SetModifier(string sourceKey, float value) {
            if (string.IsNullOrEmpty(sourceKey)) return;
            _modifiers[sourceKey] = value;
        }

        /// <summary>
        /// Gỡ bỏ giá trị cộng thêm của một nguồn.
        /// </summary>
        public void RemoveModifier(string sourceKey) {
            if (string.IsNullOrEmpty(sourceKey)) return;
            _modifiers.Remove(sourceKey);
        }

        /// <summary>
        /// Xóa sạch toàn bộ modifier.
        /// </summary>
        public void ClearModifiers() {
            _modifiers.Clear();
        }

        /// <summary>
        /// Kẹp giá trị trong khoảng giới hạn cho phép.
        /// </summary>
        public float Clamp(float value) {
            return Mathf.Clamp(value, _minValue, _maxValue);
        }
    }
}
