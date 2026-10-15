using System;
using MineMart.Core.Events;
using MineMart.Core.Math;
using MineMart.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Điều phối chính cho Canvas UI/HUB khớp 100% với cây Hierarchy thực tế:
    /// - Top_Bar: SettingBtn, ShopBtn, Coin_Bar/CoinText, Emerald_Bar/Emerald_Text
    /// - Bottom_Bar: NextMapBtn, EUGTbtn, UGMbtn, InvBtn, ADS
    /// Tự động tìm và gán reference, cập nhật tiền thời gian thực và tự gỡ RaycastTarget gây chặn click.
    /// </summary>
    public class MainCanvasHud : MonoBehaviour {
        public static MainCanvasHud Instance { get; private set; }

        [Header("Text Displays (TextMeshPro)")]
        [Tooltip("Text hiển thị tiền Vàng (CoinText)")]
        [SerializeField] private TextMeshProUGUI _coinText;

        [Tooltip("Text hiển thị Emerald (Emerald_Text)")]
        [SerializeField] private TextMeshProUGUI _emeraldText;

        [Header("Top Bar Buttons")]
        [SerializeField] private Button _settingBtn;
        [SerializeField] private Button _shopBtn;

        [Header("Bottom Bar Buttons")]
        [SerializeField] private Button _nextMapBtn;
        [SerializeField] private Button _eugtBtn;    // Emerald Upgrade Tree
        [SerializeField] private Button _ugmBtn;     // Upgrade Map
        [SerializeField] private Button _invBtn;     // Inventory
        [SerializeField] private Button _adsBtn;     // Watch Ads

        [Header("Event Channels")]
        [SerializeField] private GoldChangedEventChannelSO _goldChangedChannel;
        [SerializeField] private EmeraldChangedEventChannelSO _emeraldChangedChannel;

        [Header("Testing")]
        [Tooltip("Bật phím tắt Space / G trong Editor để test cộng 100 Vàng")]
        [SerializeField] private bool _enableDebugKeys = true;

        // Public Events cho các module khác lắng nghe
        public event Action OnSettingClicked;
        public event Action OnShopClicked;
        public event Action OnNextMapClicked;
        public event Action OnEUGTClicked;
        public event Action OnUGMClicked;
        public event Action OnInvClicked;
        public event Action OnAdsClicked;

        private double _lastGold = -1;
        private int _lastEmerald = -1;
        private Transform _coinBarTransform;
        private Transform _emeraldBarTransform;

        private void Reset() {
            AutoFindComponents();
            AutoFindEventChannel();
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
            }

            AutoFindComponents();
            AutoFindEventChannel();
            FixContainerRaycasts();
        }

        /// <summary>
        /// Tự động tìm chính xác các GameObject con theo tên trong ảnh Hierarchy của em
        /// </summary>
        public void AutoFindComponents() {
            // Tìm Text
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts) {
                if (_coinText == null && t.gameObject.name == "CoinText") {
                    _coinText = t;
                    _coinBarTransform = t.transform.parent; // Coin_Bar
                } else if (_emeraldText == null && t.gameObject.name == "Emerald_Text") {
                    _emeraldText = t;
                    _emeraldBarTransform = t.transform.parent; // Emerald_Bar
                }
            }

            // Tìm Buttons
            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons) {
                switch (b.gameObject.name) {
                    case "SettingBtn":
                        if (_settingBtn == null) _settingBtn = b;
                        break;
                    case "ShopBtn":
                        if (_shopBtn == null) _shopBtn = b;
                        break;
                    case "NextMapBtn":
                        if (_nextMapBtn == null) _nextMapBtn = b;
                        break;
                    case "EUGTbtn":
                        if (_eugtBtn == null) _eugtBtn = b;
                        break;
                    case "UGMbtn":
                        if (_ugmBtn == null) _ugmBtn = b;
                        break;
                    case "InvBtn":
                        if (_invBtn == null) _invBtn = b;
                        break;
                    case "ADS":
                        if (_adsBtn == null) _adsBtn = b;
                        break;
                }
            }
        }

        private void AutoFindEventChannel() {
#if UNITY_EDITOR
            if (_goldChangedChannel == null) {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GoldChangedEventChannelSO Event_GoldChanged");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _goldChangedChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<GoldChangedEventChannelSO>(path);
                }
            }

            if (_emeraldChangedChannel == null) {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EmeraldChangedEventChannelSO Event_EmeraldChanged");
                if (guids != null && guids.Length > 0) {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _emeraldChangedChannel = UnityEditor.AssetDatabase.LoadAssetAtPath<EmeraldChangedEventChannelSO>(path);
                }
            }
