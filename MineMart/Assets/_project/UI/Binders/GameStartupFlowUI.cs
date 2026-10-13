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

        [Tooltip("CanvasGroup của phần nội dung Studio (Logo, Tên, Subtitle) để từ từ hiện lên rồi từ từ ẩn đi trên nền tối")]
        [SerializeField] private CanvasGroup _studioContentGroup;

        [Tooltip("CanvasGroup của Panel Loading Screen")]
        [SerializeField] private CanvasGroup _loadingScreenGroup;

        [Header("Studio Splash Settings")]
        [Tooltip("Thời gian làm mờ hiện ra (giây) của logo Studio")]
        [SerializeField] private float _studioFadeInDuration = 0.8f;

        [Tooltip("Thời gian dừng hiển thị logo Studio (giây)")]
        [SerializeField] private float _studioStayDuration = 1.6f;

        [Tooltip("Thời gian làm mờ ẩn đi (giây) của logo Studio")]
        [SerializeField] private float _studioFadeOutDuration = 0.6f;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeTargetFrameRate() {
            // Khóa mượt mà 60 FPS trên thiết bị di động (Android / iOS) và chống hiện tượng giật micro-stuttering
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        private CanvasGroup _backdropGroup;
        private int _frameCounter = 0;
        private float _skipGraceTimer = 0f;
        private const float SKIP_GRACE_DURATION = 0.6f;

        private void Awake() {
            // Đảm bảo thiết lập 60 FPS luôn có hiệu lực
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (Instance == null) {
                Instance = this;
            } else if (Instance != this) {
                Destroy(gameObject);
                return;
            }

            // Tạo hoặc đảm bảo tấm rèm nền đen bảo vệ (Backdrop) nằm dưới cùng của Canvas
            // để TUYỆT ĐỐI KHÔNG BAO GIỜ bị lộ cảnh game 3D khi Studio Panel bắt đầu từ Alpha = 0!
            EnsureBackdropExists();

            // Tự động tìm Model 3D nếu người dùng kéo vào con của panel hoặc chưa gán
            if (_studio3DModel == null && _studioSplashGroup != null) {
                var meshRenderer = _studioSplashGroup.GetComponentInChildren<MeshRenderer>(true);
                if (meshRenderer != null) {
                    _studio3DModel = meshRenderer.gameObject;
                }
            }

            // Panel Studio Splash bắt đầu ở mức 0% hoàn toàn (vô hình) để từ từ hiện lên!
            if (_studioSplashGroup != null) {
                _studioSplashGroup.alpha = 0f;
                _studioSplashGroup.gameObject.SetActive(true);
                _studioSplashGroup.blocksRaycasts = true;

                // Tự động tìm hoặc gắn CanvasGroup cho Studio_Content nếu có
                if (_studioContentGroup == null) {
                    Transform contentTr = _studioSplashGroup.transform.Find("Studio_Content");
                    if (contentTr != null) {
                        _studioContentGroup = contentTr.GetComponent<CanvasGroup>();
                    }
                }

                if (_studioContentGroup != null) {
                    _studioContentGroup.alpha = 1f; // Content luôn 100% bên trong Panel để panel điều khiển toàn bộ
                    _studioContentGroup.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                }

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

        private void EnsureBackdropExists() {
            Transform backdropTr = transform.Find("Panel_BlackBackdrop");
            if (backdropTr == null) {
                GameObject backdropGo = new GameObject("Panel_BlackBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                backdropGo.transform.SetParent(transform, false);
                backdropGo.transform.SetAsFirstSibling(); // Nằm dưới cùng đè lên cảnh 3D nhưng dưới Studio & Loading

                RectTransform rt = backdropGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                Image img = backdropGo.GetComponent<Image>();
                img.color = new Color(0.04f, 0.06f, 0.09f, 1f); // Nền tối đen sạch sẽ sang trọng

                _backdropGroup = backdropGo.GetComponent<CanvasGroup>();
                _backdropGroup.alpha = 1f;
                _backdropGroup.blocksRaycasts = true;
            } else {
                backdropTr.SetAsFirstSibling();
                _backdropGroup = backdropTr.GetComponent<CanvasGroup>();
                if (_backdropGroup == null) {
                    _backdropGroup = backdropTr.gameObject.AddComponent<CanvasGroup>();
                }
                _backdropGroup.alpha = 1f;
            }
        }

        private void Start() {
            StartCoroutine(RunStartupSequenceRoutine());
        }

        private void Update() {
            // Chờ ít nhất 30 frame và 0.6s sau khi bật game để tránh nhận nhầm cú click chuột bấm nút "Play" trong Unity Editor
            _frameCounter++;
            if (_frameCounter < 30) return;

            // Cho phép người chơi chạm màn hình/click để bỏ qua logo Studio
            if (_allowSkipStudio && !_isStudioSkipped && _studioSplashGroup != null && _studioSplashGroup.gameObject.activeSelf) {
                _skipGraceTimer += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (_skipGraceTimer < SKIP_GRACE_DURATION) {
                    return;
                }

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
            // 1. Chờ 2 frame sau khi Scene khởi động để Unity nạp xong tài nguyên,
            // ổn định FPS và triệt tiêu hoàn toàn cú giật lag / cold-start spike của frame 0
            yield return null;
            yield return null;

            // ==========================================
            // GIAI ĐOẠN 1: MIYUKI STUDIO SPLASH PANEL
            // ==========================================
            if (_studioSplashGroup != null) {
                _studioSplashGroup.gameObject.SetActive(true);
                _studioSplashGroup.alpha = 0f; // Bắt đầu chính xác từ Alpha = 0

                if (_studio3DModel != null) {
                    _studio3DModel.SetActive(true);
                }

                Transform contentTr = _studioContentGroup != null 
                    ? _studioContentGroup.transform 
                    : _studioSplashGroup.transform.Find("Studio_Content");

                // 1. TỪ TỪ HIỆN LÊN (FADE IN): Alpha chạy từ 0 -> 1 (Kèm hiệu ứng phóng nhẹ cinematic 0.92 -> 1.0)
                yield return StartCoroutine(FadeStudioRoutine(_studioSplashGroup, contentTr, 0f, 1f, _studioFadeInDuration, true));

                // 2. DỪNG HIỂN THỊ LOGO (STAY / HOLD) ở Alpha = 1
                float elapsed = 0f;
                while (elapsed < _studioStayDuration && !_isStudioSkipped) {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    yield return null;
                }

                // 3. TỪ TỪ ẨN ĐI (FADE OUT): Alpha chạy từ 1 -> 0
                yield return StartCoroutine(FadeStudioRoutine(_studioSplashGroup, contentTr, _studioSplashGroup.alpha, 0f, _studioFadeOutDuration, false));

                // Dừng nhẹ 0.1s sau khi logo đã ẩn hoàn toàn
                if (!_isStudioSkipped) {
                    yield return new WaitForSecondsRealtime(0.1f);
                }

                _studioSplashGroup.gameObject.SetActive(false);
                if (_studio3DModel != null) {
                    _studio3DModel.SetActive(false);
                }
            }

            // ==========================================
            // GIAI ĐOẠN 2: LOADING SCREEN PANEL
            // Đè lên nền Backdrop đen bên dưới, TUYỆT ĐỐI KHÔNG LỘ CẢNH GAME
            // ==========================================
            if (_loadingScreenGroup != null) {
                _loadingScreenGroup.gameObject.SetActive(true);
                _loadingScreenGroup.transform.SetAsLastSibling();
                _loadingScreenGroup.alpha = 0f;

                // Chọn ngẫu nhiên một câu Tip
                PickRandomTip();

                // Fade In Loading Screen: 0 -> 1
                yield return StartCoroutine(FadeCanvasGroup(_loadingScreenGroup, 0f, 1f, _studioFadeOutDuration));
            }

            if (_loadingScreenGroup != null) {
                // Chạy tiến trình Loading giả lập
                yield return StartCoroutine(SimulateLoadingProcessRoutine());

                // Dừng nhẹ 0.35s ở mốc 100% để tạo cảm giác đã nạp trọn vẹn
                yield return new WaitForSecondsRealtime(0.35f);

                // ==========================================
                // GIAI ĐOẠN 3: FADE OUT HÉ LỘ THẾ GIỚI GAME
                // Cả Loading Screen và Backdrop đen cùng mờ dần về 0
                // ==========================================
                float fadeTimer = 0f;
                float startLoadingAlpha = _loadingScreenGroup.alpha;
                float startBackdropAlpha = _backdropGroup != null ? _backdropGroup.alpha : 0f;

                while (fadeTimer < _loadingFadeOutDuration) {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    fadeTimer += dt;
                    float t = Mathf.Clamp01(fadeTimer / _loadingFadeOutDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    _loadingScreenGroup.alpha = Mathf.Lerp(startLoadingAlpha, 0f, smoothT);
                    if (_backdropGroup != null) {
                        _backdropGroup.alpha = Mathf.Lerp(startBackdropAlpha, 0f, smoothT);
                    }
                    yield return null;
                }

                _loadingScreenGroup.alpha = 0f;
                _loadingScreenGroup.gameObject.SetActive(false);
                if (_backdropGroup != null) {
                    _backdropGroup.alpha = 0f;
                    _backdropGroup.gameObject.SetActive(false);
                }
            }

            // ==========================================
            // GIAI ĐOẠN 4: VÀO GAME HOÀN TẤT
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
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                timer += dt;
                float t = Mathf.Clamp01(timer / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                group.alpha = Mathf.Lerp(from, to, smoothT);
                yield return null;
            }

            group.alpha = to;
        }

        private IEnumerator FadeStudioRoutine(CanvasGroup group, Transform contentTr, float from, float to, float duration, bool withScale) {
            if (group == null) yield break;

            // Đặt alpha khởi điểm ngay lập tức
            group.alpha = from;

            if (_isStudioSkipped) {
                group.alpha = to;
                yield break;
            }

            if (duration <= 0f) {
                group.alpha = to;
                yield break;
            }

            Vector3 startScale = withScale ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;
            Vector3 endScale = Vector3.one;
            if (contentTr != null && withScale) {
                contentTr.localScale = startScale;
            }

            float timer = 0f;

            while (timer < duration && !_isStudioSkipped) {
                // Kẹp tối đa 50ms mỗi frame để chống mọi hiện tượng frame lag / spike làm tua nhanh hiệu ứng
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                timer += dt;
                float t = Mathf.Clamp01(timer / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                group.alpha = Mathf.Lerp(from, to, smoothT);
                if (contentTr != null && withScale) {
                    contentTr.localScale = Vector3.Lerp(startScale, endScale, smoothT);
                }
                yield return null;
            }

            if (_isStudioSkipped) {
                group.alpha = 0f;
            } else {
                group.alpha = to;
                if (contentTr != null && withScale) {
                    contentTr.localScale = endScale;
                }
            }
        }
    }
}
