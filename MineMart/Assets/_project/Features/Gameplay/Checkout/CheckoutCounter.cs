using System.Collections.Generic;
using MineMart.Data;
using MineMart.Gameplay.StackingBuffer;
using UnityEngine;

namespace MineMart.Gameplay.Checkout {
    /// <summary>
    /// Quản lý Quầy Thu Ngân Tổng (Mặt Tiền Cửa Hàng).
    /// Quản lý tập hợp các ô CounterSlot độc lập trên mặt bàn.
    /// Tách rời việc tìm ô ra khỏi cái bàn:
    /// - Cho phép Người Chơi tự do đặt vào bất kỳ ô nào họ đang đứng trước mặt.
    /// - Cung cấp API cho NPC AI tìm đường ngắn nhất đến ô còn trống (GetClosestAvailableSlot).
    /// </summary>
    public class CheckoutCounter : MonoBehaviour {
        [Header("Counter Slots")]
        [Tooltip("Danh sách các ô đặt hàng độc lập. Nếu để trống sẽ tự động tìm trong children hoặc tự cấu hình")]
        [SerializeField] private CounterSlot[] _slots;

        // Component CounterBuffer cũ (được ẩn đi để tránh gây rối Inspector, code tự nhận diện ngầm nếu có)
        [SerializeField, HideInInspector] private CounterBuffer _counterBuffer;

        // Điểm neo cũ từ thiết kế 1 quầy duy nhất (ẩn đi để tránh gây rối Inspector)
        [SerializeField, HideInInspector] private Transform _customerQueueAnchor;
        [SerializeField, HideInInspector] private Transform _workerDropOffAnchor;

        public IReadOnlyList<CounterSlot> Slots => _slots;
        public int SlotCount => _slots != null ? _slots.Length : 0;
        public CounterBuffer Buffer => _counterBuffer;
        public Transform CustomerQueueAnchor => _customerQueueAnchor != null ? _customerQueueAnchor : transform;
        public Transform WorkerDropOffAnchor => _workerDropOffAnchor != null ? _workerDropOffAnchor : transform;

        public bool HasBlocks {
            get {
                if (_slots != null && _slots.Length > 0) {
                    for (int i = 0; i < _slots.Length; i++) {
                        if (_slots[i] != null && !_slots[i].IsEmpty) return true;
                    }
                    return false;
                }
                return _counterBuffer != null && !_counterBuffer.IsEmpty;
            }
        }

        public bool IsEmpty => !HasBlocks;

        public bool IsFull {
            get {
                if (_slots != null && _slots.Length > 0) {
                    for (int i = 0; i < _slots.Length; i++) {
                        if (_slots[i] != null && !_slots[i].IsFull) return false;
                    }
                    return true;
                }
                return _counterBuffer != null && _counterBuffer.IsFull;
            }
        }

        // Quản lý danh sách quầy trung chuyển toàn cục để PlayerController tương tác trực tiếp
        private static readonly List<CheckoutCounter> s_allCounters = new List<CheckoutCounter>();
        public static IReadOnlyList<CheckoutCounter> AllCounters => s_allCounters;

        private void OnEnable() {
            if (!s_allCounters.Contains(this)) {
                s_allCounters.Add(this);
            }
        }

        private void OnDisable() {
            s_allCounters.Remove(this);
        }

        private void Reset() {
            _counterBuffer = GetComponent<CounterBuffer>();
            _slots = GetComponentsInChildren<CounterSlot>();
        }

        private void Awake() {
            InitializeSlots();
        }

        private void InitializeSlots() {
            if (_counterBuffer == null) {
                _counterBuffer = GetComponent<CounterBuffer>();
            }

            if (_slots == null || _slots.Length == 0) {
                _slots = GetComponentsInChildren<CounterSlot>();
            }

            // Nếu trong Scene chưa có GameObject con nào gắn CounterSlot nhưng đã có _counterBuffer cũ:
            // Tự động khởi tạo CounterSlot trên từng điểm neo mặt bàn để đảm bảo tương thích 100%!
            if ((_slots == null || _slots.Length == 0) && _counterBuffer != null) {
                AutoSetupSlotsFromLegacyBuffer();
            }
        }

