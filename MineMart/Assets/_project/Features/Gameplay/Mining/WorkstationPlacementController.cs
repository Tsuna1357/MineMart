using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MineMart.Gameplay.Mining {
    /// <summary>
    /// Điều khiển cơ chế Kéo Thả và Bố Trí Trạm Mỏ (Workstation Drag & Drop).
    /// Người chơi chạm/giữ vào trạm mỏ để nhấc lên, kéo đến một Ô Quy Hoạch (WorkstationPlot).
    /// Khi thả tay:
    /// - Nếu ô trống: Tự động Snap vào vị trí đã chỉ định.
    /// - Nếu ô đã có trạm mỏ khác: Tự động hoán đổi (Swap) vị trí giữa 2 ô mượt mà.
    /// - Nếu thả ra ngoài: Tự động trượt về vị trí cũ (Snap back).
    /// </summary>
    public class WorkstationPlacementController : MonoBehaviour {
        public static WorkstationPlacementController Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Thời gian nhấn giữ tối thiểu để bắt đầu nhấc quầy lên di chuyển (giây)")]
        [SerializeField] private float _holdThreshold = 0.2f;

        [Tooltip("Độ cao nâng bổng quầy mỏ khi đang kéo (mét)")]
        [SerializeField] private float _liftHeight = 0.5f;

        [Tooltip("Bán kính nhận diện ô quy hoạch gần nhất (mét)")]
        [SerializeField] private float _snapRadius = 2.2f;

        [Tooltip("Tốc độ bay theo ngón tay/chuột khi đang kéo")]
        [SerializeField] private float _followSpeed = 25f;

        [Header("Layer Masks")]
        [Tooltip("Layer mặt đất để tính toán tọa độ kéo (để mặc định nếu không chia layer riêng)")]
        [SerializeField] private LayerMask _groundLayer = ~0;

        private Camera _mainCamera;
        private MiningStation _draggedStation;
        private WorkstationPlot _originalPlot;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;

        private WorkstationPlot _hoveredPlot;
        private bool _isDragging = false;
        private float _pressTimer = 0f;
        private bool _isPointerDown = false;
        private MiningStation _potentialTarget;
        private WorkstationPlot _potentialPlot;

        public bool IsDragging => _isDragging;

        /// <summary>
        /// Sự kiện phát ra khi người chơi chạm nhẹ (Tap) vào Mỏ hoặc Plot có mỏ để mở Bong bóng nâng cấp.
        /// </summary>
        public static event Action<MiningStation> OnStationTapped;

        /// <summary>
        /// Sự kiện phát ra khi người chơi chạm nhẹ vào ô Plot rỗng để mở bảng Mua mỏ mới.
        /// </summary>
        public static event Action<WorkstationPlot> OnEmptyPlotTapped;

        /// <summary>
        /// Sự kiện phát ra khi người chơi chạm vào mặt sàn trống (dùng để đóng popup/bong bóng).
        /// </summary>
        public static event Action OnBackgroundTapped;

        public event Action<MiningStation> OnStationLifted;
        public event Action<MiningStation, WorkstationPlot> OnStationPlaced;
        public event Action<MiningStation, MiningStation> OnStationsSwapped;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
                return;
            }

            _mainCamera = Camera.main;
        }

        private void Update() {
            if (_mainCamera == null) {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            Vector2 pointerPos = GetPointerPosition();
            bool isPressing = IsPointerPressing();

            if (isPressing) {
                if (!_isPointerDown) {
                    // Nếu bắt đầu chạm vào UI tương tác (Button, Bong bóng...) -> bỏ qua hoàn toàn, không xử lý click 3D
                    if (IsPointerOverInteractiveUI(pointerPos)) {
                        return;
                    }

                    // Bắt đầu chạm vào scene 3D
                    _isPointerDown = true;
                    _pressTimer = 0f;
                    _potentialTarget = RaycastStationOrPlot(pointerPos, out _potentialPlot);
                } else if (!_isDragging && _potentialTarget != null) {
                    // Đang nhấn giữ
                    _pressTimer += Time.deltaTime;
                    if (_pressTimer >= _holdThreshold) {
                        StartDragging(_potentialTarget);
                    }
                }
            } else {
                if (_isPointerDown) {
                    // Buông tay
                    if (_isDragging) {
                        EndDragging();
                    } else if (_pressTimer < _holdThreshold) {
                        // Chạm nhẹ (Tap) -> Kích hoạt mở bong bóng nâng cấp!
                        if (_potentialTarget != null) {
                            Debug.Log($"<color=#38BDF8>[WorkstationPlacement]</color> Tap trúng Quầy Mỏ: {_potentialTarget.name}");
                            OnStationTapped?.Invoke(_potentialTarget);
                        } else if (_potentialPlot != null && _potentialPlot.CurrentStation == null) {
                            Debug.Log($"<color=#F59E0B>[WorkstationPlacement]</color> Tap trúng Ô Plot trống: {_potentialPlot.name}");
                            OnEmptyPlotTapped?.Invoke(_potentialPlot);
                        } else {
                            OnBackgroundTapped?.Invoke();
                        }
                    }
                    _isPointerDown = false;
                    _potentialTarget = null;
                    _potentialPlot = null;
                    _pressTimer = 0f;
                }
            }

            if (_isDragging && _draggedStation != null) {
                UpdateDragPosition(pointerPos);
            }
        }

        private Vector2 GetPointerPosition() {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }
            if (Mouse.current != null) {
                return Mouse.current.position.ReadValue();
            }
            return Vector2.zero;
        }

        private bool IsPointerPressing() {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) {
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra xem con trỏ chuột/ngón tay có đang bấm trúng phần tử UI tương tác (Button, Toggle, Slider...)
        /// hoặc khung Bong bóng/Popup hay không.
        /// Bỏ qua các Panel nền trong suốt để không làm nghẽn tia Raycast xuống scene 3D.
        /// </summary>
        private bool IsPointerOverInteractiveUI(Vector2 screenPos) {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current) {
                position = screenPos
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++) {
                GameObject go = results[i].gameObject;
                if (go == null) continue;

                // Nút bấm UI hoặc phần tử có thể chọn (Button, Toggle, Slider...)
                if (go.GetComponentInParent<UnityEngine.UI.Selectable>() != null) {
                    return true;
                }

                // Khung Bong bóng Nâng Cấp hoặc bất kỳ Popup/Dialog nào
                Transform curr = go.transform;
                while (curr != null) {
                    string n = curr.name;
                    if (n.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0) {
                        return true;
                    }
                    curr = curr.parent;
                }
            }
            return false;
        }

        private MiningStation RaycastStationOrPlot(Vector2 screenPos, out WorkstationPlot hitPlot) {
            hitPlot = null;
            // Bỏ qua nếu bấm trúng nút bấm UI (Button, Slider...)
            if (IsPointerOverInteractiveUI(screenPos)) {
                return null;
            }

            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f)) {
                // Kiểm tra trực tiếp vào MiningStation
                MiningStation station = hit.collider.GetComponentInParent<MiningStation>();
                if (station != null && station.IsUnlocked) {
                    hitPlot = station.CurrentPlot;
                    return station;
                }

                // Hoặc kiểm tra vào WorkstationPlot
                WorkstationPlot plot = hit.collider.GetComponentInParent<WorkstationPlot>();
                if (plot != null) {
                    hitPlot = plot;
                    MiningStation st = plot.CurrentStation;
                    if (st == null) {
                        st = plot.GetComponentInChildren<MiningStation>();
                    }
                    if (st != null && st.IsUnlocked) {
                        return st;
                    }
                }
            }
            return null;
        }

        private void StartDragging(MiningStation station) {
            _isDragging = true;
            _draggedStation = station;
            _originalPlot = station.CurrentPlot;
            _originalPosition = station.transform.position;
            _originalRotation = station.transform.rotation;

            // Đánh dấu trạm đang bị nhấc -> ngắt ngay lập tức người chơi và thợ đang đào
            _draggedStation.IsBeingDragged = true;

            // Nhấc nhẹ mỏ lên
            Vector3 liftedPos = _originalPosition + Vector3.up * _liftHeight;
            StartCoroutine(AnimateLift(station.transform, liftedPos, 0.12f));

            OnStationLifted?.Invoke(station);
        }

        private void UpdateDragPosition(Vector2 screenPos) {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, _originalPosition.y + _liftHeight, 0));

            if (groundPlane.Raycast(ray, out float enter)) {
                Vector3 worldHit = ray.GetPoint(enter);

                // Tìm kiếm Plot mà con trỏ chuột/ngón tay đang lướt vào khu vực
                WorkstationPlot nearest = WorkstationPlot.FindNearestPlot(worldHit, _snapRadius);

                if (nearest != _hoveredPlot) {
                    if (_hoveredPlot != null) {
                        _hoveredPlot.SetHighlight(false);
                    }

                    _hoveredPlot = nearest;

                    if (_hoveredPlot != null) {
                        bool isSwap = !_hoveredPlot.IsEmpty && _hoveredPlot.CurrentStation != _draggedStation;
                        _hoveredPlot.SetHighlight(true, isSwap);
                    }
                }

                // Khi vào khu vực ô đất: Tự động kéo mỏ về đúng vị trí đã setup của khu đó
                Vector3 targetDragPos;
                Quaternion targetDragRot;

                if (_hoveredPlot != null) {
                    // Hút nam châm về đúng vị trí ô đất đã cài đặt (nâng nhẹ trên sàn)
                    targetDragPos = _hoveredPlot.StationTargetPosition + Vector3.up * (_liftHeight * 0.4f);
                    targetDragRot = _hoveredPlot.StationTargetRotation;
                } else {
                    // Bay tự do theo con trỏ chuột khi ở ngoài các khu đất
                    targetDragPos = worldHit;
                    targetDragRot = _originalRotation;
                }

                _draggedStation.transform.position = Vector3.Lerp(
                    _draggedStation.transform.position,
                    targetDragPos,
                    Time.deltaTime * _followSpeed
                );

                _draggedStation.transform.rotation = Quaternion.Slerp(
                    _draggedStation.transform.rotation,
                    targetDragRot,
                    Time.deltaTime * _followSpeed
                );
            }
        }

        private void EndDragging() {
            _isDragging = false;

            MiningStation dragged = _draggedStation;
            MiningStation swapped = null;

            if (_hoveredPlot != null) {
                _hoveredPlot.SetHighlight(false);

                if (_hoveredPlot == _originalPlot) {
                    // Thả lại đúng ô cũ
                    _originalPlot.SnapStation(_draggedStation, true);
                } else if (_hoveredPlot.IsEmpty) {
                    // Ô đích đang trống -> Snap sang ô mới
                    if (_originalPlot != null) {
                        _originalPlot.ClearStation();
                    }
                    _hoveredPlot.SnapStation(_draggedStation, true);
                    OnStationPlaced?.Invoke(_draggedStation, _hoveredPlot);
                } else {
                    // Ô đích đã có mỏ khác -> Hoán đổi vị trí (Swap)
                    swapped = _hoveredPlot.CurrentStation;
                    if (swapped != null) {
                        swapped.IsBeingDragged = true; // Ngắt thợ đang đào mỏ bị hoán đổi
                    }
                    WorkstationPlot.SwapStations(_originalPlot, _hoveredPlot, true);
                    OnStationsSwapped?.Invoke(_draggedStation, swapped);
                }
            } else {
                // Thả ngoài ô quy hoạch -> Tự động trượt về vị trí cũ
                if (_originalPlot != null) {
                    _originalPlot.SnapStation(_draggedStation, true);
                } else if (_draggedStation != null) {
                    StartCoroutine(AnimateReturn(_draggedStation, _originalPosition, _originalRotation, 0.2f));
                }
            }

            _hoveredPlot = null;
            _draggedStation = null;
            _originalPlot = null;
        }

        private IEnumerator AnimateLift(Transform tr, Vector3 targetPos, float duration) {
            Vector3 startPos = tr.position;
            float elapsed = 0f;
            while (elapsed < duration && _isDragging) {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                tr.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
        }

        private IEnumerator AnimateReturn(MiningStation station, Vector3 targetPos, Quaternion targetRot, float duration) {
            if (station == null) yield break;
            Transform tr = station.transform;
            Vector3 startPos = tr.position;
            Quaternion startRot = tr.rotation;
            float elapsed = 0f;
            while (elapsed < duration && tr != null) {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                tr.position = Vector3.Lerp(startPos, targetPos, t);
                tr.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            if (tr != null) {
                tr.position = targetPos;
                tr.rotation = targetRot;
            }
            if (station != null) {
                station.IsBeingDragged = false;
                station.NotifyPlacedDown();
            }
        }
    }
}
