using System;
using System.Collections;
using MineMart.Core.Audio;
using MineMart.Economy;
using MineMart.Gameplay.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Bảng Thông Báo Chào Mừng Quay Lại & Nhận Tiền Thu Hoạch Vắng Mặt (AFK Welcome-Back Popup).
    /// Hiển thị khi người chơi mở lại game sau một khoảng thời gian offline.
    /// Tính toán theo công thức chuẩn:
    /// AFK Cash = (Trung bình cộng giá các stall đã mở bán) x (Thời gian AFK / Trung bình cộng thời gian đào)
    /// </summary>
    public class AfkWelcomeBackPopupUI : MonoBehaviour {
        public static AfkWelcomeBackPopupUI Instance { get; private set; }

        [Header("Root & Containers")]
        [Tooltip("Root Wrapper bao trùm toàn màn hình (chứa Backdrop và Dialog chính)")]
        [SerializeField] private GameObject _rootWrapper;

        [Tooltip("Khung Dialog chính để chạy hiệu ứng Pop animation")]
        [SerializeField] private RectTransform _dialogRect;

        [Tooltip("CanvasGroup điều khiển độ mờ và chặn raycast")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Text Displays")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _timeOfflineText;
        [SerializeField] private TextMeshProUGUI _cashEarnedText;

        [Header("Buttons")]
        [SerializeField] private Button _claimNormalBtn;
        [SerializeField] private TextMeshProUGUI _claimNormalBtnText;
        [SerializeField] private Button _claimDoubleBtn;
        [SerializeField] private TextMeshProUGUI _claimDoubleBtnText;
        [SerializeField] private Button _backdropBtn;

        [Header("Animation")]
        [SerializeField] private float _animDuration = 0.25f;

        private double _baseEarnings = 0;
        private Action<double> _onClaimCallback;
        private Coroutine _animCoroutine;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        public static AfkWelcomeBackPopupUI GetOrCreateInScene() {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<AfkWelcomeBackPopupUI>(FindObjectsInactive.Include);
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

            return AfkWelcomeBackUIBootstrap.CreateRuntimeAfkPopup(canvas);
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            } else if (Instance != this) {
                Destroy(gameObject);
                return;
            }

            if (_rootWrapper == null) {
                _rootWrapper = transform.parent != null && transform.parent.name.Contains("Wrapper") 
                    ? transform.parent.gameObject 
                    : gameObject;
            }

            if (_dialogRect == null) {
                _dialogRect = GetComponent<RectTransform>();
            }

            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            AutoFindReferences();
        }

        private void OnEnable() {
            if (SaveManager.Instance != null) {
                SaveManager.Instance.OnAfkEarningsGranted -= HandleAfkEarnings;
                SaveManager.Instance.OnAfkEarningsGranted += HandleAfkEarnings;
                if (SaveManager.Instance.HasPendingAfkEarnings && !_isOpen) {
                    HandleAfkEarnings(SaveManager.Instance.PendingAfkEarnings, SaveManager.Instance.PendingAfkSeconds);
                }
            }
        }

        private void OnDisable() {
            if (SaveManager.Instance != null) {
                SaveManager.Instance.OnAfkEarningsGranted -= HandleAfkEarnings;
            }
        }

        private void HandleAfkEarnings(double amount, float seconds) {
            if (amount <= 0 || _isOpen) return;
            Show(amount, seconds, (claimedAmount) => {
                SaveManager.Instance?.ClearPendingAfkEarnings();
            });
        }

        private void Start() {
            RegisterButtonEvents();
            if (SaveManager.Instance != null) {
                SaveManager.Instance.OnAfkEarningsGranted -= HandleAfkEarnings;
                SaveManager.Instance.OnAfkEarningsGranted += HandleAfkEarnings;
                if (SaveManager.Instance.HasPendingAfkEarnings && !_isOpen) {
                    HandleAfkEarnings(SaveManager.Instance.PendingAfkEarnings, SaveManager.Instance.PendingAfkSeconds);
                }
            }
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
            if (_claimNormalBtn == null || _claimDoubleBtn == null) {
                var btns = GetComponentsInChildren<Button>(true);
                foreach (var b in btns) {
                    string n = b.gameObject.name.ToLowerInvariant();
                    if (_claimNormalBtn == null && (n.Contains("claim") && !n.Contains("double") && !n.Contains("2x") || n.Contains("normal"))) {
                        _claimNormalBtn = b;
                    } else if (_claimDoubleBtn == null && (n.Contains("double") || n.Contains("2x") || n.Contains("ads"))) {
                        _claimDoubleBtn = b;
                    }
                }
            }
        }

        private void RegisterButtonEvents() {
            if (_claimNormalBtn != null) {
                _claimNormalBtn.onClick.RemoveListener(OnNormalClaimClicked);
                _claimNormalBtn.onClick.AddListener(OnNormalClaimClicked);
            }

            if (_claimDoubleBtn != null) {
                _claimDoubleBtn.onClick.RemoveListener(OnDoubleClaimClicked);
                _claimDoubleBtn.onClick.AddListener(OnDoubleClaimClicked);
            }

            if (_backdropBtn != null) {
                _backdropBtn.onClick.RemoveListener(OnNormalClaimClicked);
                _backdropBtn.onClick.AddListener(OnNormalClaimClicked);
            }
        }

        /// <summary>
        /// Hiển thị popup chào mừng quay lại với số tiền và thời gian đã vắng mặt.
        /// </summary>
        public void Show(double baseGold, float offlineSeconds, Action<double> onClaimed = null) {
            _baseEarnings = Math.Max(0, baseGold);
            _onClaimCallback = onClaimed;
            _isOpen = true;

            // Định dạng thời gian vắng mặt thân thiện
            string timeString = FormatOfflineDuration(offlineSeconds);

            if (_timeOfflineText != null) {
                _timeOfflineText.text = $"Trong lúc bạn vắng mặt <color=#FFE066>{timeString}</color>, các mỏ khoáng sản đã tích cực làm việc:";
            }

            if (_cashEarnedText != null) {
                _cashEarnedText.text = $"+{_baseEarnings:N0} <size=70%>VÀNG</size>";
            }

            if (_claimNormalBtnText != null) {
                _claimNormalBtnText.text = $"NHẬN ({_baseEarnings:N0})";
            }

            if (_claimDoubleBtnText != null) {
                _claimDoubleBtnText.text = $"X2 VỚI ADS ({_baseEarnings * 2:N0})";
            }

            if (_rootWrapper != null) _rootWrapper.SetActive(true);
            gameObject.SetActive(true);

            if (_backdropBtn != null) {
                _backdropBtn.gameObject.SetActive(true);
                var img = _backdropBtn.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
            }

            if (AudioManager.Instance != null) {
                AudioManager.Instance.PlayButtonClick();
            }

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateDialogRoutine(true));
        }

        private string FormatOfflineDuration(float seconds) {
            TimeSpan span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1) {
                return $"{(int)span.TotalHours} giờ {span.Minutes} phút";
            } else if (span.TotalMinutes >= 1) {
                return $"{span.Minutes} phút {span.Seconds} giây";
            } else {
                return $"{(int)span.TotalSeconds} giây";
            }
        }

        private void OnNormalClaimClicked() {
            ClaimAndClose(_baseEarnings);
        }

        private void OnDoubleClaimClicked() {
            // Thưởng x2 tiền AFK khi xem quảng cáo
            ClaimAndClose(_baseEarnings * 2);
        }

        private void ClaimAndClose(double finalAmount) {
            if (!_isOpen) return;
            _isOpen = false;

            if (AudioManager.Instance != null) {
                AudioManager.Instance.PlayButtonClick();
            }

            if (EconomyManager.Instance != null && finalAmount > 0) {
                EconomyManager.Instance.AddGold(finalAmount);
            }

            _onClaimCallback?.Invoke(finalAmount);

            if (_backdropBtn != null) {
                var img = _backdropBtn.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
            }

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateDialogRoutine(false));
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
                if (_backdropBtn != null) _backdropBtn.gameObject.SetActive(false);
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
            if (_backdropBtn != null) {
                var img = _backdropBtn.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
                _backdropBtn.gameObject.SetActive(false);
            }
            if (_rootWrapper != null) {
                _rootWrapper.SetActive(false);
            } else {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Helper tự động tạo giao diện AFK Welcome Back Popup thuần C# nếu Scene chưa tạo prefab.
    /// </summary>
    public static class AfkWelcomeBackUIBootstrap {
        public static AfkWelcomeBackPopupUI CreateRuntimeAfkPopup(Canvas canvas) {
            if (canvas == null) return null;

            // 1. Root Wrapper
            GameObject rootWrapper = new GameObject("AfkWelcomeBackPopup_Wrapper", typeof(RectTransform));
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
            backdropImg.color = new Color(0f, 0f, 0f, 0.65f);
            backdropImg.raycastTarget = false;
            var backdropBtn = backdropGo.GetComponent<Button>();

            // 3. Dialog Box
            GameObject dialogGo = new GameObject("AfkWelcomeBackDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(AfkWelcomeBackPopupUI));
            dialogGo.transform.SetParent(rootWrapper.transform, false);

            var dialogRect = dialogGo.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(460f, 430f);
            dialogRect.anchoredPosition = Vector2.zero;

            var dialogImg = dialogGo.GetComponent<Image>();
            dialogImg.color = new Color(0.11f, 0.13f, 0.18f, 0.98f); // Nền tối đêm cao cấp

            var canvasGroup = dialogGo.GetComponent<CanvasGroup>();
            var script = dialogGo.GetComponent<AfkWelcomeBackPopupUI>();

            // 4. Header Icon "💰"
            GameObject iconGo = new GameObject("HeaderIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconGo.transform.SetParent(dialogGo.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -20f);
            iconRect.sizeDelta = new Vector2(100f, 60f);

            var iconTmp = iconGo.GetComponent<TextMeshProUGUI>();
            iconTmp.text = "💰";
            iconTmp.fontSize = 48f;
            iconTmp.alignment = TextAlignmentOptions.Center;

            // 5. Title Text "CHÀO MỪNG TRỞ LẠI!"
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(dialogGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(-40f, 36f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "CHÀO MỪNG TRỞ LẠI!";
            titleTmp.fontSize = 24f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(1f, 0.85f, 0.35f, 1f); // Màu vàng gold lấp lánh

            // 6. Subtext vắng mặt
            GameObject timeGo = new GameObject("TimeOfflineText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeGo.transform.SetParent(dialogGo.transform, false);
            var timeRect = timeGo.GetComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(0.06f, 1f);
            timeRect.anchorMax = new Vector2(0.94f, 1f);
            timeRect.pivot = new Vector2(0.5f, 1f);
            timeRect.anchoredPosition = new Vector2(0f, -120f);
            timeRect.sizeDelta = new Vector2(0f, 50f);

            var timeTmp = timeGo.GetComponent<TextMeshProUGUI>();
            timeTmp.text = "Trong lúc bạn vắng mặt, các mỏ khoáng sản đã tích cực làm việc:";
            timeTmp.fontSize = 15f;
            timeTmp.alignment = TextAlignmentOptions.Center;
            timeTmp.color = new Color(0.85f, 0.88f, 0.95f, 1f);

            // 7. Hộp hiển thị số tiền thu được (Cash Display Box)
            GameObject cashBoxGo = new GameObject("CashBox", typeof(RectTransform), typeof(Image));
            cashBoxGo.transform.SetParent(dialogGo.transform, false);
            var cashBoxRect = cashBoxGo.GetComponent<RectTransform>();
            cashBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            cashBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            cashBoxRect.pivot = new Vector2(0.5f, 0.5f);
            cashBoxRect.anchoredPosition = new Vector2(0f, 5f);
            cashBoxRect.sizeDelta = new Vector2(380f, 75f);

            var cashBoxImg = cashBoxGo.GetComponent<Image>();
            cashBoxImg.color = new Color(0.16f, 0.20f, 0.28f, 0.9f);

            GameObject cashTextGo = new GameObject("CashText", typeof(RectTransform), typeof(TextMeshProUGUI));
            cashTextGo.transform.SetParent(cashBoxGo.transform, false);
            var cashTextRect = cashTextGo.GetComponent<RectTransform>();
            cashTextRect.anchorMin = Vector2.zero;
            cashTextRect.anchorMax = Vector2.one;
            cashTextRect.sizeDelta = Vector2.zero;

            var cashTmp = cashTextGo.GetComponent<TextMeshProUGUI>();
            cashTmp.text = "+0 VÀNG";
            cashTmp.fontSize = 32f;
            cashTmp.fontStyle = FontStyles.Bold;
            cashTmp.alignment = TextAlignmentOptions.Center;
            cashTmp.color = new Color(0.98f, 0.85f, 0.25f, 1f); // Vàng sáng rực rỡ

            // 8. Nút Nhận Thường
            GameObject normalBtnGo = new GameObject("NormalClaimBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            normalBtnGo.transform.SetParent(dialogGo.transform, false);
            var normalRect = normalBtnGo.GetComponent<RectTransform>();
            normalRect.anchorMin = new Vector2(0.08f, 0f);
            normalRect.anchorMax = new Vector2(0.48f, 0f);
            normalRect.pivot = new Vector2(0.5f, 0f);
            normalRect.anchoredPosition = new Vector2(0f, 22f);
            normalRect.sizeDelta = new Vector2(0f, 56f);

            var normalImg = normalBtnGo.GetComponent<Image>();
            normalImg.color = new Color(0.20f, 0.55f, 0.85f, 1f); // Xanh dương tươi sáng
            var normalBtn = normalBtnGo.GetComponent<Button>();

            GameObject normalTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            normalTextGo.transform.SetParent(normalBtnGo.transform, false);
            var normalTextRect = normalTextGo.GetComponent<RectTransform>();
            normalTextRect.anchorMin = Vector2.zero;
            normalTextRect.anchorMax = Vector2.one;
            normalTextRect.sizeDelta = Vector2.zero;

            var normalTmp = normalTextGo.GetComponent<TextMeshProUGUI>();
            normalTmp.text = "NHẬN";
            normalTmp.fontSize = 17f;
            normalTmp.fontStyle = FontStyles.Bold;
            normalTmp.alignment = TextAlignmentOptions.Center;
            normalTmp.color = Color.white;

            // 9. Nút Nhận x2 Ads
            GameObject doubleBtnGo = new GameObject("DoubleClaimBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            doubleBtnGo.transform.SetParent(dialogGo.transform, false);
            var doubleRect = doubleBtnGo.GetComponent<RectTransform>();
            doubleRect.anchorMin = new Vector2(0.52f, 0f);
            doubleRect.anchorMax = new Vector2(0.92f, 0f);
            doubleRect.pivot = new Vector2(0.5f, 0f);
            doubleRect.anchoredPosition = new Vector2(0f, 22f);
            doubleRect.sizeDelta = new Vector2(0f, 56f);

            var doubleImg = doubleBtnGo.GetComponent<Image>();
            doubleImg.color = new Color(0.18f, 0.80f, 0.40f, 1f); // Xanh lá ngọc lục bảo x2
            var doubleBtn = doubleBtnGo.GetComponent<Button>();

            GameObject doubleTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            doubleTextGo.transform.SetParent(doubleBtnGo.transform, false);
            var doubleTextRect = doubleTextGo.GetComponent<RectTransform>();
            doubleTextRect.anchorMin = Vector2.zero;
            doubleTextRect.anchorMax = Vector2.one;
            doubleTextRect.sizeDelta = Vector2.zero;

            var doubleTmp = doubleTextGo.GetComponent<TextMeshProUGUI>();
            doubleTmp.text = "NHẬN X2";
            doubleTmp.fontSize = 17f;
            doubleTmp.fontStyle = FontStyles.Bold;
            doubleTmp.alignment = TextAlignmentOptions.Center;
            doubleTmp.color = Color.white;

            // Inject fields
            SetField(script, "_rootWrapper", rootWrapper);
            SetField(script, "_dialogRect", dialogRect);
            SetField(script, "_canvasGroup", canvasGroup);
            SetField(script, "_titleText", titleTmp);
            SetField(script, "_timeOfflineText", timeTmp);
            SetField(script, "_cashEarnedText", cashTmp);
            SetField(script, "_claimNormalBtn", normalBtn);
            SetField(script, "_claimNormalBtnText", normalTmp);
            SetField(script, "_claimDoubleBtn", doubleBtn);
            SetField(script, "_claimDoubleBtnText", doubleTmp);
            SetField(script, "_backdropBtn", backdropBtn);

            script.SnapToClosed();
            return script;
        }

        private static void SetField(object target, string fieldName, object value) {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) {
                field.SetValue(target, value);
            }
        }
    }
}