        /// <summary>
        /// Tự động tạo CounterSlot từ các điểm neo SlotAnchor cũ của CounterBuffer nếu chưa được cấu hình thủ công.
        /// </summary>
        private void AutoSetupSlotsFromLegacyBuffer() {
            List<Transform> legacyAnchors = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);
                if (child != _customerQueueAnchor && child != _workerDropOffAnchor) {
                    legacyAnchors.Add(child);
                }
            }

            if (legacyAnchors.Count == 0) {
                legacyAnchors.Add(transform);
            }

            List<CounterSlot> createdSlots = new List<CounterSlot>();

            for (int i = 0; i < legacyAnchors.Count; i++) {
                Transform anchor = legacyAnchors[i];
                CounterSlot slot = anchor.GetComponent<CounterSlot>();
                if (slot == null) {
                    slot = anchor.gameObject.AddComponent<CounterSlot>();
                }

                // Tạo Điểm Đặt Hàng dưới sàn phía sau bàn (nơi Thợ Mỏ/Steve đứng đặt block từ mỏ lên)
                Transform placePoint = anchor.Find("PlacePoint");
                if (placePoint == null) {
                    GameObject pObj = new GameObject("PlacePoint");
                    pObj.transform.SetParent(anchor);
                    pObj.transform.localPosition = new Vector3(0f, -anchor.localPosition.y, -0.75f);
                    placePoint = pObj.transform;
                }

                // Tạo Điểm Lấy Hàng dưới sàn phía trước bàn (nơi Nhân Viên Shop đến rút block ra mang cho khách)
                Transform takePoint = anchor.Find("TakePoint");
                if (takePoint == null) {
                    GameObject tObj = new GameObject("TakePoint");
                    tObj.transform.SetParent(anchor);
                    tObj.transform.localPosition = new Vector3(0f, -anchor.localPosition.y, 0.75f);
                    takePoint = tObj.transform;
                }

                slot.ConfigureAnchors(anchor, placePoint, takePoint);
                createdSlots.Add(slot);
            }

