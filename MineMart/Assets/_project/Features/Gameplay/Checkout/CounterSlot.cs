using System.Collections.Generic;
using MineMart.Data;
using UnityEngine;

namespace MineMart.Gameplay.Checkout {
    /// <summary>
    /// Đại diện cho MỘT Ô ĐẶT HÀNG ĐỘC LẬP trên quầy thu ngân.
    /// Mỗi ô có:
    /// 1. TableAnchor: Vị trí đặt block trên mặt bàn (xếp kim tự tháp 4 block).
    /// 2. InteractionAnchor: Vị trí đứng dưới sàn ngay trước ô này để người chơi hoặc NPC tương tác.
    /// Tách biệt hoàn toàn việc chọn ô ra khỏi bàn: 
    /// - Người chơi tự do đặt vào ô mình đang đứng trước mặt.
    /// - NPC AI tự tìm ô còn trống gần nhất để đến đúng vị trí tương tác của ô đó.
    /// </summary>
    public class CounterSlot : MonoBehaviour {
        [Header("Anchors")]
        [Tooltip("Điểm neo trên mặt bàn quầy nơi các khối block được xếp")]
        [SerializeField] private Transform _tableAnchor;

        [Tooltip("Điểm neo dưới sàn nơi Thợ Mỏ/Người Chơi đứng để ĐẶT block vào ô này (Phía Mỏ)")]
        [SerializeField] private Transform _placeAnchor;

        [Tooltip("Điểm neo dưới sàn nơi Nhân Viên Cửa Hàng đứng để LẤY block ra giao cho khách (Phía Shop)")]
        [SerializeField] private Transform _takeAnchor;

        [Header("Pyramid Geometric Configuration")]
        [Tooltip("Kích thước cạnh của khối block Voxel (chuẩn hiển thị: 0.4m)")]
        [SerializeField] private float _blockSize = 0.4f;

        // Bán kính tâm tam giác đều cạnh L = 0.4m: R = 0.4 / sqrt(3) ≈ 0.231m
        // Tâm khối đáy cách mặt bàn nửa chiều cao (Y = +0.2m) để đáy khối nằm sát mặt gỗ
        // Độ cao tâm khối đỉnh: Y = 0.2m + 0.4m * sqrt(2/3) ≈ 0.527m
        private static readonly Vector3[] PyramidOffsets = new Vector3[]
        {
            new Vector3(0f, 0.2f, 0.231f),          // Block 0: Đáy trước
            new Vector3(-0.2f, 0.2f, -0.116f),     // Block 1: Đáy trái sau
            new Vector3(0.2f, 0.2f, -0.116f),      // Block 2: Đáy phải sau
            new Vector3(0f, 0.527f, 0f)            // Block 3: Đỉnh kim tự tháp (úp ở giữa)
        };

        public const int MAX_CAPACITY = 4;

        private readonly List<GameObject> _placedBlocks = new List<GameObject>(MAX_CAPACITY);
        private BlockDataSO _currentBlockType = null;

        public Transform TableAnchor => _tableAnchor != null ? _tableAnchor : transform;
        public Transform PlaceAnchor => _placeAnchor != null ? _placeAnchor : transform;
        public Transform TakeAnchor => _takeAnchor != null ? _takeAnchor : transform;

        // Tương thích ngược: InteractionAnchor mặc định trỏ về PlaceAnchor
        public Transform InteractionAnchor => PlaceAnchor;

        public int CurrentCount => _placedBlocks.Count;
        public int MaxCapacity => MAX_CAPACITY;
        public bool IsFull => _placedBlocks.Count >= MAX_CAPACITY;
        public bool IsEmpty => _placedBlocks.Count == 0;
        public bool CanAcceptBlock => !IsFull;
        public BlockDataSO CurrentBlockType => _currentBlockType;
        public IReadOnlyList<GameObject> PlacedBlocks => _placedBlocks;

        private void Awake() {
            if (_tableAnchor == null) _tableAnchor = transform;
            if (_placeAnchor == null) _placeAnchor = transform.Find("PlacePoint") ?? transform.Find("PlaceAnchor");
            if (_takeAnchor == null) _takeAnchor = transform.Find("TakePoint") ?? transform.Find("TakeAnchor");
        }

        public void ConfigureAnchors(Transform tableAnchor, Transform placeAnchor, Transform takeAnchor) {
            _tableAnchor = tableAnchor;
            _placeAnchor = placeAnchor;
            _takeAnchor = takeAnchor;
        }

        public void ConfigureAnchors(Transform tableAnchor, Transform interactionAnchor) {
            ConfigureAnchors(tableAnchor, interactionAnchor, interactionAnchor);
        }

