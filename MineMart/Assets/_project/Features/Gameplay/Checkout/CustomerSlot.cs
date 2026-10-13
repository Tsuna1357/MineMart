using System;
using System.Collections;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Feel;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using UnityEngine;

namespace MineMart.Gameplay.Checkout {
    /// <summary>
    /// Đại diện cho MỘT Ô TIẾP KHÁCH ĐỘC LẬP trên quầy phục vụ dài (Customer Service Slot).
    /// Tương tự như CounterSlot trên bàn trung chuyển, mỗi ô tiếp khách có:
    /// 1. CustomerAnchor: Vị trí dưới sàn phía trước bàn nơi khách đứng chờ gọi món và nhận món.
    /// 2. ServerAnchor: Vị trí dưới sàn phía sau bàn nơi nhân viên/người chơi đứng phục vụ cho khách tại ô này.
    /// 3. TipAnchor: Vị trí trên mặt bàn nơi tiền tip xuất hiện sau khi phục vụ xong.
    /// 4. 3D Order Visual: Hiển thị trạng thái khách chờ gọi món (?) và Model 3D của Block xoay lơ lửng trên đầu khách.
    /// </summary>
    public class CustomerSlot : MonoBehaviour {
        [Header("Scene Anchors")]
        [Tooltip("Điểm neo dưới sàn nơi khách hàng đứng chờ ở phía trước quầy")]
        [SerializeField] private Transform _customerAnchor;

        [Tooltip("Điểm neo dưới sàn nơi nhân viên/người chơi đứng phục vụ ở phía sau quầy")]
        [SerializeField] private Transform _serverAnchor;

        [Tooltip("Điểm neo trên mặt bàn nơi tiền tip sẽ xuất hiện sau khi phục vụ xong khách")]
        [SerializeField] private Transform _tipAnchor;

        [Header("Tip Gold Particle Effect (Hiệu Ứng Hạt Vàng Tỏa Ra)")]
        [Tooltip("Prefab model tùy biến cho các hạt vàng tỏa ra khi đưa block cho khách (để trống sẽ dùng thiết lập từ quầy hoặc hạt vàng tròn mặc định)")]
        [SerializeField] private GameObject _tipParticlePrefab;

        [Tooltip("Số lượng hạt tỏa ra mỗi lần giao hàng thành công")]
        [SerializeField] private int _tipParticleCount = 6;

        [Header("Tip Pickup System (Hệ Thống Rơi Tiền Tip)")]
        [Tooltip("Data cấu hình chỉ số tỉ lệ để lại tip thuần (FloatStatSO)")]
        [SerializeField] private FloatStatSO _tipChanceStat;

        [Tooltip("Prefab tiền tip rơi trên mặt bàn (để trống sẽ tự tạo đồng tiền vàng xoay tròn mặc định)")]
        [SerializeField] private GameObject _worldTipPrefab;

        [Tooltip("Màu sắc tùy chỉnh của tiền tip fallback khi chưa có Prefab (mặc định: Vàng Gold rực rỡ)")]
        [SerializeField] private Color _fallbackTipColor = new Color(1.0f, 0.85f, 0.15f, 1.0f);

        [Header("3D Order Visual Settings")]
        [Tooltip("Độ cao của bong bóng 3D so với sàn vị trí khách đứng (mặc định: 1.75m)")]
        [SerializeField] private float _bubbleHeight = 1.75f;

        [Tooltip("Kích thước của khối block 3D lơ lửng trên đầu (mặc định: 0.35m)")]
        [SerializeField] private float _block3DScale = 0.35f;

        [Tooltip("Bật khung nền bong bóng màu trắng phía sau block để dễ nhìn")]
        [SerializeField] private bool _showBackgroundPlate = true;

        [Header("Slot State")]
        [Tooltip("Hàng đợi các khách hàng đang chờ tại ô này")]
        private List<CustomerAgent> _queue = new List<CustomerAgent>();

        [Header("Order Information")]
        [Tooltip("Loại block mà khách tại ô này đang gọi")]
        [SerializeField] private BlockDataSO _currentOrderBlock;

