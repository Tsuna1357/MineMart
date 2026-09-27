using System;
using System.Collections.Generic;
using UnityEngine;
using MineMart.Data;
using MineMart.Gameplay.Orders;

namespace MineMart.Gameplay.Mining
{
    /// <summary>
    /// Quản lý Trạm Khai Thác Nguyên Liệu (Mỏ Đất, Mỏ Gỗ, Mỏ Đá...).
    /// Trạm là nơi đặt khối tài nguyên Voxel để thợ đứng đào.
    /// Quản lý Cấp độ (Level 0 = Chưa mở -> Bấm mở khóa -> Level 1), 
    /// mốc Milestone doanh thu, và vị trí thợ đứng làm việc.
    /// </summary>
    public class MiningStation : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Hồ sơ cấu hình của trạm mỏ (StallDataSO đã cấu hình trong .asset)")]
        [SerializeField] private StallDataSO _stationData;

        [Header("Scene Anchors")]
        [Tooltip("Điểm neo nơi thợ đứng cầm cuốc đào khối tài nguyên này")]
        [SerializeField] private Transform _minerAnchor;

        [Tooltip("GameObject chứa hình ảnh 3D của khối tài nguyên (ẩn khi chưa mở khóa)")]
        [SerializeField] private GameObject _visualRoot;

        [Header("State")]
        [Tooltip("Cấp độ hiện tại của trạm mỏ (Mặc định 1 = Đã mở khóa sẵn sàng khai thác)")]
        [SerializeField] private int _currentLevel = 1;

        [Header("Order-Driven Mining")]
        [Tooltip("Số lượng đơn hàng đang chờ mỏ này khai thác")]
        [SerializeField] private int _pendingOrders = 0;

        [Tooltip("Bắt buộc phải có order từ khách mới cho phép khai thác (mặc định = false để đào tự do trong giai đoạn test)")]
        [SerializeField] private bool _requireOrderToMine = false;

        private double _currentProfitMultiplier = 1.0;

        // Sự kiện thông báo khi trạm lên cấp hoặc đạt mốc milestone
        public event Action<int> OnLevelChanged;
        public event Action<MilestoneRuleSO> OnMilestoneReached;

        public StallDataSO StationData => _stationData;
        public BlockDataSO ProducedBlock => _stationData != null ? _stationData.ProducedBlock : null;
        public Transform MinerAnchor => _minerAnchor != null ? _minerAnchor : transform;
        public int CurrentLevel => _currentLevel;
        public bool IsUnlocked => _currentLevel > 0;
        public double CurrentProfitMultiplier => _currentProfitMultiplier;
        public int PendingOrders => _pendingOrders;
        public bool RequireOrderToMine => _requireOrderToMine;

        /// <summary>
        /// Kiểm tra xem có đơn hàng nào đang chờ mỏ này sản xuất không (từ OrderManager hoặc pending orders).
        /// </summary>
        public bool HasOrderInQueue
        {
            get
            {
                if (OrderManager.Instance != null && ProducedBlock != null)
                {
                    return OrderManager.Instance.HasPendingOrder(ProducedBlock);
                }
                return _pendingOrders > 0;
            }
        }

        public bool NeedsMining => IsUnlocked && (!_requireOrderToMine || HasOrderInQueue);

        // Quản lý danh sách trạm mỏ toàn cục để PlayerController tương tác bằng Anchor KHÔNG CẦN BoxCollider
        private static readonly List<MiningStation> s_allStations = new List<MiningStation>();
        public static IReadOnlyList<MiningStation> AllStations => s_allStations;

        private void OnEnable()
        {
            if (!s_allStations.Contains(this))
            {
                s_allStations.Add(this);
            }
        }

        private void OnDisable()
        {
            s_allStations.Remove(this);
        }

