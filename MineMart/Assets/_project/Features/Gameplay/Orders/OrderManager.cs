using System;
using System.Collections.Generic;
using UnityEngine;
using MineMart.Data;

namespace MineMart.Gameplay.Orders
{
    /// <summary>
    /// Bộ Điều Phối Đơn Hàng Trung Tâm (Central Order Dispatcher & Manager).
    /// Áp dụng mô hình "Bảng Phân Ngăn Hàng Đợi" (Bucketed FIFO Queue) kết hợp "Kế Toán Hạn Mức Đơn Hàng" (Order Quota Accounting):
    /// 1. Tự động khởi tạo (Lazy Auto-Creation): Luôn sẵn sàng hoạt động 100%, không lo null khi khởi động game.
    /// 2. Phân loại theo từng loại Block: Tra cứu tức thời O(1), không nghẽn cổ chai.
    /// 3. Phân biệt chặt chẽ số lượng đơn hàng:
    ///    - PendingCount: Số đơn khách đang chờ chưa có ai nhận đào (Nhu cầu thực tế cần mỏ khai thác).
    ///    - InCraftingCount: Số đơn đã có người/thợ nhận đào (Khóa mỏ lại không cho đào thừa).
    ///    - DeliveringCount: Số đơn đã đào xong thành phẩm đang trên đường bưng đi giao.
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        private static OrderManager s_instance;
        public static OrderManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindAnyObjectByType<OrderManager>();
                    if (s_instance == null)
                    {
                        GameObject go = new GameObject("OrderManager");
                        s_instance = go.AddComponent<OrderManager>();
                    }
                }
                return s_instance;
            }
        }

        [Header("Runtime State")]
        [Tooltip("Tổng số đơn hàng đang hoạt động trong toàn cửa hàng")]
        [SerializeField] private int _totalActiveOrdersCount = 0;

        [Tooltip("Danh sách toàn bộ vé đơn hàng đang hoạt động (Hiển thị trực quan trong Inspector)")]
        [SerializeField] private List<OrderTicket> _allActiveOrders = new List<OrderTicket>();

        // Bảng phân ngăn hàng đợi FIFO chứa các đơn ĐANG CHỜ (Pending) theo từng loại BlockDataSO (Tốc độ O(1))
        private readonly Dictionary<BlockDataSO, Queue<OrderTicket>> _pendingBuckets = new Dictionary<BlockDataSO, Queue<OrderTicket>>();

        // Bộ đếm tạo ID duy nhất cho từng đơn hàng
        private int _orderIdCounter = 0;

        public event Action<OrderTicket> OnOrderCreated;
        public event Action<OrderTicket> OnOrderClaimed;
        public event Action<OrderTicket> OnOrderCompleted;
        public event Action<OrderTicket> OnOrderCancelled;

        public IReadOnlyList<OrderTicket> AllActiveOrders => _allActiveOrders;
        public int TotalActiveOrdersCount => _totalActiveOrdersCount;

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
            }
            else if (s_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Tạo đơn hàng mới từ Quầy Tiếp Khách (CustomerSlot) và đưa vào hàng đợi phân ngăn.
        /// </summary>
        public OrderTicket CreateOrder(object requesterSlot, BlockDataSO block, double price)
        {
            if (block == null) return null;

            _orderIdCounter++;
            string id = $"ORD_{_orderIdCounter:D4}";

            OrderTicket ticket = new OrderTicket(id, requesterSlot, block, price);

            // 1. Thêm vào danh sách tổng toàn tiệm
            _allActiveOrders.Add(ticket);
            _totalActiveOrdersCount = _allActiveOrders.Count;

            // 2. Thêm vào ngăn FIFO riêng của loại block này (Tốc độ O(1))
            if (!_pendingBuckets.TryGetValue(block, out Queue<OrderTicket> bucket))
            {
                bucket = new Queue<OrderTicket>();
                _pendingBuckets[block] = bucket;
            }
            bucket.Enqueue(ticket);

            OnOrderCreated?.Invoke(ticket);
            Debug.Log($"[OrderManager] 📥 Tiếp nhận đơn [{ticket.OrderId}] - Cần 1 [{block.DisplayName}] | Chờ đào: {GetPendingCount(block)} | Tổng active: {_totalActiveOrdersCount}");

            return ticket;
        }

        /// <summary>
        /// Lấy số lượng đơn hàng ĐANG CHỜ KHAI THÁC (Pending) của loại block này.
        /// CHỈ khi số lượng này > 0 thì trạm mỏ mới mở khóa cho phép đào đúng bấy nhiêu khối!
        /// </summary>
        public int GetPendingCount(BlockDataSO block)
        {
            if (block == null) return 0;

            if (_pendingBuckets.TryGetValue(block, out Queue<OrderTicket> bucket))
            {
                CleanBucket(bucket);
                return bucket.Count;
            }

            return 0;
        }

        /// <summary>
        /// Kiểm tra xem có đơn hàng nào đang chờ khai thác cho loại block này không (Tốc độ O(1)).
        /// </summary>
        public bool HasPendingOrder(BlockDataSO block)
        {
            return GetPendingCount(block) > 0;
        }

        /// <summary>
        /// Tổng số lượng đơn của loại block này đang hoạt động trong tiệm (Pending + InCrafting + Delivering).
        /// </summary>
        public int GetTotalActiveCount(BlockDataSO block)
        {
            if (block == null) return 0;

            int count = 0;
            for (int i = 0; i < _allActiveOrders.Count; i++)
            {
                if (_allActiveOrders[i] != null && _allActiveOrders[i].RequiredBlock == block && _allActiveOrders[i].IsActive)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Khi Người chơi hoặc Thợ bắt đầu đào mỏ: Lập tức "đặt gạch" nhận 1 đơn Pending chuyển sang InCrafting.
        /// Thao tác này ngay lập tức giảm PendingCount đi 1, giúp mỏ không bị đào thừa block khi khách chỉ gọi 1!
        /// </summary>
        public OrderTicket ClaimNextPendingOrder(BlockDataSO block, object worker)
        {
            if (block == null) return null;

            if (_pendingBuckets.TryGetValue(block, out Queue<OrderTicket> bucket))
            {
                CleanBucket(bucket);

                if (bucket.Count > 0)
                {
                    OrderTicket ticket = bucket.Dequeue();
                    ticket.Claim(worker);

                    OnOrderClaimed?.Invoke(ticket);
                    Debug.Log($"[OrderManager] ⛏️ Worker [{worker}] đã nhận đào đơn [{ticket.OrderId}]. Còn lại chờ đào: {bucket.Count}");
                    return ticket;
                }
            }

            return null;
        }

        /// <summary>
        /// Nếu người chơi hoặc thợ rời khỏi mỏ khi chưa đào xong: Trả lại đơn về trạng thái Pending.
        /// </summary>
        public void ReleaseClaim(OrderTicket ticket)
        {
            if (ticket == null || !ticket.IsInCrafting) return;

            ticket.ReleaseClaim();

            // Đưa lại vào đầu hàng đợi của loại block đó
            if (_pendingBuckets.TryGetValue(ticket.RequiredBlock, out Queue<OrderTicket> bucket))
            {
                // Để đơn vừa trả lại lên đầu, ta dùng danh sách tạm hoặc enqueue
                bucket.Enqueue(ticket);
            }

            Debug.Log($"[OrderManager] ↩️ Đã giải phóng đơn [{ticket.OrderId}] trở lại trạng thái Pending.");
        }

        /// <summary>
        /// Đánh dấu đơn hàng đã khai thác xong thành block trên tay và đang trên đường mang đi phục vụ.
        /// </summary>
        public void MarkDelivering(OrderTicket ticket)
        {
            if (ticket == null || !ticket.IsActive) return;
            ticket.SetState(OrderState.Delivering);
            Debug.Log($"[OrderManager] 🚚 Đơn [{ticket.OrderId}] đã chế tác xong, đang vận chuyển!");
        }

        /// <summary>
        /// Hoàn tất đơn hàng khi khách đã nhận đồ và thanh toán.
        /// </summary>
        public void CompleteOrder(OrderTicket ticket)
        {
            if (ticket == null) return;

            // Bù trừ Quota: Nếu vé này chưa ở trạng thái Delivering (do người chơi lấy block của vé khác để giao)
            // thì ta phải tìm 1 vé khác đang Delivering (vé của block vừa bị cướp) và trả nó về Pending
            // để mỏ có thể cho phép đào bù lại khối lượng đã mất!
            if (ticket.IsPending || ticket.IsInCrafting)
            {
                OrderTicket ghostTicket = FindDeliveringTicket(ticket.RequiredBlock);
                if (ghostTicket != null)
                {
                    ghostTicket.SetState(OrderState.Pending);
                    ghostTicket.ReleaseClaim();
                    
                    if (_pendingBuckets.TryGetValue(ticket.RequiredBlock, out Queue<OrderTicket> bucket))
                    {
                        bucket.Enqueue(ghostTicket);
                    }
                    Debug.Log($"[OrderManager] 🔄 Quota Reclaimed! Vé [{ghostTicket.OrderId}] được chuyển về Pending do block của nó bị giao cho đơn khác.");
                }
            }

            ticket.SetState(OrderState.Completed);
            _allActiveOrders.Remove(ticket);
            _totalActiveOrdersCount = _allActiveOrders.Count;

            OnOrderCompleted?.Invoke(ticket);
            Debug.Log($"[OrderManager] ✅ Hoàn tất đơn hàng [{ticket.OrderId}]! (Tổng active còn lại: {_totalActiveOrdersCount})");
        }

        private OrderTicket FindDeliveringTicket(BlockDataSO block)
        {
            for (int i = 0; i < _allActiveOrders.Count; i++)
            {
                OrderTicket t = _allActiveOrders[i];
                if (t != null && t.RequiredBlock == block && t.State == OrderState.Delivering)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Hủy đơn hàng (khi khách rời quầy do chờ lâu).
        /// </summary>
        public void CancelOrder(OrderTicket ticket)
        {
            if (ticket == null) return;

            ticket.SetState(OrderState.Cancelled);
            _allActiveOrders.Remove(ticket);
            _totalActiveOrdersCount = _allActiveOrders.Count;

            OnOrderCancelled?.Invoke(ticket);
            Debug.Log($"[OrderManager] ❌ Đã hủy đơn hàng [{ticket.OrderId}].");
        }

        /// <summary>
        /// Hủy toàn bộ đơn hàng phát sinh từ một CustomerSlot cụ thể.
        /// </summary>
        public void CancelOrdersFromRequester(object requesterSlot)
        {
            if (requesterSlot == null) return;

            for (int i = _allActiveOrders.Count - 1; i >= 0; i--)
            {
                OrderTicket ticket = _allActiveOrders[i];
                if (ticket != null && ticket.RequesterSlot == requesterSlot)
                {
                    CancelOrder(ticket);
                }
            }
        }

        /// <summary>
        /// Dọn dẹp các đơn đã bị Hủy hoặc Hoàn Tất còn sót lại ở đầu hàng đợi (Lazy Cleanup).
        /// </summary>
        private void CleanBucket(Queue<OrderTicket> bucket)
        {
            while (bucket.Count > 0)
            {
                OrderTicket head = bucket.Peek();
                if (head == null || !head.IsPending)
                {
                    bucket.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Add Test Order (Dirt)")]
        private void DebugAddDirtOrder()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockDataSO Block_Dirt");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                BlockDataSO dirt = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockDataSO>(path);
                CreateOrder(this, dirt, 3);
            }
        }

        [ContextMenu("Debug: Add Test Order (Stone)")]
        private void DebugAddStoneOrder()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockDataSO Block_Stone");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                BlockDataSO stone = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockDataSO>(path);
                CreateOrder(this, stone, 5);
            }
        }

        [ContextMenu("Debug: Clear All Orders")]
        private void DebugClearAllOrders()
        {
            _allActiveOrders.Clear();
            _pendingBuckets.Clear();
            _totalActiveOrdersCount = 0;
            Debug.Log("[OrderManager] 🧹 Đã dọn sạch toàn bộ đơn hàng trong tiệm.");
        }
#endif
    }
}
