using System;
using System.Collections;
using MineMart.Core.Audio;
using MineMart.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Giao diện Hộp Thoại Cài Đặt Game (Settings Modal Dialog).
    /// Điều khiển 3 tính năng cốt lõi:
    /// 1. Cần điều khiển (Joystick): Bật / Tắt
    /// 2. Âm nhạc (Music / BGM): Bật / Tắt
    /// 3. Âm thanh hiệu ứng (SFX): Bật / Tắt
    /// Tự động kết nối với SettingBtn từ MainCanvasHud.
    /// Có hiệu ứng Pop-in / Fade mở đóng sang trọng.
    /// ĐẶC BIỆT: Khắc phục triệt để lỗi cản trở vuốt màn hình (Backdrop chỉ chặn raycast khi Dialog thực sự MỞ).
    /// </summary>
    public class SettingsDialogUI : MonoBehaviour {
        public static SettingsDialogUI Instance { get; private set; }

        [Header("Root & Containers")]
        [Tooltip("Root Wrapper bao trùm toàn màn hình chứa Backdrop và Dialog (sẽ SetActive false hoàn toàn khi đóng để không chặn vuốt màn hình)")]
        [SerializeField] private GameObject _rootWrapper;

        [Header("Dialog Animation Settings")]
        [Tooltip("RectTransform của khung Dialog chính (để chạy Pop Animation)")]
        [SerializeField] private RectTransform _dialogRect;

        [Tooltip("CanvasGroup điều khiển độ trong suốt và chặn raycast")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Thời gian mở/đóng hiệu ứng (giây)")]
        [SerializeField] private float _animDuration = 0.22f;

        [Header("Header & Close Controls")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _backdropButton;

        [Header("Joystick Setting Row")]
        [SerializeField] private Button _joystickToggleBtn;
        [SerializeField] private Image _joystickToggleBg;
        [SerializeField] private RectTransform _joystickHandle;
        [SerializeField] private TextMeshProUGUI _joystickStatusText;

        [Header("Music Setting Row")]
        [SerializeField] private Button _musicToggleBtn;
        [SerializeField] private Image _musicToggleBg;
        [SerializeField] private RectTransform _musicHandle;
        [SerializeField] private TextMeshProUGUI _musicStatusText;

        [Header("SFX Setting Row")]
        [SerializeField] private Button _sfxToggleBtn;
        [SerializeField] private Image _sfxToggleBg;
        [SerializeField] private RectTransform _sfxHandle;
        [SerializeField] private TextMeshProUGUI _sfxStatusText;

        [Header("Reset Progress Row")]
        [SerializeField] private Button _resetProgressBtn;
        [SerializeField] private TextMeshProUGUI _resetProgressBtnText;
        [SerializeField] private Image _resetProgressBtnBg;

        [Header("Visual Colors")]
        [SerializeField] private Color _activeColor = new Color(0.18f, 0.85f, 0.35f, 1f);   // Xanh lá tươi khi BẬT
        [SerializeField] private Color _inactiveColor = new Color(0.25f, 0.28f, 0.35f, 1f); // Xám đậm khi TẮT
        [SerializeField] private Color _activeHandleColor = Color.white;
        [SerializeField] private Color _inactiveHandleColor = new Color(0.7f, 0.72f, 0.78f, 1f);

        private Coroutine _animCoroutine;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        public static SettingsDialogUI GetOrCreateInScene() {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<SettingsDialogUI>(FindObjectsInactive.Include);
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

            return SettingsUIBootstrap.CreateRuntimeSettingsDialog(canvas);
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            }

            if (_rootWrapper == null) {
                if (transform.parent != null && transform.parent.name.Contains("Wrapper")) {
                    _rootWrapper = transform.parent.gameObject;
                } else {
                    _rootWrapper = gameObject;
                }
            }

            if (_dialogRect == null) {
                _dialogRect = GetComponent<RectTransform>();
            }

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            AutoFindReferences();
            EnsureResetProgressButtonExists();
        }

        private void Start() {
            RegisterButtonEvents();
            RefreshAllSwitchesVisual(false);

            // Chỉ snap đóng nếu hiện tại chưa có lệnh mở (tránh đóng đè khi Open() được gọi trước)
            if (!_isOpen) {
                SnapToClosed();
            }
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        private void AutoFindReferences() {
            Transform searchRoot = _rootWrapper != null ? _rootWrapper.transform : transform.parent ?? transform;
            var btns = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (var b in btns) {
                string n = b.gameObject.name.ToLowerInvariant();
                if (_closeButton == null && (n.Contains("close") || n.Contains("btnclose") || n.Contains("exit"))) {
                    _closeButton = b;
                } else if (_backdropButton == null && (n.Contains("backdrop") || n.Contains("overlay") || n.Contains("dim"))) {
                    _backdropButton = b;
                } else if (_resetProgressBtn == null && (n.Contains("reset") || n.Contains("restart") || n.Contains("clear"))) {
                    _resetProgressBtn = b;
                    _resetProgressBtnBg = b.GetComponent<Image>();
                    _resetProgressBtnText = b.GetComponentInChildren<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// Đảm bảo hàng nút Đặt Lại Tiến Trình luôn tồn tại 100%, kể cả trên Scene cũ chưa kịp cập nhật prefab.
        /// </summary>
        public void EnsureResetProgressButtonExists() {
            if (_resetProgressBtn != null) return;

            AutoFindReferences();
            if (_resetProgressBtn != null) {
                _resetProgressBtn.onClick.RemoveListener(OnResetProgressClicked);
                _resetProgressBtn.onClick.AddListener(OnResetProgressClicked);
                return;
            }

            Transform container = transform.Find("RowsContainer");
            if (container == null) {
                if (_joystickToggleBtn != null && _joystickToggleBtn.transform.parent != null) {
                    var rowParent = _joystickToggleBtn.transform.parent;
                    if (rowParent.parent != null && rowParent.parent != transform) {
                        container = rowParent.parent;
                    }
                }
            }
            if (container == null) container = transform;

            int existingRowsCount = 0;
            for (int i = 0; i < container.childCount; i++) {
                var child = container.GetChild(i);
                if (child.name.StartsWith("Row") || child.name.StartsWith("SettingRow") || child.GetComponent<Button>() != null) {
                    existingRowsCount++;
                }
            }
            if (existingRowsCount == 0) existingRowsCount = 3;

            SettingsUIBootstrap.CreateResetRow(container, "Row_ResetProgress", existingRowsCount,
                out _resetProgressBtn, out _resetProgressBtnText, out _resetProgressBtnBg);

            if (_dialogRect != null && _dialogRect.sizeDelta.y < 580f) {
                _dialogRect.sizeDelta = new Vector2(_dialogRect.sizeDelta.x, 620f);
            }

            if (_resetProgressBtn != null) {
                _resetProgressBtn.onClick.RemoveListener(OnResetProgressClicked);
                _resetProgressBtn.onClick.AddListener(OnResetProgressClicked);
            }
        }

        private void RegisterButtonEvents() {
            if (_closeButton != null) {
                _closeButton.onClick.RemoveListener(Close);
                _closeButton.onClick.AddListener(Close);
            }

            if (_backdropButton != null) {
                _backdropButton.onClick.RemoveListener(Close);
                _backdropButton.onClick.AddListener(Close);
            }

            if (_joystickToggleBtn != null) {
                _joystickToggleBtn.onClick.RemoveListener(OnJoystickToggleClicked);
                _joystickToggleBtn.onClick.AddListener(OnJoystickToggleClicked);
            }

            if (_musicToggleBtn != null) {
                _musicToggleBtn.onClick.RemoveListener(OnMusicToggleClicked);
                _musicToggleBtn.onClick.AddListener(OnMusicToggleClicked);
            }

            if (_sfxToggleBtn != null) {
                _sfxToggleBtn.onClick.RemoveListener(OnSfxToggleClicked);
                _sfxToggleBtn.onClick.AddListener(OnSfxToggleClicked);
            }

            if (_resetProgressBtn != null) {
                _resetProgressBtn.onClick.RemoveListener(OnResetProgressClicked);
                _resetProgressBtn.onClick.AddListener(OnResetProgressClicked);
            }
        }

        private void OnJoystickToggleClicked() {
            PlayClickSfx();
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.IsJoystickEnabled = !SettingsManager.Instance.IsJoystickEnabled;
                UpdateSwitchVisual(_joystickToggleBg, _joystickHandle, _joystickStatusText, SettingsManager.Instance.IsJoystickEnabled, true);
            }
        }

        private void OnMusicToggleClicked() {
            PlayClickSfx();
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.IsMusicEnabled = !SettingsManager.Instance.IsMusicEnabled;
                UpdateSwitchVisual(_musicToggleBg, _musicHandle, _musicStatusText, SettingsManager.Instance.IsMusicEnabled, true);
            }
        }

        private void OnSfxToggleClicked() {
            PlayClickSfx();
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.IsSfxEnabled = !SettingsManager.Instance.IsSfxEnabled;
                UpdateSwitchVisual(_sfxToggleBg, _sfxHandle, _sfxStatusText, SettingsManager.Instance.IsSfxEnabled, true);
            }
        }

        private void PlayClickSfx() {
            if (AudioManager.Instance != null) {
                AudioManager.Instance.PlayButtonClick();
            }
        }

        private Coroutine _resetConfirmCoroutine;
        private bool _isAwaitingResetConfirmation = false;

        private void OnResetProgressClicked() {
            PlayClickSfx();
            if (!_isAwaitingResetConfirmation) {
                _isAwaitingResetConfirmation = true;
                if (_resetProgressBtnText != null) {
                    _resetProgressBtnText.text = "⚠️ BẤM LẦN NỮA ĐỂ XÁC NHẬN!";
                }
                if (_resetProgressBtnBg != null) {
                    _resetProgressBtnBg.color = new Color(0.88f, 0.22f, 0.22f, 1f); // Đỏ cảnh báo rực
                }
                if (_resetConfirmCoroutine != null) StopCoroutine(_resetConfirmCoroutine);
                _resetConfirmCoroutine = StartCoroutine(ResetConfirmTimeoutRoutine());
            } else {
                _isAwaitingResetConfirmation = false;
                if (_resetConfirmCoroutine != null) StopCoroutine(_resetConfirmCoroutine);
                RefreshResetButtonVisual();
                if (MineMart.Gameplay.Persistence.SaveManager.Instance != null) {
                    MineMart.Gameplay.Persistence.SaveManager.Instance.ResetAllProgressAndRestart();
                }
            }
        }

        private IEnumerator ResetConfirmTimeoutRoutine() {
            yield return new WaitForSecondsRealtime(4.5f);
            _isAwaitingResetConfirmation = false;
            RefreshResetButtonVisual();
        }

        private void RefreshResetButtonVisual() {
            if (_resetProgressBtnText != null) {
                _resetProgressBtnText.text = "🗑️ ĐẶT LẠI TIẾN TRÌNH";
            }
            if (_resetProgressBtnBg != null) {
                _resetProgressBtnBg.color = new Color(0.68f, 0.18f, 0.18f, 0.95f);
            }
        }

        /// <summary>
        /// Mở Hộp thoại Cài Đặt với animation Pop Scale + Fade In.
        /// </summary>
        public void Open() {
            if (_isOpen) return;
            _isOpen = true;

            // Đóng toàn bộ các bong bóng shop ô đất và trạm mỏ đang mở
            if (PlotStallShopBubble.Instance != null && PlotStallShopBubble.Instance.IsShowing) {
                PlotStallShopBubble.Instance.Hide();
            }
            if (StationUpgradeBubble.Instance != null && StationUpgradeBubble.Instance.IsShowing) {
                StationUpgradeBubble.Instance.Hide();
            }

            // Bật toàn bộ Wrapper và Backdrop
            if (_rootWrapper != null) _rootWrapper.SetActive(true);
            gameObject.SetActive(true);
            if (_backdropButton != null) {
                _backdropButton.gameObject.SetActive(true);
                var img = _backdropButton.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
            }

            PlayClickSfx();
            EnsureResetProgressButtonExists();
            RefreshResetButtonVisual();
            RefreshAllSwitchesVisual(false);

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateDialogRoutine(true));
        }

        /// <summary>
        /// Đóng Hộp thoại Cài Đặt.
        /// </summary>
        public void Close() {
            if (!_isOpen) return;
            _isOpen = false;

            PlayClickSfx();

            if (_isAwaitingResetConfirmation) {
                _isAwaitingResetConfirmation = false;
                if (_resetConfirmCoroutine != null) StopCoroutine(_resetConfirmCoroutine);
                RefreshResetButtonVisual();
            }

            if (_backdropButton != null) {
                var img = _backdropButton.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
            }

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateDialogRoutine(false));
        }

        public void Toggle() {
            if (_isOpen) Close();
            else Open();
        }

        private void RefreshAllSwitchesVisual(bool animate) {
            bool joy = SettingsManager.Instance == null || SettingsManager.Instance.IsJoystickEnabled;
            bool music = SettingsManager.Instance == null || SettingsManager.Instance.IsMusicEnabled;
            bool sfx = SettingsManager.Instance == null || SettingsManager.Instance.IsSfxEnabled;

            UpdateSwitchVisual(_joystickToggleBg, _joystickHandle, _joystickStatusText, joy, animate);
            UpdateSwitchVisual(_musicToggleBg, _musicHandle, _musicStatusText, music, animate);
            UpdateSwitchVisual(_sfxToggleBg, _sfxHandle, _sfxStatusText, sfx, animate);
        }

        private void UpdateSwitchVisual(Image bg, RectTransform handle, TextMeshProUGUI statusText, bool isOn, bool animate) {
            if (bg != null) {
                bg.color = isOn ? _activeColor : _inactiveColor;
            }

            if (statusText != null) {
                statusText.text = isOn ? "BẬT" : "TẮT";
                statusText.color = isOn ? Color.white : new Color(0.75f, 0.78f, 0.85f);
            }

            if (handle != null) {
                // Vị trí cần gạt: Trái = Tắt (-18px), Phải = Bật (+18px)
                float targetX = isOn ? 18f : -18f;
                if (!animate) {
                    handle.anchoredPosition = new Vector2(targetX, handle.anchoredPosition.y);
                } else {
                    StartCoroutine(SlideHandleRoutine(handle, targetX));
                }

                var handleImg = handle.GetComponent<Image>();
                if (handleImg != null) {
                    handleImg.color = isOn ? _activeHandleColor : _inactiveHandleColor;
                }
            }
        }

        private IEnumerator SlideHandleRoutine(RectTransform handle, float targetX) {
            if (handle == null) yield break;
            Vector2 start = handle.anchoredPosition;
            Vector2 end = new Vector2(targetX, start.y);
            float elapsed = 0f;
            float dur = 0.14f;

            while (elapsed < dur) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                handle.anchoredPosition = Vector2.Lerp(start, end, ease);
                yield return null;
            }
            handle.anchoredPosition = end;
        }

        private IEnumerator AnimateDialogRoutine(bool opening) {
            float elapsed = 0f;
            float dur = _animDuration;

            Vector3 startScale = _dialogRect != null ? _dialogRect.localScale : Vector3.one;
            Vector3 endScale = opening ? Vector3.one : Vector3.one * 0.75f;

            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 0f;
            float endAlpha = opening ? 1f : 0f;

            if (_canvasGroup != null) {
                _canvasGroup.blocksRaycasts = opening;
                _canvasGroup.interactable = opening;
            }

            while (elapsed < dur) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);

                // Đường cong EaseOutBack cho pop scale tự nhiên
                float popT = opening ? EaseOutBack(t) : EaseInQuad(t);

                if (_dialogRect != null) {
                    _dialogRect.localScale = Vector3.LerpUnclamped(startScale, endScale, popT);
                }

                if (_canvasGroup != null) {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                }

                yield return null;
            }

            if (_dialogRect != null) _dialogRect.localScale = endScale;
            if (_canvasGroup != null) _canvasGroup.alpha = endAlpha;

            if (!opening) {
                // Tắt hoàn toàn để không cản trở thao tác vuốt màn hình
                if (_backdropButton != null) _backdropButton.gameObject.SetActive(false);
                if (_rootWrapper != null) _rootWrapper.SetActive(false);
                else gameObject.SetActive(false);
            }
        }

        private float EaseOutBack(float x) {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        private float EaseInQuad(float x) {
            return x * x;
        }

        public void SnapToClosed() {
            _isOpen = false;
            if (_dialogRect != null) _dialogRect.localScale = Vector3.one * 0.75f;
            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            if (_backdropButton != null) {
                var img = _backdropButton.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
                _backdropButton.gameObject.SetActive(false);
            }
            if (_rootWrapper != null) {
                _rootWrapper.SetActive(false);
            } else {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Helper tự động dựng giao diện Cài Đặt Game bằng code C# thuần túy nếu chưa tạo prefab trong Scene.
    /// </summary>
    public static class SettingsUIBootstrap {
        public static SettingsDialogUI CreateRuntimeSettingsDialog(Canvas canvas) {
            if (canvas == null) return null;

            // 1. Root Wrapper
            GameObject rootWrapper = new GameObject("SettingsDialog_Wrapper", typeof(RectTransform));
            rootWrapper.transform.SetParent(canvas.transform, false);

            var wrapperRect = rootWrapper.GetComponent<RectTransform>();
            wrapperRect.anchorMin = Vector2.zero;
            wrapperRect.anchorMax = Vector2.one;
            wrapperRect.sizeDelta = Vector2.zero;
            wrapperRect.anchoredPosition = Vector2.zero;

            // 2. Backdrop Dim Button
            GameObject backdropGo = new GameObject("BackdropButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(rootWrapper.transform, false);
            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.sizeDelta = Vector2.zero;
            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
            backdropImg.raycastTarget = false; // Mặc định tắt raycastTarget
            var backdropBtn = backdropGo.GetComponent<Button>();

            // 3. Dialog Box chính
            GameObject dialogGo = new GameObject("SettingsDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(SettingsDialogUI));
            dialogGo.transform.SetParent(rootWrapper.transform, false);

            var dialogRect = dialogGo.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(440f, 550f);
            dialogRect.anchoredPosition = Vector2.zero;

            var dialogImg = dialogGo.GetComponent<Image>();
            dialogImg.color = new Color(0.11f, 0.13f, 0.18f, 0.98f); // Màu xanh đêm sang trọng

            var canvasGroup = dialogGo.GetComponent<CanvasGroup>();
            var dialogScript = dialogGo.GetComponent<SettingsDialogUI>();

            // 4. Header Tiêu đề "CÀI ĐẶT"
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(dialogGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            titleRect.sizeDelta = new Vector2(-80f, 40f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "CÀI ĐẶT TRÒ CHƠI";
            titleTmp.fontSize = 22f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // 5. Nút Đóng "✕"
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(dialogGo.transform, false);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16f, -16f);
            closeRect.sizeDelta = new Vector2(36f, 36f);

            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.25f, 0.28f, 0.38f, 0.9f);
            var closeBtn = closeGo.GetComponent<Button>();

            GameObject closeTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGo.transform.SetParent(closeGo.transform, false);
            var closeTextRect = closeTextGo.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            var closeTmp = closeTextGo.GetComponent<TextMeshProUGUI>();
            closeTmp.text = "✕";
            closeTmp.fontSize = 20f;
            closeTmp.fontStyle = FontStyles.Bold;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // 6. Container chứa các hàng Cài đặt
            GameObject rowsContainer = new GameObject("RowsContainer", typeof(RectTransform));
            rowsContainer.transform.SetParent(dialogGo.transform, false);
            var rowsRect = rowsContainer.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0f, 0f);
            rowsRect.anchorMax = new Vector2(1f, 1f);
            rowsRect.anchoredPosition = new Vector2(0f, -30f);
            rowsRect.sizeDelta = new Vector2(-40f, -120f);

            // Row 1: Joystick
            CreateSettingRow(rowsContainer.transform, "SettingRow_Joystick", "🕹️ Cần Điều Khiển (Joystick)", 0,
                out Button joyBtn, out Image joyBg, out RectTransform joyHandle, out TextMeshProUGUI joyText);

            // Row 2: Music
            CreateSettingRow(rowsContainer.transform, "Row_Music", "🎵 Âm Nhạc Nền (Music)", 1,
                out Button musBtn, out Image musBg, out RectTransform musHandle, out TextMeshProUGUI musText);

            // Row 3: SFX
            CreateSettingRow(rowsContainer.transform, "Row_SFX", "🔊 Hiệu Ứng Âm Thanh (SFX)", 2,
                out Button sfxBtn, out Image sfxBg, out RectTransform sfxHandle, out TextMeshProUGUI sfxText);

            // Row 4: Reset Progress
            CreateResetRow(rowsContainer.transform, "Row_ResetProgress", 3,
                out Button resetBtn, out TextMeshProUGUI resetText, out Image resetBg);

            // Inject các trường qua Reflection để liên kết hoàn chỉnh
            SetField(dialogScript, "_rootWrapper", rootWrapper);
            SetField(dialogScript, "_dialogRect", dialogRect);
            SetField(dialogScript, "_canvasGroup", canvasGroup);
            SetField(dialogScript, "_titleText", titleTmp);
            SetField(dialogScript, "_closeButton", closeBtn);
            SetField(dialogScript, "_backdropButton", backdropBtn);

            SetField(dialogScript, "_joystickToggleBtn", joyBtn);
            SetField(dialogScript, "_joystickToggleBg", joyBg);
            SetField(dialogScript, "_joystickHandle", joyHandle);
            SetField(dialogScript, "_joystickStatusText", joyText);

            SetField(dialogScript, "_musicToggleBtn", musBtn);
            SetField(dialogScript, "_musicToggleBg", musBg);
            SetField(dialogScript, "_musicHandle", musHandle);
            SetField(dialogScript, "_musicStatusText", musText);

            SetField(dialogScript, "_sfxToggleBtn", sfxBtn);
            SetField(dialogScript, "_sfxToggleBg", sfxBg);
            SetField(dialogScript, "_sfxHandle", sfxHandle);
            SetField(dialogScript, "_sfxStatusText", sfxText);

            SetField(dialogScript, "_resetProgressBtn", resetBtn);
            SetField(dialogScript, "_resetProgressBtnText", resetText);
            SetField(dialogScript, "_resetProgressBtnBg", resetBg);

            // Đóng ngay khi vừa sinh để không chặn vuốt màn hình
            dialogScript.SnapToClosed();

            return dialogScript;
        }

        private static void CreateSettingRow(Transform parent, string goName, string label, int index,
            out Button toggleBtn, out Image toggleBg, out RectTransform handle, out TextMeshProUGUI statusText) {
            
            float rowHeight = 72f;
            float startY = -index * (rowHeight + 14f);

            GameObject rowGo = new GameObject(goName, typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, startY);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            var rowImg = rowGo.GetComponent<Image>();
            rowImg.color = new Color(0.16f, 0.18f, 0.25f, 0.85f); // Khung từng mục bo góc tối

            // Label bên trái
            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.68f, 1f);
            labelRect.anchoredPosition = new Vector2(18f, 0f);
            labelRect.sizeDelta = Vector2.zero;

            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 17f;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.92f, 0.94f, 0.98f);

            // Nút Switch Toggle bên phải
            GameObject switchGo = new GameObject("SwitchToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            switchGo.transform.SetParent(rowGo.transform, false);
            var switchRect = switchGo.GetComponent<RectTransform>();
            switchRect.anchorMin = new Vector2(1f, 0.5f);
            switchRect.anchorMax = new Vector2(1f, 0.5f);
            switchRect.pivot = new Vector2(1f, 0.5f);
            switchRect.anchoredPosition = new Vector2(-16f, 0f);
            switchRect.sizeDelta = new Vector2(76f, 36f);

            toggleBg = switchGo.GetComponent<Image>();
            toggleBg.color = new Color(0.18f, 0.85f, 0.35f, 1f);
            toggleBtn = switchGo.GetComponent<Button>();

            // Nút Handle tròn di chuyển
            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(switchGo.transform, false);
            handle = handleGo.GetComponent<RectTransform>();
            handle.anchorMin = new Vector2(0.5f, 0.5f);
            handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(28f, 28f);
            handle.anchoredPosition = new Vector2(18f, 0f); // vị trí BẬT mặc định

            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = Color.white;

            // Status Text (BẬT / TẮT)
            GameObject statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(rowGo.transform, false);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(1f, 0.5f);
            statusRect.anchorMax = new Vector2(1f, 0.5f);
            statusRect.pivot = new Vector2(1f, 0.5f);
            statusRect.anchoredPosition = new Vector2(-98f, 0f);
            statusRect.sizeDelta = new Vector2(50f, 30f);

            statusText = statusGo.GetComponent<TextMeshProUGUI>();
            statusText.text = "BẬT";
            statusText.fontSize = 14f;
            statusText.fontStyle = FontStyles.Bold;
            statusText.alignment = TextAlignmentOptions.MidlineRight;
            statusText.color = Color.white;
        }

        public static void CreateResetRow(Transform parent, string goName, int index,
            out Button resetBtn, out TextMeshProUGUI resetText, out Image resetBg) {
            
            float rowHeight = 62f;
            float startY = -index * (72f + 14f);

            GameObject rowGo = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, startY);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            var layoutElem = rowGo.GetComponent<LayoutElement>();
            layoutElem.minHeight = rowHeight;
            layoutElem.preferredHeight = rowHeight;
            layoutElem.flexibleWidth = 1f;

            resetBg = rowGo.GetComponent<Image>();
            resetBg.color = new Color(0.68f, 0.18f, 0.18f, 0.95f); // Đỏ sang trọng
            resetBg.raycastTarget = true;
            resetBtn = rowGo.GetComponent<Button>();

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(rowGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            resetText = textGo.GetComponent<TextMeshProUGUI>();
            resetText.text = "🗑️ ĐẶT LẠI TIẾN TRÌNH";
            resetText.fontSize = 16f;
            resetText.fontStyle = FontStyles.Bold;
            resetText.alignment = TextAlignmentOptions.Center;
            resetText.color = Color.white;
            resetText.raycastTarget = false;
        }

        private static void SetField(object target, string fieldName, object value) {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) {
                field.SetValue(target, value);
            }
        }
    }
}
