using System;
using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Feel;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Tables {
    /// <summary>
    /// Đại diện cho MỘT CHỖ NGỒI / MỘT Ô PHỤC VỤ ĐỘC LẬP TRÊN BÀN ĂN (Table Slot).
    /// Khác biệt với CustomerSlot ở quầy:
    /// - TUYỆT ĐỐI KHÔNG CÓ HÀNG ĐỢI (NO QUEUE).
    /// - Mỗi ghế / ô bàn chỉ tiếp nhận DUY NHẤT 1 khách tại một thời điểm.
    /// - Khi đã có khách ngồi (IsOccupied = true), khách khác không thể xếp hàng phía sau mà phải tìm bàn/ghế khác.
    /// - Vị trí của khách luôn cố định tại Ghế / CustomerAnchor, không bị lùi về sau theo số thứ tự hàng.
    /// </summary>
    public class TableSlot : MonoBehaviour {
        [Header("Scene Anchors")]
        [Tooltip("Điểm neo nơi khách ngồi / đứng tại bàn (Ghế ngồi)")]
        [SerializeField] private Transform _customerAnchor;

        [Tooltip("Điểm neo nơi nhân viên / người chơi đứng phục vụ cạnh bàn")]
        [SerializeField] private Transform _serverAnchor;

        [Tooltip("Điểm neo trên mặt bàn nơi tiền tip sẽ xuất hiện")]
        [SerializeField] private Transform _tipAnchor;

        [Tooltip("Điểm neo nơi khách ngồi sẽ quay mặt nhìn vào (ví dụ: đĩa ăn trên mặt bàn, tâm bàn)")]
        [SerializeField] private Transform _lookAtAnchor;

        [Header("Tip Gold Particle Effect (Hiệu Ứng Hạt Vàng Tỏa Ra)")]
        [Tooltip("Prefab model tùy biến cho các hạt vàng tỏa ra tại bàn khi đưa block cho khách (để trống sẽ dùng thiết lập từ bàn hoặc hạt vàng tròn mặc định)")]
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
        [Tooltip("Độ cao của bong bóng 3D so với sàn vị trí khách ngồi (mặc định: 1.75m)")]
        [SerializeField] private float _bubbleHeight = 1.75f;

        [Tooltip("Kích thước của khối block 3D lơ lửng trên đầu khách (mặc định: 0.35m)")]
        [SerializeField] private float _block3DScale = 0.35f;

        [Tooltip("Bật khung nền bong bóng màu trắng phía sau block để dễ nhìn")]
        [SerializeField] private bool _showBackgroundPlate = true;

        [Header("Slot State (Không Hàng Đợi)")]
        [Tooltip("Khách hàng duy nhất đang ngồi tại ô này (Null nếu bàn trống)")]
        [SerializeField] private CustomerAgent _currentCustomer = null;

        [Header("Order Information")]
        [Tooltip("Loại block mà khách tại bàn đang gọi")]
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
        public Transform LookAtAnchor => _lookAtAnchor;

        /// <summary>
        /// Lấy vector hướng nhìn chuẩn để khách ngồi quay mặt thẳng vào bàn ăn.
        /// Ưu tiên:
        /// 1. Nếu Designer đặt LookAtAnchor (đĩa ăn, mép bàn trước mặt khách) -> nhìn vào LookAtAnchor.
        /// 2. Nếu có TipAnchor -> nhìn về phía đĩa tiền Tip trên bàn.
        /// 3. Fallback: Hướng forward của chính ghế ngồi CustomerAnchor.
        /// Tuyệt đối KHÔNG nhìn chéo sang ServerAnchor của nhân viên!
        /// </summary>
        public Vector3 GetCustomerFacingDirection() {
            if (_lookAtAnchor != null) {
                Vector3 dir = _lookAtAnchor.position - CustomerAnchor.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) return dir.normalized;
            }
            if (_tipAnchor != null && _tipAnchor != transform) {
                Vector3 dir = _tipAnchor.position - CustomerAnchor.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) return dir.normalized;
            }
            return CustomerAnchor.forward;
        }

        /// <summary>
        /// Ô bàn đã có người ngồi hay chưa (Không có hàng đợi).
        /// </summary>
        public bool IsOccupied => _currentCustomer != null;
        public CustomerAgent CurrentCustomer => _currentCustomer;

        public bool HasActiveOrder => (_activeTicket != null && _activeTicket.IsActive) || (_hasActiveOrder && _currentOrderBlock != null);
        public BlockDataSO CurrentOrderBlock => _currentOrderBlock;
        public double CurrentOrderPrice => _currentOrderPrice;
        public bool NeedsOrderTake => _needsOrderTake;

        public event Action<BlockDataSO> OnOrderPlaced;
        public event Action OnOrderCompleted;

        private void Reset() {
            AutoDiscoverAnchors();
        }

        private void OnValidate() {
            AutoDiscoverAnchors();
        }

        private void Awake() {
            AutoDiscoverAnchors();
            EnsureVisualSetup();
        }

        private void Start() {
            // Khởi động đơn ban đầu nếu được gán sẵn trong Inspector khi test Scene
            if (_activeTicket == null && _hasActiveOrder && _currentOrderBlock != null) {
                double price = _currentOrderPrice > 0 ? _currentOrderPrice : MiningStation.GetSellPriceForBlock(_currentOrderBlock);
                _activeTicket = OrderManager.Instance.CreateOrder(this, _currentOrderBlock, price);
            }
            UpdateOrderVisual();
        }

        private void Update() {
            if (_visualRoot == null || !_visualRoot.activeSelf) return;

            // 1. Luôn xoay mặt về Camera chính để người chơi nhìn rõ nhất (Billboarding)
            if (Camera.main != null) {
                _visualRoot.transform.rotation = Camera.main.transform.rotation;
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
        /// Tự động dò tìm các Anchor con theo tên chuẩn (Customer, Waiter, Tip, LookAnchor, SeatAnchor, ServerAnchor, v.v.).
        /// Đảm bảo tương thích 100% khi nhân bản/copy từ CustomerCounter hoặc tạo mới bàn ăn.
        /// </summary>
        public void AutoDiscoverAnchors() {
            if (_customerAnchor == null) {
                Transform found = transform.Find("Customer");
                if (found == null) found = transform.Find("CustomerAnchor");
                if (found == null) found = transform.Find("CustomerPoint");
                if (found == null) found = transform.Find("SeatAnchor");
                if (found == null) found = transform.Find("SeatPoint");
                if (found == null) found = transform.Find("Seat");
                if (found != null) _customerAnchor = found;
            }

            if (_serverAnchor == null) {
                Transform found = transform.Find("Waiter");
                if (found == null) found = transform.Find("ServerAnchor");
                if (found == null) found = transform.Find("ServerPoint");
                if (found == null) found = transform.Find("Server");
                if (found == null) found = transform.Find("Staff");
                if (found != null) _serverAnchor = found;
            }

            if (_tipAnchor == null) {
                Transform found = transform.Find("Tip");
                if (found == null) found = transform.Find("TipAnchor");
                if (found == null) found = transform.Find("TipPoint");
                if (found == null) found = transform.Find("TableAnchor");
                if (found != null) _tipAnchor = found;
            }

            if (_lookAtAnchor == null) {
                Transform found = transform.Find("LookAnchor");
                if (found == null) found = transform.Find("LookAt");
                if (found == null) found = transform.Find("LookPoint");
                if (found == null) found = transform.Find("FaceAnchor");
                if (found == null) found = transform.Find("FacingPoint");
                if (found != null) _lookAtAnchor = found;
            }
        }

        /// <summary>
        /// Khách hàng ngồi vào vị trí bàn này.
        /// Trả về true nếu ngồi thành công; false nếu bàn đã có người ngồi trước (Không cho xếp hàng!).
        /// </summary>
        public bool TryOccupy(CustomerAgent agent) {
            if (agent == null) return false;

            // Nếu bàn đã có người -> Từ chối ngay lập tức, không cho xếp hàng!
            if (IsOccupied) {
                if (_currentCustomer == agent) return true;
                return false;
            }

            _currentCustomer = agent;
            return true;
        }

        /// <summary>
        /// Khách hàng rời khỏi bàn (hoàn tất bữa ăn hoặc hủy đơn).
        /// </summary>
        public void Vacate(CustomerAgent agent) {
            if (agent != null && _currentCustomer == agent) {
                _needsOrderTake = false;
                CancelOrder();
                _currentCustomer = null;
            }
        }

        /// <summary>
        /// Vị trí ngồi chuẩn của khách tại ô bàn này (bám sát mặt phẳng NavMesh).
        /// Vì không có xếp hàng, vị trí luôn là điểm CustomerAnchor.
        /// </summary>
        public Vector3 GetSeatPosition() {
            Vector3 targetPos = CustomerAnchor.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) {
                return hit.position;
            }
            return targetPos;
        }

        /// <summary>
        /// Khách hàng ngồi vào bàn và giơ tay gọi nhân viên ra nhận order (hiển thị "?").
        /// </summary>
        public void SetNeedsOrderTake(bool needs) {
            // Chỉ cho phép hiển thị dấu "?" nếu thực sự có khách đang ngồi tại bàn này!
            if (needs && _currentCustomer == null) {
                _needsOrderTake = false;
                UpdateOrderVisual();
                return;
            }

            _needsOrderTake = needs;
            UpdateOrderVisual();
        }

        /// <summary>
        /// Nhân viên/Người chơi đã ra bàn nhận order. Đẩy order vào hệ thống xử lý trung tâm.
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
            Debug.Log($"[TableSlot - {name}] 🍽️ Khách tại bàn đã gọi: 1 [{blockData.DisplayName}] - Giá {price} Vàng!");
        }

        /// <summary>
        /// Kiểm tra xem nhân viên/người chơi có đang đứng đúng vị trí phục vụ cạnh bàn (ServerAnchor) không.
        /// </summary>
        public bool IsPlayerAtServerPosition(Vector3 playerPosition, float maxRadius = 0.85f) {
            if (_serverAnchor == null) return false;

            float distServerSqr = (_serverAnchor.position - playerPosition).sqrMagnitude;
            if (distServerSqr > maxRadius * maxRadius) return false;

            if (_customerAnchor != null) {
                float distCustSqr = (_customerAnchor.position - playerPosition).sqrMagnitude;
                if (distServerSqr >= distCustSqr) {
                    // Đang đứng ở phía ghế khách ngồi -> Không cho giao hàng từ góc này!
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Kiểm tra xem block trên tay có đúng loại mà khách tại bàn đang đợi không.
        /// </summary>
        public bool CanFulfillWith(BlockDataSO carriedBlockData) {
            if (!HasActiveOrder || carriedBlockData == null) return false;
            return carriedBlockData == _currentOrderBlock;
        }

        /// <summary>
        /// Giao hàng trực tiếp cho khách tại bàn:
        /// Tiêu thụ block, cộng vàng thanh toán và hoàn tất đơn hàng.
        /// </summary>
        public bool TryDeliverOrder(GameObject carriedBlockInstance, BlockDataSO carriedBlockData, OrderDeliveredEventChannelSO orderDeliveredChannel) {
            if (!CanFulfillWith(carriedBlockData)) return false;

            // 1. Tiêu hủy block thành phẩm trên tay
            if (carriedBlockInstance != null) {
                Destroy(carriedBlockInstance);
            }

            // 2. Bắn sự kiện thanh toán qua kênh OrderDeliveredEventChannelSO để EconomyManager cộng vàng
            if (orderDeliveredChannel != null) {
                OrderDeliveryPayload payload = new OrderDeliveryPayload(
                    stallId: "TableCounter",
                    blockId: _currentOrderBlock.BlockId,
                    basePrice: _currentOrderPrice > 0 ? _currentOrderPrice : MiningStation.GetSellPriceForBlock(_currentOrderBlock),
                    deliveryPosition: CustomerAnchor.position,
                    isVipCustomer: false
                );
                orderDeliveredChannel.RaiseEvent(payload);
            }

            // 2.5. Hiệu ứng các hạt vàng tròn tỏa ra tại TipAnchor
            Vector3 tipPos = _tipAnchor != null ? _tipAnchor.position : (transform.position + Vector3.up * 0.8f);
            GameObject particlePrefab = GetParticlePrefab();
            TipCoinBurstEffect.Play(tipPos, particlePrefab, _tipParticleCount);

            // 2.6. Kiểm tra tỉ lệ rơi tiền Tip tại bàn (Tỉ lệ đọc trực tiếp từ FloatStatSO.CurrentValue)
            float tipChance = _tipChanceStat != null ? _tipChanceStat.CurrentValue : 0.15f;

            if (UnityEngine.Random.value <= tipChance) {
                // Tiền tip = Trung bình cộng giá của toàn bộ block đã mở bán tại shop nhân với 2
                double tipAmount = MiningStation.CalculateTipAmount();
                WorldTipPickup.CreateTipInstance(tipPos, tipAmount, _worldTipPrefab, _fallbackTipColor);
                Debug.Log($"<color=#f1c40f>[TableSlot - {name}]</color> 💰 Khách tại bàn đã để lại {tipAmount} Vàng tiền Tip!");
            }

            // 3. Hoàn tất đơn hàng và giải phóng trạng thái order của ô bàn
            if (_activeTicket != null && OrderManager.Instance != null) {
                OrderManager.Instance.CompleteOrder(_activeTicket);
                _activeTicket = null;
            }

            _hasActiveOrder = false;
            _needsOrderTake = false;
            double payout = _currentOrderPrice > 0 ? _currentOrderPrice : MiningStation.GetSellPriceForBlock(_currentOrderBlock);
            _currentOrderBlock = null;
            _currentOrderPrice = 0;

            OnOrderCompleted?.Invoke();
            UpdateOrderVisual();
            Debug.Log($"[TableSlot - {name}] ✅ Đã phục vụ bàn ăn thành công! Nhận {payout} Vàng!");

            return true;
        }

        private GameObject GetParticlePrefab() {
            if (_tipParticlePrefab != null) return _tipParticlePrefab;
            TableCounter parentTable = GetComponentInParent<TableCounter>();
            if (parentTable != null && parentTable.TipParticlePrefab != null) {
                return parentTable.TipParticlePrefab;
            }
            return null;
        }

        /// <summary>
        /// Hủy đơn hàng tại bàn (khi khách rời đi do chờ quá lâu hoặc đổi vị trí).
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
        // 3D ORDER VISUAL (BONG BÓNG ĐƠN HÀNG 3D TRÊN ĐẦU KHÁCH / BÀN ĂN)
        // =========================================================================

        private void EnsureVisualSetup() {
            if (_visualRoot != null) return;

            _visualRoot = new GameObject("TableOrderVisual");
            _visualRoot.transform.SetParent(transform);
            _visualRoot.transform.position = CustomerAnchor.position + Vector3.up * _bubbleHeight;

            // 1. Tấm nền bong bóng Quad trắng
            if (_showBackgroundPlate) {
                _backgroundPlate = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _backgroundPlate.name = "BackgroundPlate";
                _backgroundPlate.transform.SetParent(_visualRoot.transform);
                _backgroundPlate.transform.localPosition = Vector3.zero;
                _backgroundPlate.transform.localScale = new Vector3(0.68f, 0.68f, 1f);

                Collider col = _backgroundPlate.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer r = _backgroundPlate.GetComponent<Renderer>();
                if (r != null) {
                    Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.96f, 0.96f, 0.96f, 0.92f);
                    r.material = mat;
                }
            }

            // 2. Chữ "?" khi khách đang chờ nhận order
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
                _visualRoot.SetActive(true);
                if (_textWaitingObj != null) _textWaitingObj.SetActive(true);
                if (_blockHolder != null) _blockHolder.SetActive(false);

                if (_currentBlockModelInstance != null) {
                    Destroy(_currentBlockModelInstance);
                    _currentBlockModelInstance = null;
                }
            } else if (HasActiveOrder && _currentOrderBlock != null) {
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

                Collider[] colliders = _currentBlockModelInstance.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++) {
                    Destroy(colliders[i]);
                }

                _currentBlockModelInstance.transform.localPosition = Vector3.zero;
                _currentBlockModelInstance.transform.localScale = Vector3.one * _block3DScale;
            } else {
                _visualRoot.SetActive(false);
                if (_currentBlockModelInstance != null) {
                    Destroy(_currentBlockModelInstance);
                    _currentBlockModelInstance = null;
                }
            }
        }

        private Color GetBlockColor(string blockId) {
            if (string.IsNullOrEmpty(blockId)) return Color.gray;
            switch (blockId.ToLower()) {
                case "dirt": return new Color(0.55f, 0.35f, 0.2f);
                case "wood": return new Color(0.6f, 0.45f, 0.25f);
                case "stone": return new Color(0.5f, 0.5f, 0.5f);
                case "iron": return new Color(0.85f, 0.75f, 0.65f);
                case "gold": return new Color(1f, 0.85f, 0.1f);
                case "diamond": return new Color(0.2f, 0.9f, 0.95f);
                default: return new Color(0.4f, 0.7f, 0.4f);
            }
        }

        private void OnDrawGizmos() {
            if (_customerAnchor != null) {
                // Khung hình tròn vị trí ngồi của Khách: Vàng khi trống, Xanh lá khi có khách
                Color custColor = IsOccupied ? new Color(0.1f, 0.9f, 0.3f, 0.9f) : new Color(1f, 0.85f, 0.1f, 0.85f);
                DrawGroundCircle(_customerAnchor.position, 0.35f, custColor);
                Gizmos.color = custColor;
                Gizmos.DrawWireSphere(_customerAnchor.position, 0.3f);

                // Mũi tên chỉ hướng quay mặt của khách vào bàn
                Vector3 lookDir = GetCustomerFacingDirection();
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
                Gizmos.DrawRay(_customerAnchor.position + Vector3.up * 0.05f, lookDir * 0.5f);

                if (HasActiveOrder) {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(_customerAnchor.position + Vector3.up * 1f, Vector3.one * 0.35f);
                }
            }

            if (_serverAnchor != null) {
                // Khung hình tròn vị trí đứng phục vụ của Nhân Viên: Màu tím / hồng đặc trưng
                Color servColor = new Color(0.9f, 0.3f, 0.9f, 0.85f);
                DrawGroundCircle(_serverAnchor.position, 0.35f, servColor);
                Gizmos.color = servColor;
                Gizmos.DrawWireSphere(_serverAnchor.position, 0.3f);
                Gizmos.DrawRay(_serverAnchor.position + Vector3.up * 0.05f, _serverAnchor.forward * 0.45f);
            }

            if (_lookAtAnchor != null) {
                // Điểm nhìn của khách: Quả cầu màu xanh ngọc Cyan
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_lookAtAnchor.position, 0.15f);
                if (_customerAnchor != null) {
                    Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                    Gizmos.DrawLine(_customerAnchor.position + Vector3.up * 0.1f, _lookAtAnchor.position);
                }
            }

            if (_customerAnchor != null && _serverAnchor != null) {
                Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
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

