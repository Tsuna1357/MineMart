using System;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using UnityEngine;

namespace MineMart.Gameplay.Tables {
    /// <summary>
    /// Quản lý Bàn Ăn / Bàn Tiếp Khách (Table Counter / Dining Table).
    /// Hỗ trợ cả BÀN ĐƠN (1 ghế) lẫn BÀN LỚN NHIỀU GHẾ (Multi-Seat Table: 2, 4, 6 ghế).
    /// Khác biệt với CustomerCounter (quầy thu ngân/order):
    /// - TUYỆT ĐỐI KHÔNG CÓ HÀNG ĐỢI XẾP HÀNG (NO QUEUE).
    /// - Mỗi ghế / TableSlot chỉ tiếp nhận đúng 1 khách.
    /// - Khi tất cả các ghế đã có người ngồi (IsOccupied), bàn được coi là KÍN CHỖ.
    /// - Khách mới đến nếu thấy bàn kín chỗ sẽ không xếp hàng sau lưng khách cũ mà tìm bàn khác hoặc chờ.
    /// </summary>
    public class TableCounter : MonoBehaviour {
        [Header("Table Slots (Các Ghế / Ô Bàn)")]
        [Tooltip("Danh sách các ô/ghế ngồi độc lập trên bàn. Nếu để trống sẽ tự động tìm trong children")]
        [SerializeField] private TableSlot[] _slots;

        [Header("Legacy Anchors (Fallback nếu là bàn đơn chưa tạo TableSlot con)")]
        [Tooltip("Điểm neo nơi khách ngồi (dành cho bàn 1 ghế)")]
        [SerializeField] private Transform _customerAnchor;

        [Tooltip("Điểm neo nơi nhân viên đứng phục vụ")]
        [SerializeField] private Transform _serverAnchor;

        [Header("Event Channels")]
        [Tooltip("Kênh phát tín hiệu khi đơn hàng được giao thành công cho khách tại bàn")]
        [SerializeField] private OrderDeliveredEventChannelSO _orderDeliveredChannel;

        public IReadOnlyList<TableSlot> Slots => _slots;
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

