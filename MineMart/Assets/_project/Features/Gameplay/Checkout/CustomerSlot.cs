using System;
using System.Collections;
using UnityEngine;
using MineMart.Data;
using MineMart.Core.Events;
using MineMart.Gameplay.Orders;

namespace MineMart.Gameplay.Checkout
{
    /// <summary>
    /// Đại diện cho MỘT Ô TIẾP KHÁCH ĐỘC LẬP trên quầy phục vụ dài (Customer Service Slot).
    /// Tương tự như CounterSlot trên bàn trung chuyển, mỗi ô tiếp khách có:
    /// 1. CustomerAnchor: Vị trí dưới sàn phía trước bàn nơi khách đứng chờ gọi món và nhận món.
    /// 2. ServerAnchor: Vị trí dưới sàn phía sau bàn nơi nhân viên/người chơi đứng phục vụ cho khách tại ô này.
    /// 3. TipAnchor: Vị trí trên mặt bàn nơi tiền tip xuất hiện sau khi phục vụ xong.
    /// 4. Auto Order Loop: Tự động tạo đơn hàng phục vụ cho việc kiểm thử độc lập trên từng slot.
    /// </summary>
    public class CustomerSlot : MonoBehaviour
    {
        [Header("Scene Anchors")]
        [Tooltip("Điểm neo dưới sàn nơi khách hàng đứng chờ ở phía trước quầy")]
        [SerializeField] private Transform _customerAnchor;

        [Tooltip("Điểm neo dưới sàn nơi nhân viên/người chơi đứng phục vụ ở phía sau quầy")]
        [SerializeField] private Transform _serverAnchor;

        [Tooltip("Điểm neo trên mặt bàn nơi tiền tip sẽ xuất hiện sau khi phục vụ xong khách")]
        [SerializeField] private Transform _tipAnchor;

        [Header("Slot State")]
        [Tooltip("Đang có khách chiếm chỗ tại ô này hay chưa")]
        [SerializeField] private bool _isOccupiedByCustomer = false;

        [Tooltip("Tham chiếu tới GameObject khách hàng đang đứng tại ô này (nếu có)")]
        [SerializeField] private GameObject _currentCustomer;

        [Header("Order Information")]
        [Tooltip("Loại block mà khách tại ô này đang gọi")]
        [SerializeField] private BlockDataSO _currentOrderBlock;

        [Tooltip("Giá tiền vàng khách sẽ thanh toán khi nhận đúng block")]
        [SerializeField] private double _currentOrderPrice;

        [Tooltip("Trạng thái đang có đơn hàng chờ phục vụ")]
        [SerializeField] private bool _hasActiveOrder = false;

        [Header("Auto Order Loop (MVP Testing)")]
        [Tooltip("Tự động tạo đơn hàng định kỳ cho ô này để test vòng lặp ngay khi vào game mà không cần chờ NPC")]
        [SerializeField] private bool _autoGenerateOrders = true;

        [Tooltip("Loại block mặc định nếu chưa gán block thủ công")]
        [SerializeField] private BlockDataSO _defaultOrderBlock;

        [Tooltip("Thời gian chờ trước khi tạo đơn tiếp theo (giây)")]
        [SerializeField] private float _orderInterval = 1.5f;

        private OrderTicket _activeTicket;

        public Transform CustomerAnchor => _customerAnchor != null ? _customerAnchor : transform;
        public Transform ServerAnchor => _serverAnchor != null ? _serverAnchor : transform;
        public Transform TipAnchor => _tipAnchor != null ? _tipAnchor : transform;

        public bool IsOccupied => _isOccupiedByCustomer;
        public GameObject CurrentCustomer => _currentCustomer;
        public bool HasActiveOrder => (_activeTicket != null && _activeTicket.IsActive) || (_hasActiveOrder && _currentOrderBlock != null);
        public BlockDataSO CurrentOrderBlock => _currentOrderBlock;
        public double CurrentOrderPrice => _currentOrderPrice;

        public event Action<BlockDataSO> OnOrderPlaced;
        public event Action OnOrderCompleted;

        private void Reset()
        {
            AutoDiscoverAnchors();
        }

        private void Awake()
        {
            AutoDiscoverAnchors();

#if UNITY_EDITOR
            if (_defaultOrderBlock == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockDataSO Block_Dirt");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _defaultOrderBlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockDataSO>(path);
                }
            }
#endif
        }