        [Tooltip("Giá tiền vàng khách sẽ thanh toán khi nhận đúng block")]
        [SerializeField] private double _currentOrderPrice;

        [Tooltip("Trạng thái đang có đơn hàng chờ phục vụ")]
        [SerializeField] private bool _hasActiveOrder = false;

        [Tooltip("Trạng thái đang có khách chờ nhân viên ra nhận order")]
        [SerializeField] private bool _needsOrderTake = false;

        private OrderTicket _activeTicket;

        // Visual Components
        private GameObject _visualRoot;
        private GameObject _backgroundPlate;
        private GameObject _textWaitingObj;
        private TextMesh _textWaitingMesh;
        private GameObject _blockHolder;
        private GameObject _currentBlockModelInstance;

        public Transform CustomerAnchor => _customerAnchor != null ? _customerAnchor : transform;
        public Transform ServerAnchor => _serverAnchor != null ? _serverAnchor : transform;
        public Transform TipAnchor => _tipAnchor != null ? _tipAnchor : transform;

        public bool IsOccupied => _queue.Count > 0;
        public int QueueCount => _queue.Count;
        public CustomerAgent CurrentCustomer => _queue.Count > 0 ? _queue[0] : null;
        public bool HasActiveOrder => (_activeTicket != null && _activeTicket.IsActive) || (_hasActiveOrder && _currentOrderBlock != null);
        public BlockDataSO CurrentOrderBlock => _currentOrderBlock;
        public double CurrentOrderPrice => _currentOrderPrice;
        public bool NeedsOrderTake => _needsOrderTake;

        public event Action<BlockDataSO> OnOrderPlaced;
        public event Action OnOrderCompleted;

        private void Reset() {
            AutoDiscoverAnchors();
        }

        private Camera _mainCamera;

        private void Awake() {
            AutoDiscoverAnchors();
            EnsureVisualSetup();
            _mainCamera = Camera.main;
        }

        private void Start() {
            // Khởi động đơn hàng ban đầu nếu slot được gán sẵn trong Inspector
            if (_activeTicket == null && _hasActiveOrder && _currentOrderBlock != null) {
                double price = _currentOrderPrice > 0 ? _currentOrderPrice : Mining.MiningStation.GetSellPriceForBlock(_currentOrderBlock);
                _activeTicket = OrderManager.Instance.CreateOrder(this, _currentOrderBlock, price);
            }
            UpdateOrderVisual();
        }

        private void Update() {
            if (_visualRoot == null || !_visualRoot.activeSelf) return;

            // 1. Luôn xoay mặt về Camera chính để người chơi nhìn rõ nhất (Billboarding)
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null) {
                _visualRoot.transform.rotation = _mainCamera.transform.rotation;
            }

            // 2. Chuyển động bập bồng nhẹ nhàng (Bobbing)
            float bob = Mathf.Sin(Time.time * 2.8f) * 0.04f;
            _visualRoot.transform.position = CustomerAnchor.position + Vector3.up * (_bubbleHeight + bob);

