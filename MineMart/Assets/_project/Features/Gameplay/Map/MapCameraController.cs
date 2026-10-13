using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MineMart.Gameplay.Mining;
using MineMart.Gameplay.Player;

namespace MineMart.Gameplay.Map {
    /// <summary>
    /// Điều khiển Kéo / Cuộn Bản Đồ Theo Chiều Dọc (Vertical-Only Map Pan Camera).
    /// Khóa cố định trục ngang X, chỉ cho phép người chơi vuốt dọc lên và xuống để quan sát toàn bộ map.
    /// Hỗ trợ mượt mà:
    /// 1. Quán tính trôi dọc êm ái (Vertical Inertia & Damping).
    /// 2. Giới hạn biên dọc (_minZ, _maxZ) tránh trôi ra ngoài khuôn viên đảo.
    /// 3. Loại trừ va chạm với UI, Joystick và thao tác di dời quầy mỏ.
    /// 4. Tự động quay về bám theo Người chơi khi điều khiển nhân vật chạy hoặc chạm đúp (Double Tap).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour {
        public static MapCameraController Instance { get; private set; }

        [Header("Vertical Scroll Settings")]
        [Tooltip("Tốc độ / Độ nhạy kéo bản đồ theo chiều dọc (mặc định: 1.0)")]
        [SerializeField] private float _dragSensitivity = 1.0f;

        [Tooltip("Độ mượt khi di chuyển camera (càng cao càng bám sát tay kéo)")]
        [SerializeField] private float _smoothSpeed = 12f;

        [Tooltip("Hệ số quán tính trôi sau khi buông tay (0 = dừng ngay, 0.9 = trôi êm)")]
        [SerializeField] [Range(0f, 0.95f)] private float _inertiaDamping = 0.88f;

        [Header("Vertical Boundaries (Giới Hạn Biên Dọc)")]
        [Tooltip("Tọa độ Z tối thiểu (phía nam/cổng đón khách)")]
        [SerializeField] private float _minZ = -22f;

        [Tooltip("Tọa độ Z tối đa (phía bắc/sau tiệm)")]
        [SerializeField] private float _maxZ = 12f;

        [Header("Player Tracking")]
        [Tooltip("Tự động trượt camera bám theo khi Người chơi chạm tới mép viền trên/dưới màn hình")]
        [SerializeField] private bool _autoFollowPlayerOnMove = true;

        [Header("Screen Edge Deadzone (Khung Viền Camera Bám Rìa)")]
        [Tooltip("Tỷ lệ lề trên màn hình (0.05 đến 0.40): Khi người chơi vượt quá (1 - lề trên), camera mới nhích lên. Mặc định 0.18 (18% trên)")]
        [SerializeField] [Range(0.05f, 0.40f)] private float _topEdgeMargin = 0.18f;

        [Tooltip("Tỷ lệ lề dưới màn hình (0.05 đến 0.40): Khi người chơi vượt quá lề dưới, camera mới nhích xuống. Mặc định 0.18 (18% dưới)")]
        [SerializeField] [Range(0.05f, 0.40f)] private float _bottomEdgeMargin = 0.18f;

        [Tooltip("Độ lệch Z của Camera so với Player khi chạm đúp (Double Tap)")]
        [SerializeField] private float _cameraZOffset = -10f;

        [Tooltip("Độ cao Y cố định của Camera")]
        [SerializeField] private float _fixedHeightY = 18f;

        private Camera _camera;
        private Vector3 _targetPosition;
        private float _dragVelocityZ;
        private float _fixedX;
        private Vector2 _lastScreenPosition;
        private bool _isDragging = false;
        private float _lastTapTime = 0f;
        private const float DOUBLE_TAP_THRESHOLD = 0.28f;