            _slots = createdSlots.ToArray();
        }

        /// <summary>
        /// Dành cho NPC AI (Thợ Mỏ): Tìm ô CounterSlot còn trống (!IsFull) 
        /// có Điểm Đặt Hàng (PlaceAnchor) gần vị trí fromPosition nhất để đến cất block.
        /// </summary>
        public CounterSlot GetClosestAvailableSlot(Vector3 fromPosition) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot != null && slot.CanAcceptBlock) {
                    float distSqr = (slot.PlaceAnchor.position - fromPosition).sqrMagnitude;
                    if (distSqr < minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        bestSlot = slot;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Nhân Viên Cửa Hàng: Tìm ô có sẵn block (!IsEmpty) 
        /// có Điểm Lấy Hàng (TakeAnchor) gần vị trí fromPosition nhất để rút block ra.
        /// </summary>
        public CounterSlot GetClosestSlotWithBlocks(Vector3 fromPosition) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot != null && !slot.IsEmpty) {
                    float distSqr = (slot.TakeAnchor.position - fromPosition).sqrMagnitude;
                    if (distSqr < minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        bestSlot = slot;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Người Chơi: Tìm ô CounterSlot mà người chơi đang đứng trước mặt 
        /// (khoảng cách tới điểm tương tác <= maxRadius) để đặt block vào đúng ô đó.
        /// </summary>
        public CounterSlot GetSlotNearPosition(Vector3 playerPosition, float maxRadius) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = maxRadius * maxRadius;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot != null && slot.CanAcceptBlock) {
                    // Cho phép người chơi tiếp cận từ bất kỳ hướng nào: Phía Đặt (Mỏ), Phía Lấy (Shop), hoặc sát Mặt Bàn
                    float distPlace = (slot.PlaceAnchor.position - playerPosition).sqrMagnitude;
                    float distTake = (slot.TakeAnchor.position - playerPosition).sqrMagnitude;
                    float distTable = (slot.TableAnchor.position - playerPosition).sqrMagnitude;
                    float distSqr = Mathf.Min(distPlace, distTake, distTable);

                    if (distSqr <= minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        bestSlot = slot;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Thợ/Player: Tìm ô CounterSlot gần nhất có thể tiếp nhận loại block này (ô trống hoặc đang chứa cùng loại block).
        /// </summary>
        public CounterSlot GetClosestAvailableSlotForBlock(Vector3 fromPosition, BlockDataSO blockData) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot != null && slot.CanAcceptBlockType(blockData)) {
                    float distSqr = (slot.InteractionAnchor.position - fromPosition).sqrMagnitude;
                    if (distSqr < minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        bestSlot = slot;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Nhân Viên Shop: Tìm ô đang có sẵn đúng loại block mà khách đang cần để đến lấy giao hàng.
        /// </summary>
        public CounterSlot GetClosestSlotWithBlockType(Vector3 fromPosition, BlockDataSO blockData) {
            if (_slots == null || _slots.Length == 0 || blockData == null) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot != null && !slot.IsEmpty && slot.CurrentBlockType == blockData) {
                    float distSqr = (slot.TakeAnchor.position - fromPosition).sqrMagnitude;
                    if (distSqr < minDistanceSqr) {
                        minDistanceSqr = distSqr;
                        bestSlot = slot;
                    }
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Kiểm tra trên toàn bộ quầy trung chuyển xem hiện có sẵn block thuộc loại này không.
        /// </summary>
        public bool HasBlockType(BlockDataSO blockData) {
            if (_slots == null || _slots.Length == 0 || blockData == null) return false;
            for (int i = 0; i < _slots.Length; i++) {
                if (_slots[i] != null && !_slots[i].IsEmpty && _slots[i].CurrentBlockType == blockData) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Đặt block lên ô phù hợp kèm thông tin loại block.
        /// </summary>
        public bool TryPlaceBlock(GameObject blockInstance, BlockDataSO blockData, out Vector3 targetPosition) {
            if (_slots != null && _slots.Length > 0) {
                for (int i = 0; i < _slots.Length; i++) {
                    if (_slots[i] != null && _slots[i].CanAcceptBlockType(blockData)) {
                        return _slots[i].TryPlaceBlock(blockInstance, blockData, out targetPosition);
                    }
                }
            }

            if (_counterBuffer != null) {
                return _counterBuffer.TryPlaceBlock(blockInstance, out targetPosition);
            }

            targetPosition = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Rút đúng loại block mà khách hàng đang order từ quầy trung chuyển.
        /// </summary>
        public bool TryTakeBlockOfType(BlockDataSO requestedType, out GameObject blockInstance) {
            if (_slots != null && _slots.Length > 0 && requestedType != null) {
                for (int i = 0; i < _slots.Length; i++) {
                    if (_slots[i] != null && !_slots[i].IsEmpty && _slots[i].CurrentBlockType == requestedType) {
                        return _slots[i].TryTakeBlock(out blockInstance, out _);
                    }
                }
            }

            blockInstance = null;
            return false;
        }

        /// <summary>
        /// Overload tương thích ngược: Đặt block lên ô phù hợp.
        /// </summary>
        public bool TryPlaceBlock(GameObject blockInstance, out Vector3 targetPosition) {
            return TryPlaceBlock(blockInstance, null, out targetPosition);
        }

        /// <summary>
        /// Khách hàng lấy block từ quầy thu ngân mang về.
        /// </summary>
        public bool TryTakeBlock(out GameObject blockInstance) {
            if (_slots != null && _slots.Length > 0) {
                for (int i = 0; i < _slots.Length; i++) {
                    if (_slots[i] != null && !_slots[i].IsEmpty) {
                        return _slots[i].TryTakeBlock(out blockInstance);
                    }
                }
            }

            if (_counterBuffer != null) {
                return _counterBuffer.TryTakeBlock(out blockInstance);
            }

            blockInstance = null;
            return false;
        }

        /// <summary>
        /// Dành cho Thợ / Đầu bếp ĐẶT HÀNG: Tìm ô CounterSlot mà người chơi đang đứng trước mặt ở PHÍA ĐẶT (PlaceAnchor - Phía Mỏ).
        /// Tuyệt đối không cho phép đặt khi đang đứng ở phía lấy hàng (TakeAnchor).
        /// </summary>
        public CounterSlot GetSlotAtPlacePosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = maxRadius * maxRadius;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot == null) continue;

                // CHỈ đo khoảng cách tới PlaceAnchor (Phía Mỏ)
                float distSqr = (slot.PlaceAnchor.position - playerPosition).sqrMagnitude;

                if (distSqr <= minDistanceSqr) {
                    minDistanceSqr = distSqr;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Nhân Viên / Phục Vụ LẤY HÀNG: Tìm ô CounterSlot mà người chơi đang đứng trước mặt ở PHÍA LẤY (TakeAnchor - Phía Shop).
        /// Tuyệt đối không cho phép lấy khi đang đứng ở phía đặt hàng (PlaceAnchor).
        /// </summary>
        public CounterSlot GetSlotAtTakePosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_slots == null || _slots.Length == 0) return null;

            CounterSlot bestSlot = null;
            float minDistanceSqr = maxRadius * maxRadius;

            for (int i = 0; i < _slots.Length; i++) {
                CounterSlot slot = _slots[i];
                if (slot == null) continue;

                // CHỈ đo khoảng cách tới TakeAnchor (Phía Shop)
                float distSqr = (slot.TakeAnchor.position - playerPosition).sqrMagnitude;

                if (distSqr <= minDistanceSqr) {
                    minDistanceSqr = distSqr;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Dành cho Người Chơi Phục Vụ: CHỈ lấy block từ ô nếu người chơi đang đứng đúng ở PHÍA LẤY (TakeAnchor).
        /// Tuyệt đối không cho lấy nếu đang đứng ở phía đặt hàng (PlaceAnchor).
        /// </summary>
        public bool TryTakeBlockAtTakeAnchor(Vector3 playerPosition, float maxRadius, out GameObject blockInstance, out BlockDataSO blockData) {
            CounterSlot slot = GetSlotAtTakePosition(playerPosition, maxRadius);
            if (slot != null && !slot.IsEmpty) {
                return slot.TryTakeBlock(out blockInstance, out blockData);
            }

            blockInstance = null;
            blockData = null;
            return false;
        }

        /// <summary>
        /// Tương thích ngược: Tìm ô gần nhất tới người chơi.
        /// </summary>
        public CounterSlot GetClosestSlotToPlayer(Vector3 playerPosition, float maxRadius = 0.85f) {
            return GetSlotAtPlacePosition(playerPosition, maxRadius) ?? GetSlotAtTakePosition(playerPosition, maxRadius);
        }

        /// <summary>
        /// Tương thích ngược cho TryTakeBlockFromSpecificSlot.
        /// </summary>
        public bool TryTakeBlockFromSpecificSlot(Vector3 playerPosition, float maxRadius, out GameObject blockInstance, out BlockDataSO blockData) {
            return TryTakeBlockAtTakeAnchor(playerPosition, maxRadius, out blockInstance, out blockData);
        }

        /// <summary>
        /// Dành riêng cho NPC Runner AI: Tìm và lấy đúng loại block theo đường đi đã được điều hướng.
        /// </summary>
        public bool TryTakeBlockForOrder(BlockDataSO requestedType, out GameObject blockInstance, out BlockDataSO blockData) {
            if (_slots != null && _slots.Length > 0 && requestedType != null) {
                for (int i = 0; i < _slots.Length; i++) {
                    if (_slots[i] != null && !_slots[i].IsEmpty && _slots[i].CurrentBlockType == requestedType) {
                        return _slots[i].TryTakeBlock(out blockInstance, out blockData);
                    }
                }
            }

            blockInstance = null;
            blockData = null;
            return false;
        }

        private void OnDrawGizmosSelected() {
            // Vẽ vị trí đứng của khách hàng (Màu vàng)
            if (_customerQueueAnchor != null) {
                Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.7f);
                Gizmos.DrawWireSphere(_customerQueueAnchor.position, 0.4f);
            }

            // Vẽ vị trí đứng giao hàng của thợ (Màu xanh dương)
            if (_workerDropOffAnchor != null) {
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.7f);
                Gizmos.DrawWireSphere(_workerDropOffAnchor.position, 0.4f);
            }
        }
    }
}
