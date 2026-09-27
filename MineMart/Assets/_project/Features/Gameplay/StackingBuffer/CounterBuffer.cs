using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Gameplay.StackingBuffer
{
    /// <summary>
    /// Quản lý bộ đệm mặt bàn và thuật toán xếp đống Kim Tự Tháp Tam Giác (Triangle Pyramid) 3D tại quầy thu ngân.
    /// Số lượng ô đệm (Slots) co giãn động theo số điểm neo Transform gắn trên Prefab.
    /// Mỗi ô chứa tối đa 4 block chuẩn 0.4x0.4x0.4m: 3 block tạo đáy tam giác phẳng + 1 block ngự trên đỉnh.
    /// Thuật toán 2 tầng: Ưu tiên ô hoàn toàn trống -> Xếp vào ô ít block nhất (tối đa 4 block / ô).
    /// </summary>
    public class CounterBuffer : MonoBehaviour
    {
        [Header("Dynamic Slot Anchors")]
        [Tooltip("Kéo thả các Transform điểm neo của từng ô trên mặt bàn quầy vào đây")]
        [SerializeField] private Transform[] _slotAnchors;

        [Header("Pyramid Stacking Parameters")]
        [Tooltip("Kích thước cạnh của khối block Voxel (chuẩn hiển thị: 0.4m)")]
        [SerializeField] private float _blockSize = 0.4f;

        // Cấu hình 4 vị trí hình học của Kim Tự Tháp Tam Giác (chuẩn 0.4m, đáy phẳng sát mặt bàn)
        // 3 block đáy tam giác đều (Y = +0.2m) + 1 block đỉnh chóp (Y = +0.527m)
        private static readonly Vector3[] PyramidOffsets = new Vector3[]
        {
            new Vector3(0f, 0.2f, 0.231f),          // Block 0: Đáy trước
            new Vector3(-0.2f, 0.2f, -0.116f),     // Block 1: Đáy trái sau
            new Vector3(0.2f, 0.2f, -0.116f),      // Block 2: Đáy phải sau
            new Vector3(0f, 0.527f, 0f)            // Block 3: Đỉnh kim tự tháp
        };

        public const int BLOCKS_PER_SLOT = 4;

        // Quản lý danh sách block trong từng ô slot
        private List<GameObject>[] _slotStacks;

        public int SlotCount => _slotAnchors != null ? _slotAnchors.Length : 0;
        public int BlocksPerSlot => BLOCKS_PER_SLOT;
        public int MaxCapacity => SlotCount * BLOCKS_PER_SLOT;

        public int TotalBlockCount
        {
            get
            {
                if (_slotStacks == null) return 0;
                int total = 0;
                for (int i = 0; i < _slotStacks.Length; i++)
                {
                    total += _slotStacks[i].Count;
                }
                return total;
            }
        }

        public bool IsFull => TotalBlockCount >= MaxCapacity;
        public bool IsEmpty => TotalBlockCount == 0;

        private void Awake()
        {
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            if (_slotAnchors == null || _slotAnchors.Length == 0)
            {
                _slotAnchors = new Transform[] { transform };
            }

            _slotStacks = new List<GameObject>[_slotAnchors.Length];
            for (int i = 0; i < _slotAnchors.Length; i++)
            {
                _slotStacks[i] = new List<GameObject>(BLOCKS_PER_SLOT);
            }
        }

        /// <summary>
        /// Đặt một block mới lên quầy thu ngân theo mô hình Kim Tự Tháp Tam Giác.
        /// Ưu tiên ô trống -> sau đó đến ô có ít block nhất (< 4).
        /// </summary>
        public bool TryPlaceBlock(GameObject blockInstance, out Vector3 targetPosition)
        {
            if (blockInstance == null)
            {
                targetPosition = Vector3.zero;
                return false;
            }

            if (_slotStacks == null) InitializeSlots();

            // Bước 1: Ưu tiên ô hoàn toàn trống để dàn đều mặt bàn
            for (int i = 0; i < _slotStacks.Length; i++)
            {
                if (_slotStacks[i].Count == 0)
                {
                    _slotStacks[i].Add(blockInstance);
                    targetPosition = GetSlotWorldPosition(i, 0);
                    return true;
                }
            }

            // Bước 2: Tìm ô đang có ít block nhất (< BLOCKS_PER_SLOT = 4)
            int lowestSlot = -1;
            int minCount = BLOCKS_PER_SLOT;

            for (int i = 0; i < _slotStacks.Length; i++)
            {
                int count = _slotStacks[i].Count;
                if (count < minCount)
                {
                    minCount = count;
                    lowestSlot = i;
                }
            }

            if (lowestSlot != -1)
            {
                int currentCount = _slotStacks[lowestSlot].Count;
                _slotStacks[lowestSlot].Add(blockInstance);
                targetPosition = GetSlotWorldPosition(lowestSlot, currentCount);
                return true;
            }

            // Đã kín toàn bộ kim tự tháp trên các ô (Full công suất)
            targetPosition = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Lấy một block từ đỉnh kim tự tháp của ô nhiều hàng nhất để giao cho khách hàng.
        /// </summary>
        public bool TryTakeBlock(out GameObject blockInstance)
        {
            blockInstance = null;
            if (IsEmpty) return false;

            // Tìm ô đang có nhiều block nhất để lấy hàng
            int highestSlot = -1;
            int maxCount = 0;

            for (int i = 0; i < _slotStacks.Length; i++)
            {
                int count = _slotStacks[i].Count;
                if (count > maxCount)
                {
                    maxCount = count;
                    highestSlot = i;
                }
            }

            if (highestSlot != -1 && _slotStacks[highestSlot].Count > 0)
            {
                int topIndex = _slotStacks[highestSlot].Count - 1;
                blockInstance = _slotStacks[highestSlot][topIndex];
                _slotStacks[highestSlot].RemoveAt(topIndex);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tính toán vị trí thế giới 3D của block thứ blockIndex trong ô slotIndex theo tọa độ Kim Tự Tháp.
        /// </summary>
        public Vector3 GetSlotWorldPosition(int slotIndex, int blockIndexInSlot)
        {
            if (slotIndex < 0 || slotIndex >= _slotAnchors.Length) return transform.position;
            Transform anchor = _slotAnchors[slotIndex];
            if (anchor == null) return transform.position;

            int clampedIndex = Mathf.Clamp(blockIndexInSlot, 0, BLOCKS_PER_SLOT - 1);
            Vector3 localOffset = PyramidOffsets[clampedIndex];

            return anchor.position + (anchor.rotation * localOffset);
        }

        private void OnDrawGizmosSelected()
        {
            // Vẽ mô phỏng khung dây 3D màu xanh lá cây của 4 khối block kim tự tháp (size 0.4m)
            if (_slotAnchors == null) return;

            Gizmos.color = new Color(0.2f, 0.85f, 0.3f, 0.7f);
            Vector3 cubeSize = Vector3.one * _blockSize;

            Matrix4x4 oldMat = Gizmos.matrix;
            for (int i = 0; i < _slotAnchors.Length; i++)
            {
                if (_slotAnchors[i] == null) continue;
                Transform anchor = _slotAnchors[i];

                for (int b = 0; b < BLOCKS_PER_SLOT; b++)
                {
                    Vector3 blockCenter = anchor.position + (anchor.rotation * PyramidOffsets[b]);
                    Gizmos.matrix = Matrix4x4.TRS(blockCenter, anchor.rotation, cubeSize);
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                }
            }
            Gizmos.matrix = oldMat;
        }
    }
}
