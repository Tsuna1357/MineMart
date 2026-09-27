using System;
using UnityEngine;

namespace MineMart.Core.Events
{
    /// <summary>
    /// Kênh sự kiện ScriptableObject: Bắn tín hiệu khi số dư tiền Vàng thay đổi.
    /// EconomyManager -> phát tín hiệu khi cộng/trừ tiền.
    /// UI (MainHudBinder) -> lắng nghe để cập nhật số tiền hiển thị trên HUD.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_GoldChanged", menuName = "MineMart/Events/Gold Changed Channel")]
    public class GoldChangedEventChannelSO : ScriptableObject
    {
        public event Action<double> OnGoldChanged;

        public void RaiseEvent(double newBalance)
        {
            OnGoldChanged?.Invoke(newBalance);
        }
    }
}
