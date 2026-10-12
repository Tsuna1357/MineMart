using System;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using UnityEngine;

namespace MineMart.Gameplay.Checkout {
    /// <summary>
    /// Quản lý Quầy Tiếp Khách / Quầy Phục Vụ Khách Hàng (Customer Service Counter).
    /// Hỗ trợ cả BÀN ĐƠN LẺ lẫn BÀN DÀI NHIỀU Ô (Multi-Slot Counter).
    /// Quản lý tập hợp các CustomerSlot độc lập trên mặt bàn:
    /// - Khách hàng (NPC Customer AI) tự tìm đến ô chưa có khách đứng (GetAvailableSlot).
    /// - Nhân viên / Người chơi phục vụ đứng ở ô nào sẽ giao hàng trực tiếp cho khách ở ô đó.
    /// Tương thích ngược 100% với các scene thiết lập 1 quầy duy nhất.
    /// </summary>
    public class CustomerCounter : MonoBehaviour {
        [Header("Customer Slots")]
        [Tooltip("Danh sách các ô tiếp khách độc lập. Nếu để trống sẽ tự động tìm trong children")]
        [SerializeField] private CustomerSlot[] _slots;

        [Header("Legacy Anchors (Tự động chuyển đổi nếu không chia ô)")]
        [Tooltip("Điểm neo nơi khách hàng đứng (dành cho bàn 1 khách)")]
        [SerializeField] private Transform _customerAnchor;

        [Tooltip("Điểm neo nơi nhân viên đứng phục vụ (dành cho bàn 1 khách)")]
        [SerializeField] private Transform _serverAnchor;

        [Header("Tip Gold Particle Effect (Hiệu Ứng Hạt Vàng Tỏa Ra)")]
        [Tooltip("Prefab model tùy biến cho các hạt vàng tỏa ra tại quầy khi giao block cho khách (nếu các slot con không gán riêng, sẽ dùng prefab này)")]
        [SerializeField] private GameObject _tipParticlePrefab;

        [Header("Event Channels")]
        [Tooltip("Kênh phát tín hiệu khi đơn hàng được giao thành công cho khách")]
        [SerializeField] private OrderDeliveredEventChannelSO _orderDeliveredChannel;

        public GameObject TipParticlePrefab => _tipParticlePrefab;
        public IReadOnlyList<CustomerSlot> Slots => _slots;
        public int SlotCount => _slots != null ? _slots.Length : 0;
        public OrderDeliveredEventChannelSO OrderDeliveredChannel => _orderDeliveredChannel;

        public Transform CustomerAnchor {
            get {
                if (_slots != null && _slots.Length > 0 && _slots[0] != null) return _slots[0].CustomerAnchor;
                return _customerAnchor != null ? _customerAnchor : transform;
            }
        }

        public Transform ServerAnchor {
            get {
                if (_slots != null && _slots.Length > 0 && _slots[0] != null) return _slots[0].ServerAnchor;
                return _serverAnchor != null ? _serverAnchor : transform;
            }
        }

        public bool HasActiveOrder {
            get {
                if (_slots != null && _slots.Length > 0) {
                    for (int i = 0; i < _slots.Length; i++) {
                        if (_slots[i] != null && _slots[i].HasActiveOrder) return true;
                    }
                    return false;
                }
                return false;
            }
        }

        public bool HasAvailableSlot => GetAvailableSlot() != null;

        public event Action<BlockDataSO> OnOrderPlaced;
        public event Action OnOrderCompleted;

        // Quản lý danh sách quầy đang active để PlayerController tìm kiếm bằng Anchor
        private static readonly List<CustomerCounter> s_allCounters = new List<CustomerCounter>();
        public static IReadOnlyList<CustomerCounter> AllCounters => s_allCounters;

        private void OnEnable() {
            if (!s_allCounters.Contains(this)) {
                s_allCounters.Add(this);
            }
        }

        private void OnDisable() {
            s_allCounters.Remove(this);
        }

        private void Reset() {
            InitializeSlots();
        }

        private void Awake() {
            InitializeSlots();
        }



