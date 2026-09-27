using System.Collections.Generic;
using MineMart.Core.Events;
using MineMart.Data;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Customers;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Orders;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Staff {
    public enum WorkerRole {
        Miner,          // Đào -> Bỏ lên bàn trung chuyển
        Runner,         // Nhận order -> Bê từ bàn trung chuyển -> Giao cho khách
        DirectSeller    // Nhận order -> Đào -> Giao trực tiếp cho khách
    }

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

        private NavMeshAgent _agent;
        private WorkerState _state = WorkerState.Idle;

        // Visual / Animation (Tái sử dụng chung Pop effect sau này)
        private GameObject _carriedBlockInstance;
        private BlockDataSO _carriedBlockData;
        private Transform _carryAnchor;

        // Context Targets
        private CustomerSlot _targetCustomerSlot;
        private MiningStation _targetMiningStation;
        private CheckoutCounter _targetCheckoutCounter;
        private OrderTicket _activeTicket;

        private float _timer = 0f;

        private void Awake() {
            _agent = GetComponent<NavMeshAgent>();
            // Nhân viên đi nhanh hơn khách một chút để phục vụ cho kịp
            _agent.speed = _moveSpeed;
            // Cho phép nhân viên đi xuyên qua nhau và xuyên qua khách để không bị kẹt đường
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            // Giảm nửa tầm tương tác (cự ly với) để thợ phải tiến sát vào quầy mới được lấy/đặt đồ
            _agent.stoppingDistance = _stoppingDistance;

            Transform foundAnchor = transform.Find("CarryAnchor");
            if (foundAnchor != null) _carryAnchor = foundAnchor;
            else {
                GameObject obj = new GameObject("CarryAnchor");
                obj.transform.SetParent(transform);
                obj.transform.localPosition = new Vector3(0, 1.25f, 0);
                _carryAnchor = obj.transform;
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
        }

        // ==========================================
        // 1. TÌM VIỆC LÀM (IDLE)
        // ==========================================
        private void UpdateIdle() {
            // Tạp vụ / Thợ Bán: Ưu tiên số 1 là ra nhận Order nếu có khách đang vẫy tay
            if (_role == WorkerRole.Runner || _role == WorkerRole.DirectSeller) {
                CustomerSlot slotNeedsOrder = FindSlotNeedingOrder();
                if (slotNeedsOrder != null) {
                    // Tạm thời claim ngầm bằng cách báo slot đó hết need, tránh AI khác bu vào
                    // (Sẽ cần refine lại khóa chặt chẽ hơn sau)
                    slotNeedsOrder.SetNeedsOrderTake(false);
                    _targetCustomerSlot = slotNeedsOrder;
                    _state = WorkerState.WalkingToTakeOrder;
                    _agent.SetDestination(slotNeedsOrder.ServerAnchor.position);
                    return;
                }
            }

            // Tạp vụ: Ưu tiên 2 là xem trên bàn có quặng nào khách đang cần không để bê ra
            if (_role == WorkerRole.Runner && _carriedBlockData == null) {
                foreach (var customerCounter in CustomerCounter.AllCounters) {
                    foreach (var slot in customerCounter.Slots) {
                        if (slot != null && slot.HasActiveOrder && !slot.NeedsOrderTake) {
                            // Xem trên bàn có block này không (chỉ dò xem có không, chưa lấy ngay)
                            foreach (var checkoutCounter in CheckoutCounter.AllCounters) {
                                if (checkoutCounter.HasBlockType(slot.CurrentOrderBlock)) {
                                    _targetCheckoutCounter = checkoutCounter;
                                    _targetCustomerSlot = slot; // Ghi nhớ khách nào đang chờ

                                    _state = WorkerState.WalkingToCheckoutCounterForPickup;

                                    // Tìm vị trí lấy đồ (TakeAnchor)
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

            // Thợ / Thợ Bán: Ưu tiên tìm mỏ để đào
            if ((_role == WorkerRole.Miner || _role == WorkerRole.DirectSeller) && _carriedBlockData == null) {
                MiningStation station = FindStationNeedingMining();
                if (station != null) {
                    _activeTicket = station.ClaimOrderForMining(this);
                    if (_activeTicket != null || !station.RequireOrderToMine) {
                        station.SetOccupied(true); // Nhận là phải giữ ngay kẻo người khác vào cướp
                        _targetMiningStation = station;
                        _state = WorkerState.WalkingToMine;
                        _agent.SetDestination(station.MinerAnchor.position);
                        return;
                    }
                }
            }
        }

        // ==========================================
        // 2. NHẬN ORDER
        // ==========================================
        private void UpdateWalkingToTakeOrder() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                // Khi đã đến nơi nhận Order, quay mặt về phía quầy
                transform.rotation = _targetCustomerSlot.ServerAnchor.rotation;
                _agent.updateRotation = false; // Tắt xoay để NavMesh không ghi đè

                _state = WorkerState.TakingOrder;
                _timer = 0f;
            }
        }

        private void UpdateTakingOrder() {
            // Ép hướng quay mặt
            transform.rotation = _targetCustomerSlot.ServerAnchor.rotation;

            _timer += Time.deltaTime;
            if (_timer >= _orderTakeTime) {
                if (CustomerManager.Instance != null) {
                    BlockDataSO randomBlock = CustomerManager.Instance.GetRandomUnlockedBlock();
                    if (randomBlock != null) {
                        double price = randomBlock.BaseValue > 0 ? randomBlock.BaseValue : 3;
                        _targetCustomerSlot.TakeOrder(randomBlock, price);
                    }
                }
                _targetCustomerSlot = null;
                _agent.updateRotation = true; // Bật lại xoay
                _state = WorkerState.Idle; // Quay lại tìm việc
            }
        }

        // ==========================================
        // 3. ĐÀO MỎ
        // ==========================================
        private void UpdateWalkingToMine() {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                _state = WorkerState.Mining;
                _timer = 0f;

                // Quay mặt vào mỏ
                transform.rotation = _targetMiningStation.MinerAnchor.rotation;
                _agent.updateRotation = false; // Tạm tắt xoay của agent để không bị giật
            }
        }

        private void UpdateMining() {
            _timer += Time.deltaTime;
            if (_timer >= _targetMiningStation.GetBaseCraftTime()) {
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

                _targetMiningStation.SetOccupied(false);

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
                    // Tìm khách hàng đang cần cục này
                    _targetCustomerSlot = FindCustomerNeedingBlock(_carriedBlockData);
                    if (_targetCustomerSlot != null) {
                        _agent.SetDestination(_targetCustomerSlot.ServerAnchor.position);
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
                if (_targetCheckoutCounter != null && _targetCustomerSlot != null) {
                    if (_targetCheckoutCounter.TryTakeBlockOfType(_targetCustomerSlot.CurrentOrderBlock, out GameObject blockInstance)) {
                        _carriedBlockData = _targetCustomerSlot.CurrentOrderBlock;
                        _carriedBlockInstance = blockInstance;

                        _carriedBlockInstance.transform.SetParent(_carryAnchor);
                        _carriedBlockInstance.transform.localPosition = Vector3.zero;
                        _carriedBlockInstance.transform.localRotation = Quaternion.identity;

                        // Lấy xong thì đi giao cho khách
                        _state = WorkerState.WalkingToCustomerCounter;
                        _agent.SetDestination(_targetCustomerSlot.ServerAnchor.position);
                    } else {
                        // Đồ đã bị ai đó lấy mất, quay lại Idle
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
                // Quay mặt vào khách
                transform.rotation = _targetCustomerSlot.ServerAnchor.rotation;
                _agent.updateRotation = false;

                _state = WorkerState.DeliveringToCustomer;
                _timer = 0f;
            }
        }

        private void UpdateDeliveringToCustomer() {
            // Ép hướng quay mặt
            transform.rotation = _targetCustomerSlot.ServerAnchor.rotation;

            if (_targetCustomerSlot != null && _carriedBlockData != null) {
                CustomerCounter parentCounter = FindParentCounter(_targetCustomerSlot);
                OrderDeliveredEventChannelSO channel = parentCounter != null ? parentCounter.OrderDeliveredChannel : null;

                // Gọi hàm giao hàng của Slot
                bool success = _targetCustomerSlot.TryDeliverOrder(_carriedBlockInstance, _carriedBlockData, channel);
                if (success) {
                    _carriedBlockInstance = null;
                    _carriedBlockData = null;
                }
            }
            _targetCustomerSlot = null;
            _agent.updateRotation = true; // Bật lại xoay
            _state = WorkerState.Idle;
        }

        // ==========================================
        // UTILS
        // ==========================================
        private CustomerSlot FindSlotNeedingOrder() {
            foreach (var counter in CustomerCounter.AllCounters) {
                foreach (var slot in counter.Slots) {
                    // LƯU Ý: Nếu có nhiều AI, cần cờ IsClaimedByAI để không bị tranh giành
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
                if (counter.Slots != null) {
                    foreach (var s in counter.Slots) {
                        if (s == slot) return counter;
                    }
                }
            }
            return null;
        }

        private MiningStation FindStationNeedingMining() {
            foreach (var station in MiningStation.AllStations) {
                if (station.NeedsMining) return station;
            }
            return null;
        }

        private CheckoutCounter FindAvailableCheckoutCounter() {
            // Trả về quầy trung chuyển còn chỗ trống
            if (CheckoutCounter.AllCounters.Count > 0) return CheckoutCounter.AllCounters[0];
            return null;
        }

        private CustomerSlot FindCustomerNeedingBlock(BlockDataSO block) {
            foreach (var counter in CustomerCounter.AllCounters) {
                foreach (var slot in counter.Slots) {
                    if (slot.CanFulfillWith(block)) return slot;
                }
            }
            return null;
        }

        // TODO: Viết tiếp logic Bỏ lên bàn, Rút từ bàn và Giao thẳng
    }
}