        /// <summary>
        /// Thêm đơn hàng cần mỏ này sản xuất.
        /// </summary>
        public void AddPendingOrder(int count = 1)
        {
            _pendingOrders += Mathf.Max(1, count);
        }

        /// <summary>
        /// Tiêu thụ 1 đơn hàng khi thợ hoặc người chơi đào xong 1 block mà không dùng cơ chế Claim trước.
        /// Tự động cập nhật trạng thái đơn sang Delivering trên OrderManager.
        /// </summary>
        public void ConsumePendingOrder()
        {
            if (OrderManager.Instance != null && ProducedBlock != null)
            {
                OrderTicket ticket = OrderManager.Instance.ClaimNextPendingOrder(ProducedBlock, this);
                if (ticket != null)
                {
                    OrderManager.Instance.MarkDelivering(ticket);
                }
            }

            if (_pendingOrders > 0)
            {
                _pendingOrders--;
            }
        }

        /// <summary>
        /// Người chơi hoặc Thợ claim trước 1 đơn hàng Pending khi bắt đầu bổ cuốc đào.
        /// Việc claim ngay lập tức làm giảm PendingCount về 0 (nếu chỉ có 1 đơn), giúp mỏ khóa lại không cho đào thừa!
        /// </summary>
        public OrderTicket ClaimOrderForMining(object worker)
        {
            if (OrderManager.Instance != null && ProducedBlock != null)
            {
                return OrderManager.Instance.ClaimNextPendingOrder(ProducedBlock, worker);
            }
            return null;
        }

        /// <summary>
        /// Hoàn trả đơn hàng về trạng thái Pending nếu người chơi/thợ bỏ dở việc đào giữa chừng.
        /// </summary>
        public void ReleaseClaim(OrderTicket ticket)
        {
            if (OrderManager.Instance != null && ticket != null)
            {
                OrderManager.Instance.ReleaseClaim(ticket);
            }
        }