        /// <summary>
        /// Kiểm tra ô có thể tiếp nhận loại block này không (Phải trống hoặc cùng loại block và chưa đầy).
        /// </summary>
        public bool CanAcceptBlockType(BlockDataSO blockData) {
            if (IsFull) return false;
            if (IsEmpty || _currentBlockType == null) return true;
            return _currentBlockType == blockData;
        }

        /// <summary>
        /// Thử đặt một block vào ô này theo hình học Kim Tự Tháp Tam Giác.
        /// </summary>
        public bool TryPlaceBlock(GameObject blockInstance, BlockDataSO blockData, out Vector3 targetWorldPos) {
            if (blockInstance == null || !CanAcceptBlockType(blockData)) {
                targetWorldPos = Vector3.zero;
                return false;
            }

            if (IsEmpty) {
                _currentBlockType = blockData;
            }

            int index = _placedBlocks.Count;
            _placedBlocks.Add(blockInstance);

            Vector3 localOffset = PyramidOffsets[index];
            // Sử dụng vị trí và góc xoay thuần túy để triệt tiêu hoàn toàn méo hình do Scale của bàn cha!
            targetWorldPos = TableAnchor.position + TableAnchor.rotation * localOffset;
            return true;
        }

        /// <summary>
        /// Overload tương thích ngược cho TryPlaceBlock khi chưa truyền blockData.
        /// </summary>
        public bool TryPlaceBlock(GameObject blockInstance, out Vector3 targetWorldPos) {
            return TryPlaceBlock(blockInstance, null, out targetWorldPos);
        }

        /// <summary>
        /// Lấy một block trên đỉnh kim tự tháp của ô này để giao cho khách hàng.
        /// </summary>
        public bool TryTakeBlock(out GameObject blockInstance, out BlockDataSO blockData) {
            if (IsEmpty) {
                blockInstance = null;
                blockData = null;
                return false;
            }

            int lastIndex = _placedBlocks.Count - 1;
            blockInstance = _placedBlocks[lastIndex];
            blockData = _currentBlockType;
            _placedBlocks.RemoveAt(lastIndex);

            if (_placedBlocks.Count == 0) {
                _currentBlockType = null;
            }

            return true;
        }

        /// <summary>
        /// Overload tương thích ngược cho TryTakeBlock.
        /// </summary>
        public bool TryTakeBlock(out GameObject blockInstance) {
            return TryTakeBlock(out blockInstance, out _);
        }

        /// <summary>
        /// Xóa sạch danh sách block (dùng khi reset scene hoặc clear quầy).
        /// </summary>
        public void ClearBlocks() {
            for (int i = 0; i < _placedBlocks.Count; i++) {
                if (_placedBlocks[i] != null) {
                    Destroy(_placedBlocks[i]);
                }
            }
            _placedBlocks.Clear();
        }

        private void OnDrawGizmos() {
            Vector3 tablePos = TableAnchor.position;
            Vector3 placePos = PlaceAnchor.position;
            Vector3 takePos = TakeAnchor.position;

            // 1. Vẽ Điểm Đặt Block (Phía Mỏ - Màu Cam)
            Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.9f);
            Gizmos.DrawWireSphere(placePos, 0.25f);
            Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.4f);
            Gizmos.DrawLine(placePos, tablePos);

            // 2. Vẽ Điểm Lấy Block (Phía Shop - Màu Tím)
            Gizmos.color = new Color(0.85f, 0.1f, 0.95f, 0.9f);
            Gizmos.DrawWireSphere(takePos, 0.25f);
            Gizmos.color = new Color(0.85f, 0.1f, 0.95f, 0.4f);
            Gizmos.DrawLine(takePos, tablePos);

            // 3. Vẽ khung 4 vị trí kim tự tháp chuẩn xác trên mặt bàn
            Matrix4x4 oldMatrix = Gizmos.matrix;
            for (int i = 0; i < MAX_CAPACITY; i++) {
                Vector3 blockWorldPos = TableAnchor.position + TableAnchor.rotation * PyramidOffsets[i];
                Gizmos.matrix = Matrix4x4.TRS(blockWorldPos, TableAnchor.rotation, Vector3.one * _blockSize);

                bool isOccupied = i < _placedBlocks.Count;
                if (isOccupied) {
                    Gizmos.color = new Color(0.2f, 0.85f, 0.3f, 0.75f); // Đã có block (Xanh lá)
                    Gizmos.DrawCube(Vector3.zero, Vector3.one * 0.95f);
                } else {
                    Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.45f); // Ô chờ đặt block (Vàng cam)
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                }
            }
            Gizmos.matrix = oldMatrix;
        }
    }
}
