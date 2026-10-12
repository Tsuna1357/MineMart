using System;
using UnityEngine;

namespace MineMart.Core.Events {
    /// <summary>
    /// Kênh sự kiện ScriptableObject: Bắn tín hiệu khi số dư Gem / Emerald (Hard Currency) thay đổi.
    /// EconomyManager -> phát tín hiệu khi cộng/trừ Gem.
    /// UI (MainCanvasHud) -> lắng nghe để cập nhật số Gem hiển thị trên HUD.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_EmeraldChanged", menuName = "MineMart/Events/Emerald Changed Channel")]
    public class EmeraldChangedEventChannelSO : ScriptableObject {
        public event Action<int> OnEmeraldChanged;

        public void RaiseEvent(int newBalance) {
            OnEmeraldChanged?.Invoke(newBalance);
        }
    }
}