            // 3. Xoay tròn nhẹ khối block 3D (Minecraft item spinning)
            if (_blockHolder != null && _blockHolder.activeSelf) {
                _blockHolder.transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>
        /// Tự động tìm kiếm các Anchor con theo tên chuẩn để người dùng dễ kéo thả hoặc tạo cấu trúc.
        /// </summary>
        public void AutoDiscoverAnchors() {
            if (_customerAnchor == null) {
                Transform found = transform.Find("CustomerAnchor");
                if (found == null) found = transform.Find("CustomerPoint");
                if (found != null) _customerAnchor = found;
            }

            if (_serverAnchor == null) {
                Transform found = transform.Find("ServerAnchor");
                if (found == null) found = transform.Find("ServerPoint");
                if (found != null) _serverAnchor = found;
            }

            if (_tipAnchor == null) {
                Transform found = transform.Find("TipAnchor");
                if (found == null) found = transform.Find("TipPoint");
                if (found == null) found = transform.Find("TableAnchor"); // Tương thích ngược nếu đã đặt tên TableAnchor
                if (found != null) _tipAnchor = found;
            }
        }

        /// <summary>
        /// Khách hàng tham gia vào hàng đợi tại ô này.
        /// </summary>
        public void JoinQueue(CustomerAgent agent) {
            if (agent != null && !_queue.Contains(agent)) {
                _queue.Add(agent);
            }
        }

        /// <summary>
        /// Khách hàng rời khỏi hàng đợi.
        /// </summary>
        public void LeaveQueue(CustomerAgent agent) {
            if (agent != null && _queue.Contains(agent)) {
                bool wasFirst = _queue[0] == agent;
                _queue.Remove(agent);

                // Nếu người rời đi là người đang giao dịch (người đầu tiên), hủy đơn của họ
                if (wasFirst || _queue.Count == 0) {
                    _needsOrderTake = false;
                    CancelOrder();
                }

                // Thông báo cho tất cả người phía sau tiến lên 1 bước
                for (int i = 0; i < _queue.Count; i++) {
                    if (_queue[i] != null) {
                        _queue[i].AdvanceInLine();
                    }
                }
            }
        }

        /// <summary>
        /// Trả về số thứ tự của khách hàng trong hàng đợi (0 là người đứng đầu).
        /// </summary>
        public int GetCustomerIndex(CustomerAgent agent) {
            return _queue.IndexOf(agent);
        }

        /// <summary>
        /// Tính toán hướng hàng đợi kéo dài ra xa khỏi quầy (hướng về phía sảnh đón khách).
        /// Luôn đi từ ServerAnchor (phía nhân viên) xuyên qua CustomerAnchor (phía khách) và hướng ra ngoài.
        /// </summary>
        public Vector3 GetQueueDirection() {
            if (_customerAnchor != null && _serverAnchor != null) {
                Vector3 dir = _customerAnchor.position - _serverAnchor.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) {
                    return dir.normalized;
                }
            }

            if (_customerAnchor != null) {
                Vector3 dir = _customerAnchor.position - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) {
                    return dir.normalized;
                }
                return _customerAnchor.forward;
            }

            return transform.forward;
        }

