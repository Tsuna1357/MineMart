using MineMart.Data;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MineMart.Gameplay.Player {
    /// <summary>
    /// Điều khiển Nhân vật chính (Steve / Chủ Shop).
    /// Hỗ trợ New Input System (phím WASD trên PC & Floating Joystick trên Mobile).
    /// Quản lý di chuyển góc nhìn 45 độ, đào block tại trạm mỏ và mang giao ra quầy thu ngân.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour {
        [Header("Movement Settings")]
        [Tooltip("Tốc độ di chuyển cơ bản của người chơi (chuẩn GDD: 4.5 m/s)")]
        [SerializeField] private float _moveSpeed = 4.5f;

        [Tooltip("Tốc độ xoay người theo hướng di chuyển")]
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Input System")]
        [Tooltip("Kéo action Move từ InputSystem_Actions vào đây")]
        [SerializeField] private InputActionReference _moveAction;

        [Header("Carrying & Interaction")]
        [Tooltip("Điểm neo trên đầu hoặc trước ngực để gắn khối block khi đang vác")]
        [SerializeField] private Transform _carryAnchor;

        [Tooltip("Transform chứa visual/model của nhân vật để chạy hiệu ứng Pop Squash/Stretch (để trống sẽ tự tìm child)")]
        [SerializeField] private Transform _visualModel;

        [Tooltip("Bán kính tương tác với trạm mỏ và quầy thu ngân")]
        [SerializeField] private float _interactionRadius = 0.7f;

        private CharacterController _characterController;
        private GameObject _carriedBlockInstance;
        private BlockDataSO _carriedBlockData;
        private float _miningTimer = 0f;
        private MiningStation _currentMiningStation;
        private CheckoutCounter _currentCheckoutCounter;
        private CustomerCounter _currentCustomerCounter;
        private OrderTicket _activeMiningTicket;

        // Bộ đệm Collider quét tương tác không rác bộ nhớ (Zero-GC NonAlloc)
        private readonly Collider[] _nearbyCollidersBuffer = new Collider[16];

        // Trạng thái khóa hành động khi đang đào mỏ (Mining Action Lock)
        private bool _isMiningLocked = false;
        private bool _isOrderTakingLocked = false;
        private float _orderTakingTimer = 0f;
        private bool _isPopping = false;
        private float _popTimer = 0f;
        private const float POP_DURATION = 0.16f;

        // Cooldown ngăn ngừa bốc lên rồi đặt xuống liên tục giữa người chơi và bàn trung chuyển
        private float _counterActionCooldown = 0f;

        public bool IsCarryingBlock => _carriedBlockInstance != null;
        public bool IsMiningLocked => _isMiningLocked;
        public float MiningProgress => (_currentMiningStation != null && _currentMiningStation.GetBaseCraftTime() > 0)
            ? Mathf.Clamp01(_miningTimer / _currentMiningStation.GetBaseCraftTime())
            : 0f;

        private void Awake() {
            _characterController = GetComponent<CharacterController>();

            if (_carryAnchor == null) {
                // Kiểm tra xem đã có sẵn child CarryAnchor hoặc GameObject con chưa
                Transform foundAnchor = transform.Find("CarryAnchor") ?? transform.Find("GameObject");
                if (foundAnchor != null) {
                    _carryAnchor = foundAnchor;
                } else {
                    // Tự động tạo điểm neo trên đầu với độ cao vừa vặn cho block 0.4m
                    GameObject anchorObj = new GameObject("CarryAnchor");
                    anchorObj.transform.SetParent(transform);
                    anchorObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                    _carryAnchor = anchorObj.transform;
                }
            }

            if (_visualModel == null) {
                // Nếu bản thân Player có MeshRenderer (như Cube), visualModel chính là transform này
                if (GetComponent<MeshRenderer>() != null || GetComponent<SkinnedMeshRenderer>() != null) {
                    _visualModel = transform;
                } else {
                    // Tìm child chứa Renderer (bỏ qua carry anchor)
                    for (int i = 0; i < transform.childCount; i++) {
                        Transform child = transform.GetChild(i);
                        if (child != _carryAnchor && child.GetComponent<Renderer>() != null) {
                            _visualModel = child;
                            break;
                        }
                    }

                    if (_visualModel == null) {
                        _visualModel = transform;
                    }
                }
            }
        }

        private void OnEnable() {
            _moveAction?.action?.Enable();
        }

        private void OnDisable() {
            _moveAction?.action?.Disable();
        }

        private void Update() {
            if (_counterActionCooldown > 0f) {
                _counterActionCooldown -= Time.deltaTime;
            }

            HandleMovement();
            DetectNearbyInteractions();
            HandleWorkCycle();
            UpdatePopEffect();
        }

        /// <summary>
        /// Xử lý di chuyển theo góc nhìn 45 độ Isometric và xoay hướng mặt.
        /// Khi đang bị khóa cứng để khai thác mỏ (_isMiningLocked), vô hiệu hóa toàn bộ di chuyển.
        /// </summary>
        private void HandleMovement() {
            // Khóa cứng di chuyển khi đang đào mỏ hoặc nhận order cho đến khi xong
            if (_isMiningLocked || _isOrderTakingLocked) {
                return;
            }

            Vector2 input = Vector2.zero;

            // 1. Đọc từ New Input System Action
            if (_moveAction != null && _moveAction.action != null) {
                input = _moveAction.action.ReadValue<Vector2>();
            }

            // 2. Fallback trực tiếp từ bàn phím (nếu chưa kịp gán action trên Inspector)
            if (input == Vector2.zero && Keyboard.current != null) {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                input = new Vector2(x, y).normalized;
            }

            Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

            if (moveDirection.sqrMagnitude > 0.001f) {
                // Di chuyển nhân vật
                _characterController.Move(moveDirection * (_moveSpeed * Time.deltaTime));

                // Xoay mặt mượt mà theo hướng di chuyển
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            // Đảm bảo nhân vật luôn đứng vững trên mặt sàn (Gravity nhẹ)
            if (!_characterController.isGrounded) {
                _characterController.Move(Vector3.down * (9.81f * Time.deltaTime));
            }
        }

        /// <summary>
        /// Quét các trạm mỏ và quầy thu ngân xung quanh trong bán kính tương tác không sinh rác bộ nhớ.
        /// </summary>
        private void DetectNearbyInteractions() {
            // Nếu người chơi đang bị khóa trong tiến trình đào mỏ, giữ nguyên trạm mỏ đang thao tác
            if (_isMiningLocked && _currentMiningStation != null) {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _interactionRadius, _nearbyCollidersBuffer);

            _currentMiningStation = null;
            _currentCheckoutCounter = null;
            _currentCustomerCounter = null;

            for (int i = 0; i < hitCount; i++) {
                Collider col = _nearbyCollidersBuffer[i];
                if (col == null) continue;

                // 1. Quét Trạm Mỏ: CHỈ nhận tương tác nếu mỏ đã mở khóa VÀ đang có đơn hàng cần khai thác (NeedsMining)
                if (_currentMiningStation == null) {
                    MiningStation station = col.GetComponentInParent<MiningStation>();
                    if (station != null && station.NeedsMining) {
                        _currentMiningStation = station;
                    }
                }

                // 2. Quét Quầy Trung Chuyển Tiếp Nhận Hàng (CheckoutCounter qua Collider)
                if (_currentCheckoutCounter == null) {
                    CheckoutCounter counter = col.GetComponentInParent<CheckoutCounter>();
                    if (counter != null) {
                        _currentCheckoutCounter = counter;
                    }
                }
            }

            // Quét Trạm Mỏ trực tiếp bằng Điểm Đào (MinerAnchor) - KHÔNG CẦN BoxCollider!
            if (_currentMiningStation == null && MiningStation.AllStations.Count > 0) {
                float maxDistSqr = 1.6f * 1.6f;
                for (int s = 0; s < MiningStation.AllStations.Count; s++) {
                    MiningStation station = MiningStation.AllStations[s];
                    if (station != null && station.NeedsMining) {
                        float distSqr = (station.MinerAnchor.position - transform.position).sqrMagnitude;
                        if (distSqr <= maxDistSqr) {
                            _currentMiningStation = station;
                            break;
                        }
                    }
                }
            }

            // Quét Quầy Tiếp Khách (CustomerCounter): BẮT BUỘC người chơi phải đứng ở vị trí ServerAnchor (Phía Phục Vụ)!
            // Tuyệt đối không cho phép nhận tương tác khi người chơi đang đứng ở phía khách hàng (CustomerAnchor)
            if (_currentCustomerCounter == null && CustomerCounter.AllCounters.Count > 0) {
                for (int c = 0; c < CustomerCounter.AllCounters.Count; c++) {
                    CustomerCounter counter = CustomerCounter.AllCounters[c];
                    if (counter != null && counter.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                        _currentCustomerCounter = counter;
                        break;
                    }
                }
            }

            // Quét Quầy Trung Chuyển (CheckoutCounter) trực tiếp bằng khoảng cách - KHÔNG CẦN BoxCollider!
            if (_currentCheckoutCounter == null && CheckoutCounter.AllCounters.Count > 0) {
                float maxDistSqr = 1.6f * 1.6f;
                for (int k = 0; k < CheckoutCounter.AllCounters.Count; k++) {
                    CheckoutCounter counter = CheckoutCounter.AllCounters[k];
                    if (counter != null) {
                        float distSqr = (counter.transform.position - transform.position).sqrMagnitude;
                        if (distSqr <= maxDistSqr) {
                            _currentCheckoutCounter = counter;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Chu trình làm việc: Đào tại mỏ khi có Order -> Giao thẳng cho khách hoặc đặt lên bàn trung chuyển.
        /// Đồng thời cho phép người chơi đóng vai PHỤC VỤ (Waiter): Bốc block từ bàn mang ra tiếp khách!
        /// Tích hợp cơ chế Magnet Pop-Snap dứt khoát và khóa cứng di chuyển (Mining Action Lock).
        /// </summary>
        private void HandleWorkCycle() {
            // TÌNH HUỐNG 1: Tay đang trống và tiếp cận Mỏ đang có đơn hàng -> Hút "póc" dính cộp và khóa cứng đào
            if (!IsCarryingBlock && _currentMiningStation != null && (_currentMiningStation.NeedsMining || _activeMiningTicket != null)) {
                if (!_isMiningLocked) {
                    // Đặt gạch nhận 1 đơn hàng từ hàng đợi OrderManager (giảm PendingCount về 0 ngay lập tức để mỏ khóa lại)
                    _activeMiningTicket = _currentMiningStation.ClaimOrderForMining(this);

                    // Nếu trạm yêu cầu phải có order mà không claim được vé nào (hết vé hoặc người khác đã nhận), không cho đào
                    if (_currentMiningStation.RequireOrderToMine && _activeMiningTicket == null) {
                        return;
                    }

                    // Lần đầu bước vào vùng mỏ: Hút dứt khoát "póc" vào MinerAnchor và khóa cứng
                    SnapToAnchorInstant(_currentMiningStation.MinerAnchor.position, _currentMiningStation.transform.position);
                    TriggerPopEffect();
                    _isMiningLocked = true;
                    _miningTimer = 0f;
                    _currentMiningStation.SetOccupied(true);
                } else {
                    // Đang trong thời gian khóa khai thác: Cố định vị trí và đếm tiến độ đào
                    FixPositionAtAnchor(_currentMiningStation.MinerAnchor.position);

                    _miningTimer += Time.deltaTime;
                    float requiredTime = _currentMiningStation.GetBaseCraftTime();

                    if (_miningTimer >= requiredTime) {
                        _miningTimer = 0f;
                        PickUpBlockFromStation(_currentMiningStation);

                        // Tiêu thụ / Hoàn tất việc đào đơn hàng tại mỏ (chuyển vé sang Delivering)
                        if (_activeMiningTicket != null) {
                            _currentMiningStation.FinishMiningOrder(_activeMiningTicket);
                            _activeMiningTicket = null;
                        } else {
                            _currentMiningStation.ConsumePendingOrder();
                        }

                        // Khai thác xong: Mở khóa di chuyển và nảy nhẹ block lên đầu
                        _isMiningLocked = false;
                        _currentMiningStation.SetOccupied(false);
                        TriggerPopEffect();
                    }
                }
            } else {
                // Nếu bước ra khỏi mỏ, mỏ hết order hoặc đã vác block: Giải phóng khóa, trả vé claim (nếu có) và reset timer
                if (_currentMiningStation == null || (!_currentMiningStation.NeedsMining && _activeMiningTicket == null) || IsCarryingBlock) {
                    if (_activeMiningTicket != null) {
                        if (OrderManager.Instance != null) {
                            OrderManager.Instance.ReleaseClaim(_activeMiningTicket);
                        }
                        _activeMiningTicket = null;
                    }

                    if (_currentMiningStation != null) {
                        _currentMiningStation.SetOccupied(false);
                    }

                    _isMiningLocked = false;
                    _miningTimer = 0f;
                }
            }

            // TÌNH HUỐNG 2A: Đang vác block trên tay và tiếp cận Quầy Tiếp Khách -> Giao thẳng tay cho khách (Direct Handover)!
            if (IsCarryingBlock && _currentCustomerCounter != null) {
                if (_currentCustomerCounter.CanFulfillAtPosition(transform.position, _carriedBlockData, out _)) {
                    DeliverBlockToCustomer(_currentCustomerCounter);
                    TriggerPopEffect();
                    return;
                }
            }

            // TÌNH HUỐNG 3: Tay đang trống và tiếp cận Quầy Tiếp Khách -> Khách đang chờ order -> "Póc" vào nhận order 1.5s
            if (!IsCarryingBlock && _currentCustomerCounter != null) {
                CustomerSlot targetSlot = null;
                for (int i = 0; i < _currentCustomerCounter.Slots.Count; i++) {
                    CustomerSlot slot = _currentCustomerCounter.Slots[i];
                    if (slot != null && slot.NeedsOrderTake && slot.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                        targetSlot = slot;
                        break;
                    }
                }

                if (targetSlot != null) {
                    if (!_isOrderTakingLocked) {
                        // Hút dứt khoát "póc" vào ServerAnchor và khóa cứng
                        SnapToAnchorInstant(targetSlot.ServerAnchor.position, targetSlot.transform.position);
                        TriggerPopEffect();
                        _isOrderTakingLocked = true;
                        _orderTakingTimer = 0f;
                    } else {
                        FixPositionAtAnchor(targetSlot.ServerAnchor.position);
                        _orderTakingTimer += Time.deltaTime;

                        if (_orderTakingTimer >= 1.5f) {
                            _orderTakingTimer = 0f;

                            BlockDataSO randomBlock = CustomerManager.Instance != null ? CustomerManager.Instance.GetRandomUnlockedBlock() : null;
                            if (randomBlock != null) {
                                double price = randomBlock.BaseValue > 0 ? randomBlock.BaseValue : 3;
                                targetSlot.TakeOrder(randomBlock, price);
                            }

                            _isOrderTakingLocked = false;
                            TriggerPopEffect();
                        }
                    }
                } else {
                    _isOrderTakingLocked = false;
                    _orderTakingTimer = 0f;
                }
            } else {
                _isOrderTakingLocked = false;
                _orderTakingTimer = 0f;
            }

            // TÌNH HUỐNG 2B (ĐẦU BẾP / THỢ): Đang vác block trên tay và tiếp cận Quầy Trung Chuyển -> Đặt lên ô phù hợp trên bàn
            if (IsCarryingBlock && _currentCheckoutCounter != null && _counterActionCooldown <= 0f) {
                if (!_currentCheckoutCounter.IsFull) {
                    DeliverBlockToCheckout(_currentCheckoutCounter);
                    _counterActionCooldown = 0.5f;
                    TriggerPopEffect();
                    return;
                }
            }

            // TÌNH HUỐNG 3 (PHỤC VỤ / WAITER): Tay đang trống và tiếp cận Quầy Trung Chuyển có block -> Bốc block từ bàn lên đầu mang cho khách!
            if (!IsCarryingBlock && _currentCheckoutCounter != null && !_currentCheckoutCounter.IsEmpty && _counterActionCooldown <= 0f) {
                PickUpBlockFromCheckout(_currentCheckoutCounter);
            }
        }

        /// <summary>
        /// Hút dứt khoát (Pop Snap) nhân vật về đúng điểm neo mỏ và xoay mặt hướng thẳng vào mục tiêu.
        /// Giữ nguyên độ cao trục Y mặt sàn hiện tại của người chơi để tuyệt đối không bị bay lên cao hay chìm xuống đất.
        /// </summary>
        private void SnapToAnchorInstant(Vector3 anchorTargetPos, Vector3 lookTargetPos) {
            if (_characterController != null) _characterController.enabled = false;

            // Chỉ snap tọa độ X và Z, bảo toàn nguyên vẹn độ cao trục Y của người chơi
            transform.position = new Vector3(anchorTargetPos.x, transform.position.y, anchorTargetPos.z);

            Vector3 lookDir = lookTargetPos - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }

            if (_characterController != null) _characterController.enabled = true;
        }

        /// <summary>
        /// Giữ chặt vị trí tại điểm neo trong suốt thời gian khóa đào mỏ (bảo toàn trục Y).
        /// </summary>
        private void FixPositionAtAnchor(Vector3 anchorTargetPos) {
            Vector3 targetPos = new Vector3(anchorTargetPos.x, transform.position.y, anchorTargetPos.z);
            if (Vector3.Distance(transform.position, targetPos) > 0.01f) {
                if (_characterController != null) _characterController.enabled = false;
                transform.position = targetPos;
                if (_characterController != null) _characterController.enabled = true;
            }
        }

        /// <summary>
        /// Kích hoạt hiệu ứng "Póc" (Juice Pop / Squash & Stretch) nảy đàn hồi như nam châm.
        /// </summary>
        private void TriggerPopEffect() {
            _isPopping = true;
            _popTimer = 0f;
        }

        /// <summary>
        /// Cập nhật hiệu ứng biến dạng Squash & Stretch bảo toàn thể tích tạo cảm giác nịnh mắt.
        /// </summary>
        private void UpdatePopEffect() {
            if (!_isPopping || _visualModel == null) return;

            _popTimer += Time.deltaTime;
            float t = _popTimer / POP_DURATION;

            if (t >= 1f) {
                _isPopping = false;
                _visualModel.localScale = Vector3.one;
                return;
            }

            // Curve hình sin: Nén xuống 0.78 rồi nảy đàn hồi về 1.0
            float sinVal = Mathf.Sin(t * Mathf.PI);
            float scaleY = Mathf.Lerp(1.0f, 0.78f, sinVal);
            float scaleXZ = 1f / Mathf.Sqrt(Mathf.Max(scaleY, 0.1f));

            _visualModel.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
        }

        /// <summary>
        /// Tạo block thành phẩm (size 0.4m) và nhấc lên đầu người chơi.
        /// Toàn bộ Collider trên block đều được loại bỏ để tránh xung đột vật lý với CharacterController.
        /// </summary>
        private void PickUpBlockFromStation(MiningStation station) {
            BlockDataSO blockData = station.ProducedBlock;
            if (blockData == null) return;

            GameObject blockPrefab = blockData.Prefab3D;
            if (blockPrefab != null) {
                _carriedBlockInstance = Instantiate(blockPrefab, _carryAnchor.position, Quaternion.identity, _carryAnchor);
            } else {
                // Fallback tạo một khối lập phương Cube 3D chuẩn kích thước 0.4m
                _carriedBlockInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _carriedBlockInstance.transform.SetParent(_carryAnchor);
                _carriedBlockInstance.transform.localPosition = Vector3.zero;
                _carriedBlockInstance.transform.localScale = Vector3.one * 0.4f;
            }

            // Loại bỏ toàn bộ Collider trên block để tránh đẩy CharacterController bay lên cao khi đặt hàng
            Collider[] colliders = _carriedBlockInstance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) {
                Destroy(colliders[i]);
            }

            _carriedBlockData = blockData;
        }

        /// <summary>
        /// Giao thẳng tay block cho khách hàng đang chờ tại CustomerCounter (Direct Handover).
        /// BẮT BUỘC: Người chơi phải đứng ở đúng vị trí phục vụ (ServerAnchor) của ô khách cần block đó!
        /// Tuyệt đối không cho phép đứng ở vị trí của khách (CustomerAnchor) hay bất kỳ đâu ngoài vị trí phục vụ.
        /// </summary>
        private void DeliverBlockToCustomer(CustomerCounter counter) {
            if (_carriedBlockInstance == null || counter == null) return;

            if (!counter.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                return;
            }

            if (counter.TryDeliverOrderAtPosition(transform.position, _carriedBlockInstance, _carriedBlockData)) {
                _carriedBlockInstance = null;
                _carriedBlockData = null;
            }
        }

        /// <summary>
        /// Dành cho Người Chơi đóng vai Phục Vụ (Waiter/Server):
        /// CHỈ cho phép nhấc block khi người chơi đang đứng ở đúng PHÍA LẤY HÀNG (TakeAnchor - Phía Shop).
        /// Tuyệt đối KHÔNG lấy nếu đang đứng ở phía đặt hàng (PlaceAnchor) hoặc ô đó đang trống!
        /// </summary>
        private void PickUpBlockFromCheckout(CheckoutCounter counter) {
            if (counter == null || IsCarryingBlock) return;

            // CHỈ lấy từ đúng điểm TakeAnchor (Phía Shop) trước mặt người chơi (bán kính 0.85m)
            if (counter.TryTakeBlockAtTakeAnchor(transform.position, 0.85f, out GameObject blockInstance, out BlockDataSO blockData)) {
                // Nhấc block lên đầu người chơi
                blockInstance.transform.SetParent(_carryAnchor);
                blockInstance.transform.localPosition = Vector3.zero;
                blockInstance.transform.localRotation = Quaternion.identity;
                blockInstance.transform.localScale = Vector3.one * 0.4f;

                // Xóa bỏ toàn bộ Collider trên block để tránh xung đột với CharacterController
                Collider[] colliders = blockInstance.GetComponentsInChildren<Collider>();
                for (int c = 0; c < colliders.Length; c++) {
                    Destroy(colliders[c]);
                }

                _carriedBlockInstance = blockInstance;
                _carriedBlockData = blockData;
                _counterActionCooldown = 0.5f;

                TriggerPopEffect();
                Debug.Log($"[PlayerController] 🍽️ Người chơi đã nhấc block [{blockData.DisplayName}] từ Phía Lấy (TakeAnchor) lên đầu để đi phục vụ!");
            }
        }

        /// <summary>
        /// Chuyển block từ trên đầu người chơi đặt lên ô tại Quầy Trung Chuyển.
        /// CHỈ cho phép đặt khi người chơi đang đứng ở đúng PHÍA ĐẶT HÀNG (PlaceAnchor - Phía Mỏ).
        /// Tuyệt đối KHÔNG cho đặt khi đang đứng ở phía lấy hàng (TakeAnchor).
        /// </summary>
        private void DeliverBlockToCheckout(CheckoutCounter counter) {
            if (_carriedBlockInstance == null || counter == null) return;

            // 1. Tìm đúng ô CounterSlot mà người chơi đang đứng trước mặt ở PHÍA ĐẶT (PlaceAnchor, bán kính 0.85m)
            CounterSlot targetSlot = counter.GetSlotAtPlacePosition(transform.position, 0.85f);

            // 2. CHỈ đặt nếu ô đó hợp lệ và có thể tiếp nhận loại block này
            if (targetSlot != null && targetSlot.CanAcceptBlockType(_carriedBlockData)) {
                if (targetSlot.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPosition)) {
                    // Tháo khỏi điểm neo người chơi và dịch chuyển đến vị trí trên mặt bàn của ô đó
                    _carriedBlockInstance.transform.SetParent(null);
                    _carriedBlockInstance.transform.position = targetPosition;
                    _carriedBlockInstance.transform.rotation = counter.transform.rotation;

                    _carriedBlockInstance = null;
                    _carriedBlockData = null;
                    _counterActionCooldown = 0.5f;

                    TriggerPopEffect();
                    Debug.Log($"[PlayerController] 📦 Đã đặt block vào ô [{targetSlot.name}] từ Phía Đặt (PlaceAnchor)!");
                }
            }
        }

        private void OnDrawGizmosSelected() {
            // Vẽ vòng tròn bán kính tương tác màu lục lam (Cyan)
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
    }
}