        /// <summary>
        /// Xác nhận đã đào xong 1 khối block, chuyển vé sang trạng thái Delivering (bưng đi giao).
        /// </summary>
        public void FinishMiningOrder(OrderTicket ticket)
        {
            if (OrderManager.Instance != null && ticket != null)
            {
                OrderManager.Instance.MarkDelivering(ticket);
            }

            if (_pendingOrders > 0)
            {
                _pendingOrders--;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Add 1 Pending Order")]
        private void DebugAddOrder()
        {
            if (OrderManager.Instance != null && ProducedBlock != null)
            {
                OrderManager.Instance.CreateOrder(this, ProducedBlock, ProducedBlock.BaseValue > 0 ? ProducedBlock.BaseValue : 3);
            }
            else
            {
                AddPendingOrder(1);
            }
            Debug.Log($"[MiningStation] Đã thêm 1 đơn hàng vào {name}. Đơn trong OrderManager: {(OrderManager.Instance != null && ProducedBlock != null ? OrderManager.Instance.GetPendingCount(ProducedBlock) : _pendingOrders)}");
        }

        [ContextMenu("Debug: Clear Pending Orders")]
        private void DebugClearOrders()
        {
            _pendingOrders = 0;
            Debug.Log($"[MiningStation] Đã xóa hết đơn chờ trên {name}.");
        }
#endif

        private void Awake()
        {
            if (_minerAnchor == null)
            {
                Transform foundAnchor = transform.Find("MinerAnchor");
                if (foundAnchor != null) _minerAnchor = foundAnchor;
            }

            if (_visualRoot == null)
            {
                _visualRoot = gameObject;
            }

            // Đảm bảo mỏ luôn sẵn sàng ở cấp 1 nếu tạo mới chưa gán level
            if (_currentLevel < 1)
            {
                _currentLevel = 1;
            }

#if UNITY_EDITOR
            if (_stationData == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:StallDataSO Stall_Dirt");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _stationData = UnityEditor.AssetDatabase.LoadAssetAtPath<StallDataSO>(path);
                }
            }
#endif

            RecalculateMilestoneMultipliers();
            UpdateVisualState();
        }

        private void Start()
        {
            // Đảm bảo trạm mỏ luôn có ít nhất 1 đơn khởi đầu để người chơi đào được ngay khi test
            if (_pendingOrders <= 0 && _requireOrderToMine)
            {
                _pendingOrders = 1;
            }
        }

        /// <summary>
        /// Lấy chi phí cần thiết để mở khóa hoặc nâng cấp lên cấp tiếp theo.
        /// </summary>
        public double GetNextUpgradeCost()
        {
            if (_stationData == null) return 0;
            return _stationData.CalculateUpgradeCost(_currentLevel);
        }

        /// <summary>
        /// Mở khóa trạm (chuyển từ Level 0 lên Level 1).
        /// Hỗ trợ cả trường hợp mở khóa miễn phí 0 Vàng (như Quầy Đất tân thủ).
        /// </summary>
        public bool TryUnlock()
        {
            if (IsUnlocked) return false;

            _currentLevel = 1;
            RecalculateMilestoneMultipliers();
            UpdateVisualState();

            OnLevelChanged?.Invoke(_currentLevel);
            return true;
        }

        /// <summary>
        /// Nâng cấp trạm lên 1 cấp mới, đồng thời kiểm tra chạm mốc Milestone nhân doanh thu.
        /// </summary>
        public void Upgrade()
        {
            if (!IsUnlocked)
            {
                TryUnlock();
                return;
            }

            _currentLevel++;
            CheckMilestones();
            OnLevelChanged?.Invoke(_currentLevel);
        }

        /// <summary>
        /// Nạp lại cấp độ trạm từ SaveManager khi load game.
        /// </summary>
        public void SetLevel(int level)
        {
            _currentLevel = Mathf.Max(0, level);
            RecalculateMilestoneMultipliers();
            UpdateVisualState();
            OnLevelChanged?.Invoke(_currentLevel);
        }

        /// <summary>
        /// Tính toán giá bán cuối cùng của 1 block sản xuất từ trạm này (đã nhân các mốc Milestone).
        /// </summary>
        public double GetFinalSellPrice()
        {
            if (ProducedBlock == null) return 0;
            return ProducedBlock.BaseValue * _currentProfitMultiplier;
        }

        /// <summary>
        /// Thời gian đào gốc của block tại trạm này.
        /// </summary>
        public float GetBaseCraftTime()
        {
            if (ProducedBlock == null) return 1.0f;
            return ProducedBlock.BaseCraftTime;
        }

        private void CheckMilestones()
        {
            if (_stationData == null || _stationData.MilestoneRules == null) return;

            for (int i = 0; i < _stationData.MilestoneRules.Count; i++)
            {
                MilestoneRuleSO rule = _stationData.MilestoneRules[i];
                if (rule != null && _currentLevel == rule.TargetLevel)
                {
                    _currentProfitMultiplier *= rule.ProfitMultiplier;
                    OnMilestoneReached?.Invoke(rule);
                }
            }
        }

        private void RecalculateMilestoneMultipliers()
        {
            _currentProfitMultiplier = 1.0;
            if (_stationData == null || _stationData.MilestoneRules == null) return;

            for (int i = 0; i < _stationData.MilestoneRules.Count; i++)
            {
                MilestoneRuleSO rule = _stationData.MilestoneRules[i];
                if (rule != null && _currentLevel >= rule.TargetLevel)
                {
                    _currentProfitMultiplier *= rule.ProfitMultiplier;
                }
            }
        }

        private void UpdateVisualState()
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(IsUnlocked);
            }
        }

        private void OnDrawGizmos()
        {
            // Vẽ vị trí đứng làm việc của thợ mỏ (Màu cam)
            if (_minerAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
                Gizmos.DrawWireSphere(_minerAnchor.position, 0.3f);
            }
        }
    }
}
