using System;
using MineMart.Data;
using UnityEngine;

namespace MineMart.Gameplay.Orders {
    /// <summary>
    /// Các trạng thái vòng đời của một đơn hàng trong xưởng.
    /// </summary>
    public enum OrderState {
        /// <summary>Đơn hàng mới tạo, đang chờ thợ mỏ nhận làm</summary>
        Pending,

        /// <summary>Đã có thợ mỏ hoặc người chơi nhận khai thác</summary>
        InCrafting,

        /// <summary>Đã đào xong, đang trên đường mang đi phục vụ</summary>
        Delivering,

        /// <summary>Đã trao tận tay khách và hoàn tất thanh toán</summary>
        Completed,

        /// <summary>Đơn bị hủy (khách rời đi do hết kiên nhẫn)</summary>
        Cancelled
    }

    /// <summary>
    /// Thực thể đại diện cho một Đơn Hàng (Order Ticket) trong toàn hệ thống.
    /// Mang đầy đủ thông tin về khách yêu cầu, loại block cần, giá trị thanh toán và trạng thái xử lý.
    /// </summary>
    [System.Serializable]
    public class OrderTicket {
        [SerializeField] private string _orderId;
        [SerializeField] private BlockDataSO _requiredBlock;
        [SerializeField] private double _price;
        [SerializeField] private OrderState _state;
        [SerializeField] private float _createdTime;

        // Tham chiếu không serialize
        [System.NonSerialized] private object _requesterSlot;
        [System.NonSerialized] private object _claimedWorker;

        public string OrderId => _orderId;
        public BlockDataSO RequiredBlock => _requiredBlock;
        public double Price => _price;
        public OrderState State => _state;
        public float CreatedTime => _createdTime;
        public object RequesterSlot => _requesterSlot;
        public object ClaimedWorker => _claimedWorker;

        public bool IsPending => _state == OrderState.Pending;
        public bool IsInCrafting => _state == OrderState.InCrafting;
        public bool IsDelivering => _state == OrderState.Delivering;
        public bool IsCompleted => _state == OrderState.Completed;
        public bool IsCancelled => _state == OrderState.Cancelled;
        public bool IsActive => _state == OrderState.Pending || _state == OrderState.InCrafting || _state == OrderState.Delivering;

        public OrderTicket(string orderId, object requesterSlot, BlockDataSO requiredBlock, double price) {
            _orderId = orderId;
            _requesterSlot = requesterSlot;
            _requiredBlock = requiredBlock;
            _price = price;
            _state = OrderState.Pending;
            _createdTime = Time.time;
            _claimedWorker = null;
        }

        public void SetState(OrderState newState) {
            _state = newState;
        }

        public void Claim(object worker) {
            _claimedWorker = worker;
            _state = OrderState.InCrafting;
        }

        public void ReleaseClaim() {
            _claimedWorker = null;
            if (_state == OrderState.InCrafting) {
                _state = OrderState.Pending;
            }
        }
    }
}
