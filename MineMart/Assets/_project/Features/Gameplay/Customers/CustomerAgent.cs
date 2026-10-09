using System;
using MineMart.Data;
using MineMart.Gameplay.Checkout;
using MineMart.Gameplay.Common;
using MineMart.Gameplay.Tables;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Customers {
    public enum CustomerState {
        Idle,
        WalkingToQueue,
        WaitingInQueue,
        Ordering,
        Leaving
    }

    /// <summary>
    /// Bộ não của AI Khách hàng (Sử dụng NavMeshAgent).
    /// Thứ tự ưu tiên chọn vị trí phục vụ:
    /// 1. ƯU TIÊN 1: Tìm ô quầy tiếp khách (CustomerSlot) đang HOÀN TOÀN TRỐNG (QueueCount == 0, chưa có khách đứng) gần nhất.
    /// 2. ƯU TIÊN 2: Nếu tất cả quầy đều đã có người đứng, tìm ghế bàn ăn (TableSlot) còn trống gần nhất để ngồi gọi món.
    /// 3. ƯU TIÊN 3: Nếu hết chỗ cả ở quầy (trực tiếp) lẫn bàn ăn (ghế), khách mới bắt đầu xếp hàng tại quầy có hàng đợi ngắn nhất.
    /// Hỗ trợ Object Pooling: Tái sử dụng qua hàm Spawn() và ReturnToPool() không sinh/hủy rác bộ nhớ.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CustomerAgent : MonoBehaviour {
        private NavMeshAgent _agent;
        private CharacterAnimationController _animController;
        private CustomerState _state = CustomerState.Idle;

        // Điểm phục vụ hiện tại (Khách chỉ ở 1 trong 2 nơi: hoặc ngồi bàn ăn, hoặc đứng xếp hàng ở quầy)
        private CustomerSlot _targetCounterSlot;
        private TableSlot _targetTableSlot;

        private Vector3 _exitPos;
        private float _leaveTimer = 0f;
        private bool _isPooled = false;

        public event Action<CustomerAgent> OnLeaveStore;

        public CustomerState State => _state;
        public CustomerSlot TargetCounterSlot => _targetCounterSlot;
        public TableSlot TargetTableSlot => _targetTableSlot;
        public bool IsSittingAtTable => _targetTableSlot != null;

        /// <summary>
        /// Đánh dấu đối tượng được quản lý bởi Object Pool, ngăn tự động tìm chỗ trong Start().
        /// </summary>
        public void MarkAsPooled() {
            _isPooled = true;
        }

        private void Awake() {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null) {
                // Tắt va chạm vật lý né tránh để khách đi xuyên qua nhau mượt mà (Yêu cầu MVP)
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                _agent.stoppingDistance = 0.05f;
            }

            _animController = GetComponent<CharacterAnimationController>();
            if (_animController == null) {
                _animController = gameObject.AddComponent<CharacterAnimationController>();
            }
        }

        private void Start() {
            // Chỉ tự chạy nếu đối tượng được đặt sẵn thủ công độc lập trong Scene khi test
            if (!_isPooled && transform.parent == null && _state == CustomerState.Idle && gameObject.activeInHierarchy) {
                _exitPos = transform.position;
                FindAndJoinBestSlot();
            }
        }

        /// <summary>
        /// Tự động tìm kiếm vị trí phục vụ tốt nhất trong tiệm theo thứ tự ưu tiên:
        /// - ƯU TIÊN 1: Tìm ô quầy tiếp khách (CustomerSlot) đang HOÀN TOÀN TRỐNG (QueueCount == 0, chưa có ai đứng) gần nhất.
        /// - ƯU TIÊN 2: Nếu tất cả quầy đều đã có khách đứng, tìm ghế bàn ăn (TableSlot) còn trống gần nhất.
        /// - ƯU TIÊN 3: Nếu hết chỗ cả ở bàn lẫn quầy trực tiếp, khách mới bắt đầu xếp hàng tại quầy có hàng đợi ngắn nhất.
        /// </summary>
        public void FindAndJoinBestSlot() {
            // 1. ƯU TIÊN 1: Quét tìm quầy tiếp khách đang HOÀN TOÀN TRỐNG (chưa có khách nào đứng)
            CustomerSlot freeCounterSlot = CustomerCounter.GetClosestAvailableSlotGlobal(transform.position);
            if (freeCounterSlot != null) {
                JoinTargetCounter(freeCounterSlot);
                return;
            }

            // 2. ƯU TIÊN 2: Nếu tất cả quầy đều đã có người đứng, tìm ghế bàn ăn còn trống
            TableSlot freeTableSlot = TableCounter.GetClosestAvailableSlotGlobal(transform.position);
            if (freeTableSlot != null && freeTableSlot.TryOccupy(this)) {
                JoinTargetTable(freeTableSlot);
                return;
            }

            // 3. ƯU TIÊN 3: Hết chỗ cả ở quầy (trực tiếp) lẫn bàn ăn (ghế), lúc này khách mới bắt đầu xếp hàng ở quầy
            CustomerSlot queueSlot = CustomerCounter.GetShortestQueueSlotGlobal();
            if (queueSlot != null) {
                JoinTargetCounter(queueSlot);
                return;
            }

            // 4. Nếu toàn bộ tiệm đều kín chỗ và quá tải, rời tiệm an toàn
            LeaveStore();
        }

        /// <summary>
        /// Tham gia an toàn vào một CustomerSlot (ở Quầy tiếp khách).
        /// </summary>
        public void JoinTargetCounter(CustomerSlot newSlot) {
            ClearCurrentOccupancy();

            _targetCounterSlot = newSlot;
            if (_targetCounterSlot != null) {
                _targetCounterSlot.JoinQueue(this);
                _state = CustomerState.WalkingToQueue;
                UpdateDestination();
            } else {
                LeaveStore();
            }
        }

        /// <summary>
        /// Tham gia an toàn vào một TableSlot (Ngồi vào ghế tại Bàn ăn - KHÔNG XẾP HÀNG).
        /// </summary>
        public void JoinTargetTable(TableSlot newTableSlot) {
            ClearCurrentOccupancy();

            _targetTableSlot = newTableSlot;
            if (_targetTableSlot != null) {
                _state = CustomerState.WalkingToQueue;
                UpdateDestination();
            } else {
                LeaveStore();
            }
        }

        // Tương thích ngược với các lời gọi cũ
        public void JoinTargetSlot(CustomerSlot newSlot) => JoinTargetCounter(newSlot);

        /// <summary>
        /// Kích hoạt và tái sử dụng khách hàng từ Object Pool.
        /// </summary>
        public void Spawn(Vector3 spawnPosition, Quaternion spawnRotation) {
            _isPooled = true;

            if (_agent == null) {
                _agent = GetComponent<NavMeshAgent>();
                if (_agent != null) {
                    _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                }
            }

            _leaveTimer = 0f;

            if (_agent != null) {
                _agent.enabled = false;
            }

            // Đảm bảo vị trí nằm chuẩn trên NavMesh
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) {
                spawnPosition = hit.position;
            }

            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            _exitPos = spawnPosition;

            gameObject.SetActive(true);

            if (_animController != null) {
                _animController.RefreshModel();
            }

            if (_agent != null) {
                _agent.enabled = true;
                _agent.Warp(spawnPosition);
            }

            // Tự động tìm bàn ăn trống trước, nếu kín bàn mới ra quầy xếp hàng
            FindAndJoinBestSlot();
        }

        /// <summary>
        /// Được gọi bởi CustomerSlot khi người phía trước đã đi ra, yêu cầu người này nhích lên.
        /// </summary>
        public void AdvanceInLine() {
            if (_targetTableSlot != null) return; // Khách ngồi bàn không có hàng đợi!

            if (_state == CustomerState.WaitingInQueue || _state == CustomerState.WalkingToQueue) {
                _state = CustomerState.WalkingToQueue;
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
                    _agent.isStopped = false;
                    _agent.updateRotation = true;
                }
                UpdateDestination();
            }
        }

        /// <summary>
        /// Đã lên đến đầu hàng ở quầy, giơ tay chờ nhân viên ra nhận order.
        /// </summary>
        private void NotifyReachedFrontOfLine() {
            _state = CustomerState.Ordering;

            if (_agent != null && _agent.enabled) {
                _agent.updateRotation = false;
                _agent.velocity = Vector3.zero;
                _agent.isStopped = true;
            }

            if (_targetCounterSlot != null) {
                _targetCounterSlot.SetNeedsOrderTake(true);
                _targetCounterSlot.OnOrderCompleted += HandleOrderCompleted;
            }
        }

        /// <summary>
        /// Đã bước đến ghế ngồi của bàn ăn, ngồi xuống và giơ tay chờ gọi món.
        /// </summary>
        private void NotifyReachedTableSeat() {
            _state = CustomerState.Ordering;

            if (_agent != null && _agent.enabled) {
                _agent.updateRotation = false;
                _agent.velocity = Vector3.zero;
                _agent.isStopped = true;
            }

            if (_targetTableSlot != null) {
                _targetTableSlot.SetNeedsOrderTake(true);
                _targetTableSlot.OnOrderCompleted += HandleOrderCompleted;
            }
        }

        private void HandleOrderCompleted() {
            ClearCurrentOccupancy();
            LeaveStore();
        }

        private void LeaveQueueAndStore() {
            ClearCurrentOccupancy();
            LeaveStore();
        }

        private void ClearCurrentOccupancy() {
            if (_targetCounterSlot != null) {
                _targetCounterSlot.OnOrderCompleted -= HandleOrderCompleted;
                _targetCounterSlot.LeaveQueue(this);
                _targetCounterSlot = null;
            }

            if (_targetTableSlot != null) {
                _targetTableSlot.OnOrderCompleted -= HandleOrderCompleted;
                _targetTableSlot.Vacate(this);
                _targetTableSlot = null;
            }
        }

        private void LeaveStore() {
            _state = CustomerState.Leaving;
            _leaveTimer = 0f;

            ClearCurrentOccupancy();

            // Quay về đúng điểm Spawn ngoài cửa
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
                _agent.isStopped = false;      // BẮT BUỘC BẬT LẠI ĐỂ KHÁCH BƯỚC ĐI!
                _agent.updateRotation = true;  // Cho phép xoay hướng tự nhiên khi đi
                _agent.ResetPath();

                Vector3 targetExit = _exitPos;
                if (NavMesh.SamplePosition(_exitPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) {
                    targetExit = hit.position;
                }
                _agent.SetDestination(targetExit);
            }

            OnLeaveStore?.Invoke(this);
        }

        /// <summary>
        /// Trả khách hàng về Object Pool (ẩn GameObject, dọn dẹp liên kết).
        /// </summary>
        public void ReturnToPool() {
            _state = CustomerState.Idle;
            _leaveTimer = 0f;

            ClearCurrentOccupancy();

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
                _agent.ResetPath();
                _agent.enabled = false;
            }

            gameObject.SetActive(false);
        }

        private void OnDisable() {
            ClearCurrentOccupancy();
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
                _agent.ResetPath();
            }
            _state = CustomerState.Idle;
        }

        private void UpdateDestination() {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
            _agent.updateRotation = true;

            if (_targetTableSlot != null) {
                // Di chuyển thẳng đến chỗ ngồi tại bàn
                _agent.SetDestination(_targetTableSlot.GetSeatPosition());
            } else if (_targetCounterSlot != null) {
                // Di chuyển đến vị trí xếp hàng tại quầy
                int index = _targetCounterSlot.GetCustomerIndex(this);
                Vector3 pos = _targetCounterSlot.GetQueuePosition(index);
                _agent.SetDestination(pos);
            }
        }

        private void Update() {
            if (_state == CustomerState.WalkingToQueue) {
                if (_agent != null && _agent.enabled && !_agent.pathPending) {
                    if (_targetTableSlot != null) {
                        // Di chuyển đến bàn ăn
                        Vector3 seatPos = _targetTableSlot.GetSeatPosition();
                        float distSq = (new Vector2(transform.position.x, transform.position.z) - new Vector2(seatPos.x, seatPos.z)).sqrMagnitude;

                        if (_agent.remainingDistance <= 0.2f || distSq <= 0.2f * 0.2f) {
                            _agent.isStopped = true;
                            _agent.velocity = Vector3.zero;
                            _agent.updateRotation = false;

                            // Định vị chính xác vào ghế ngồi
                            transform.position = new Vector3(seatPos.x, transform.position.y, seatPos.z);
                            Vector3 initialFacing = _targetTableSlot.GetCustomerFacingDirection();
                            if (initialFacing.sqrMagnitude > 0.001f) {
                                transform.rotation = Quaternion.LookRotation(initialFacing, Vector3.up);
                            } else {
                                transform.rotation = _targetTableSlot.CustomerAnchor.rotation;
                            }

                            NotifyReachedTableSeat();
                        }
                    } else if (_targetCounterSlot != null) {
                        // Di chuyển đến hàng đợi tại quầy
                        int index = _targetCounterSlot.GetCustomerIndex(this);
                        Vector3 targetPos = _targetCounterSlot.GetQueuePosition(index);
                        float distSq = (new Vector2(transform.position.x, transform.position.z) - new Vector2(targetPos.x, targetPos.z)).sqrMagnitude;

                        if (_agent.remainingDistance <= 0.15f || distSq <= 0.15f * 0.15f) {
                            _agent.isStopped = true;
                            _agent.velocity = Vector3.zero;
                            _agent.updateRotation = false;

                            transform.position = new Vector3(targetPos.x, transform.position.y, targetPos.z);

                            if (index == 0) {
                                NotifyReachedFrontOfLine();
                            } else {
                                _state = CustomerState.WaitingInQueue;
                            }
                        }
                    }
                }
            } else if (_state == CustomerState.Ordering) {
                if (_targetTableSlot != null) {
                    // Tại bàn ăn: Quay mặt thẳng vào điểm nhìn được setup (LookAtAnchor / mặt bàn), không nhìn chéo về ServerAnchor!
                    Vector3 lookDir = _targetTableSlot.GetCustomerFacingDirection();
                    if (lookDir.sqrMagnitude > 0.001f) {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
                    }
                } else if (_targetCounterSlot != null) {
                    // Đứng ở đầu quầy: Luôn quay thẳng mặt vào bàn / điểm ServerAnchor của nhân viên
                    Transform targetAnchor = _targetCounterSlot.ServerAnchor != null ? _targetCounterSlot.ServerAnchor : _targetCounterSlot.transform;
                    Vector3 lookDir = targetAnchor.position - transform.position;
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f) {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
                    }
                }
            } else if (_state == CustomerState.WaitingInQueue) {
                // Đứng xếp hàng phía sau ở quầy: Quay mặt theo hướng hàng đợi nhìn về phía quầy
                if (_targetCounterSlot != null) {
                    Vector3 lookDir = -_targetCounterSlot.GetQueueDirection();
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f) {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 12f * Time.deltaTime);
                    }
                }
            } else if (_state == CustomerState.Leaving) {
                _leaveTimer += Time.deltaTime;

                if (_agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.isStopped) {
                    _agent.isStopped = false;
                }

                float distToExitSqr = (new Vector2(transform.position.x, transform.position.z) - new Vector2(_exitPos.x, _exitPos.z)).sqrMagnitude;
                bool reachedExit = distToExitSqr <= 1.0f * 1.0f ||
                                   (_agent != null && _agent.enabled && !_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= 0.5f);

                if (reachedExit || _leaveTimer >= 30f) {
                    ReturnToPool();
                }
            }
        }
    }
}