#endif
        }

        /// <summary>
        /// Tự động gỡ RaycastTarget trên các Panel nền rỗng để không bị cản click xuống sàn 3D
        /// </summary>
        private void FixContainerRaycasts() {
            string[] containerNames = { "UI/HUB", "MainGamePanel", "Top_Bar", "Bottom_Bar", "Coin_Bar", "Emerald_Bar" };
            foreach (var img in GetComponentsInChildren<Image>(true)) {
                foreach (var cName in containerNames) {
                    if (img.gameObject.name == cName) {
                        img.raycastTarget = false;
                        break;
                    }
                }
            }
        }

        private void OnEnable() {
            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged += HandleGoldChanged;
            }
            if (_emeraldChangedChannel != null) {
                _emeraldChangedChannel.OnEmeraldChanged += HandleEmeraldChanged;
            }

            RegisterButtons();
            ForceSyncGold();
        }

        private void Start() {
            ForceSyncGold();
            InitRuntimeControls();
        }

        private void InitRuntimeControls() {
            VirtualJoystick.GetOrCreateInScene();
            SettingsDialogUI.GetOrCreateInScene();
            EmeraldUpgradeTreePanelUI.GetOrCreateInScene();
            AfkWelcomeBackPopupUI.GetOrCreateInScene();
        }

        private void Update() {
            // Đọc trực tiếp EconomyManager (Soft Currency) & HardCurrencyManager (Hard Currency) mỗi frame
            if (EconomyManager.Instance != null) {
                double current = EconomyManager.Instance.CurrentGold;
                if (Math.Abs(current - _lastGold) > 0.001) {
                    HandleGoldChanged(current);
                }
            }

            if (HardCurrencyManager.Instance != null) {
                int currentEmerald = HardCurrencyManager.Instance.CurrentEmerald;
                if (currentEmerald != _lastEmerald) {
                    HandleEmeraldChanged(currentEmerald);
                }
            }

#if UNITY_EDITOR
            // Phím tắt kiểm tra tiền nhảy số tức thì qua New Input System
            if (_enableDebugKeys) {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.gKey.wasPressedThisFrame)) {
                    TestAddGold(100);
                }
            }
