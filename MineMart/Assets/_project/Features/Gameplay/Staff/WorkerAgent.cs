using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Common;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using MineMart.Gameplay.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Staff {
    public enum WorkerState {
        Idle,
        WalkingToTakeOrder,
        TakingOrder,
        WalkingToMine,
        Mining,
        WalkingToCheckoutCounter,
        WaitingForTableSpace,
        WalkingToCheckoutCounterForPickup, // Mới thêm: Đi tới bàn để bê đồ
        WalkingToCustomerCounter,
        DeliveringToCustomer
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class WorkerAgent : MonoBehaviour {
        [Header("Settings")]
        [SerializeField] private WorkerRole _role;
        [SerializeField] private float _orderTakeTime = 1.5f;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _stoppingDistance = 0.5f;

        public WorkerRole Role => _role;

        /// <summary>
        /// Gán vai trò cho nhân viên (được điều phối từ MapManager).
        /// </summary>
        public void SetRole(WorkerRole role) {
            _role = role;
        }

        private NavMeshAgent _agent;
        private WorkerState _state = WorkerState.Idle;

        private static readonly List<WorkerAgent> s_allWorkers = new List<WorkerAgent>();
        public static IReadOnlyList<WorkerAgent> AllWorkers => s_allWorkers;

        // Visual / Animation (Tái sử dụng chung Pop effect sau này)
        private GameObject _carriedBlockInstance;
        private BlockDataSO _carriedBlockData;
        private Transform _carryAnchor;

        // Context Targets
        private CustomerSlot _targetCustomerSlot;
        private TableSlot _targetTableSlot;
        private MiningStation _targetMiningStation;
        private CheckoutCounter _targetCheckoutCounter;
        private OrderTicket _activeTicket;

        private float _timer = 0f;
        private MiningStation _interruptedStation;
        private float _savedCraftTimer = 0f;
        private CharacterAnimationController _animController;

        private void Awake() {
            _agent = GetComponent<NavMeshAgent>();
            // Nhân viên đi nhanh hơn khách một chút để phục vụ cho kịp
            _agent.speed = _moveSpeed;
            _agent.angularSpeed = 3600f;
            _agent.acceleration = 60f;
            // Cho phép nhân viên đi xuyên qua nhau và xuyên qua khách để không bị kẹt đường
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            // Giảm nửa tầm tương tác (cự ly với) để thợ phải tiến sát vào quầy mới được lấy/đặt đồ
            _agent.stoppingDistance = _stoppingDistance;

            _animController = GetComponent<CharacterAnimationController>();
            if (_animController == null) {
                _animController = gameObject.AddComponent<CharacterAnimationController>();
            }

            Transform foundAnchor = transform.Find("CarryAnchor");
            if (foundAnchor != null) _carryAnchor = foundAnchor;
            else {
                GameObject obj = new GameObject("CarryAnchor");
                obj.transform.SetParent(transform);
                obj.transform.localPosition = new Vector3(0, 1.25f, 0);
                _carryAnchor = obj.transform;
            }
        }

        private void OnEnable() {
            if (!s_allWorkers.Contains(this)) {
                s_allWorkers.Add(this);
            }
            MiningStation.OnStationRelocated += HandleStationRelocated;

            if (Map.MapManager.Instance != null) {
                ApplySpeedBonus(Map.MapManager.Instance.AISpeedBonusPercent);
            }
        }

        /// <summary>
        /// Áp dụng hệ số thưởng tốc độ chạy từ MapManager (Upgrade: IncreaseAISpeed).
        /// </summary>
        public void ApplySpeedBonus(float bonusPercent) {
            if (_agent != null) {
                _agent.speed = _moveSpeed * (1f + Mathf.Max(0f, bonusPercent));
            }
        }

        private void OnDisable() {
            s_allWorkers.Remove(this);
            MiningStation.OnStationRelocated -= HandleStationRelocated;
        }

        /// <summary>
        /// Ngắt hành động khai thác nếu trạm mỏ bị nhấc lên di chuyển,
        /// bảo lưu thời gian đã đào để thợ có thể tiếp tục khi mỏ hạ cánh.
        /// </summary>
        public float CancelMiningIfTarget(MiningStation station) {
            if (station == null) return 0f;
            float saved = 0f;

            if (_targetMiningStation == station) {
                if (_state == WorkerState.Mining) {
                    saved = _timer;
                }
                if (_activeTicket != null) {
                    station.ReleaseClaim(_activeTicket);
                    _activeTicket = null;
                }
                station.ReleaseSlot(this);
                _targetMiningStation = null;
                _timer = 0f;
                _agent.updateRotation = true;
                if (_agent.isOnNavMesh) {
                    _agent.ResetPath();
                }
                _state = WorkerState.Idle;
            }

            _interruptedStation = station;
            _savedCraftTimer = saved;
            return saved;
        }

        private void HandleStationRelocated(MiningStation station) {
            if (station == null || !station.IsUnlocked) return;

            // Nếu thợ đang rảnh rỗi và có vai trò đào mỏ
            if (_state == WorkerState.Idle && _carriedBlockData == null && (_role == WorkerRole.Miner || _role == WorkerRole.DirectSeller)) {
                // Ưu tiên mỏ vừa bị dời đi mà thợ đang đào dở, hoặc mỏ đang cần người đào
                if ((_interruptedStation == station || _interruptedStation == null) && station.NeedsMining) {
                    _activeTicket = station.ClaimOrderForMining(this);
                    if (_activeTicket != null || !station.RequireOrderToMine) {
                        Transform anchor = station.ClaimSlot(this);
                        _targetMiningStation = station;
                        _state = WorkerState.WalkingToMine;
                        _agent.SetDestination(anchor.position);
                        Debug.Log($"[WorkerAgent] 🏃 Worker {name} tự động chạy sang vị trí mới của [{station.StationData?.StallName ?? station.name}] để tiếp tục đào!");
                    }
                }
            }
        }

        private void Update() {
            switch (_state) {
                case WorkerState.Idle:
                    UpdateIdle();
                    break;
                case WorkerState.WalkingToTakeOrder:
                    UpdateWalkingToTakeOrder();
                    break;
                case WorkerState.TakingOrder:
                    UpdateTakingOrder();
                    break;
                case WorkerState.WalkingToMine:
                    UpdateWalkingToMine();
                    break;
                case WorkerState.Mining:
                    UpdateMining();
                    break;
                case WorkerState.WalkingToCheckoutCounter:
                    UpdateWalkingToCheckoutCounter();
                    break;
                case WorkerState.WaitingForTableSpace:
                    UpdateWaitingForTableSpace();
                    break;
                case WorkerState.WalkingToCheckoutCounterForPickup:
                    UpdateWalkingToCheckoutCounterForPickup();
                    break;
                case WorkerState.WalkingToCustomerCounter:
                    UpdateWalkingToCustomerCounter();
                    break;
                case WorkerState.DeliveringToCustomer:
                    UpdateDeliveringToCustomer();
                    break;
            }

            // Đồng bộ trạng thái di chuyển và nhún nảy (Bouncy Bobbing) mượt mà cho Worker
            if (_animController != null) {
                bool isWorkerWalking = _state == WorkerState.WalkingToTakeOrder ||
                                       _state == WorkerState.WalkingToMine ||
                                       _state == WorkerState.WalkingToCheckoutCounter ||
                                       _state == WorkerState.WalkingToCheckoutCounterForPickup ||
                                       _state == WorkerState.WalkingToCustomerCounter;
                bool isAgentMoving = _agent != null && _agent.enabled && (
                    _agent.velocity.sqrMagnitude > 0.04f ||
                    _agent.desiredVelocity.sqrMagnitude > 0.04f ||
                    (_agent.hasPath && _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, 0.1f))
                );
                bool isMoving = isWorkerWalking || isAgentMoving;
                _animController.SetMoveSpeed(_agent != null ? _agent.speed : _moveSpeed);
                _animController.SetMoving(isMoving);
            }
        }

        // ==========================================
        // 1. TÌM VIỆC LÀM (IDLE)
        // ==========================================
        private void UpdateIdle() {
            // Tạp vụ / Thợ Bán: Ưu tiên số 1 là ra nhận Order nếu có khách đang vẫy tay
            if (_role == WorkerRole.Runner || _role == WorkerRole.DirectSeller) {
                // Ưu tiên 1A: Khách vẫy tay tại Bàn ăn
                TableSlot tableSlotNeedsOrder = FindTableSlotNeedingOrder();
                if (tableSlotNeedsOrder != null) {
                    tableSlotNeedsOrder.SetNeedsOrderTake(false);
                    _targetTableSlot = tableSlotNeedsOrder;
                    _targetCustomerSlot = null;
                    _state = WorkerState.WalkingToTakeOrder;
                    _agent.SetDestination(tableSlotNeedsOrder.ServerAnchor.position);
                    return;
                }

                // Ưu tiên 1B: Khách vẫy tay tại Quầy phục vụ
                CustomerSlot counterSlotNeedsOrder = FindSlotNeedingOrder();
                if (counterSlotNeedsOrder != null) {
                    counterSlotNeedsOrder.SetNeedsOrderTake(false);
                    _targetCustomerSlot = counterSlotNeedsOrder;
                    _targetTableSlot = null;
                    _state = WorkerState.WalkingToTakeOrder;
                    _agent.SetDestination(counterSlotNeedsOrder.ServerAnchor.position);
                    return;
                }
            }

            // Tạp vụ: Ưu tiên 2 là xem trên bàn có quặng nào khách đang cần không để bê ra
            if (_role == WorkerRole.Runner && _carriedBlockData == null) {
                // Ưu tiên 2A: Tìm khách tại Bàn Ăn đang cần quặng này
                foreach (var tableCounter in TableCounter.AllTables) {
                    foreach (var slot in tableCounter.Slots) {
                        if (slot != null && slot.HasActiveOrder && !slot.NeedsOrderTake) {
                            foreach (var checkoutCounter in CheckoutCounter.AllCounters) {
                                if (checkoutCounter.HasBlockType(slot.CurrentOrderBlock)) {
                                    _targetCheckoutCounter = checkoutCounter;
                                    _targetTableSlot = slot;
                                    _targetCustomerSlot = null;

                                    _state = WorkerState.WalkingToCheckoutCounterForPickup;
                                    CounterSlot counterSlot = checkoutCounter.GetSlotAtTakePosition(checkoutCounter.transform.position, 100f);
                                    if (counterSlot != null) {
                                        _agent.SetDestination(counterSlot.TakeAnchor.position);
                                    } else {
                                        _agent.SetDestination(checkoutCounter.transform.position);
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }

                // Ưu tiên 2B: Tìm khách tại Quầy Phục Vụ đang cần quặng này
                foreach (var customerCounter in CustomerCounter.AllCounters) {
                    foreach (var slot in customerCounter.Slots) {
                        if (slot != null && slot.HasActiveOrder && !slot.NeedsOrderTake) {
                            foreach (var checkoutCounter in CheckoutCounter.AllCounters) {
                                if (checkoutCounter.HasBlockType(slot.CurrentOrderBlock)) {
                                    _targetCheckoutCounter = checkoutCounter;
                                    _targetCustomerSlot = slot;
                                    _targetTableSlot = null;

                                    _state = WorkerState.WalkingToCheckoutCounterForPickup;
                                    CounterSlot counterSlot = checkoutCounter.GetSlotAtTakePosition(checkoutCounter.transform.position, 100f);
                                    if (counterSlot != null) {
                                        _agent.SetDestination(counterSlot.TakeAnchor.position);
                                    } else {
                                        _agent.SetDestination(checkoutCounter.transform.position);
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Thợ Mỏ (Miner) hoặc Thợ Bán (DirectSeller) tìm mỏ để đào
            if (_carriedBlockData == null) {
                if (_role == WorkerRole.DirectSeller) {
                    MiningStation station = FindStationWithActiveOrder();
                    if (station != null) {
                        _activeTicket = station.ClaimOrderForMining(this);
                        if (_activeTicket != null) {
                            Transform anchor = station.ClaimSlot(this);
                            _targetMiningStation = station;
                            _state = WorkerState.WalkingToMine;
                            _agent.SetDestination(anchor.position);
                            return;
                        }
                    }
                } else if (_role == WorkerRole.Miner) {
                    CheckoutCounter table = FindAvailableCheckoutCounter();
                    bool tableHasSpace = table != null && !table.IsFull;

                    MiningStation station = FindStationNeedingMining();
                    if (station != null) {
                        _activeTicket = station.ClaimOrderForMining(this);
                        if (_activeTicket != null || (tableHasSpace && !station.RequireOrderToMine)) {
                            Transform anchor = station.ClaimSlot(this);
                            _targetMiningStation = station;
                            _state = WorkerState.WalkingToMine;
                            _agent.SetDestination(anchor.position);
                            return;
                        }
                    }
                }
            }
        }

        // ==========================================
        // 2. NHẬN ORDER
        // ==========================================
        private void UpdateWalkingToTakeOrder() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                if (_targetTableSlot != null) {
                    transform.rotation = _targetTableSlot.ServerAnchor.rotation;
                } else if (_targetCustomerSlot != null) {
                    transform.rotation = _targetCustomerSlot.ServerAnchor.rotation;
                }
                _agent.updateRotation = false;

                _state = WorkerState.TakingOrder;
                _timer = 0f;
            }
        }

        private void UpdateTakingOrder() {
            Transform serverAnchor = _targetTableSlot != null ? _targetTableSlot.ServerAnchor : (_targetCustomerSlot != null ? _targetCustomerSlot.ServerAnchor : null);
            if (serverAnchor != null) {
                transform.rotation = serverAnchor.rotation;
            }

            _timer += Time.deltaTime;
            if (_timer >= _orderTakeTime) {
                if (CustomerManager.Instance != null) {
                    BlockDataSO randomBlock = CustomerManager.Instance.GetRandomUnlockedBlock();
                    if (randomBlock != null) {
                        double price = MiningStation.GetSellPriceForBlock(randomBlock);
                        if (_targetTableSlot != null) {
                            _targetTableSlot.TakeOrder(randomBlock, price);
                        } else if (_targetCustomerSlot != null) {
                            _targetCustomerSlot.TakeOrder(randomBlock, price);
                        }
                    }
                }
                _targetTableSlot = null;
                _targetCustomerSlot = null;
                _agent.updateRotation = true;
                _state = WorkerState.Idle;
            }
        }

        // ==========================================
        // 3. ĐÀO MỎ
        // ==========================================
        private void UpdateWalkingToMine() {
            if (_targetMiningStation == null || _targetMiningStation.IsBeingDragged) {
                if (_targetMiningStation != null) {
                    _targetMiningStation.ReleaseSlot(this);
                    if (_activeTicket != null) {
                        _targetMiningStation.ReleaseClaim(_activeTicket);
                        _activeTicket = null;
                    }
                    _targetMiningStation = null;
                }
                _agent.updateRotation = true;
                _state = WorkerState.Idle;
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                Transform slotAnchor = _targetMiningStation.GetMinerAnchorFor(this);
                if (slotAnchor != null && Vector3.Distance(transform.position, slotAnchor.position) > 1.2f) {
                    _agent.SetDestination(slotAnchor.position);
                    return;
                }

                _state = WorkerState.Mining;
                _timer = _targetMiningStation != null ? _targetMiningStation.CurrentCraftTimer : 0f;
                _savedCraftTimer = 0f;
                _interruptedStation = null;

                // Quay mặt vào mỏ
                transform.rotation = slotAnchor.rotation;
                _agent.updateRotation = false; // Tạm tắt xoay của agent để không bị giật
            }
        }

        private void UpdateMining() {
            if (_targetMiningStation == null || _targetMiningStation.IsBeingDragged ||
                Vector3.Distance(transform.position, _targetMiningStation.transform.position) > 2.5f) {
                if (_targetMiningStation != null) {
                    _targetMiningStation.ReleaseSlot(this);
                    if (_activeTicket != null) {
                        _targetMiningStation.ReleaseClaim(_activeTicket);
                        _activeTicket = null;
                    }
                    _targetMiningStation = null;
                }
                _agent.updateRotation = true;
                _state = WorkerState.Idle;
                return;
            }

            bool completed = _targetMiningStation.AddMiningProgress(Time.deltaTime);
            _timer = _targetMiningStation.CurrentCraftTimer;

            if (completed) {
                // Spawn cục quặng ảo cầm trên tay
                _carriedBlockData = _targetMiningStation.ProducedBlock;
                if (_targetMiningStation.ProducedBlock.Prefab3D != null) {
                    _carriedBlockInstance = Instantiate(_targetMiningStation.ProducedBlock.Prefab3D, _carryAnchor);
                    _carriedBlockInstance.transform.localPosition = Vector3.zero;
                }

                if (_activeTicket != null) {
                    _targetMiningStation.FinishMiningOrder(_activeTicket);
                    _activeTicket = null; // Giao phó cho OrderManager lo tiếp
                } else {
                    _targetMiningStation.ConsumePendingOrder();
                }

                _targetMiningStation.ReleaseSlot(this);

                // Khai thác xong, bật lại updateRotation
                _agent.updateRotation = true;

                // Chuyển hướng tùy theo Role
                if (_role == WorkerRole.Miner) {
                    _state = WorkerState.WalkingToCheckoutCounter;
                    _targetCheckoutCounter = FindAvailableCheckoutCounter();
                    if (_targetCheckoutCounter != null) {
                        // Đi đến phía sau bàn trung chuyển (PlaceAnchor)
                        CounterSlot slot = _targetCheckoutCounter.GetSlotAtPlacePosition(_targetCheckoutCounter.transform.position, 100f);
                        if (slot != null) {
                            _agent.SetDestination(slot.PlaceAnchor.position);
                        } else {
                            _agent.SetDestination(_targetCheckoutCounter.transform.position);
                        }
                    }
                } else if (_role == WorkerRole.DirectSeller) {
                    _state = WorkerState.WalkingToCustomerCounter;
                    _targetTableSlot = FindTableSlotNeedingBlock(_carriedBlockData);
                    if (_targetTableSlot != null) {
                        _targetCustomerSlot = null;
                        _agent.SetDestination(_targetTableSlot.ServerAnchor.position);
                    } else {
                        _targetCustomerSlot = FindCustomerNeedingBlock(_carriedBlockData);
                        if (_targetCustomerSlot != null) {
                            _agent.SetDestination(_targetCustomerSlot.ServerAnchor.position);
                        }
                    }
                }
            }
        }

        // ==========================================
        // 4. BỎ LÊN BÀN TRUNG CHUYỂN (DÀNH CHO THỢ)
        // ==========================================
        private void UpdateWalkingToCheckoutCounter() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                if (_targetCheckoutCounter != null && !_targetCheckoutCounter.IsFull) {
                    // Lắp gạch vào bàn, PHẢI truyền blockData để bàn biết nó là khối gì!
                    if (_targetCheckoutCounter.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPos)) {
                        _carriedBlockInstance.transform.SetParent(null); // Gỡ parent để không bị méo scale
                        _carriedBlockInstance.transform.position = targetPos;
                        _carriedBlockInstance.transform.rotation = _targetCheckoutCounter.transform.rotation;

                        _carriedBlockInstance = null;
                        _carriedBlockData = null;
                        _state = WorkerState.Idle; // Xong việc, quay lại tìm mỏ
                    }
                } else {
                    // Bàn đầy, đứng chờ
                    _state = WorkerState.WaitingForTableSpace;
                }
            }
        }

        private void UpdateWaitingForTableSpace() {
            // Liên tục check xem bàn có chỗ trống chưa
            if (_targetCheckoutCounter != null && !_targetCheckoutCounter.IsFull) {
                if (_targetCheckoutCounter.TryPlaceBlock(_carriedBlockInstance, _carriedBlockData, out Vector3 targetPos)) {
                    _carriedBlockInstance.transform.SetParent(null); // Gỡ parent để không bị méo scale
                    _carriedBlockInstance.transform.position = targetPos;
                    _carriedBlockInstance.transform.rotation = _targetCheckoutCounter.transform.rotation;

                    _carriedBlockInstance = null;
                    _carriedBlockData = null;
                    _state = WorkerState.Idle;
                }
            }
        }

        // ==========================================
        // 4.5. TẠP VỤ ĐẾN BÀN LẤY ĐỒ
        // ==========================================
        private void UpdateWalkingToCheckoutCounterForPickup() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                BlockDataSO neededBlock = _targetTableSlot != null ? _targetTableSlot.CurrentOrderBlock : (_targetCustomerSlot != null ? _targetCustomerSlot.CurrentOrderBlock : null);

                if (_targetCheckoutCounter != null && neededBlock != null) {
                    if (_targetCheckoutCounter.TryTakeBlockOfType(neededBlock, out GameObject blockInstance)) {
                        _carriedBlockData = neededBlock;
                        _carriedBlockInstance = blockInstance;

                        _carriedBlockInstance.transform.SetParent(_carryAnchor);
                        _carriedBlockInstance.transform.localPosition = Vector3.zero;
                        _carriedBlockInstance.transform.localRotation = Quaternion.identity;

                        // Lấy xong thì đi giao cho khách
                        _state = WorkerState.WalkingToCustomerCounter;
                        if (_targetTableSlot != null) {
                            _agent.SetDestination(_targetTableSlot.ServerAnchor.position);
                        } else if (_targetCustomerSlot != null) {
                            _agent.SetDestination(_targetCustomerSlot.ServerAnchor.position);
                        }
                    } else {
                        // Đồ đã bị ai đó lấy mất, quay lại Idle
                        _targetTableSlot = null;
                        _targetCustomerSlot = null;
                        _targetCheckoutCounter = null;
                        _state = WorkerState.Idle;
                    }
                } else {
                    _state = WorkerState.Idle;
                }
            }
        }

        // ==========================================
        // 5. GIAO HÀNG TRỰC TIẾP CHO KHÁCH (TẠP VỤ & THỢ BÁN)
        // ==========================================
        private void UpdateWalkingToCustomerCounter() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                Transform serverAnchor = _targetTableSlot != null ? _targetTableSlot.ServerAnchor : (_targetCustomerSlot != null ? _targetCustomerSlot.ServerAnchor : null);
                if (serverAnchor != null) {
                    transform.rotation = serverAnchor.rotation;
                }
                _agent.updateRotation = false;

                _state = WorkerState.DeliveringToCustomer;
                _timer = 0f;
            }
        }

        private void UpdateDeliveringToCustomer() {
            Transform serverAnchor = _targetTableSlot != null ? _targetTableSlot.ServerAnchor : (_targetCustomerSlot != null ? _targetCustomerSlot.ServerAnchor : null);
            if (serverAnchor != null) {
                transform.rotation = serverAnchor.rotation;
            }

            if (_carriedBlockData != null) {
                if (_targetTableSlot != null) {
                    TableCounter parentTable = FindParentTable(_targetTableSlot);
                    OrderDeliveredEventChannelSO channel = parentTable != null ? parentTable.OrderDeliveredChannel : null;

                    bool success = _targetTableSlot.TryDeliverOrder(_carriedBlockInstance, _carriedBlockData, channel);
                    if (success) {
                        _carriedBlockInstance = null;
                        _carriedBlockData = null;
                    }
                } else if (_targetCustomerSlot != null) {
                    CustomerCounter parentCounter = FindParentCounter(_targetCustomerSlot);
                    OrderDeliveredEventChannelSO channel = parentCounter != null ? parentCounter.OrderDeliveredChannel : null;

                    bool success = _targetCustomerSlot.TryDeliverOrder(_carriedBlockInstance, _carriedBlockData, channel);
                    if (success) {
                        _carriedBlockInstance = null;
                        _carriedBlockData = null;
                    }
                }
            }

            _targetTableSlot = null;
            _targetCustomerSlot = null;
            _agent.updateRotation = true; // Bật lại xoay
            _state = WorkerState.Idle;
        }

        // ==========================================
        // UTILS
        // ==========================================
        private TableSlot FindTableSlotNeedingOrder() {
            foreach (var table in TableCounter.AllTables) {
                foreach (var slot in table.Slots) {
                    if (slot != null && slot.NeedsOrderTake) {
                        return slot;
                    }
                }
            }
            return null;
        }

        private CustomerSlot FindSlotNeedingOrder() {
            foreach (var counter in CustomerCounter.AllCounters) {
                foreach (var slot in counter.Slots) {
                    if (slot != null && slot.NeedsOrderTake) {
                        return slot;
                    }
                }
            }
            return null;
        }

        private TableCounter FindParentTable(TableSlot slot) {
            if (slot == null) return null;
            foreach (var table in TableCounter.AllTables) {
                if (table.Slots != null) {
                    foreach (var s in table.Slots) {
                        if (s == slot) return table;
                    }
                }
            }
            return null;
        }

        private CustomerCounter FindParentCounter(CustomerSlot slot) {
            if (slot == null) return null;
            foreach (var counter in CustomerCounter.AllCounters) {
                if (counter.Slots != null) {
                    foreach (var s in counter.Slots) {
                        if (s == slot) return counter;
                    }
                }
            }
            return null;
        }

        private MiningStation FindStationWithActiveOrder() {
            foreach (var station in MiningStation.AllStations) {
                if (station != null && station.NeedsMining && station.HasOrderInQueue) {
                    return station;
                }
            }
            return null;
        }

        private MiningStation FindStationNeedingMining() {
            // Ưu tiên 1: Mỏ đang có đơn hàng chờ
            foreach (var station in MiningStation.AllStations) {
                if (station != null && station.NeedsMining && station.HasOrderInQueue) return station;
            }

            // Ưu tiên 2: Mỏ mở khóa tự do không bắt buộc đơn
            foreach (var station in MiningStation.AllStations) {
                if (station != null && station.NeedsMining && !station.RequireOrderToMine) return station;
            }
            return null;
        }

        private CheckoutCounter FindAvailableCheckoutCounter() {
            // Trả về quầy trung chuyển còn chỗ trống
            if (CheckoutCounter.AllCounters.Count > 0) return CheckoutCounter.AllCounters[0];
            return null;
        }

        private TableSlot FindTableSlotNeedingBlock(BlockDataSO block) {
            foreach (var table in TableCounter.AllTables) {
                foreach (var slot in table.Slots) {
                    if (slot != null && slot.CanFulfillWith(block)) return slot;
                }
            }
            return null;
        }

        private CustomerSlot FindCustomerNeedingBlock(BlockDataSO block) {
            foreach (var counter in CustomerCounter.AllCounters) {
                foreach (var slot in counter.Slots) {
                    if (slot != null && slot.CanFulfillWith(block)) return slot;
                }
            }
            return null;
        }
    }
}