        /// <summary>
        /// Bàn đang có ít nhất một đơn hàng cần phục vụ hay không.
        /// </summary>
        public bool HasActiveOrder {
            get {
                if (_slots != null && _slots.Length > 0) {
                    for (int i = 0; i < _slots.Length; i++) {
                        if (_slots[i] != null && _slots[i].HasActiveOrder) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Bàn còn ít nhất một chỗ ngồi/ghế trống hay không.
        /// </summary>
        public bool HasAvailableSlot => GetAvailableSlot() != null;

        public event Action<BlockDataSO> OnOrderPlaced;
        public event Action OnOrderCompleted;

        // Quản lý danh sách tất cả các bàn đang hoạt động trong toàn bộ cửa hàng
        private static readonly List<TableCounter> s_allTables = new List<TableCounter>();
        public static IReadOnlyList<TableCounter> AllTables => s_allTables;

        private void OnEnable() {
            if (!s_allTables.Contains(this)) {
                s_allTables.Add(this);
            }
        }

        private void OnDisable() {
            s_allTables.Remove(this);
        }

        private void Reset() {
            InitializeSlots();
        }

        private void Awake() {
            InitializeSlots();
        }

        /// <summary>
        /// Khởi tạo và quét toàn bộ các ô TableSlot dưới bàn.
        /// Tự động tương thích nếu bàn đơn chỉ có CustomerAnchor và ServerAnchor.
        /// </summary>
        public void InitializeSlots() {
            if (_slots == null || _slots.Length == 0) {
                _slots = GetComponentsInChildren<TableSlot>();
            }

            // Nếu người dùng chưa tạo TableSlot con nhưng đã có các Anchor cũ:
            // Tự động tạo 1 TableSlot ngay trên GameObject này để bàn hoạt động ngay lập tức!
            if (_slots == null || _slots.Length == 0) {
                if (_customerAnchor == null) {
                    Transform found = transform.Find("CustomerAnchor");
                    if (found == null) found = transform.Find("SeatAnchor");
                    if (found != null) _customerAnchor = found;
                }
                if (_serverAnchor == null) {
                    Transform found = transform.Find("ServerAnchor");
                    if (found != null) _serverAnchor = found;
                }

                TableSlot defaultSlot = GetComponent<TableSlot>();
                if (defaultSlot == null) {
                    defaultSlot = gameObject.AddComponent<TableSlot>();
                }
                _slots = new TableSlot[] { defaultSlot };
            }

            // Đăng ký sự kiện chuyển tiếp từ các slot
            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
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

        #region Customer AI Query (Dành cho Khách Hàng Tìm Bàn Trống - KHÔNG XẾP HÀNG)

        /// <summary>
        /// Tìm ô/ghế đầu tiên trên bàn này chưa có khách ngồi (Không xếp hàng).
        /// </summary>
        public TableSlot GetAvailableSlot() {
            if (_slots == null || _slots.Length == 0) return null;

            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
                if (slot != null && !slot.IsOccupied) {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Tìm ô/ghế chưa có người ngồi gần nhất với vị trí của khách trên bàn này.
        /// </summary>
        public TableSlot GetClosestAvailableSlot(Vector3 customerPosition) {
            if (_slots == null || _slots.Length == 0) return null;

            TableSlot closestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
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

        /// <summary>
        /// Tìm ô bàn trống đầu tiên trên TOÀN BỘ các bàn ăn trong cửa hàng (Global).
        /// Khác với CustomerCounter (chọn quầy ngắn nhất): Hàm này CHỈ trả về ghế đang HOÀN TOÀN TRỐNG,
        /// nếu tất cả các bàn đều đã có người ngồi thì trả về null (KHÔNG CHO PHÉP XẾP HÀNG!).
        /// </summary>
        public static TableSlot GetAvailableSlotGlobal() {
            for (int i = 0; i < s_allTables.Count; i++) {
                TableCounter table = s_allTables[i];
                if (table != null && table.Slots != null) {
                    TableSlot available = table.GetAvailableSlot();
                    if (available != null) {
                        return available;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Tìm ô bàn trống gần nhất với khách trên TOÀN BỘ các bàn trong cửa hàng (Global).
        /// </summary>
        public static TableSlot GetClosestAvailableSlotGlobal(Vector3 customerPosition) {
            TableSlot closestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < s_allTables.Count; i++) {
                TableCounter table = s_allTables[i];
                if (table != null && table.Slots != null) {
                    for (int j = 0; j < table.Slots.Count; j++) {
                        TableSlot slot = table.Slots[j];
                        if (slot != null && !slot.IsOccupied) {
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

        #endregion

        #region Server / Player Query (Dành cho Nhân Viên / Người Chơi Phục Vụ Bàn)

        /// <summary>
        /// Tìm ô bàn mà nhân viên/người chơi đang đứng ngay trước vị trí phục vụ (ServerAnchor).
        /// </summary>
        /// <summary>
        /// Tìm ô bàn tại vị trí người chơi đang đứng phục vụ.
        /// Đặc biệt tối ưu khi NHIỀU GHẾ DÙNG CHUNG 1 SERVER ANCHOR:
        /// - Ưu tiên số 1: Trả về ô nào ĐANG CẦN NHẬN ORDER (NeedsOrderTake == true).
        /// - Ưu tiên số 2: Trả về ô nào ĐANG CÓ ĐƠN HÀNG CHỜ PHỤC VỤ (HasActiveOrder == true).
        /// - Sau đó mới trả về ô gần nhất.
        /// Giúp người chơi đứng tại 1 vị trí phục vụ chung có thể nhận order của khách thứ 2, 3, v.v. lần lượt không bị kẹt!
        /// </summary>
        public TableSlot GetSlotAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_slots == null || _slots.Length == 0) return null;

            TableSlot closestSlot = null;
            float minDistanceSqr = maxRadius * maxRadius;

            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
                if (slot != null && slot.IsPlayerAtServerPosition(playerPosition, maxRadius)) {
                    // Ưu tiên số 1: Ô đang có khách giơ tay gọi món (?)
                    if (slot.NeedsOrderTake) {
                        return slot;
                    }

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
        /// </summary>
        public bool IsPlayerAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            return GetSlotAtServerPosition(playerPosition, maxRadius) != null;
        }

        /// <summary>
        /// Kiểm tra xem tại vị trí người chơi đang đứng phục vụ, có khách ngồi bàn đang cần loại block này không.
        /// </summary>
        public bool CanFulfillWith(BlockDataSO carriedBlockData) {
            if (_slots == null || _slots.Length == 0 || carriedBlockData == null) return false;

            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
                if (slot != null && slot.CanFulfillWith(carriedBlockData)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra xem tại vị trí người chơi đang đứng phục vụ có ô nào cần loại block này không.
        /// Hỗ trợ quét qua TOÀN BỘ các ô cùng dùng chung ServerAnchor.
        /// </summary>
        public bool CanFulfillAtPosition(Vector3 playerPosition, BlockDataSO carriedBlockData, out TableSlot targetSlot) {
            targetSlot = null;
            if (_slots == null || _slots.Length == 0 || carriedBlockData == null) return false;

            for (int i = 0; i < _slots.Length; i++) {
                TableSlot slot = _slots[i];
                if (slot != null && slot.IsPlayerAtServerPosition(playerPosition, 0.85f) && slot.CanFulfillWith(carriedBlockData)) {
                    targetSlot = slot;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Giao hàng trực tiếp từ người chơi cho khách tại bàn.
        /// </summary>
        public bool TryDeliverOrder(GameObject carriedBlockInstance, BlockDataSO carriedBlockData) {
            if (carriedBlockInstance == null || carriedBlockData == null) return false;

            Vector3 playerPos = carriedBlockInstance.transform.parent != null
                ? carriedBlockInstance.transform.parent.position
                : transform.position;

            return TryDeliverOrderAtPosition(playerPos, carriedBlockInstance, carriedBlockData);
        }

        /// <summary>
        /// Giao hàng chính xác tại ô bàn mà người chơi đang đứng trước mặt ServerAnchor.
        /// </summary>
        public bool TryDeliverOrderAtPosition(Vector3 playerPosition, GameObject carriedBlockInstance, BlockDataSO carriedBlockData) {
            if (CanFulfillAtPosition(playerPosition, carriedBlockData, out TableSlot targetSlot)) {
                return targetSlot.TryDeliverOrder(carriedBlockInstance, carriedBlockData, _orderDeliveredChannel);
            }

            return false;
        }

        #endregion

        private void OnValidate() {
            InitializeSlots();
        }

        private void OnDrawGizmos() {
            // Khung viền bàn ăn
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 1.2f));

            TableSlot[] slotsToDraw = _slots;
            if (slotsToDraw == null || slotsToDraw.Length == 0) {
                slotsToDraw = GetComponentsInChildren<TableSlot>();
            }

            if (slotsToDraw != null && slotsToDraw.Length > 0) {
                for (int i = 0; i < slotsToDraw.Length; i++) {
                    TableSlot slot = slotsToDraw[i];
                    if (slot != null) {
                        // Tia nối từ tâm bàn tới vị trí ngồi của khách
                        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
                        Gizmos.DrawLine(transform.position + Vector3.up * 0.4f, slot.CustomerAnchor.position);

                        // Khung hình tròn vị trí ngồi của Khách: Vàng khi trống, Xanh lá khi có khách
                        Color custColor = slot.IsOccupied ? new Color(0.1f, 0.9f, 0.3f, 0.9f) : new Color(1f, 0.85f, 0.1f, 0.85f);
                        DrawGroundCircle(slot.CustomerAnchor.position, 0.35f, custColor);
                        Gizmos.color = custColor;
                        Gizmos.DrawWireSphere(slot.CustomerAnchor.position, 0.25f);

                        // Hướng nhìn chuẩn của khách vào bàn
                        Vector3 facingDir = slot.GetCustomerFacingDirection();
                        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                        Gizmos.DrawRay(slot.CustomerAnchor.position + Vector3.up * 0.05f, facingDir * 0.45f);

                        // Khung hình tròn vị trí đứng phục vụ của Nhân Viên: Màu tím / hồng đặc trưng
                        Color servColor = new Color(0.9f, 0.3f, 0.9f, 0.85f);
                        DrawGroundCircle(slot.ServerAnchor.position, 0.35f, servColor);
                        Gizmos.color = servColor;
                        Gizmos.DrawWireSphere(slot.ServerAnchor.position, 0.25f);
                        Gizmos.DrawRay(slot.ServerAnchor.position + Vector3.up * 0.05f, slot.ServerAnchor.forward * 0.45f);

                        // Điểm LookAtAnchor nếu có
                        if (slot.LookAtAnchor != null) {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawWireSphere(slot.LookAtAnchor.position, 0.12f);
                            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
                            Gizmos.DrawLine(slot.CustomerAnchor.position + Vector3.up * 0.05f, slot.LookAtAnchor.position);
                        }

                        // Tia nối giữa khách và nhân viên phục vụ của slot đó
                        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                        Gizmos.DrawLine(slot.CustomerAnchor.position, slot.ServerAnchor.position);

                        // Hộp vị trí tiền tip
                        if (slot.TipAnchor != null) {
                            Gizmos.color = new Color(1f, 0.7f, 0f, 0.85f);
                            Gizmos.DrawWireCube(slot.TipAnchor.position, new Vector3(0.35f, 0.08f, 0.35f));
                        }
                    }
                }
            } else {
                // Fallback nếu là bàn đơn chưa có TableSlot con
                if (_customerAnchor != null) {
                    Color custColor = new Color(1f, 0.85f, 0.1f, 0.85f);
                    DrawGroundCircle(_customerAnchor.position, 0.35f, custColor);
                    Gizmos.color = custColor;
                    Gizmos.DrawWireSphere(_customerAnchor.position, 0.25f);
                    Gizmos.DrawRay(_customerAnchor.position + Vector3.up * 0.05f, _customerAnchor.forward * 0.45f);
                }

                if (_serverAnchor != null) {
                    Color servColor = new Color(0.9f, 0.3f, 0.9f, 0.85f);
                    DrawGroundCircle(_serverAnchor.position, 0.35f, servColor);
                    Gizmos.color = servColor;
                    Gizmos.DrawWireSphere(_serverAnchor.position, 0.25f);
                    Gizmos.DrawRay(_serverAnchor.position + Vector3.up * 0.05f, _serverAnchor.forward * 0.45f);
                }
            }
        }

        private void DrawGroundCircle(Vector3 center, float radius, Color color, int segments = 24) {
            Gizmos.color = color;
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0.02f, 0f);
            for (int i = 1; i <= segments; i++) {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0.02f, Mathf.Sin(rad) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}

