using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Điều phối luồng khởi động trò chơi (App Startup Flow Controller):
    /// 1. Màn hình Splash Studio (Miyuki Studio) với hiệu ứng Fade in/out và tính năng Chạm để bỏ qua (Skip).
    /// 2. Màn hình Tải game (Loading Screen) với thanh tiến trình mượt mà (0% -> 100%), thông báo trạng thái và câu Tips ngẫu nhiên.
    /// 3. Fade out mượt mà hé lộ thế giới game khi hoàn tất quá trình khởi tạo.
    /// </summary>
    public class GameStartupFlowUI : MonoBehaviour {
        public static GameStartupFlowUI Instance { get; private set; }

        [Header("Panel CanvasGroups")]
        [Tooltip("CanvasGroup của Panel Studio Splash (Miyuki Studio)")]
        [SerializeField] private CanvasGroup _studioSplashGroup;

        [Tooltip("CanvasGroup của Panel Loading Screen")]
        [SerializeField] private CanvasGroup _loadingScreenGroup;

        [Header("Studio Splash Settings")]
        [Tooltip("Thời gian làm mờ hiện ra (giây) của logo Studio")]
        [SerializeField] private float _studioFadeInDuration = 0.6f;

        [Tooltip("Thời gian dừng hiển thị logo Studio (giây)")]
        [SerializeField] private float _studioStayDuration = 1.8f;

        [Tooltip("Thời gian chuyển tiếp mờ (Fade) từ màn hình Studio sang màn hình Loading (giây)")]
        [SerializeField] private float _studioFadeOutDuration = 0.5f;

        [Tooltip("Cho phép người chơi chạm màn hình/bấm chuột để bỏ qua nhanh màn hình Studio")]
        [SerializeField] private bool _allowSkipStudio = true;

        [Header("Studio 3D Model Support (Tùy chọn)")]
        [Tooltip("Model 3D của Studio (nếu có). Script sẽ tự động kích hoạt khi bắt đầu và tắt khi kết thúc màn hình Studio để không ảnh hưởng đến gameplay.")]
        [SerializeField] private GameObject _studio3DModel;

        [Header("Loading Screen Settings")]
        [Tooltip("Thời gian tối thiểu chạy thanh Loading (giây) để người chơi kịp nhìn thấy tips và hiệu ứng")]
        [SerializeField] private float _minLoadingDuration = 2.2f;

        [Tooltip("Thời gian làm mờ biến mất (giây) của màn hình Loading khi đạt 100%")]
        [SerializeField] private float _loadingFadeOutDuration = 0.6f;

        [Tooltip("Tự động tiêu hủy Canvas này sau khi hoàn tất để tối ưu bộ nhớ")]
        [SerializeField] private bool _destroyOnComplete = true;

        [Header("Loading UI Elements")]
        [Tooltip("Slider hiển thị tiến trình nạp (tùy chọn)")]
        [SerializeField] private Slider _loadingBar;

        [Tooltip("Image dạng Filled hiển thị tiến trình (tùy chọn thay cho Slider)")]
        [SerializeField] private Image _loadingFillImage;

        [Tooltip("Text hiển thị phần trăm (vd: 45%, 100%)")]
        [SerializeField] private TextMeshProUGUI _percentageText;

        [Tooltip("Text hiển thị trạng thái nạp (vd: Đang tải dữ liệu mỏ...)")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Tooltip("Text hiển thị câu mẹo chơi (Tips)")]
        [SerializeField] private TextMeshProUGUI _tipText;

        [Header("Tips Pool")]
        [Tooltip("Danh sách các câu mẹo chơi hiển thị ngẫu nhiên khi tải game")]
        [SerializeField] private List<string> _tips = new List<string> {
            "💡 Mẹo: Nâng cấp trạm mỏ sẽ giúp tăng mạnh giá bán quặng và tốc độ khai thác!",
            "💡 Mẹo: Thuê thêm Thợ Vận Chuyển để khách hàng không phải chờ đợi lâu.",
            "💡 Mẹo: Bàn ngoài trời giúp tăng thêm không gian phục vụ nhiều khách cùng lúc.",
            "💡 Mẹo: Đừng quên nhận tiền thưởng tích lũy AFK mỗi khi quay trở lại game!",
            "💡 Mẹo: Cây nâng cấp Ngọc Lục Bảo (EUGT) mang lại các bổ trợ vĩnh viễn cho tất cả các bản đồ."
        };

        // Sự kiện thông báo khi toàn bộ chuỗi khởi động đã hoàn tất và người chơi bước vào game
        public static event Action OnStartupSequenceCompleted;

        private bool _isStudioSkipped = false;
        private bool _isSequenceFinished = false;
        public bool IsSequenceFinished => _isSequenceFinished;

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else if (Instance != this) {
                Destroy(gameObject);
                return;
            }

            // Tự động tìm Model 3D nếu người dùng kéo vào con của panel hoặc chưa gán
            if (_studio3DModel == null && _studioSplashGroup != null) {
                var meshRenderer = _studioSplashGroup.GetComponentInChildren<MeshRenderer>(true);
                if (meshRenderer != null) {
                    _studio3DModel = meshRenderer.gameObject;
                }
            }

            // Đảm bảo Canvas khởi động luôn ở trạng thái chuẩn bị
            if (_studioSplashGroup != null) {
                _studioSplashGroup.alpha = 0f;
                _studioSplashGroup.gameObject.SetActive(true);
                _studioSplashGroup.blocksRaycasts = true;
                if (_studio3DModel != null) {
                    _studio3DModel.SetActive(true);
                }
            }

            if (_loadingScreenGroup != null) {
                _loadingScreenGroup.alpha = 0f;
                _loadingScreenGroup.gameObject.SetActive(false);
                _loadingScreenGroup.blocksRaycasts = true;
            }
        }

        private void Start() {
            StartCoroutine(RunStartupSequenceRoutine());
        }

        private void Update() {
            // Cho phép người chơi chạm màn hình/click để bỏ qua logo Studio
            if (_allowSkipStudio && !_isStudioSkipped && _studioSplashGroup != null && _studioSplashGroup.gameObject.activeSelf) {
                bool isPressed = false;
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
                    isPressed = true;
                } else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) {
                    isPressed = true;
                }

                if (isPressed) {
                    _isStudioSkipped = true;
                }
            }
        }

        private IEnumerator RunStartupSequenceRoutine() {
            // ==========================================
            // GIAI ĐOẠN 1: MIYUKI STUDIO SPLASH PANEL
            // ==========================================
            if (_studioSplashGroup != null) {
                _studioSplashGroup.gameObject.SetActive(true);
                if (_studio3DModel != null) {
                    _studio3DModel.SetActive(true);
                }

                // Fade In Studio Logo
                yield return StartCoroutine(FadeCanvasGroup(_studioSplashGroup, 0f, 1f, _studioFadeInDuration));

                // Dừng hiển thị logo theo thời gian cấu hình (hoặc người chơi bấm Skip)
                float elapsed = 0f;
                while (elapsed < _studioStayDuration && !_isStudioSkipped) {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // ==========================================
            // GIAI ĐOẠN 2: LOADING SCREEN PANEL
            // Giữ nguyên nền tối Studio bên dưới, cho Loading Screen Fade In đè lên trên
            // để TUYỆT ĐỐI KHÔNG BỊ LỘ CẢNH GAME đằng sau!
            // ==========================================
            if (_loadingScreenGroup != null) {
                _loadingScreenGroup.gameObject.SetActive(true);
                _loadingScreenGroup.transform.SetAsLastSibling(); // Đảm bảo Loading Panel luôn nằm trên cùng đè lên Studio
                _loadingScreenGroup.alpha = 0f;

                // Chọn ngẫu nhiên một câu Tip
                PickRandomTip();

                // Fade In Loading Screen (đè dần lên nền Studio)
                yield return StartCoroutine(FadeCanvasGroup(_loadingScreenGroup, 0f, 1f, _studioFadeOutDuration));
            }

            // Khi Loading Screen đã hiện hoàn toàn (alpha = 1 che kín 100% màn hình),
            // an toàn tắt hoàn toàn Panel Studio và Model 3D
            if (_studioSplashGroup != null) {
                _studioSplashGroup.gameObject.SetActive(false);
            }
            if (_studio3DModel != null) {
                _studio3DModel.SetActive(false);
            }

            if (_loadingScreenGroup != null) {
                // Chạy tiến trình Loading
                yield return StartCoroutine(SimulateLoadingProcessRoutine());

                // Dừng nhẹ 0.35s ở mốc 100% để tạo cảm giác đã nạp trọn vẹn
                yield return new WaitForSecondsRealtime(0.35f);

                // Fade Out Loading Screen để hé lộ thế giới game
                yield return StartCoroutine(FadeCanvasGroup(_loadingScreenGroup, 1f, 0f, _loadingFadeOutDuration));
                _loadingScreenGroup.gameObject.SetActive(false);
            }

            // ==========================================
            // GIAI ĐOẠN 3: VÀO GAME HOÀN TẤT
            // ==========================================
            _isSequenceFinished = true;
            OnStartupSequenceCompleted?.Invoke();

            if (_destroyOnComplete) {
                Destroy(gameObject);
            }
        }

        private IEnumerator SimulateLoadingProcessRoutine() {
            float progress = 0f;
            float duration = Mathf.Max(1.0f, _minLoadingDuration);
            float timer = 0f;

            // Danh sách các trạng thái nạp chân thực
            var steps = new (float threshold, string status)[] {
                (0.15f, "Đang khởi tạo hệ thống..."),
                (0.40f, "Đang nạp dữ liệu tiến trình & tài nguyên..."),
                (0.70f, "Đang đồng bộ bản đồ & các trạm khai thác..."),
                (0.90f, "Đang kiểm tra thu nhập ngoại tuyến AFK..."),
                (1.00f, "Sẵn sàng vào game!")
            };

            int stepIndex = 0;

            while (timer < duration) {
                timer += Time.unscaledDeltaTime;
                float targetNormalized = Mathf.Clamp01(timer / duration);

                // Easing mượt mà (SmoothStep)
                progress = Mathf.SmoothStep(0f, 1f, targetNormalized);

                // Cập nhật text trạng thái theo mốc
                while (stepIndex < steps.Length && progress >= steps[stepIndex].threshold) {
                    UpdateStatus(steps[stepIndex].status);
                    stepIndex++;
                }

                UpdateProgressVisuals(progress);
                yield return null;
            }

            // Đảm bảo đạt đúng 100%
            UpdateProgressVisuals(1.0f);
            UpdateStatus("Sẵn sàng vào game!");
        }

        private void UpdateProgressVisuals(float progress01) {
            if (_loadingBar != null) {
                _loadingBar.value = progress01;
            }

            if (_loadingFillImage != null) {
                _loadingFillImage.fillAmount = progress01;
            }

            if (_percentageText != null) {
                int percentInt = Mathf.RoundToInt(progress01 * 100f);
                _percentageText.text = $"{percentInt}%";
            }
        }

        private void UpdateStatus(string message) {
            if (_statusText != null && !string.IsNullOrEmpty(message)) {
                _statusText.text = message;
            }
        }

        private void PickRandomTip() {
            if (_tipText != null && _tips != null && _tips.Count > 0) {
                int rndIdx = UnityEngine.Random.Range(0, _tips.Count);
                _tipText.text = _tips[rndIdx];
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration) {
            if (group == null) yield break;

            if (duration <= 0f) {
                group.alpha = to;
                yield break;
            }

            float timer = 0f;
            group.alpha = from;

            while (timer < duration) {
                timer += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, timer / duration);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