#endif
        }

        private void OnDisable() {
            if (_goldChangedChannel != null) {
                _goldChangedChannel.OnGoldChanged -= HandleGoldChanged;
            }
            if (_emeraldChangedChannel != null) {
                _emeraldChangedChannel.OnEmeraldChanged -= HandleEmeraldChanged;
            }

            UnregisterButtons();
        }

        private void HandleGoldChanged(double newAmount) {
            bool hasChanged = (_lastGold >= 0 && Math.Abs(newAmount - _lastGold) > 0.001);
            _lastGold = newAmount;

            if (_coinText != null) {
                _coinText.text = NumberFormatter.Format(newAmount);
            }

            // Hiệu ứng nảy nhẹ khi nhận thêm tiền
            if (hasChanged) {
                PunchScaleEffect();
            }
        }

        private void HandleEmeraldChanged(int newAmount) {
            bool hasChanged = (_lastEmerald >= 0 && newAmount != _lastEmerald);
            _lastEmerald = newAmount;

            UpdateEmeraldDisplay(newAmount);

            // Hiệu ứng nảy nhẹ thanh Gem khi nhận thêm Gem
            if (hasChanged) {
                PunchEmeraldScaleEffect();
            }
        }

        public void UpdateEmeraldDisplay(int amount) {
            if (_emeraldText != null) {
                _emeraldText.text = amount.ToString();
            }
        }

        private void ForceSyncGold() {
            ForceSyncBalances();
        }

        private void ForceSyncBalances() {
            if (EconomyManager.Instance != null) {
                HandleGoldChanged(EconomyManager.Instance.CurrentGold);
            } else {
                if (_coinText != null) _coinText.text = "0";
            }

            if (HardCurrencyManager.Instance != null) {
                HandleEmeraldChanged(HardCurrencyManager.Instance.CurrentEmerald);
            } else {
                UpdateEmeraldDisplay(0);
            }
        }

        private Coroutine _punchRoutine;
        private Coroutine _emeraldPunchRoutine;

        private void PunchScaleEffect() {
            Transform target = _coinBarTransform != null ? _coinBarTransform : (_coinText != null ? _coinText.transform : null);
            if (target != null) {
                if (_punchRoutine != null) StopCoroutine(_punchRoutine);
                _punchRoutine = StartCoroutine(SmoothPunchRoutine(target));
            }
        }

        private void PunchEmeraldScaleEffect() {
            Transform target = _emeraldBarTransform != null ? _emeraldBarTransform : (_emeraldText != null ? _emeraldText.transform : null);
            if (target != null) {
                if (_emeraldPunchRoutine != null) StopCoroutine(_emeraldPunchRoutine);
                _emeraldPunchRoutine = StartCoroutine(SmoothPunchRoutine(target));
            }
        }

        private System.Collections.IEnumerator SmoothPunchRoutine(Transform target) {
            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                if (target != null) {
                    target.localScale = Vector3.one * scale;
                }
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        private void RegisterButtons() {
            if (_settingBtn != null) _settingBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút SettingBtn!");
                var dialog = SettingsDialogUI.GetOrCreateInScene();
                if (dialog != null) {
                    dialog.Toggle();
                }
                OnSettingClicked?.Invoke();
            });

            if (_shopBtn != null) _shopBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút ShopBtn!");
                OnShopClicked?.Invoke();
            });

            if (_nextMapBtn != null) _nextMapBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút NextMapBtn!");
                OnNextMapClicked?.Invoke();
            });

            if (_eugtBtn != null) _eugtBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút EUGTbtn (Emerald Upgrade Tree)!");
                var eugtPanel = EmeraldUpgradeTreePanelUI.GetOrCreateInScene();
                if (eugtPanel != null) {
                    eugtPanel.Toggle();
                }
                OnEUGTClicked?.Invoke();
            });

            if (_ugmBtn != null) _ugmBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút UGMbtn (Upgrade Map)!");
                OnUGMClicked?.Invoke();
            });

            if (_invBtn != null) _invBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút InvBtn (Inventory)!");
                OnInvClicked?.Invoke();
            });

            if (_adsBtn != null) _adsBtn.onClick.AddListener(() => {
                Debug.Log("[Canvas UI] Bấm nút ADS (Watch Ads)!");
                OnAdsClicked?.Invoke();
            });
        }

        private void UnregisterButtons() {
            if (_settingBtn != null) _settingBtn.onClick.RemoveAllListeners();
            if (_shopBtn != null) _shopBtn.onClick.RemoveAllListeners();
            if (_nextMapBtn != null) _nextMapBtn.onClick.RemoveAllListeners();
            if (_eugtBtn != null) _eugtBtn.onClick.RemoveAllListeners();
            if (_ugmBtn != null) _ugmBtn.onClick.RemoveAllListeners();
            if (_invBtn != null) _invBtn.onClick.RemoveAllListeners();
            if (_adsBtn != null) _adsBtn.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Bấm chuột phải vào component trong Inspector chọn "Test Add 100 Gold"
        /// </summary>
        [ContextMenu("Test Add 100 Gold")]
        public void TestAddGoldContextMenu() {
            TestAddGold(100);
        }

        public void TestAddGold(double amount) {
            if (EconomyManager.Instance != null) {
                EconomyManager.Instance.AddGold(amount);
                Debug.Log($"<color=#10B981>[Test Add Gold]</color> Đã cộng {amount} Vàng! Tổng ví: {EconomyManager.Instance.CurrentGold}");
            } else {
                Debug.LogWarning("[Test Add Gold] Không tìm thấy EconomyManager.Instance trong Scene!");
            }
        }
    }
}
