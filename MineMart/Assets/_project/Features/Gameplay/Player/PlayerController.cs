using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Core.Settings;
using MineMart.Data;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Common;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using MineMart.Gameplay.Tables;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace MineMart.Gameplay.Player {
    /// <summary>
    /// Điều khiển Nhân vật chính (Steve / Chủ Shop).
    /// Hỗ trợ New Input System (phím WASD trên PC & Floating Joystick trên Mobile).
    /// Hỗ trợ chế độ Hybrid Control: Tự động chuyển sang Auto-Pilot sau 5s buông tay,
    /// và ngắt tức thì trả quyền điều khiển khi người chơi chạm lại.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour {
        public static PlayerController Instance { get; private set; }

        [Header("Movement Settings")]
        [Tooltip("Tốc độ di chuyển cơ bản của người chơi (chuẩn GDD: 4.5 m/s)")]
        [SerializeField] private float _moveSpeed = 4.5f;

        [Tooltip("Hệ số tăng tốc khi người chơi tự tay điều khiển bằng Joystick hoặc Bàn phím (VD: 1.0 = giữ nguyên tốc độ, 1.25 = nhanh hơn 25%, 1.5 = nhanh hơn 50%)")]
        [SerializeField] private float _manualMoveSpeedMultiplier = 1.25f;

        private float _speedBonusPercent = 0f;
        public float BaseMoveSpeed => _moveSpeed;
        public float CurrentMoveSpeed => _moveSpeed * (1f + Mathf.Max(0f, _speedBonusPercent));

        /// <summary>
        /// Tốc độ di chuyển thực tế khi người chơi tự tay điều khiển (đã nhân hệ số ManualMoveSpeedMultiplier).
        /// </summary>
        public float ManualMoveSpeed => CurrentMoveSpeed * Mathf.Max(0.1f, _manualMoveSpeedMultiplier);

        public float ManualMoveSpeedMultiplier {
            get => _manualMoveSpeedMultiplier;
            set => _manualMoveSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// Áp dụng hệ số thưởng tốc độ chạy từ MapManager (Upgrade: IncreaseAISpeed).
        /// </summary>
        public void ApplySpeedBonus(float bonusPercent) {
            _speedBonusPercent = bonusPercent;
            if (_navMeshAgent != null) {
                _navMeshAgent.speed = CurrentMoveSpeed;
            }
        }

        [Tooltip("Tốc độ xoay người theo hướng di chuyển")]
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Auto-Pilot Settings")]
        [Tooltip("Cho phép tính năng tự động hoạt động sau 5s buông tay")]
        [SerializeField] private bool _autoPilotFeatureEnabled = true;

        [Tooltip("Thời gian buông tay (giây) trước khi kích hoạt Auto-Pilot (chuẩn: 5.0s)")]
        [SerializeField] private float _autoPilotIdleThreshold = 5.0f;

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

        [Header("Player Mining Settings")]
        [Tooltip("Hệ số nhân tốc độ đào của người chơi (mặc định = 2.0 để người chơi đào nhanh gấp đôi thợ)")]
        [SerializeField] private float _playerMiningSpeedMultiplier = 2.0f;

        private CharacterController _characterController;
        private NavMeshAgent _navMeshAgent;
        private GameObject _carriedBlockInstance;
        private BlockDataSO _carriedBlockData;
        private float _miningTimer = 0f;
        private MiningStation _interruptedMiningStation;
        private float _savedMiningTimer = 0f;
        private MiningStation _currentMiningStation;
        private CheckoutCounter _currentCheckoutCounter;
        private CustomerCounter _currentCustomerCounter;
        private TableCounter _currentTableCounter;
        private OrderTicket _activeMiningTicket;

        // Bộ đếm Collider quét tương tác không rác bộ nhớ (Zero-GC NonAlloc)
        private readonly Collider[] _nearbyCollidersBuffer = new Collider[16];

        // Trạng thái khóa hành động khi đang đào mỏ hoặc nhận order (Action Lock)
        private bool _isMiningLocked = false;
        private bool _isOrderTakingLocked = false;
        private float _orderTakingTimer = 0f;
        private bool _isPopping = false;
        private float _popTimer = 0f;
        private const float POP_DURATION = 0.16f;

        // Cooldown ngăn ngừa bốc lên rồi đặt xuống liên tục giữa người chơi và bàn trung chuyển
        private float _counterActionCooldown = 0f;

        // Quản lý trạng thái Auto-Pilot
        private float _idleTimer = 0f;
        private bool _isAutoPiloting = false;
        private PlayerAutoState _autoState = PlayerAutoState.Idle;
        private float _autoActionTimer = 0f;

        private CustomerSlot _autoTargetCustomerSlot;
        private MiningStation _autoTargetMiningStation;
        private CheckoutCounter _autoTargetCheckoutCounter;
        private CharacterAnimationController _animController;

        private enum PlayerAutoState {
            Idle,
            WalkingToTakeOrder,
            TakingOrder,
            WalkingToMine,
            Mining,
            WalkingToDeliverCustomer,
            WalkingToPickupCheckout,
            WalkingToDeliverCheckout,
            WaitingForCheckoutSpace
        }

        public bool IsCarryingBlock => _carriedBlockInstance != null;
        public bool IsMiningLocked => _isMiningLocked;
        public float MiningProgress => (_currentMiningStation != null && _currentMiningStation.GetBaseCraftTime() > 0)
            ? Mathf.Clamp01(_miningTimer / _currentMiningStation.GetBaseCraftTime())
            : 0f;

        public bool IsAutoPilotFeatureEnabled => _autoPilotFeatureEnabled;
        public bool IsAutoPiloting => _isAutoPiloting;
        public float CurrentIdleTime => _idleTimer;

        /// <summary>
        /// Bật hoặc tắt chức năng Auto-Pilot (dùng cho nút bấm Setting hoặc kịch bản).
        /// Nếu tắt trong khi đang auto, lập tức trả quyền điều khiển về cho người chơi.
        /// </summary>
        public void SetAutoPilotFeatureEnabled(bool enabled) {
            _autoPilotFeatureEnabled = enabled;
            if (!enabled && _isAutoPiloting) {
                CancelAutoPilot();
            }
        }

        /// <summary>
        /// Cưỡng bức chuyển đổi trạng thái Auto-Pilot ngay lập tức (không cần chờ hết 5s buông tay).
        /// </summary>
        public void SetAutoPilotActive(bool active) {
            if (active) {
                if (_autoPilotFeatureEnabled && !_isAutoPiloting) {
                    ActivateAutoPilot();
                }
            } else {
                if (_isAutoPiloting) {
                    CancelAutoPilot();
                }
            }
        }

        private void Awake() {
            Instance = this;
            _characterController = GetComponent<CharacterController>();
            if (_characterController != null) {
                // Đảm bảo stepOffset và slopeLimit đủ cao để bước lên bậc thang 1 block (1 mét)
                _characterController.stepOffset = Mathf.Max(_characterController.stepOffset, 1.15f);
                _characterController.slopeLimit = Mathf.Max(_characterController.slopeLimit, 55f);

                // 1. Tự động nâng Center lên Height / 2 nếu đang là 0 để chân đứng đúng trên sàn, tránh bị kẹt cứng vào lòng đất
                if (_characterController.center.y < 0.1f) {
                    _characterController.center = new Vector3(_characterController.center.x, _characterController.height * 0.5f, _characterController.center.z);
                }
            }

            // 2. Tự động tắt mọi Collider con bên trong model (nếu có) để tránh CharacterController tự va chạm gây kẹt đứng yên
            Collider[] childColliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < childColliders.Length; i++) {
                if (childColliders[i] != _characterController) {
                    childColliders[i].enabled = false;
                }
            }

            // 3. Tự động tắt Apply Root Motion trên Animator (FBX thường mặc định bật, gây khóa cứng transform.position)
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null) {
                anim.applyRootMotion = false;
            }

            // Khởi tạo NavMeshAgent để phục vụ Auto-Pilot tự tìm đường
            _navMeshAgent = GetComponent<NavMeshAgent>();
            if (_navMeshAgent == null) {
                _navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }
            _navMeshAgent.speed = CurrentMoveSpeed;
            _navMeshAgent.angularSpeed = 3600f; // Tốc độ xoay cực cao
            _navMeshAgent.acceleration = 60f; // Tăng tốc dứt khoát
            _navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Đi xuyên người khác theo GDD
            _navMeshAgent.stoppingDistance = 0.45f;
            _navMeshAgent.updateRotation = false; // Tắt xoay mặc định của NavMesh để xoay tức thì không moonwalk
            _navMeshAgent.enabled = false; // Mặc định tắt để CharacterController xử lý thao tác tay

            // 4. Khởi tạo Animation Controller (Chuyển đổi Run / Idle mượt mà)
            _animController = GetComponent<CharacterAnimationController>();
            if (_animController == null) {
                _animController = gameObject.AddComponent<CharacterAnimationController>();
            }
            _animController.AutoDetectMovement = false; // PlayerController trực tiếp quản lý chuyển động từ Input / AutoPilot
            _animController.EnableBouncyBobbing = false; // Mặc định khi người chơi tự điều khiển tay thì chạy thẳng bình thường

            if (_carryAnchor == null) {
                Transform foundAnchor = CharacterAnimationController.FindChildRecursive(transform, "CarryAnchor") 
                    ?? CharacterAnimationController.FindChildRecursive(transform, "Block_Positons")
                    ?? CharacterAnimationController.FindChildRecursive(transform, "GameObject");
                if (foundAnchor != null) {
                    _carryAnchor = foundAnchor;
                } else {
                    GameObject anchorObj = new GameObject("CarryAnchor");
                    anchorObj.transform.SetParent(transform);
                    anchorObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                    _carryAnchor = anchorObj.transform;
                }
            }

            if (_visualModel == null) {
                if (GetComponent<MeshRenderer>() != null || GetComponent<SkinnedMeshRenderer>() != null) {
                    _visualModel = transform;
                } else {
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
            MiningStation.OnStationRelocated += HandleStationRelocated;
        }

        private void OnDisable() {
            _moveAction?.action?.Disable();
            MiningStation.OnStationRelocated -= HandleStationRelocated;
        }

        private void Start() {
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnJoystickToggled += HandleJoystickToggled;
                HandleJoystickToggled(SettingsManager.Instance.IsJoystickEnabled);
            }
        }

        private void OnDestroy() {
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnJoystickToggled -= HandleJoystickToggled;
            }

            if (Instance == this) {
                Instance = null;
            }
        }

        private void HandleJoystickToggled(bool enabled) {
            if (!enabled) {
                SettingsManager.VirtualJoystickInput = Vector2.zero;
            }
        }

        private void Update() {
            if (_counterActionCooldown > 0f) {
                _counterActionCooldown -= Time.deltaTime;
            }

            // Đọc input từ bàn phím hoặc joystick
            Vector2 input = ReadMovementInput();
            bool hasInput = input.sqrMagnitude > 0.001f;

            if (hasInput) {
                _idleTimer = 0f;
                if (_isAutoPiloting) {
                    CancelAutoPilot();
                }
            } else {
                // Chỉ đếm thời gian buông tay khi không bị khóa hành động (đào/nhận order)
                if (_autoPilotFeatureEnabled && !_isAutoPiloting && !_isMiningLocked && !_isOrderTakingLocked) {
                    _idleTimer += Time.deltaTime;
                    if (_idleTimer >= _autoPilotIdleThreshold) {
                        ActivateAutoPilot();
                    }
                }
            }

            if (_isAutoPiloting) {
                UpdateAutoPilot();
            } else {
                HandleMovement(input);
                DetectNearbyInteractions();
                HandleWorkCycle();
            }

            // Cập nhật Animation Run / Idle
            if (_animController != null) {
                bool isWalkingInAuto = _isAutoPiloting && (
                    _autoState == PlayerAutoState.WalkingToTakeOrder ||
                    _autoState == PlayerAutoState.WalkingToMine ||
                    _autoState == PlayerAutoState.WalkingToDeliverCustomer ||
                    _autoState == PlayerAutoState.WalkingToPickupCheckout ||
                    _autoState == PlayerAutoState.WalkingToDeliverCheckout
                );
                bool isNavMoving = _navMeshAgent != null && _navMeshAgent.enabled && (
                    _navMeshAgent.velocity.sqrMagnitude > 0.01f || 
                    _navMeshAgent.desiredVelocity.sqrMagnitude > 0.01f ||
                    (_navMeshAgent.hasPath && _navMeshAgent.remainingDistance > Mathf.Max(_navMeshAgent.stoppingDistance, 0.1f))
                );

                bool isManualMoving = !_isAutoPiloting && (hasInput || (_characterController != null && _characterController.velocity.sqrMagnitude > 0.05f));
                bool isPlayerMoving = (_isAutoPiloting && (isWalkingInAuto || isNavMoving)) || isManualMoving;
                float activeSpeed = _isAutoPiloting ? CurrentMoveSpeed : ManualMoveSpeed;
                _animController.SetMoveSpeed(activeSpeed);
                _animController.SetMoving(isPlayerMoving);
            }

            UpdatePopEffect();
        }

        private float _autoPilotWaddleTimer = 0f;

        private void LateUpdate() {
            // Khi ở chế độ AI Auto-Pilot và đang di chuyển: nghiêng lắc lư toàn thân (waddle) như Worker / Customer
            if (_isAutoPiloting && _animController != null && _animController.IsMoving) {
                _autoPilotWaddleTimer += Time.deltaTime * 7f;
                float tiltZ = Mathf.Sin(_autoPilotWaddleTimer) * 8.5f;
                transform.rotation *= Quaternion.Euler(0f, 0f, -tiltZ);
            } else {
                _autoPilotWaddleTimer = 0f;
            }
        }

        /// <summary>
        /// Đọc input di chuyển từ New Input System hoặc Fallback Keyboard.
        /// Tự động tôn trọng cài đặt Bật/Tắt Joystick từ SettingsManager.
        /// </summary>
        private Vector2 ReadMovementInput() {
            Vector2 input = Vector2.zero;

            bool joystickAllowed = SettingsManager.Instance == null || SettingsManager.Instance.IsJoystickEnabled;

            if (joystickAllowed && _moveAction != null && _moveAction.action != null) {
                input = _moveAction.action.ReadValue<Vector2>();
            }

            // Đọc thêm từ cần điều khiển cảm ứng ảo (Virtual Joystick) trên màn hình
            if (joystickAllowed && input == Vector2.zero && SettingsManager.VirtualJoystickInput.sqrMagnitude > 0.001f) {
                input = SettingsManager.VirtualJoystickInput;
            }

            if (input == Vector2.zero && Keyboard.current != null) {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                input = new Vector2(x, y).normalized;
            }

            return input;
        }

        /// <summary>
        /// Xử lý di chuyển thủ công khi người chơi cầm lái.
        /// </summary>
        private void HandleMovement(Vector2 input) {
            if (_isMiningLocked || _isOrderTakingLocked) {
                return;
            }

            Vector3 moveDirection = new Vector3(input.x, 0f, input.y).normalized;

            if (moveDirection.sqrMagnitude > 0.001f) {
                float moveDist = ManualMoveSpeed * Time.deltaTime;
                Vector3 horizontalMovement = moveDirection * moveDist;

                // =========================================================================
                // THUẬT TOÁN CLIMB BẬC THANG 1 BLOCK (1 MÉT) TỰ ĐỘNG CHO CHARACTER CONTROLLER
                // Khắc phục triệt để hạn chế của PhysX capsule: khi đi thẳng/ngang vuông góc
                // vào mặt phẳng đứng của khối 1m, cylinder va chạm phẳng nên Unity không tự bước lên.
                // =========================================================================
                float charRadius = _characterController.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                Vector3 footPos = transform.position + moveDirection * (charRadius * 0.75f) + Vector3.up * 0.2f;
                Vector3 headPos = transform.position + moveDirection * (charRadius * 0.75f) + Vector3.up * 1.25f;

                // 1. Kiểm tra chân có chạm vào mặt đứng của bậc thang không (khoảng cách 0.35m phía trước mép capsule)
                if (Physics.Raycast(footPos, moveDirection, out RaycastHit footHit, 0.35f, ~0, QueryTriggerInteraction.Ignore)) {
                    // 2. Kiểm tra khoảng không trên đầu (1.25m) phải thông thoáng (không phải tường cao hay vật cản)
                    if (!Physics.Raycast(headPos, moveDirection, 0.45f, ~0, QueryTriggerInteraction.Ignore)) {
                        // 3. Bắn tia từ đỉnh bậc thang xuống để lấy cao độ chính xác của mặt trên
                        Vector3 stepTopOrigin = transform.position + moveDirection * (charRadius + 0.25f) + Vector3.up * 1.35f;
                        if (Physics.Raycast(stepTopOrigin, Vector3.down, out RaycastHit stepHit, 1.4f, ~0, QueryTriggerInteraction.Ignore)) {
                            float stepHeight = stepHit.point.y - transform.position.y;
                            // Chiều cao bậc hợp lệ (từ 0.08m đến 1.15m) và mặt trên là mặt phẳng đi được (normal hướng lên)
                            if (stepHeight > 0.08f && stepHeight <= 1.15f && stepHit.normal.y > 0.65f) {
                                // Bước trèo lên đỉnh bậc thang:
                                // Nhấc capsule lên cao hơn mặt bậc 0.04m -> tiến ngang vào lòng bậc -> hạ chân tiếp đất
                                _characterController.Move(Vector3.up * (stepHeight + 0.04f));
                                _characterController.Move(horizontalMovement);
                                _characterController.Move(Vector3.down * 0.04f);
                                goto AfterMove;
                            }
                        }
                    }
                }

                // Di chuyển bình thường khi không gặp bậc thang
                _characterController.Move(horizontalMovement);

            AfterMove:
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            if (!_characterController.isGrounded) {
                _characterController.Move(Vector3.down * (18f * Time.deltaTime));
            }
        }

        // =========================================================================
        // AUTO-PILOT SYSTEM (THỢ SIÊU CẤP TỰ HÀNH)
        // =========================================================================

        private void ActivateAutoPilot() {
            _isAutoPiloting = true;
            if (_characterController != null) {
                _characterController.enabled = false;
            }
            if (_navMeshAgent != null) {
                _navMeshAgent.enabled = true;
                _navMeshAgent.Warp(transform.position);
            }
            _autoState = PlayerAutoState.Idle;
            _autoActionTimer = 0f;
            TriggerPopEffect();
            Debug.Log("[PlayerController] 🤖 Kích hoạt chế độ Auto-Pilot sau 5 giây buông tay!");
        }

        private void CancelAutoPilot() {
            _isAutoPiloting = false;
            _idleTimer = 0f;

            if (_navMeshAgent != null && _navMeshAgent.enabled) {
                _navMeshAgent.ResetPath();
                _navMeshAgent.updateRotation = true;
                _navMeshAgent.enabled = false;
            }

            if (_characterController != null) {
                _characterController.enabled = true;
            }

            if (_autoTargetMiningStation != null) {
                _autoTargetMiningStation.ReleaseSlot(this);
                if (_activeMiningTicket != null) {
                    if (OrderManager.Instance != null) {
                        OrderManager.Instance.ReleaseClaim(_activeMiningTicket);
                    }
                    _activeMiningTicket = null;
                }
                _autoTargetMiningStation = null;
            }

            _autoTargetCustomerSlot = null;
            _autoTargetCheckoutCounter = null;
            _autoActionTimer = 0f;
            _autoState = PlayerAutoState.Idle;

            Debug.Log("[PlayerController] 🎮 Người chơi chạm tay điều khiển - Đã hủy Auto-Pilot!");
        }

        /// <summary>
        /// Ngắt hành động khai thác nếu trạm mỏ bị người chơi nhấc lên di chuyển,
        /// đồng thời lưu lại tiến độ đào để có thể tiếp tục khi mỏ được đặt xuống.
        /// </summary>
        public float CancelMiningIfTarget(MiningStation station) {
            if (station == null) return 0f;
            float saved = 0f;

            if (_currentMiningStation == station) {
                saved = _miningTimer;
                if (_activeMiningTicket != null) {
                    station.ReleaseClaim(_activeMiningTicket);
                    _activeMiningTicket = null;
                }
                station.ReleaseSlot(this);
                _currentMiningStation = null;
                _isMiningLocked = false;
                _miningTimer = 0f;
            }

            if (_autoTargetMiningStation == station) {
                if (saved <= 0f) saved = _autoActionTimer;
                if (_activeMiningTicket != null) {
                    station.ReleaseClaim(_activeMiningTicket);
                    _activeMiningTicket = null;
                }
                station.ReleaseSlot(this);
                _autoTargetMiningStation = null;
                _autoActionTimer = 0f;
                if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh) {
                    _navMeshAgent.ResetPath();
                    _navMeshAgent.updateRotation = true;
                }
                _autoState = PlayerAutoState.Idle;
            }

            _interruptedMiningStation = station;
            _savedMiningTimer = saved;
            return saved;
        }

        public void CancelManualMining() {
            if (_activeMiningTicket != null) {
                if (_currentMiningStation != null) {
                    _currentMiningStation.ReleaseClaim(_activeMiningTicket);
                } else if (OrderManager.Instance != null) {
                    OrderManager.Instance.ReleaseClaim(_activeMiningTicket);
                }
                _activeMiningTicket = null;
            }

            if (_currentMiningStation != null) {
                _currentMiningStation.ReleaseSlot(this);
                _currentMiningStation = null;
            }

            _isMiningLocked = false;
            _miningTimer = 0f;
            _interruptedMiningStation = null;
            _savedMiningTimer = 0f;
        }

        private void HandleStationRelocated(MiningStation station) {
            if (station == null || !station.IsUnlocked || !_isAutoPiloting) return;

            // Nếu người chơi đang bật Auto-Pilot và ở trạng thái rảnh rỗi (Idle)
            if (_autoState == PlayerAutoState.Idle && !IsCarryingBlock) {
                if ((_interruptedMiningStation == station || _interruptedMiningStation == null) && station.NeedsMining) {
                    _activeMiningTicket = station.ClaimOrderForMining(this);
                    if (_activeMiningTicket != null || !station.RequireOrderToMine) {
                        Transform targetAnchor = station.ClaimSlot(this);
                        _autoTargetMiningStation = station;
                        _autoState = PlayerAutoState.WalkingToMine;
                        SetAutoDestination(targetAnchor.position);
                        Debug.Log($"[PlayerController] 🤖 Auto-Pilot: Tự động chạy sang vị trí mới của [{station.StationData?.StallName ?? station.name}] để tiếp tục đào!");
                    }
                }
            }
        }

        private void SetAutoDestination(Vector3 targetPos) {
            if (_navMeshAgent != null && _navMeshAgent.enabled) {
                _navMeshAgent.updateRotation = false;
                _navMeshAgent.SetDestination(targetPos);
                // Xoay tức thì ngay frame đầu tiên về hướng mục tiêu để không bị trễ/moonwalk
                Vector3 toTarget = targetPos - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.02f) {
                    transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
                }
            }
        }

        private void UpdateAutoPilot() {
            switch (_autoState) {
                case PlayerAutoState.Idle:
                    UpdateAutoIdle();
                    break;
                case PlayerAutoState.WalkingToTakeOrder:
                    UpdateAutoWalkingToTakeOrder();
                    break;
                case PlayerAutoState.TakingOrder:
                    UpdateAutoTakingOrder();
                    break;
                case PlayerAutoState.WalkingToMine:
                    UpdateAutoWalkingToMine();
                    break;
                case PlayerAutoState.Mining:
                    UpdateAutoMining();
                    break;
                case PlayerAutoState.WalkingToDeliverCustomer:
                    UpdateAutoWalkingToDeliverCustomer();
                    break;
                case PlayerAutoState.WalkingToPickupCheckout:
                    UpdateAutoWalkingToPickupCheckout();
                    break;
                case PlayerAutoState.WalkingToDeliverCheckout:
                    UpdateAutoWalkingToDeliverCheckout();
                    break;
                case PlayerAutoState.WaitingForCheckoutSpace:
                    UpdateAutoWaitingForCheckoutSpace();
                    break;
            }

            // Đồng bộ xoay tức thì theo hướng di chuyển của NavMeshAgent khi đang đi bộ
            UpdateAutoRotation();
        }

        private void UpdateAutoRotation() {
            if (!_isAutoPiloting || _navMeshAgent == null || !_navMeshAgent.enabled) return;

            bool isWalking = _autoState == PlayerAutoState.WalkingToTakeOrder ||
                             _autoState == PlayerAutoState.WalkingToMine ||
                             _autoState == PlayerAutoState.WalkingToDeliverCustomer ||
                             _autoState == PlayerAutoState.WalkingToPickupCheckout ||
                             _autoState == PlayerAutoState.WalkingToDeliverCheckout;

            if (isWalking) {
                Vector3 moveDir = _navMeshAgent.desiredVelocity;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.05f) {
                    // Xoay tức thì 100% theo hướng di chuyển thực tế, loại bỏ hoàn toàn moonwalk
                    transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
                }
            }
        }

        private void UpdateAutoIdle() {
            // ƯU TIÊN 1: ĐANG CẦM BLOCK TRÊN TAY
            if (IsCarryingBlock) {
                // 1.1 Khách hàng nào đang cần block này?
                CustomerSlot neededCustomer = FindCustomerNeedingBlock(_carriedBlockData);
                if (neededCustomer != null) {
                    _autoTargetCustomerSlot = neededCustomer;
                    _autoState = PlayerAutoState.WalkingToDeliverCustomer;
                    SetAutoDestination(neededCustomer.ServerAnchor.position);
                    return;
                }

                // 1.2 Không có khách cần ngay -> Mang ra bàn trung chuyển
                CheckoutCounter availableCounter = FindAvailableCheckoutCounter();
                if (availableCounter != null && !availableCounter.IsFull) {
                    _autoTargetCheckoutCounter = availableCounter;
                    _autoState = PlayerAutoState.WalkingToDeliverCheckout;
                    CounterSlot slot = availableCounter.GetSlotAtPlacePosition(availableCounter.transform.position, 100f);
                    SetAutoDestination(slot != null ? slot.PlaceAnchor.position : availableCounter.transform.position);
                    return;
                }
                return;
            }

            // ƯU TIÊN 2: TAY TRỐNG - XEM CÓ KHÁCH NÀO CẦN NHẬN ORDER KHÔNG
            CustomerSlot slotNeedsOrder = FindSlotNeedingOrder();
            if (slotNeedsOrder != null) {
                _autoTargetCustomerSlot = slotNeedsOrder;
                _autoState = PlayerAutoState.WalkingToTakeOrder;
                SetAutoDestination(slotNeedsOrder.ServerAnchor.position);
                return;
            }

            // ƯU TIÊN 3: TAY TRỐNG - XEM TRÊN BÀN CÓ BLOCK NÀO KHÁCH ĐANG CHỜ KHÔNG
            foreach (var customerCounter in CustomerCounter.AllCounters) {
                if (customerCounter == null || customerCounter.Slots == null) continue;
                foreach (var cSlot in customerCounter.Slots) {
                    if (cSlot != null && cSlot.HasActiveOrder && !cSlot.NeedsOrderTake) {
                        foreach (var checkout in CheckoutCounter.AllCounters) {
                            if (checkout != null && checkout.HasBlockType(cSlot.CurrentOrderBlock)) {
                                _autoTargetCheckoutCounter = checkout;
                                _autoTargetCustomerSlot = cSlot;
                                _autoState = PlayerAutoState.WalkingToPickupCheckout;

                                CounterSlot takeSlot = checkout.GetSlotAtTakePosition(checkout.transform.position, 100f);
                                SetAutoDestination(takeSlot != null ? takeSlot.TakeAnchor.position : checkout.transform.position);
                                return;
                            }
                        }
                    }
                }
            }

            // ƯU TIÊN 4: TAY TRỐNG - TÌM MỎ ĐANG CÓ ORDER CẦN ĐÀO
            MiningStation station = FindStationNeedingMining();
            if (station != null) {
                _activeMiningTicket = station.ClaimOrderForMining(this);
                if (_activeMiningTicket != null || !station.RequireOrderToMine) {
                    Transform targetAnchor = station.ClaimSlot(this);
                    _autoTargetMiningStation = station;
                    _autoState = PlayerAutoState.WalkingToMine;
                    SetAutoDestination(targetAnchor.position);
                    return;
                }
            }
        }

        private void UpdateAutoWalkingToTakeOrder() {
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance) {
                if (_autoTargetCustomerSlot != null && _autoTargetCustomerSlot.NeedsOrderTake) {
                    transform.rotation = _autoTargetCustomerSlot.ServerAnchor.rotation;
                    _navMeshAgent.updateRotation = false;
                    _autoState = PlayerAutoState.TakingOrder;
                    _autoActionTimer = 0f;
                } else {
                    _autoState = PlayerAutoState.Idle;
                }
            }
        }

        private void UpdateAutoTakingOrder() {
            if (_autoTargetCustomerSlot == null || !_autoTargetCustomerSlot.NeedsOrderTake) {
                _navMeshAgent.updateRotation = true;
                _autoTargetCustomerSlot = null;
                _autoState = PlayerAutoState.Idle;
                return;
            }

            transform.rotation = _autoTargetCustomerSlot.ServerAnchor.rotation;
            _autoActionTimer += Time.deltaTime;

            if (_autoActionTimer >= 1.5f) {
                if (CustomerManager.Instance != null) {
                    BlockDataSO randomBlock = CustomerManager.Instance.GetRandomUnlockedBlock();
                    if (randomBlock != null) {
                        double price = MiningStation.GetSellPriceForBlock(randomBlock);
                        _autoTargetCustomerSlot.TakeOrder(randomBlock, price);
                    }
                }
                _autoTargetCustomerSlot = null;
                _navMeshAgent.updateRotation = true;
                TriggerPopEffect();
                _autoState = PlayerAutoState.Idle;
            }
        }

        private void UpdateAutoWalkingToMine() {
            if (_autoTargetMiningStation == null || _autoTargetMiningStation.IsBeingDragged) {
                if (_autoTargetMiningStation != null) {
                    _autoTargetMiningStation.ReleaseSlot(this);
                    if (_activeMiningTicket != null) {
                        _autoTargetMiningStation.ReleaseClaim(_activeMiningTicket);
                        _activeMiningTicket = null;
                    }
                    _autoTargetMiningStation = null;
                }
                _navMeshAgent.updateRotation = true;
                _autoState = PlayerAutoState.Idle;
                return;
            }

            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance) {
                Transform anchor = _autoTargetMiningStation.GetMinerAnchorFor(this);
                // Đảm bảo khoảng cách thực tế tới anchor đã tới đích thật sự (tránh lag tọa độ khi trạm vừa dời vị trí)
                if (anchor != null && Vector3.Distance(transform.position, anchor.position) > 1.2f) {
                    SetAutoDestination(anchor.position);
                    return;
                }

                transform.rotation = anchor.rotation;
                _navMeshAgent.updateRotation = false;
                _autoState = PlayerAutoState.Mining;
                var myAutoSlot = _autoTargetMiningStation != null ? _autoTargetMiningStation.GetSlotForWorker(this) : null;
                float savedTime = _autoTargetMiningStation != null ? _autoTargetMiningStation.GetSavedCraftTimer(this) : 0f;
                if (savedTime <= 0f && myAutoSlot != null) savedTime = myAutoSlot.CurrentCraftTimer;
                if (savedTime <= 0f && _autoTargetMiningStation != null) savedTime = _autoTargetMiningStation.CurrentCraftTimer;
                _autoActionTimer = savedTime;
                _savedMiningTimer = 0f;
                _interruptedMiningStation = null;
                TriggerPopEffect();
            }
        }

        private void UpdateAutoMining() {
            if (_autoTargetMiningStation == null || _autoTargetMiningStation.IsBeingDragged ||
                Vector3.Distance(transform.position, _autoTargetMiningStation.transform.position) > 2.5f) {
                if (_autoTargetMiningStation != null) {
                    _autoTargetMiningStation.ReleaseSlot(this);
                    if (_activeMiningTicket != null) {
                        _autoTargetMiningStation.ReleaseClaim(_activeMiningTicket);
                        _activeMiningTicket = null;
                    }
                    _autoTargetMiningStation = null;
                }
                _navMeshAgent.updateRotation = true;
                _autoState = PlayerAutoState.Idle;
                return;
            }

            Transform anchor = _autoTargetMiningStation.GetMinerAnchorFor(this);
            transform.rotation = anchor.rotation;

            bool completed = _autoTargetMiningStation.AddMiningProgress(this, Time.deltaTime * _playerMiningSpeedMultiplier);
            var myAutoSlot = _autoTargetMiningStation.GetSlotForWorker(this);
            _autoActionTimer = myAutoSlot != null ? myAutoSlot.CurrentCraftTimer : _autoTargetMiningStation.CurrentCraftTimer;

            if (completed) {
                PickUpBlockFromStation(_autoTargetMiningStation);

                if (_activeMiningTicket != null) {
                    _autoTargetMiningStation.FinishMiningOrder(_activeMiningTicket);
                    _activeMiningTicket = null;
                } else {
                    _autoTargetMiningStation.ConsumePendingOrder();
                }

                _autoTargetMiningStation.ReleaseSlot(this);
                _autoTargetMiningStation = null;
                _navMeshAgent.updateRotation = true;
                TriggerPopEffect();
                _autoState = PlayerAutoState.Idle;
            }
        }

        private void UpdateAutoWalkingToDeliverCustomer() {
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance) {
                if (_autoTargetCustomerSlot != null && _carriedBlockInstance != null) {
                    transform.rotation = _autoTargetCustomerSlot.ServerAnchor.rotation;
                    CustomerCounter parentCounter = FindParentCounter(_autoTargetCustomerSlot);
                    OrderDeliveredEventChannelSO channel = parentCounter != null ? parentCounter.OrderDeliveredChannel : null;

                    bool success = _autoTargetCustomerSlot.TryDeliverOrder(_carriedBlockInstance, _carriedBlockData, channel);
                    if (success) {
                        _carriedBlockInstance = null;
                        _carriedBlockData = null;
                        TriggerPopEffect();
                    }
                }

                _autoTargetCustomerSlot = null;
                _navMeshAgent.updateRotation = true;
                _autoState = PlayerAutoState.Idle;
            }
        }

        private void UpdateAutoWalkingToPickupCheckout() {
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance) {
                if (_autoTargetCheckoutCounter != null && _autoTargetCustomerSlot != null) {
                    if (_autoTargetCheckoutCounter.TryTakeBlockOfType(_autoTargetCustomerSlot.CurrentOrderBlock, out GameObject blockInstance)) {
                        blockInstance.transform.SetParent(_carryAnchor);
                        blockInstance.transform.localPosition = Vector3.zero;
                        blockInstance.transform.localRotation = Quaternion.identity;
                        blockInstance.transform.localScale = Vector3.one * 0.4f;

                        Collider[] colliders = blockInstance.GetComponentsInChildren<Collider>();
                        for (int c = 0; c < colliders.Length; c++) {
                            Destroy(colliders[c]);
                        }

                        _carriedBlockInstance = blockInstance;
                        _carriedBlockData = _autoTargetCustomerSlot.CurrentOrderBlock;
                        CarriedBlockPriceBadge.Attach(_carriedBlockInstance, _carriedBlockData);

                        _autoState = PlayerAutoState.WalkingToDeliverCustomer;
                        SetAutoDestination(_autoTargetCustomerSlot.ServerAnchor.position);
                        TriggerPopEffect();
                        return;
                    }
                }

                _autoTargetCustomerSlot = null;
                _autoTargetCheckoutCounter = null;
                _autoState = PlayerAutoState.Idle;
            }
        }

        private void UpdateAutoWalkingToDeliverCheckout() {
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance) {
                if (_autoTargetCheckoutCounter != null && !_autoTargetCheckoutCounter.IsFull && _carriedBlockInstance != null) {
                    if (_autoTargetCheckoutCounter.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPos)) {
                        CarriedBlockPriceBadge.Detach(_carriedBlockInstance);
                        _carriedBlockInstance.transform.SetParent(null);
                        _carriedBlockInstance.transform.position = targetPos;
                        _carriedBlockInstance.transform.rotation = _autoTargetCheckoutCounter.transform.rotation;

                        _carriedBlockInstance = null;
                        _carriedBlockData = null;
                        TriggerPopEffect();
                        _autoTargetCheckoutCounter = null;
                        _autoState = PlayerAutoState.Idle;
                        return;
                    }
                }

                _autoState = PlayerAutoState.WaitingForCheckoutSpace;
            }
        }

        private void UpdateAutoWaitingForCheckoutSpace() {
            if (_autoTargetCheckoutCounter != null && !_autoTargetCheckoutCounter.IsFull && _carriedBlockInstance != null) {
                if (_autoTargetCheckoutCounter.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPos)) {
                    CarriedBlockPriceBadge.Detach(_carriedBlockInstance);
                    _carriedBlockInstance.transform.SetParent(null);
                    _carriedBlockInstance.transform.position = targetPos;
                    _carriedBlockInstance.transform.rotation = _autoTargetCheckoutCounter.transform.rotation;

                    _carriedBlockInstance = null;
                    _carriedBlockData = null;
                    TriggerPopEffect();
                    _autoTargetCheckoutCounter = null;
                    _autoState = PlayerAutoState.Idle;
                }
            }
        }

        // =========================================================================
        // MANUAL INTERACTION SYSTEM
        // =========================================================================

        private void DetectNearbyInteractions() {
            if (_isMiningLocked && _currentMiningStation != null) {
                float distSqr = (_currentMiningStation.transform.position - transform.position).sqrMagnitude;
                if (!_currentMiningStation.IsBeingDragged && distSqr <= 2.5f * 2.5f) {
                    return;
                }

                // Nếu mỏ đã bị kéo đi, hoán đổi hoặc di chuyển xa -> Hủy khóa đào ngay lập tức
                CancelManualMining();
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _interactionRadius, _nearbyCollidersBuffer);

            _currentMiningStation = null;
            _currentCheckoutCounter = null;
            _currentCustomerCounter = null;
            _currentTableCounter = null;

            for (int i = 0; i < hitCount; i++) {
                Collider col = _nearbyCollidersBuffer[i];
                if (col == null) continue;

                if (_currentMiningStation == null) {
                    MiningStation station = col.GetComponentInParent<MiningStation>();
                    if (station != null && station.NeedsMining) {
                        _currentMiningStation = station;
                    }
                }

                if (_currentCheckoutCounter == null) {
                    CheckoutCounter counter = col.GetComponentInParent<CheckoutCounter>();
                    if (counter != null) {
                        _currentCheckoutCounter = counter;
                    }
                }
            }

            if (_currentMiningStation == null && MiningStation.AllStations.Count > 0) {
                float maxDistSqr = 1.6f * 1.6f;
                for (int s = 0; s < MiningStation.AllStations.Count; s++) {
                    MiningStation station = MiningStation.AllStations[s];
                    if (station != null && station.NeedsMining) {
                        bool foundNearSlot = false;
                        for (int k = 0; k < station.Slots.Count; k++) {
                            var slot = station.Slots[k];
                            if (slot != null && slot.MinerAnchor != null && (!slot.IsOccupied || slot.OccupyingWorker == (object)this)) {
                                float distSqr = (slot.MinerAnchor.position - transform.position).sqrMagnitude;
                                if (distSqr <= maxDistSqr) {
                                    _currentMiningStation = station;
                                    foundNearSlot = true;
                                    break;
                                }
                            }
                        }
                        if (foundNearSlot) break;
                    }
                }
            }

            // Quét tìm Bàn Ăn gần nhất người chơi đang đứng phục vụ
            if (_currentTableCounter == null && TableCounter.AllTables.Count > 0) {
                for (int t = 0; t < TableCounter.AllTables.Count; t++) {
                    TableCounter table = TableCounter.AllTables[t];
                    if (table != null && table.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                        _currentTableCounter = table;
                        break;
                    }
                }
            }

            if (_currentCustomerCounter == null && CustomerCounter.AllCounters.Count > 0) {
                for (int c = 0; c < CustomerCounter.AllCounters.Count; c++) {
                    CustomerCounter counter = CustomerCounter.AllCounters[c];
                    if (counter != null && counter.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                        _currentCustomerCounter = counter;
                        break;
                    }
                }
            }

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

        private void HandleWorkCycle() {
            // TÌNH HUỐNG 1: Tay đang trống và tiếp cận Mỏ đang có đơn hàng
            if (!IsCarryingBlock && _currentMiningStation != null && (_currentMiningStation.NeedsMining || _activeMiningTicket != null)) {
                if (!_isMiningLocked) {
                    _activeMiningTicket = _currentMiningStation.ClaimOrderForMining(this);

                    if (_currentMiningStation.RequireOrderToMine && _activeMiningTicket == null) {
                        return;
                    }

                    Transform anchor = _currentMiningStation.ClaimSlot(this);
                    SnapToAnchorInstant(anchor.position, _currentMiningStation.transform.position);
                    TriggerPopEffect();
                    _isMiningLocked = true;
                    var myManualSlot = _currentMiningStation.GetSlotForWorker(this);
                    float savedManualTime = _currentMiningStation.GetSavedCraftTimer(this);
                    if (savedManualTime <= 0f && myManualSlot != null) savedManualTime = myManualSlot.CurrentCraftTimer;
                    if (savedManualTime <= 0f) savedManualTime = _currentMiningStation.CurrentCraftTimer;
                    _miningTimer = savedManualTime;
                    _savedMiningTimer = 0f;
                    _interruptedMiningStation = null;
                } else {
                    Transform anchor = _currentMiningStation.GetMinerAnchorFor(this);
                    Vector3 anchorPos = anchor != null ? anchor.position : _currentMiningStation.transform.position;
                    float distSqr = (anchorPos - transform.position).sqrMagnitude;
                    if (_currentMiningStation.IsBeingDragged || distSqr > 3.0f * 3.0f) {
                        CancelManualMining();
                        return;
                    }

                    if (anchor != null) {
                        FixPositionAtAnchor(anchor.position);
                    }

                    bool completed = _currentMiningStation.AddMiningProgress(this, Time.deltaTime * _playerMiningSpeedMultiplier);
                    var myManualSlot = _currentMiningStation.GetSlotForWorker(this);
                    _miningTimer = myManualSlot != null ? myManualSlot.CurrentCraftTimer : _currentMiningStation.CurrentCraftTimer;

                    if (completed) {
                        _miningTimer = 0f;
                        PickUpBlockFromStation(_currentMiningStation);

                        if (_activeMiningTicket != null) {
                            _currentMiningStation.FinishMiningOrder(_activeMiningTicket);
                            _activeMiningTicket = null;
                        } else {
                            _currentMiningStation.ConsumePendingOrder();
                        }

                        _isMiningLocked = false;
                        _currentMiningStation.ReleaseSlot(this);
                        TriggerPopEffect();
                    }
                }
            } else {
                if (_currentMiningStation == null || (!_currentMiningStation.NeedsMining && _activeMiningTicket == null) || IsCarryingBlock) {
                    if (_activeMiningTicket != null) {
                        if (OrderManager.Instance != null) {
                            OrderManager.Instance.ReleaseClaim(_activeMiningTicket);
                        }
                        _activeMiningTicket = null;
                    }

                    if (_currentMiningStation != null) {
                        _currentMiningStation.ReleaseSlot(this);
                    }

                    _isMiningLocked = false;
                    _miningTimer = 0f;
                }
            }

            // TÌNH HUỐNG 2A-Table: Đang vác block trên tay và tiếp cận Bàn Ăn
            if (IsCarryingBlock && _currentTableCounter != null) {
                if (_currentTableCounter.CanFulfillAtPosition(transform.position, _carriedBlockData, out _)) {
                    DeliverBlockToTable(_currentTableCounter);
                    TriggerPopEffect();
                    return;
                }
            }

            // TÌNH HUỐNG 2A: Đang vác block trên tay và tiếp cận Quầy Tiếp Khách
            if (IsCarryingBlock && _currentCustomerCounter != null) {
                if (_currentCustomerCounter.CanFulfillAtPosition(transform.position, _carriedBlockData, out _)) {
                    DeliverBlockToCustomer(_currentCustomerCounter);
                    TriggerPopEffect();
                    return;
                }
            }

            // TÌNH HUỐNG 3-Table: Tay đang trống và tiếp cận Bàn Ăn -> Nhận order 1.5s
            if (!IsCarryingBlock && _currentTableCounter != null) {
                TableSlot targetTableSlot = null;
                for (int i = 0; i < _currentTableCounter.Slots.Count; i++) {
                    TableSlot slot = _currentTableCounter.Slots[i];
                    if (slot != null && slot.NeedsOrderTake && slot.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                        targetTableSlot = slot;
                        break;
                    }
                }

                if (targetTableSlot != null) {
                    if (!_isOrderTakingLocked) {
                        SnapToAnchorInstant(targetTableSlot.ServerAnchor.position, targetTableSlot.transform.position);
                        TriggerPopEffect();
                        _isOrderTakingLocked = true;
                        _orderTakingTimer = 0f;
                    } else {
                        FixPositionAtAnchor(targetTableSlot.ServerAnchor.position);
                        _orderTakingTimer += Time.deltaTime;

                        if (_orderTakingTimer >= 1.5f) {
                            _orderTakingTimer = 0f;

                            BlockDataSO randomBlock = CustomerManager.Instance != null ? CustomerManager.Instance.GetRandomUnlockedBlock() : null;
                            if (randomBlock != null) {
                                double price = MiningStation.GetSellPriceForBlock(randomBlock);
                                targetTableSlot.TakeOrder(randomBlock, price);
                            }

                            _isOrderTakingLocked = false;
                            TriggerPopEffect();
                        }
                    }
                    return;
                }
            }

            // TÌNH HUỐNG 3: Tay đang trống và tiếp cận Quầy Tiếp Khách -> Nhận order 1.5s
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
                                double price = MiningStation.GetSellPriceForBlock(randomBlock);
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

            // TÌNH HUỐNG 2B: Đang vác block trên tay và tiếp cận Quầy Trung Chuyển -> Đặt lên bàn
            if (IsCarryingBlock && _currentCheckoutCounter != null && _counterActionCooldown <= 0f) {
                if (!_currentCheckoutCounter.IsFull) {
                    DeliverBlockToCheckout(_currentCheckoutCounter);
                    _counterActionCooldown = 0.5f;
                    TriggerPopEffect();
                    return;
                }
            }

            // TÌNH HUỐNG 3B: Phục vụ bốc block từ bàn trung chuyển lên đầu
            if (!IsCarryingBlock && _currentCheckoutCounter != null && !_currentCheckoutCounter.IsEmpty && _counterActionCooldown <= 0f) {
                PickUpBlockFromCheckout(_currentCheckoutCounter);
            }
        }

        private void SnapToAnchorInstant(Vector3 anchorTargetPos, Vector3 lookTargetPos) {
            bool ccWasEnabled = _characterController != null && _characterController.enabled;
            if (ccWasEnabled) _characterController.enabled = false;

            transform.position = new Vector3(anchorTargetPos.x, transform.position.y, anchorTargetPos.z);

            Vector3 lookDir = lookTargetPos - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }

            if (ccWasEnabled && !_isAutoPiloting) _characterController.enabled = true;
        }

        private void FixPositionAtAnchor(Vector3 anchorTargetPos) {
            Vector3 targetPos = new Vector3(anchorTargetPos.x, transform.position.y, anchorTargetPos.z);
            if (Vector3.Distance(transform.position, targetPos) > 0.01f) {
                bool ccWasEnabled = _characterController != null && _characterController.enabled;
                if (ccWasEnabled) _characterController.enabled = false;
                transform.position = targetPos;
                if (ccWasEnabled && !_isAutoPiloting) _characterController.enabled = true;
            }
        }

        private void TriggerPopEffect() {
            _isPopping = true;
            _popTimer = 0f;
        }

        private void UpdatePopEffect() {
            if (!_isPopping || _visualModel == null) return;

            _popTimer += Time.deltaTime;
            float t = _popTimer / POP_DURATION;

            if (t >= 1f) {
                _isPopping = false;
                _visualModel.localScale = Vector3.one;
                return;
            }

            float sinVal = Mathf.Sin(t * Mathf.PI);
            float scaleY = Mathf.Lerp(1.0f, 0.78f, sinVal);
            float scaleXZ = 1f / Mathf.Sqrt(Mathf.Max(scaleY, 0.1f));

            _visualModel.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
        }

        private void PickUpBlockFromStation(MiningStation station) {
            BlockDataSO blockData = station.ProducedBlock;
            if (blockData == null) return;

            GameObject blockPrefab = blockData.Prefab3D;
            if (blockPrefab != null) {
                _carriedBlockInstance = Instantiate(blockPrefab, _carryAnchor.position, Quaternion.identity, _carryAnchor);
            } else {
                _carriedBlockInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _carriedBlockInstance.transform.SetParent(_carryAnchor);
                _carriedBlockInstance.transform.localPosition = Vector3.zero;
                _carriedBlockInstance.transform.localScale = Vector3.one * 0.4f;
            }

            Collider[] colliders = _carriedBlockInstance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) {
                Destroy(colliders[i]);
            }

            _carriedBlockData = blockData;
            CarriedBlockPriceBadge.Attach(_carriedBlockInstance, blockData);
        }

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

        private void DeliverBlockToTable(TableCounter table) {
            if (_carriedBlockInstance == null || table == null) return;

            if (!table.IsPlayerAtServerPosition(transform.position, 0.85f)) {
                return;
            }

            if (table.TryDeliverOrderAtPosition(transform.position, _carriedBlockInstance, _carriedBlockData)) {
                _carriedBlockInstance = null;
                _carriedBlockData = null;
            }
        }

        private void PickUpBlockFromCheckout(CheckoutCounter counter) {
            if (counter == null || IsCarryingBlock) return;

            if (counter.TryTakeBlockAtTakeAnchor(transform.position, 0.85f, out GameObject blockInstance, out BlockDataSO blockData)) {
                blockInstance.transform.SetParent(_carryAnchor);
                blockInstance.transform.localPosition = Vector3.zero;
                blockInstance.transform.localRotation = Quaternion.identity;
                blockInstance.transform.localScale = Vector3.one * 0.4f;

                Collider[] colliders = blockInstance.GetComponentsInChildren<Collider>();
                for (int c = 0; c < colliders.Length; c++) {
                    Destroy(colliders[c]);
                }

                _carriedBlockInstance = blockInstance;
                _carriedBlockData = blockData;
                CarriedBlockPriceBadge.Attach(_carriedBlockInstance, blockData);
                _counterActionCooldown = 0.5f;

                TriggerPopEffect();
                Debug.Log($"[PlayerController] 🍽️ Người chơi đã nhấc block [{blockData.DisplayName}] từ Phía Lấy (TakeAnchor) lên đầu!");
            }
        }

        private void DeliverBlockToCheckout(CheckoutCounter counter) {
            if (_carriedBlockInstance == null || counter == null) return;

            CounterSlot targetSlot = counter.GetSlotAtPlacePosition(transform.position, 0.85f);

            if (targetSlot != null && targetSlot.CanAcceptBlockType(_carriedBlockData)) {
                if (targetSlot.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPosition)) {
                    CarriedBlockPriceBadge.Detach(_carriedBlockInstance);
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

        // =========================================================================
        // HELPER UTILS
        // =========================================================================

        private CustomerSlot FindSlotNeedingOrder() {
            foreach (var counter in CustomerCounter.AllCounters) {
                if (counter == null || counter.Slots == null) continue;
                foreach (var slot in counter.Slots) {
                    if (slot != null && slot.NeedsOrderTake) {
                        return slot;
                    }
                }
            }
            return null;
        }

        private CustomerCounter FindParentCounter(CustomerSlot slot) {
            if (slot == null) return null;
            foreach (var counter in CustomerCounter.AllCounters) {
                if (counter != null && counter.Slots != null) {
                    foreach (var s in counter.Slots) {
                        if (s == slot) return counter;
                    }
                }
            }
            return null;
        }

        private MiningStation FindStationNeedingMining() {
            foreach (var station in MiningStation.AllStations) {
                if (station != null && station.NeedsMining) return station;
            }
            return null;
        }

        private CheckoutCounter FindAvailableCheckoutCounter() {
            if (CheckoutCounter.AllCounters.Count > 0) return CheckoutCounter.AllCounters[0];
            return null;
        }

        private CustomerSlot FindCustomerNeedingBlock(BlockDataSO block) {
            if (block == null) return null;
            foreach (var counter in CustomerCounter.AllCounters) {
                if (counter == null || counter.Slots == null) continue;
                foreach (var slot in counter.Slots) {
                    if (slot != null && slot.CanFulfillWith(block)) return slot;
                }
            }
            return null;
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
    }
}