        private void Start()
        {
            // Khởi động đơn hàng ban đầu:
            // 1. Nếu slot đã được thiết lập order sẵn trong Inspector: đẩy ngay vào OrderManager
            // 2. Hoặc nếu bật tự động sinh đơn (_autoGenerateOrders): tự động sinh đơn đầu tiên
            if (_activeTicket == null)
            {
                if (_hasActiveOrder && _currentOrderBlock != null)
                {
                    _activeTicket = OrderManager.Instance.CreateOrder(this, _currentOrderBlock, _currentOrderPrice > 0 ? _currentOrderPrice : _currentOrderBlock.BaseValue);
                }
                else if (_autoGenerateOrders)
                {
                    TryGenerateNextOrder();
                }
            }
        }

        /// <summary>
        /// Tự động tìm kiếm các Anchor con theo tên chuẩn để người dùng dễ kéo thả hoặc tạo cấu trúc.
        /// </summary>
        public void AutoDiscoverAnchors()
        {
            if (_customerAnchor == null)
            {
                Transform found = transform.Find("CustomerAnchor");
                if (found == null) found = transform.Find("CustomerPoint");
                if (found != null) _customerAnchor = found;
            }

            if (_serverAnchor == null)
            {
                Transform found = transform.Find("ServerAnchor");
                if (found == null) found = transform.Find("ServerPoint");
                if (found != null) _serverAnchor = found;
            }

            if (_tipAnchor == null)
            {
                Transform found = transform.Find("TipAnchor");
                if (found == null) found = transform.Find("TipPoint");
                if (found == null) found = transform.Find("TableAnchor"); // Tương thích ngược nếu đã đặt tên TableAnchor
                if (found != null) _tipAnchor = found;
            }
        }

        /// <summary>
        /// Khách hàng bước tới và nhận ô này.
        /// </summary>
        public void AssignCustomer(GameObject customer)
        {
            _currentCustomer = customer;
            _isOccupiedByCustomer = true;
        }

        /// <summary>
        /// Khách hàng rời khỏi ô này sau khi nhận xong hàng hoặc hết kiên nhẫn.
        /// </summary>
        public void ReleaseCustomer()
        {
            _currentCustomer = null;
            _isOccupiedByCustomer = false;
            CancelOrder();
        }

        /// <summary>
        /// Tạo đơn hàng mới tại ô này và gửi vào OrderManager trung tâm.
        /// </summary>
        public void CreateOrder(BlockDataSO blockData, double price)
        {
            if (blockData == null) return;

            _currentOrderBlock = blockData;
            _currentOrderPrice = price;
            _hasActiveOrder = true;

            // Đẩy đơn hàng vào OrderManager trung tâm (Decoupled 100% khỏi trạm mỏ)
            _activeTicket = OrderManager.Instance.CreateOrder(this, blockData, price);

            OnOrderPlaced?.Invoke(blockData);
            Debug.Log($"[CustomerSlot - {name}] 🔔 Khách gọi đơn: 1 [{blockData.DisplayName}] - Giá {price} Vàng!");
        }

        /// <summary>
        /// Tự động sinh đơn hàng tiếp theo cho ô này (dùng cho test MVP độc lập trên từng slot).
        /// </summary>
        public void TryGenerateNextOrder()
        {
            if (_activeTicket != null && _activeTicket.IsActive) return;

            BlockDataSO targetBlock = _currentOrderBlock != null ? _currentOrderBlock : _defaultOrderBlock;

#if UNITY_EDITOR
            // Fallback tìm asset block đất mặc định trong Editor nếu chưa gán
            if (targetBlock == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockDataSO Block_Dirt");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    targetBlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockDataSO>(path);
                }
            }
#endif