        /// <summary>
        /// Khởi tạo và quét toàn bộ các ô CustomerSlot dưới bàn.
        /// Tự động tương thích nếu Scene cũ chỉ có CustomerAnchor và ServerAnchor đơn lẻ.
        /// </summary>
        public void InitializeSlots() {
            if (_slots == null || _slots.Length == 0) {
                _slots = GetComponentsInChildren<CustomerSlot>();
            }

            // Nếu người dùng chưa tạo CustomerSlot con nhưng đã có các Anchor cũ:
            // Tự động tạo 1 CustomerSlot ngay trên GameObject này để bàn hoạt động ngay lập tức!
            if ((_slots == null || _slots.Length == 0)) {
                if (_customerAnchor == null) {
                    Transform found = transform.Find("CustomerAnchor");
                    if (found != null) _customerAnchor = found;
                }
                if (_serverAnchor == null) {
                    Transform found = transform.Find("ServerAnchor");
                    if (found != null) _serverAnchor = found;
                }

                CustomerSlot defaultSlot = GetComponent<CustomerSlot>();
                if (defaultSlot == null) {
                    defaultSlot = gameObject.AddComponent<CustomerSlot>();
                }
                _slots = new CustomerSlot[] { defaultSlot };
            }

            // Đăng ký sự kiện chuyển tiếp từ các slot
            for (int i = 0; i < _slots.Length; i++) {
                CustomerSlot slot = _slots[i];
                if (slot != null) {
                    slot.OnOrderPlaced -= ForwardOrderPlaced;
                    slot.OnOrderPlaced += ForwardOrderPlaced;

                    slot.OnOrderCompleted -= ForwardOrderCompleted;
                    slot.OnOrderCompleted += ForwardOrderCompleted;
                }
            }

#if UNITY_EDITOR
            if (_orderDeliveredChannel == null) {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:OrderDeliveredEventChannelSO Event_OrderDelivered");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _orderDeliveredChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<OrderDeliveredEventChannelSO>(path);
                }
            }
#endif
        }

        private void ForwardOrderPlaced(BlockDataSO block) => OnOrderPlaced?.Invoke(block);
        private void ForwardOrderCompleted() => OnOrderCompleted?.Invoke();

        #region Customer AI Query (Dành cho Khách Hàng)

        /// <summary>
        /// Tìm ô đầu tiên trên bàn chưa có khách đứng chiếm chỗ.
        /// </summary>
        public CustomerSlot GetAvailableSlot() {
            if (_slots == null || _slots.Length == 0) return null;

            for (int i = 0; i < _slots.Length; i++) {
                CustomerSlot slot = _slots[i];
                if (slot != null && !slot.IsOccupied) {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Tìm ô có hàng đợi ngắn nhất trên toàn bộ các quầy tiếp khách (Global).
        /// Dành cho CustomerAgent mới sinh ra tìm chỗ xếp hàng.
        /// </summary>
        public static CustomerSlot GetShortestQueueSlotGlobal() {
            CustomerSlot bestSlot = null;
            int minQueue = int.MaxValue;

            for (int i = 0; i < s_allCounters.Count; i++) {
                CustomerCounter counter = s_allCounters[i];
                if (counter != null && counter.gameObject.activeInHierarchy && counter.Slots != null) {
                    for (int j = 0; j < counter.Slots.Count; j++) {
                        CustomerSlot slot = counter.Slots[j];
                        if (slot != null) {
                            int count = slot.QueueCount;
                            if (count < minQueue) {
                                minQueue = count;
                                bestSlot = slot;
                            }
                        }
                    }
                }
            }
            return bestSlot;
        }

        /// <summary>
        /// Tìm ô quầy tiếp khách ĐANG HOÀN TOÀN TRỐNG (QueueCount == 0, chưa có khách đứng) gần nhất trên TOÀN BỘ các quầy trong tiệm (Global).
        /// Dùng khi khách ưu tiên tìm quầy trống trực tiếp trước khi tính đến bàn ăn hay xếp hàng.
        /// </summary>
        public static CustomerSlot GetClosestAvailableSlotGlobal(Vector3 customerPosition) {
            CustomerSlot closestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < s_allCounters.Count; i++) {
                CustomerCounter counter = s_allCounters[i];
                if (counter != null && counter.gameObject.activeInHierarchy && counter.Slots != null) {
                    for (int j = 0; j < counter.Slots.Count; j++) {
                        CustomerSlot slot = counter.Slots[j];
                        if (slot != null && !slot.IsOccupied) { // !IsOccupied tương đương QueueCount == 0
                            float distSqr = (slot.CustomerAnchor.position - customerPosition).sqrMagnitude;
                            if (distSqr < minDistanceSqr) {
                                minDistanceSqr = distSqr;
                                closestSlot = slot;
                            }
                        }
                    }
                }
            }

            return closestSlot;
        }

        /// <summary>
        /// Tìm ô chưa có khách đứng gần nhất với vị trí hiện tại của khách.
        /// </summary>
        public CustomerSlot GetClosestAvailableSlot(Vector3 customerPosition) {
            if (_slots == null || _slots.Length == 0) return null;

            CustomerSlot closestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                CustomerSlot slot = _slots[i];
                if (slot != null && !slot.IsOccupied) {
                    float distSqr = (slot.CustomerAnchor.position - customerPosition).sqrMagnitude;
                    if (distSqr < minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        closestSlot = slot;
                    }
                }
            }

            return closestSlot;
        }

        #endregion

        #region Server / Player Query (Dành cho Nhân Viên Phục Vụ)

        /// <summary>
        /// Tìm ô mà nhân viên/người chơi đang đứng ngay trước mặt vị trí phục vụ (ServerAnchor).
        /// </summary>
        public CustomerSlot GetSlotAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_slots == null || _slots.Length == 0) return null;

