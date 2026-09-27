using System;
using MineMart.Data;
using MineMart.Gameplay.Checkout;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Customers {
    public enum CustomerState {
        WalkingToQueue,
        WaitingInQueue,
        Ordering,
        Leaving
    }

    /// <summary>
    /// Bộ não của AI Khách hàng (Sử dụng NavMeshAgent).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CustomerAgent : MonoBehaviour {
        private NavMeshAgent _agent;
        private CustomerState _state;
        private CustomerSlot _targetSlot;
        private Vector3 _exitPos;

        public event Action<CustomerAgent> OnLeaveStore;

        private void Awake() {
            _agent = GetComponent<NavMeshAgent>();
            // Tắt va chạm vật lý né tránh để khách đi xuyên qua nhau mượt mà (Yêu cầu MVP)
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void Start() {
            // Ghi nhớ lại vị trí ngoài cửa lúc vừa sinh ra để lát nữa mua xong đi về đúng đường này
            _exitPos = transform.position;

            // Tìm ô quầy có hàng đợi ngắn nhất
            _targetSlot = CustomerCounter.GetShortestQueueSlotGlobal();
            if (_targetSlot != null) {
                _targetSlot.JoinQueue(this);
                _state = CustomerState.WalkingToQueue;
                UpdateDestination();
            } else {
                // Nếu không có quầy nào, đi về
                LeaveStore();
            }
        }

        /// <summary>
        /// Được gọi bởi CustomerSlot khi người phía trước đã đi ra, yêu cầu người này nhích lên.
        /// </summary>
        public void AdvanceInLine() {
            if (_state == CustomerState.WaitingInQueue || _state == CustomerState.WalkingToQueue) {
                _state = CustomerState.WalkingToQueue;
                UpdateDestination();
            }
        }

        /// <summary>
        /// Đã lên đến đầu hàng, giơ tay chờ nhân viên ra nhận order.
        /// </summary>
        private void NotifyReachedFrontOfLine() {
            _state = CustomerState.Ordering;

            if (_targetSlot != null) {
                _targetSlot.SetNeedsOrderTake(true);
                _targetSlot.OnOrderCompleted += HandleOrderCompleted;
            }
        }

        private void HandleOrderCompleted() {
            if (_targetSlot != null) {
                _targetSlot.OnOrderCompleted -= HandleOrderCompleted;
            }
            LeaveQueueAndStore();
        }

        private void LeaveQueueAndStore() {
            if (_targetSlot != null) {
                _targetSlot.LeaveQueue(this);
            }
            LeaveStore();
        }

        private void LeaveStore() {
            _state = CustomerState.Leaving;

            // Quay về đúng cái điểm Spawn ngoài cửa lúc nãy
            _agent.SetDestination(_exitPos);

            Destroy(gameObject, 5f); // 5 giây sau tự hủy
            OnLeaveStore?.Invoke(this);
        }

        private void UpdateDestination() {
            if (_targetSlot != null) {
                int index = _targetSlot.GetCustomerIndex(this);
                Vector3 pos = _targetSlot.GetQueuePosition(index);
                _agent.SetDestination(pos);
            }
        }

        private void Update() {
            if (_state == CustomerState.WalkingToQueue) {
                if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
                    _state = CustomerState.WaitingInQueue;

                    // Khách đi tới nơi sẽ xoay mặt vuông góc thẳng vào quầy (khớp với rotation của Anchor)
                    if (_targetSlot != null) {
                        transform.rotation = _targetSlot.CustomerAnchor.rotation;

                        // Nếu đang là người đầu tiên
                        if (_targetSlot.GetCustomerIndex(this) == 0) {
                            NotifyReachedFrontOfLine();
                        }
                    }
                }
            }
        }
    }
}
