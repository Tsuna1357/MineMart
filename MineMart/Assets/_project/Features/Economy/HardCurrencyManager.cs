
using System;
using MineMart.Core.Events;
using UnityEngine;

namespace MineMart.Economy {
    /// <summary>
    /// Quản lý Tiền Tệ Cao Cấp (Hard Currency / Emerald / Gem) hoàn toàn độc lập.
    /// Tách biệt 100% khỏi Soft Currency (Tiền Vàng theo Map trong EconomyManager).
    /// Quản lý số dư toàn cục (Account-Wide), sống qua tất cả các Map và chu kỳ Prestige.
    /// </summary>
    public class HardCurrencyManager : MonoBehaviour {
        private static HardCurrencyManager _instance;
        public static HardCurrencyManager Instance {
            get {
                if (_instance == null) {
                    _instance = FindAnyObjectByType<HardCurrencyManager>();
                    if (_instance == null) {
                        GameObject go = new GameObject("[HardCurrencyManager]");
                        _instance = go.AddComponent<HardCurrencyManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Event Channel")]
        [Tooltip("Kênh phát tín hiệu khi số dư Hard Currency (Emerald / Gem) thay đổi để HUD cập nhật")]
        [SerializeField] private EmeraldChangedEventChannelSO _emeraldChangedChannel;

        [Header("Balance")]
        [Tooltip("Số dư Hard Currency hiện tại (chỉ lưu trong phiên chơi RAM, không tự tiện lưu PlayerPrefs)")]
        [SerializeField] private int _currentEmerald = 0;

        public int CurrentEmerald => _currentEmerald;
        public int CurrentHardCurrency => _currentEmerald;

        public event Action<int> OnEmeraldChanged;

        private const string PREF_KEY_HARD_CURRENCY = "MineMart_Global_Emerald";

        private void Awake() {
            if (_instance == null) {
                _instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                // Dọn sạch toàn bộ dữ liệu PlayerPrefs cũ nếu trước đó đã từng lưu
                if (PlayerPrefs.HasKey(PREF_KEY_HARD_CURRENCY)) {
                    PlayerPrefs.DeleteKey(PREF_KEY_HARD_CURRENCY);
                    PlayerPrefs.Save();
                }
            } else if (_instance != this) {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            if (_emeraldChangedChannel == null) {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EmeraldChangedEventChannelSO Event_EmeraldChanged");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _emeraldChangedChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<EmeraldChangedEventChannelSO>(path);
                }
            }
#endif
        }

        private void Start() {
            // Bắn tín hiệu số dư ban đầu khi vào game để UI hiển thị số tiền chính xác
            _emeraldChangedChannel?.RaiseEvent(_currentEmerald);
            OnEmeraldChanged?.Invoke(_currentEmerald);
        }

        /// <summary>
        /// Cộng Hard Currency (Gem / Emerald) vào ví trong phiên chơi.
        /// </summary>
        public void AddEmerald(int amount) {
            if (amount <= 0) return;

            _currentEmerald += amount;
            _emeraldChangedChannel?.RaiseEvent(_currentEmerald);
            OnEmeraldChanged?.Invoke(_currentEmerald);
        }

        public void AddHardCurrency(int amount) => AddEmerald(amount);

        /// <summary>
        /// Tiêu Hard Currency khi mua nâng cấp vĩnh viễn (EUGT) hoặc tính năng đặc biệt.
        /// Trả về true nếu đủ số dư và giao dịch thành công.
        /// </summary>
        public bool TrySpendEmerald(int amount) {
            if (amount < 0) return false;
            if (amount == 0) return true;

            if (_currentEmerald >= amount) {
                _currentEmerald -= amount;
                _emeraldChangedChannel?.RaiseEvent(_currentEmerald);
                OnEmeraldChanged?.Invoke(_currentEmerald);
                return true;
            }

            return false;
        }

        public bool TrySpendHardCurrency(int amount) => TrySpendEmerald(amount);

        /// <summary>
        /// Đặt lại số dư Hard Currency.
        /// </summary>
        public void SetEmerald(int amount) {
            _currentEmerald = Math.Max(0, amount);
            _emeraldChangedChannel?.RaiseEvent(_currentEmerald);
            OnEmeraldChanged?.Invoke(_currentEmerald);
        }

        public void SetHardCurrency(int amount) => SetEmerald(amount);
    }
}
