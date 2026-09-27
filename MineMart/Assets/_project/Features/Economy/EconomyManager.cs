using System;
using UnityEngine;
using MineMart.Core.Events;

namespace MineMart.Economy
{
    /// <summary>
    /// Kế toán trưởng kiêm Ví Tiền Vàng duy nhất của người chơi.
    /// Quản lý số dư CurrentGold (kiểu double), tự động thu tiền khi đơn hàng hoàn tất,
    /// cung cấp API trừ tiền an toàn khi mở khóa/nâng cấp, và phát tín hiệu cập nhật HUD UI.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        [Header("Event Channels")]
        [Tooltip("Kênh nhận thông báo khi có đơn hàng được giao thành công")]
        [SerializeField] private OrderDeliveredEventChannelSO _orderDeliveredChannel;

        [Tooltip("Kênh phát tín hiệu khi số dư tiền vàng thay đổi để HUD cập nhật")]
        [SerializeField] private GoldChangedEventChannelSO _goldChangedChannel;

        [Header("Initial Balance & Settings")]
        [Tooltip("Số dư tiền vàng khởi đầu (mặc định 0 cho người chơi mới)")]
        [SerializeField] private double _currentGold = 0;

        [Tooltip("Hệ số nhân thưởng khi phục vụ khách hàng VIP")]
        [SerializeField] private double _vipMultiplier = 2.0;

        public double CurrentGold => _currentGold;

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

#if UNITY_EDITOR
            if (_orderDeliveredChannel == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:OrderDeliveredEventChannelSO Event_OrderDelivered");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _orderDeliveredChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<OrderDeliveredEventChannelSO>(path);
                }
            }

            if (_goldChangedChannel == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GoldChangedEventChannelSO Event_GoldChanged");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _goldChangedChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<GoldChangedEventChannelSO>(path);
                }
            }
#endif
        }

        private void OnEnable()
        {
            if (_orderDeliveredChannel != null)
            {
                _orderDeliveredChannel.OnOrderDelivered += HandleOrderDelivered;
            }
        }

        private void OnDisable()
        {
            if (_orderDeliveredChannel != null)
            {
                _orderDeliveredChannel.OnOrderDelivered -= HandleOrderDelivered;
            }
        }

        private void Start()
        {
            // Bắn tín hiệu số dư ban đầu khi vào game để UI hiển thị số tiền chính xác
            _goldChangedChannel?.RaiseEvent(_currentGold);
        }

        /// <summary>
        /// Xử lý sự kiện khi có đơn hàng block được giao cho khách.
        /// </summary>
        private void HandleOrderDelivered(OrderDeliveryPayload payload)
        {
            double earnedAmount = payload.basePrice;
            if (payload.isVipCustomer)
            {
                earnedAmount *= _vipMultiplier;
            }

            AddGold(earnedAmount);
        }

        /// <summary>
        /// Cộng tiền vào ví và thông báo cho toàn bộ hệ thống.
        /// </summary>
        public void AddGold(double amount)
        {
            if (amount <= 0) return;

            _currentGold += amount;
            _goldChangedChannel?.RaiseEvent(_currentGold);
        }

        /// <summary>
        /// Kiểm tra và trừ tiền khi người chơi mở khóa hoặc nâng cấp mỏ.
        /// Trả về true nếu giao dịch thành công; false nếu không đủ tiền.
        /// </summary>
        public bool TrySpendGold(double amount)
        {
            if (amount < 0) return false;

            // Hỗ trợ trường hợp mở khóa 0 Vàng (như Quầy Đất tân thủ)
            if (amount == 0) return true;

            if (_currentGold >= amount)
            {
                _currentGold -= amount;
                _goldChangedChannel?.RaiseEvent(_currentGold);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Nạp lại số dư tiền vàng khi khôi phục dữ liệu từ SaveManager.
        /// </summary>
        public void SetGold(double amount)
        {
            _currentGold = Math.Max(0, amount);
            _goldChangedChannel?.RaiseEvent(_currentGold);
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