        public bool IsDraggingMap => _isDragging;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(this);
                return;
            }

            _camera = GetComponent<Camera>();
            _fixedHeightY = transform.position.y;
            _fixedX = transform.position.x;
            _targetPosition = transform.position;

            if (Mathf.Approximately(_cameraZOffset, 0f) && PlayerController.Instance != null) {
                _cameraZOffset = transform.position.z - PlayerController.Instance.transform.position.z;
            }
        }

        private void Start() {
            _targetPosition = new Vector3(_fixedX, _fixedHeightY, transform.position.z);
        }

        private void Update() {
            HandleInput();
            HandlePlayerFollow();
            HandleInertiaAndClamping();
            ApplyCameraMovement();
        }

        private void HandlePlayerFollow() {
            if (PlayerController.Instance == null || _camera == null) return;

            // Nếu người chơi đang chủ động vuốt bản đồ thì camera hoàn toàn ưu tiên theo tay người chơi
            if (_isDragging) return;

            if (!_autoFollowPlayerOnMove) return;

            // QUAN TRỌNG: Khi ở chế độ AI (Auto-Pilot), người chơi muốn tự do kéo map đi bất kỳ đâu
            // để quan sát toàn tiệm mà không bị camera tự động bám theo hay giật về phía nhân vật!
            if (PlayerController.Instance.IsAutoPiloting) return;

            Vector3 playerPos = PlayerController.Instance.transform.position;

            // Tính khoảng cách Z từ Camera đến vị trí giao cắt của rìa trên và rìa dưới trên mặt sàn
            float offsetTop = GetZOffsetForViewportY(1f - _topEdgeMargin, playerPos.y);
            float offsetBottom = GetZOffsetForViewportY(_bottomEdgeMargin, playerPos.y);

            // Khoảng cách Z hiện tại từ Player tới Camera target
            float relativeZ = playerPos.z - _targetPosition.z;

            // 1. Khi người chơi chạm/vượt quá rìa trên màn hình -> Camera nhích lên (+Z)
            if (relativeZ > offsetTop) {
                _targetPosition.z = playerPos.z - offsetTop;
                ClampTargetPosition();
            }
            // 2. Khi người chơi chạm/vượt quá rìa dưới màn hình -> Camera nhích xuống (-Z)
            else if (relativeZ < offsetBottom) {
                _targetPosition.z = playerPos.z - offsetBottom;
                ClampTargetPosition();
            }
            // 3. Khi người chơi ở trong vùng an toàn giữa 2 rìa màn hình -> Camera đứng yên hoàn toàn!
        }

        /// <summary>
        /// Tính khoảng cách delta Z từ vị trí Camera đến điểm chiếu của Viewport Y trên mặt phẳng sàn Y.
        /// </summary>
        private float GetZOffsetForViewportY(float viewportY, float groundY) {
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, viewportY, 1f));
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (groundPlane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                return hitPoint.z - transform.position.z;
            }
            return 10f;
        }

        private void HandleInput() {
            // 1. Kiểm tra thao tác kéo thả di dời quầy mỏ đang diễn ra hoặc đang giữ mỏ -> Không kéo map
            if (WorkstationPlacementController.Instance != null && (WorkstationPlacementController.Instance.IsDragging || WorkstationPlacementController.Instance.IsInteracting)) {
                _isDragging = false;
                return;
            }

            // 2. Xử lý Input Kéo Map (Nhận cử chỉ vuốt dọc ở mọi thời điểm, không bị khóa khi nhân vật chạy)
            Vector2 currentScreenPos = Vector2.zero;
            bool isPointerDown = false;
            bool isPointerHeld = false;
            bool isPointerUp = false;

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0) {
                var touch = Touchscreen.current.touches[0];
                currentScreenPos = touch.position.ReadValue();
                isPointerDown = touch.press.wasPressedThisFrame;
                isPointerHeld = touch.press.isPressed;
                isPointerUp = touch.press.wasReleasedThisFrame;
            } else if (Mouse.current != null) {
                currentScreenPos = Mouse.current.position.ReadValue();
                isPointerDown = Mouse.current.leftButton.wasPressedThisFrame;
                isPointerHeld = Mouse.current.leftButton.isPressed;
                isPointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            }

            // Bắt đầu chạm
            if (isPointerDown) {
                // Bỏ qua nếu chạm vào giao diện UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                    _isDragging = false;
                    return;
                }

                // Bỏ qua nếu chạm vào Quầy Mỏ hoặc Ô Plot (để người chơi bấm nâng cấp hoặc nhấn giữ kéo quầy mà không bị rung giật camera)
                if (WorkstationPlacementController.Instance != null && WorkstationPlacementController.Instance.IsPointerOverStationOrPlot(currentScreenPos)) {
                    _isDragging = false;
                    return;
                }

                // Phát hiện Chạm đúp (Double Tap) để lấy lại tiêu điểm Player
                if (Time.time - _lastTapTime <= DOUBLE_TAP_THRESHOLD) {
                    FocusOnPlayer();
                    _lastTapTime = 0f;
                    _isDragging = false;
                    return;
                }
                _lastTapTime = Time.time;

                _isDragging = true;
                _lastScreenPosition = currentScreenPos;
                _dragVelocityZ = 0f;
            }
            // Đang giữ và kéo dọc
            else if (isPointerHeld && _isDragging) {
                Vector2 deltaScreen = currentScreenPos - _lastScreenPosition;
                _lastScreenPosition = currentScreenPos;

                if (Mathf.Abs(deltaScreen.y) > 0.001f) {
                    // Chuyển đổi khoảng cách màn hình sang khoảng cách World 3D theo góc nghiêng Orthographic
                    float factor = (_camera.orthographicSize * 2f) / Screen.height * _dragSensitivity;

                    // Camera nghiêng góc ~60 độ chiếu xuống sàn:
                    float sinAngle = Mathf.Max(0.5f, Mathf.Sin(Mathf.Deg2Rad * transform.eulerAngles.x));
                    float deltaZ = -deltaScreen.y * factor / sinAngle;

                    _targetPosition.z += deltaZ;
                    _dragVelocityZ = deltaZ / Mathf.Max(0.001f, Time.deltaTime);

                    ClampTargetPosition();
                }
            }
            // Buông tay
            else if (isPointerUp && _isDragging) {
                _isDragging = false;
            }
        }

        private void HandleInertiaAndClamping() {
            // Khi không còn giữ tay kéo, áp dụng quán tính trôi dọc nhẹ nhàng
            if (!_isDragging && Mathf.Abs(_dragVelocityZ) > 0.01f) {
                _targetPosition.z += _dragVelocityZ * Time.deltaTime;
                _dragVelocityZ *= Mathf.Pow(_inertiaDamping, Time.deltaTime * 60f);
                ClampTargetPosition();
            } else if (!_isDragging) {
                _dragVelocityZ = 0f;
            }
        }

        private void ClampTargetPosition() {
            _targetPosition.x = _fixedX;
            _targetPosition.y = _fixedHeightY;
            _targetPosition.z = Mathf.Clamp(_targetPosition.z, _minZ, _maxZ);
        }

        private void ApplyCameraMovement() {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _smoothSpeed);
        }

        /// <summary>
        /// Đưa góc nhìn Camera quay trở về trung tâm Người chơi mượt mà.
        /// </summary>
        public void FocusOnPlayer() {
            if (PlayerController.Instance != null) {
                Vector3 playerPos = PlayerController.Instance.transform.position;
                _targetPosition.z = playerPos.z + _cameraZOffset;
                ClampTargetPosition();
                _dragVelocityZ = 0f;
            }
        }

        /// <summary>
        /// Di chuyển tiêu điểm camera tới một tọa độ World chỉ định (ví dụ xem quầy vừa mở khóa).
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos) {
            _targetPosition.z = worldPos.z + _cameraZOffset;
            ClampTargetPosition();
            _dragVelocityZ = 0f;
        }

        private void OnDrawGizmosSelected() {
            // Vẽ đường giới hạn hành trình dọc của Camera trên Scene View
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Vector3 start = new Vector3(_fixedX, 0.1f, _minZ);
            Vector3 end = new Vector3(_fixedX, 0.1f, _maxZ);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, 0.5f);
            Gizmos.DrawWireSphere(end, 0.5f);
        }
    }
}
