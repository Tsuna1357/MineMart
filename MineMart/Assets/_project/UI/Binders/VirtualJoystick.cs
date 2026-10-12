using MineMart.Core.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Cần Điều Khiển Cảm Ứng Ảo (Virtual Joystick) trên màn hình.
    /// - Hiển thị ở góc dưới màn hình, thiên về giữa hơn để thao tác ngón cái tự nhiên.
    /// - Chỉ bắt Raycast trên đúng diện tích vòng tròn Joystick (không che chắn thao tác vuốt map).
    /// - Tự động đồng bộ với trạng thái Bật/Tắt trong Cài Đặt (SettingsManager).
    /// - Xuất giá trị Vector2 thời gian thực vào SettingsManager.VirtualJoystickInput cho PlayerController đọc.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
        public static VirtualJoystick Instance { get; private set; }
        public static Vector2 InputDirection { get; private set; } = Vector2.zero;

        [Header("Rect References")]
        [Tooltip("RectTransform của vòng tròn nền Joystick")]
        [SerializeField] private RectTransform _backgroundRect;

        [Tooltip("RectTransform của núm cần gạt bên trong")]
        [SerializeField] private RectTransform _handleRect;

        [Header("Settings")]
        [Tooltip("Bán kính tối đa núm cần gạt di chuyển ra khỏi tâm (pixel)")]
        [SerializeField] private float _handleRange = 75f;

        [Tooltip("Độ mờ khi không chạm")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleAlpha = 0.65f;

        [Tooltip("Độ mờ khi đang giữ chạm")]
        [Range(0f, 1f)]
        [SerializeField] private float _activeAlpha = 0.95f;

        [SerializeField] private CanvasGroup _canvasGroup;

        private Canvas _canvas;

        private void Reset() {
            AutoFindReferences();
        }

        private void OnValidate() {
            AutoFindReferences();
        }

        public void AutoFindReferences() {
            if (_backgroundRect == null) {
                _backgroundRect = GetComponent<RectTransform>();
            }
            if (_handleRect == null && transform.childCount > 0) {
                _handleRect = transform.GetChild(0) as RectTransform;
            }
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public static VirtualJoystick GetOrCreateInScene() {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (found != null) {
                Instance = found;
                return Instance;
            }

            Canvas canvas = null;
            if (MainCanvasHud.Instance != null) {
                canvas = MainCanvasHud.Instance.GetComponentInParent<Canvas>();
            }
            if (canvas == null) {
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                for (int i = 0; i < allCanvases.Length; i++) {
                    if (allCanvases[i].renderMode != RenderMode.WorldSpace) {
                        canvas = allCanvases[i];
                        break;
                    }
                }
            }
            if (canvas == null) return null;

            return VirtualJoystickBootstrap.CreateRuntimeJoystick(canvas);
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            }

            AutoFindReferences();

            if (_canvasGroup == null) {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvas = GetComponentInParent<Canvas>();
            SetAlpha(_idleAlpha);
        }

        private void Start() {
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnJoystickToggled -= HandleJoystickToggled;
                SettingsManager.Instance.OnJoystickToggled += HandleJoystickToggled;
                HandleJoystickToggled(SettingsManager.Instance.IsJoystickEnabled);
            }
        }

        private void OnDestroy() {
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnJoystickToggled -= HandleJoystickToggled;
            }
            InputDirection = Vector2.zero;
            SettingsManager.VirtualJoystickInput = Vector2.zero;
            if (Instance == this) Instance = null;
        }

        private void HandleJoystickToggled(bool enabled) {
            gameObject.SetActive(enabled);
            if (!enabled) {
                InputDirection = Vector2.zero;
                SettingsManager.VirtualJoystickInput = Vector2.zero;
                if (_handleRect != null) _handleRect.anchoredPosition = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData) {
            SetAlpha(_activeAlpha);
            UpdateJoystickPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData) {
            UpdateJoystickPosition(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) {
            InputDirection = Vector2.zero;
            SettingsManager.VirtualJoystickInput = Vector2.zero;

            if (_handleRect != null) {
                _handleRect.anchoredPosition = Vector2.zero;
            }

            SetAlpha(_idleAlpha);
        }

        private void UpdateJoystickPosition(PointerEventData eventData) {
            if (_backgroundRect == null || _handleRect == null) return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? eventData.pressEventCamera : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _backgroundRect,
                eventData.position,
                cam,
                out Vector2 localPoint
            )) {
                // Giới hạn trong phạm vi bán kính
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, _handleRange);
                _handleRect.anchoredPosition = clamped;

                // Xuất hướng chuẩn hóa (-1 đến 1)
                Vector2 dir = clamped / _handleRange;
                InputDirection = dir;
                SettingsManager.VirtualJoystickInput = dir;
            }
        }

        private void SetAlpha(float alpha) {
            if (_canvasGroup != null) {
                _canvasGroup.alpha = alpha;
            }
        }
    }

    /// <summary>
    /// Khởi tạo Virtual Joystick giao diện đẹp mắt, to hơn, dịch chuyển ra giữa màn hình ở dưới.
    /// </summary>
    public static class VirtualJoystickBootstrap {
        public static VirtualJoystick CreateRuntimeJoystick(Canvas canvas) {
            if (canvas == null) return null;

            Transform parentTransform = canvas.transform;
            // Ưu tiên đặt làm con của MainGamePanel để cùng cấp với Top_Bar / Bottom_Bar trong Hierarchy
            Transform mainGamePanel = canvas.transform.Find("MainGamePanel") ?? canvas.transform.Find("UI/HUB/MainGamePanel");
            if (mainGamePanel != null) {
                parentTransform = mainGamePanel;
            }

            // 1. Root Joystick GameObject (đặt ở nửa dưới màn hình, dịch chuyển ra giữa hơn)
            GameObject joystickRoot = new GameObject("VirtualJoystick", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VirtualJoystick));
            joystickRoot.transform.SetParent(parentTransform, false);

            var rootRect = joystickRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            // Vị trí mới: x: 240px (dời ra giữa hơn nhiều so với 130px), y: 200px (nâng cao hơn mép đáy)
            rootRect.anchoredPosition = new Vector2(240f, 200f);
            // Kích thước mới: 220x220 px (to hơn rõ rệt so với 160x160 px cũ)
            rootRect.sizeDelta = new Vector2(220f, 220f);

            // Nền vòng tròn viền tối mờ
            var bgImg = joystickRoot.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.16f, 0.24f, 0.65f); // Xanh đen translucent
            bgImg.raycastTarget = true; // CHỈ vòng tròn này nhận raycast

            // 2. Núm gạt Joystick Handle
            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(joystickRoot.transform, false);

            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            // Kích thước núm mới: 88x88 px (to hơn so với 64x64 px cũ)
            handleRect.sizeDelta = new Vector2(88f, 88f);

            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.2f, 0.85f, 0.95f, 0.95f); // Xanh ngọc nổi bật
            handleImg.raycastTarget = false; // Núm không cần chặn raycast

            // Gán các trường cho script
            var joystickScript = joystickRoot.GetComponent<VirtualJoystick>();
            SetField(joystickScript, "_backgroundRect", rootRect);
            SetField(joystickScript, "_handleRect", handleRect);
            SetField(joystickScript, "_handleRange", 75f); // Bán kính di chuyển 75px
            SetField(joystickScript, "_idleAlpha", 0.65f);
            SetField(joystickScript, "_activeAlpha", 0.95f);

            // Kiểm tra trạng thái lưu trước đó
            if (SettingsManager.Instance != null && !SettingsManager.Instance.IsJoystickEnabled) {
                joystickRoot.SetActive(false);
            }

            return joystickScript;
        }

        private static void SetField(object target, string fieldName, object value) {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }
    }
}