            if (targetBlock != null)
            {
                double price = targetBlock.BaseValue > 0 ? targetBlock.BaseValue : 3;
                CreateOrder(targetBlock, price);
            }
        }

        private IEnumerator RoutineAutoNextOrder()
        {
            yield return new WaitForSeconds(_orderInterval);
            if (!HasActiveOrder)
            {
                TryGenerateNextOrder();
            }
        }

        /// <summary>
        /// Kiểm tra xem nhân viên/người chơi có đang đứng đúng vị trí phục vụ (ServerAnchor) của ô này không.
        /// Yêu cầu khoảng cách <= maxRadius VÀ phải gần ServerAnchor hơn CustomerAnchor.
        /// </summary>
        public bool IsPlayerAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f)
        {
            if (_serverAnchor == null) return false;

            float distServerSqr = (_serverAnchor.position - playerPosition).sqrMagnitude;
            if (distServerSqr > maxRadius * maxRadius) return false;

            if (_customerAnchor != null)
            {
                float distCustSqr = (_customerAnchor.position - playerPosition).sqrMagnitude;
                if (distServerSqr >= distCustSqr)
                {
                    // Đang đứng ở phía khách hàng -> Không được giao hàng!
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Kiểm tra xem block trên tay có đúng loại mà ô này đang cần không.
        /// </summary>
        public bool CanFulfillWith(BlockDataSO carriedBlockData)
        {
            if (!HasActiveOrder || carriedBlockData == null) return false;
            return carriedBlockData == _currentOrderBlock;
        }

        /// <summary>
        /// Thực hiện giao hàng trực tiếp cho khách tại ô này:
        /// Tiêu thụ block, bắn sự kiện thanh toán tiền vàng và hoàn tất đơn hàng.
        /// </summary>
        public bool TryDeliverOrder(GameObject carriedBlockInstance, BlockDataSO carriedBlockData, OrderDeliveredEventChannelSO orderDeliveredChannel)
        {
            if (!CanFulfillWith(carriedBlockData)) return false;

            // 1. Tiêu hủy block thành phẩm trên tay
            if (carriedBlockInstance != null)
            {
                Destroy(carriedBlockInstance);
            }

            // 2. Bắn sự kiện thanh toán qua kênh OrderDeliveredEventChannelSO để EconomyManager cộng vàng
            if (orderDeliveredChannel != null)
            {
                OrderDeliveryPayload payload = new OrderDeliveryPayload(
                    stallId: "CustomerCounter",
                    blockId: _currentOrderBlock.BlockId,
                    basePrice: _currentOrderPrice > 0 ? _currentOrderPrice : _currentOrderBlock.BaseValue,
                    deliveryPosition: CustomerAnchor.position,
                    isVipCustomer: false
                );
                orderDeliveredChannel.RaiseEvent(payload);
            }

            // 3. Hoàn tất đơn hàng và giải phóng trạng thái order của slot
            if (_activeTicket != null && OrderManager.Instance != null)
            {
                OrderManager.Instance.CompleteOrder(_activeTicket);
                _activeTicket = null;
            }

            _hasActiveOrder = false;
            BlockDataSO completedBlock = _currentOrderBlock;
            double payout = _currentOrderPrice > 0 ? _currentOrderPrice : (_currentOrderBlock != null ? _currentOrderBlock.BaseValue : 3);
            _currentOrderBlock = null;
            _currentOrderPrice = 0;

            OnOrderCompleted?.Invoke();
            Debug.Log($"[CustomerSlot - {name}] ✅ Giao hàng thành công! Đã nhận {payout} Vàng từ khách!");

            // 4. Tự động sinh đơn tiếp theo nếu bật Auto Generate cho ô này
            if (_autoGenerateOrders)
            {
                StartCoroutine(RoutineAutoNextOrder());
            }

            return true;
        }

        /// <summary>
        /// Hủy đơn hàng tại ô này (khi khách rời đi).
        /// </summary>
        public void CancelOrder()
        {
            if (_activeTicket != null && OrderManager.Instance != null)
            {
                OrderManager.Instance.CancelOrder(_activeTicket);
                _activeTicket = null;
            }

            _hasActiveOrder = false;
            _currentOrderBlock = null;
            _currentOrderPrice = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_defaultOrderBlock == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockDataSO Block_Dirt");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _defaultOrderBlock = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockDataSO>(path);
                }
            }
        }

        [ContextMenu("Debug: Create Test Order")]
        private void DebugCreateTestOrder()
        {
            TryGenerateNextOrder();
            Debug.Log($"[CustomerSlot - {name}] Đã tạo đơn hàng test cho ô này.");
        }

        [ContextMenu("Debug: Cancel Order")]
        private void DebugCancelOrder()
        {
            CancelOrder();
            Debug.Log($"[CustomerSlot - {name}] Đã hủy đơn hàng trên ô này.");
        }
#endif

        private void OnDrawGizmos()
        {
            // Vẽ vị trí đứng của khách hàng (Màu vàng sáng)
            if (_customerAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
                Gizmos.DrawWireSphere(_customerAnchor.position, 0.3f);

                // Nếu đang có đơn hàng: Vẽ hộp lập phương màu xanh lá trên đầu vị trí khách đứng
                if (HasActiveOrder)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(_customerAnchor.position + Vector3.up * 1f, Vector3.one * 0.4f);
                }
            }

            // Vẽ vị trí đứng phục vụ của nhân viên/người chơi (Màu tím/hồng)
            if (_serverAnchor != null)
            {
                Gizmos.color = new Color(0.9f, 0.3f, 0.9f, 0.8f);
                Gizmos.DrawWireSphere(_serverAnchor.position, 0.3f);
            }

            // Vẽ đường nối giữa khách và nhân viên phục vụ của slot này
            if (_customerAnchor != null && _serverAnchor != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(_customerAnchor.position, _serverAnchor.position);
            }

            // Vẽ vị trí xuất hiện tiền Tip trên mặt bàn (Màu vàng cam rực rỡ)
            if (_tipAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.85f);
                Gizmos.DrawWireCube(_tipAnchor.position, new Vector3(0.35f, 0.08f, 0.35f));
            }
        }
    }
}