        /// <summary>
        /// Tính toán vị trí đứng dựa trên số thứ tự hàng đợi (mỗi người cách nhau 1.15m lùi về phía sảnh).
        /// Đảm bảo vị trí luôn bám chuẩn trên NavMesh để khách không bị khựng/khực do lệch sàn.
        /// </summary>
        public Vector3 GetQueuePosition(int index) {
            Vector3 targetPos = CustomerAnchor.position;
            if (index > 0) {
                targetPos += GetQueueDirection() * (index * 1.15f);
            }

            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) {
                return hit.position;
            }
            return targetPos;
        }

        /// <summary>
        /// Khách hàng đứng vào vị trí và giơ tay gọi nhân viên ra nhận order.
        /// </summary>
        public void SetNeedsOrderTake(bool needs) {
            // Chỉ cho phép hiển thị dấu "?" nếu thực sự có khách hàng đang xếp đầu hàng tại ô này!
            if (needs && (_queue.Count == 0 || _queue[0] == null)) {
                _needsOrderTake = false;
                UpdateOrderVisual();
                return;
            }

            _needsOrderTake = needs;
            UpdateOrderVisual();
        }

        /// <summary>
        /// Nhân viên/Người chơi đã ra quầy nhận order. Chuyển order vào hệ thống và bắt đầu phục vụ.
        /// </summary>
        public void TakeOrder(BlockDataSO blockData, double price) {
            if (blockData == null) return;

            if (price <= 0) {
                price = MiningStation.GetSellPriceForBlock(blockData);
            }

            _needsOrderTake = false;
            _currentOrderBlock = blockData;
            _currentOrderPrice = price;
            _hasActiveOrder = true;

            // Đẩy đơn hàng vào OrderManager trung tâm
            _activeTicket = OrderManager.Instance.CreateOrder(this, blockData, price);

            OnOrderPlaced?.Invoke(blockData);
            UpdateOrderVisual();
            Debug.Log($"[CustomerSlot - {name}] 📝 Đã nhận order: 1 [{blockData.DisplayName}] - Giá {price} Vàng!");
        }

        /// <summary>
        /// Kiểm tra xem nhân viên/người chơi có đang đứng đúng vị trí phục vụ (ServerAnchor) của ô này không.
        /// Yêu cầu khoảng cách <= maxRadius VÀ phải gần ServerAnchor hơn CustomerAnchor.
        /// </summary>
        public bool IsPlayerAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_serverAnchor == null) return false;

            float distServerSqr = (_serverAnchor.position - playerPosition).sqrMagnitude;
            if (distServerSqr > maxRadius * maxRadius) return false;

            if (_customerAnchor != null) {
                float distCustSqr = (_customerAnchor.position - playerPosition).sqrMagnitude;
                if (distServerSqr >= distCustSqr) {
                    // Đang đứng ở phía khách hàng -> Không được giao hàng!
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Kiểm tra xem block trên tay có đúng loại mà ô này đang cần không.
        /// </summary>
        public bool CanFulfillWith(BlockDataSO carriedBlockData) {
            if (!HasActiveOrder || carriedBlockData == null) return false;
            return carriedBlockData == _currentOrderBlock;
        }

        /// <summary>
        /// Thực hiện giao hàng trực tiếp cho khách tại ô này:
        /// Tiêu thụ block, bắn sự kiện thanh toán tiền vàng và hoàn tất đơn hàng.
        /// </summary>
        public bool TryDeliverOrder(GameObject carriedBlockInstance, BlockDataSO carriedBlockData, OrderDeliveredEventChannelSO orderDeliveredChannel) {
            if (!CanFulfillWith(carriedBlockData)) return false;

            // 1. Tiêu hủy block thành phẩm trên tay
            if (carriedBlockInstance != null) {
                Destroy(carriedBlockInstance);
            }

            // 1.5. Phát hiệu ứng các hạt vàng tròn (hoặc model tùy biến) tỏa ra tại vị trí TipAnchor
            Vector3 tipPos = _tipAnchor != null ? _tipAnchor.position : (CustomerAnchor.position + Vector3.up * 0.8f);
            GameObject particlePrefab = GetParticlePrefab();
            TipCoinBurstEffect.Play(tipPos, particlePrefab, _tipParticleCount);

            // 1.6. Kiểm tra tỉ lệ rơi tiền Tip tại quầy (Tỉ lệ đọc trực tiếp từ FloatStatSO.CurrentValue)
            float tipChance = _tipChanceStat != null ? _tipChanceStat.CurrentValue : 0.15f;

            if (UnityEngine.Random.value <= tipChance) {
                // Tiền tip = Trung bình cộng giá của toàn bộ block đã mở bán tại shop nhân với 2
                double tipAmount = MiningStation.CalculateTipAmount();
                WorldTipPickup.CreateTipInstance(tipPos, tipAmount, _worldTipPrefab, _fallbackTipColor);
                Debug.Log($"<color=#f1c40f>[CustomerSlot - {name}]</color> 💰 Khách đã để lại {tipAmount} Vàng tiền Tip tại quầy!");
            }

            // 2. Bắn sự kiện thanh toán qua kênh OrderDeliveredEventChannelSO để EconomyManager cộng vàng
            if (orderDeliveredChannel != null) {
                OrderDeliveryPayload payload = new OrderDeliveryPayload(
                    stallId: "CustomerCounter",
                    blockId: _currentOrderBlock.BlockId,
                    basePrice: _currentOrderPrice > 0 ? _currentOrderPrice : MiningStation.GetSellPriceForBlock(_currentOrderBlock),
                    deliveryPosition: CustomerAnchor.position,
                    isVipCustomer: false
                );
                orderDeliveredChannel.RaiseEvent(payload);
            }

            // 3. Hoàn tất đơn hàng và giải phóng trạng thái order của slot
            if (_activeTicket != null && OrderManager.Instance != null) {
                OrderManager.Instance.CompleteOrder(_activeTicket);
                _activeTicket = null;
            }

            _hasActiveOrder = false;
            BlockDataSO completedBlock = _currentOrderBlock;
            double payout = _currentOrderPrice > 0 ? _currentOrderPrice : MiningStation.GetSellPriceForBlock(_currentOrderBlock);
            _currentOrderBlock = null;
            _currentOrderPrice = 0;

            OnOrderCompleted?.Invoke();
            UpdateOrderVisual();
            Debug.Log($"[CustomerSlot - {name}] ✅ Giao hàng thành công! Đã nhận {payout} Vàng từ khách!");

            return true;
        }

        private GameObject GetParticlePrefab() {
            if (_tipParticlePrefab != null) return _tipParticlePrefab;
            var parentCounter = GetComponentInParent<CustomerCounter>();
            if (parentCounter != null) return parentCounter.TipParticlePrefab;
            return null;
        }

        /// <summary>
        /// Hủy đơn hàng tại ô này (khi khách rời đi).
        /// </summary>
        public void CancelOrder() {
            if (_activeTicket != null && OrderManager.Instance != null) {
                OrderManager.Instance.CancelOrder(_activeTicket);
                _activeTicket = null;
            }

            _needsOrderTake = false;
            _hasActiveOrder = false;
            _currentOrderBlock = null;
            _currentOrderPrice = 0;
            UpdateOrderVisual();
        }

        // =========================================================================
        // 3D ORDER VISUAL (BONG BÓNG ĐƠN HÀNG 3D TRÊN ĐẦU KHÁCH)
        // =========================================================================

        private void EnsureVisualSetup() {
            if (_visualRoot != null) return;

            _visualRoot = new GameObject("OrderBubbleVisual");
            _visualRoot.transform.SetParent(transform);
            _visualRoot.transform.position = CustomerAnchor.position + Vector3.up * _bubbleHeight;

            // 1. Tạo tấm nền bong bóng (Background Plate)
            if (_showBackgroundPlate) {
                _backgroundPlate = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _backgroundPlate.name = "BackgroundPlate";
                _backgroundPlate.transform.SetParent(_visualRoot.transform);
                _backgroundPlate.transform.localPosition = Vector3.zero;
                _backgroundPlate.transform.localScale = new Vector3(0.68f, 0.68f, 1f);

                // Loại bỏ collider
                Collider col = _backgroundPlate.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer r = _backgroundPlate.GetComponent<Renderer>();
                if (r != null) {
                    Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.96f, 0.96f, 0.96f, 0.92f);
                    r.material = mat;
                }
            }

            // 2. Chữ "?" hoặc "..." khi khách đang chờ nhận order
            _textWaitingObj = new GameObject("WaitingText");
            _textWaitingObj.transform.SetParent(_visualRoot.transform);
            _textWaitingObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            _textWaitingObj.transform.localScale = Vector3.one * 0.08f;
            _textWaitingMesh = _textWaitingObj.AddComponent<TextMesh>();
            _textWaitingMesh.text = "?";
            _textWaitingMesh.fontSize = 52;
            _textWaitingMesh.alignment = TextAlignment.Center;
            _textWaitingMesh.anchor = TextAnchor.MiddleCenter;
            _textWaitingMesh.color = new Color(0.2f, 0.2f, 0.2f);

            // 3. Holder chứa Block 3D xoay
            _blockHolder = new GameObject("BlockHolder");
            _blockHolder.transform.SetParent(_visualRoot.transform);
            _blockHolder.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            _visualRoot.SetActive(false);
        }

        private void UpdateOrderVisual() {
            EnsureVisualSetup();

            if (_needsOrderTake) {
                // Khách đang đứng quầy chờ nhận order: Hiển thị "?"
                _visualRoot.SetActive(true);
                if (_textWaitingObj != null) _textWaitingObj.SetActive(true);
                if (_blockHolder != null) _blockHolder.SetActive(false);

                if (_currentBlockModelInstance != null) {
                    Destroy(_currentBlockModelInstance);
                    _currentBlockModelInstance = null;
                }
            } else if (HasActiveOrder && _currentOrderBlock != null) {
                // Đã nhận order: Hiển thị khối Block 3D lơ lửng
                _visualRoot.SetActive(true);
                if (_textWaitingObj != null) _textWaitingObj.SetActive(false);
                if (_blockHolder != null) _blockHolder.SetActive(true);

                if (_currentBlockModelInstance != null) {
                    Destroy(_currentBlockModelInstance);
                }

                if (_currentOrderBlock.Prefab3D != null) {
                    _currentBlockModelInstance = Instantiate(_currentOrderBlock.Prefab3D, _blockHolder.transform);
                } else {
                    _currentBlockModelInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _currentBlockModelInstance.transform.SetParent(_blockHolder.transform);
                    Renderer r = _currentBlockModelInstance.GetComponent<Renderer>();
                    if (r != null) {
                        r.material.color = GetBlockColor(_currentOrderBlock.BlockId);
                    }
                }

                // Xóa toàn bộ Collider trên model hiển thị
                Collider[] colliders = _currentBlockModelInstance.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++) {
                    Destroy(colliders[i]);
                }

                _currentBlockModelInstance.transform.localPosition = Vector3.zero;
                _currentBlockModelInstance.transform.localRotation = Quaternion.Euler(15f, 45f, 0f);
                _currentBlockModelInstance.transform.localScale = Vector3.one * _block3DScale;
            } else {
                // Không có đơn hàng -> Ẩn toàn bộ
                if (_currentBlockModelInstance != null) {
                    Destroy(_currentBlockModelInstance);
                    _currentBlockModelInstance = null;
                }
                if (_visualRoot != null) {
                    _visualRoot.SetActive(false);
                }
            }
        }

        private Color GetBlockColor(string blockId) {
            if (string.IsNullOrEmpty(blockId)) return Color.white;
            if (blockId.Contains("Wood") || blockId == "0001") return new Color(0.65f, 0.45f, 0.25f);
            if (blockId.Contains("Dirt") || blockId == "0002") return new Color(0.55f, 0.38f, 0.2f);
            if (blockId.Contains("Stone") || blockId == "0003") return new Color(0.6f, 0.6f, 0.6f);
            return new Color(0.3f, 0.75f, 0.4f);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Cancel Order")]
        private void DebugCancelOrder() {
            CancelOrder();
            Debug.Log($"[CustomerSlot - {name}] Đã hủy đơn hàng trên ô này.");
        }
#endif

        private void OnDrawGizmos() {
            if (_customerAnchor != null) {
                Color custColor = IsOccupied ? new Color(0.1f, 0.9f, 0.3f, 0.9f) : new Color(1f, 0.85f, 0.1f, 0.85f);
                DrawGroundCircle(_customerAnchor.position, 0.35f, custColor);
                Gizmos.color = custColor;
                Gizmos.DrawWireSphere(_customerAnchor.position, 0.3f);
                Gizmos.DrawRay(_customerAnchor.position + Vector3.up * 0.05f, _customerAnchor.forward * 0.45f);

                // Vẽ trước 3 vị trí xếp hàng lùi về phía sảnh để Designer dễ quan sát luồng di chuyển
                Vector3 queueDir = GetQueueDirection();
                for (int q = 1; q <= 3; q++) {
                    Vector3 qPos = _customerAnchor.position + queueDir * (q * 1.15f);
                    Color qColor = new Color(0.2f, 0.8f, 1f, 0.6f / q);
                    DrawGroundCircle(qPos, 0.28f, qColor);
                    Gizmos.color = qColor;
                    Gizmos.DrawWireSphere(qPos, 0.25f);
                    Gizmos.DrawLine(qPos, qPos - queueDir * 1.15f);
                }

                if (HasActiveOrder) {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(_customerAnchor.position + Vector3.up * 1f, Vector3.one * 0.4f);
                }
            }

            if (_serverAnchor != null) {
                Color servColor = new Color(0.9f, 0.3f, 0.9f, 0.85f);
                DrawGroundCircle(_serverAnchor.position, 0.35f, servColor);
                Gizmos.color = servColor;
                Gizmos.DrawWireSphere(_serverAnchor.position, 0.3f);
                Gizmos.DrawRay(_serverAnchor.position + Vector3.up * 0.05f, _serverAnchor.forward * 0.45f);
            }

            if (_customerAnchor != null && _serverAnchor != null) {
                Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(_customerAnchor.position, _serverAnchor.position);
            }

            if (_tipAnchor != null) {
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.85f);
                Gizmos.DrawWireCube(_tipAnchor.position, new Vector3(0.35f, 0.08f, 0.35f));
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
