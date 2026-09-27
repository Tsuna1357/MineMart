using System;
using UnityEngine;

namespace MineMart.Core.Events
{
    [Serializable]
    public struct OrderDeliveryPayload
    {
        public string stallId;
        public string blockId;
        public double basePrice;
        public Vector3 deliveryPosition;
        public bool isVipCustomer;

        public OrderDeliveryPayload(string stallId, string blockId, double basePrice, Vector3 deliveryPosition, bool isVipCustomer = false)
        {
            this.stallId = stallId;
            this.blockId = blockId;
            this.basePrice = basePrice;
            this.deliveryPosition = deliveryPosition;
            this.isVipCustomer = isVipCustomer;
        }
    }

    /// <summary>
    /// Kênh sự kiện ScriptableObject: Bắn tín hiệu khi khách nhận hàng thành công.
    /// Quầy hàng (Stall) -> bắn sự kiện.
    /// Ví tiền (EconomyManager) & Hiệu ứng (JuiceManager) -> lắng nghe sự kiện.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_OrderDelivered", menuName = "MineMart/Events/Order Delivered Channel")]
    public class OrderDeliveredEventChannelSO : ScriptableObject
    {
        public event Action<OrderDeliveryPayload> OnOrderDelivered;

        public void RaiseEvent(OrderDeliveryPayload payload)
        {
            OnOrderDelivered?.Invoke(payload);
        }
    }
}