            CustomerSlot closestSlot = null;
            float minDistanceSqr = maxRadius * maxRadius;

            for (int i = 0; i < _slots.Length; i++) {
                CustomerSlot slot = _slots[i];
                if (slot != null && slot.IsPlayerAtServerPosition(playerPosition, maxRadius)) {
                    float distSqr = (slot.ServerAnchor.position - playerPosition).sqrMagnitude;
                    if (distSqr <= minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        closestSlot = slot;
                    }
                }
            }

            return closestSlot;
        }

        /// <summary>
        /// Kiểm tra xem người chơi có đang đứng ở vị trí phục vụ (ServerAnchor) của BẤT KỲ ô nào trên bàn không.
        /// Tuyệt đối không cho phép nếu đang đứng ở phía khách hàng.
        /// </summary>
        public bool IsPlayerAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            return GetSlotAtServerPosition(playerPosition, maxRadius) != null;
        }

        /// <summary>
        /// Kiểm tra xem tại vị trí người chơi đang đứng phục vụ, có khách đang cần loại block này không.
        /// </summary>
        public bool CanFulfillWith(BlockDataSO carriedBlockData) {
            if (_slots == null || _slots.Length == 0 || carriedBlockData == null) return false;

            for (int i = 0; i < _slots.Length; i++) {
                CustomerSlot slot = _slots[i];
                if (slot != null && slot.CanFulfillWith(carriedBlockData)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra xem tại slot cụ thể mà người chơi đang đứng phục vụ có cần loại block này không.
        /// </summary>
        public bool CanFulfillAtPosition(Vector3 playerPosition, BlockDataSO carriedBlockData, out CustomerSlot targetSlot) {
            targetSlot = GetSlotAtServerPosition(playerPosition);
            if (targetSlot != null && targetSlot.CanFulfillWith(carriedBlockData)) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Giao hàng trực tiếp từ người chơi cho khách tại ô mà người chơi đang đứng phục vụ.
        /// </summary>
        public bool TryDeliverOrder(GameObject carriedBlockInstance, BlockDataSO carriedBlockData) {
            if (carriedBlockInstance == null || carriedBlockData == null) return false;

            Vector3 playerPos = carriedBlockInstance.transform.parent != null
                ? carriedBlockInstance.transform.parent.position
                : transform.position;

            return TryDeliverOrderAtPosition(playerPos, carriedBlockInstance, carriedBlockData);
        }

        /// <summary>
        /// Giao hàng chính xác tại ô mà người chơi đang đứng trước mặt ServerAnchor.
        /// </summary>
        public bool TryDeliverOrderAtPosition(Vector3 playerPosition, GameObject carriedBlockInstance, BlockDataSO carriedBlockData) {
            CustomerSlot targetSlot = GetSlotAtServerPosition(playerPosition, 0.85f);
            if (targetSlot != null && targetSlot.CanFulfillWith(carriedBlockData)) {
                return targetSlot.TryDeliverOrder(carriedBlockInstance, carriedBlockData, _orderDeliveredChannel);
            }

            return false;
        }

        #endregion
    }
}
