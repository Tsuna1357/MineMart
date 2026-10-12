using System;
using UnityEngine;

namespace MineMart.Core.Settings {
    /// <summary>
    /// Quản lý trạng thái Cài Đặt Game (Settings Manager).
    /// Lưu trữ bền vững qua PlayerPrefs:
    /// 1. Cần điều khiển (Virtual Joystick): Bật / Tắt
    /// 2. Âm nhạc nền (BGM / Music): Bật / Tắt
    /// 3. Hiệu ứng âm thanh (SFX): Bật / Tắt
    /// </summary>
    public class SettingsManager : MonoBehaviour {
        public static SettingsManager Instance { get; private set; }

        private const string PREF_KEY_JOYSTICK = "MineMart_Settings_Joystick";
        private const string PREF_KEY_MUSIC = "MineMart_Settings_Music";
        private const string PREF_KEY_SFX = "MineMart_Settings_SFX";

        [Header("Runtime State")]
        [SerializeField] private bool _isJoystickEnabled = true;
        [SerializeField] private bool _isMusicEnabled = true;
        [SerializeField] private bool _isSfxEnabled = true;

        /// <summary>
        /// Vector điều hướng từ cần gạt ảo trên màn hình (Virtual Joystick), giá trị từ (-1,-1) đến (1,1).
        /// Được cập nhật thời gian thực từ UI VirtualJoystick và đọc bởi PlayerController.
        /// </summary>
        public static Vector2 VirtualJoystickInput { get; set; } = Vector2.zero;

        /// <summary>
        /// Bật/Tắt Cần điều khiển ảo (Joystick).
        /// </summary>
        public bool IsJoystickEnabled {
            get => _isJoystickEnabled;
            set {
                if (_isJoystickEnabled != value) {
                    _isJoystickEnabled = value;
                    PlayerPrefs.SetInt(PREF_KEY_JOYSTICK, value ? 1 : 0);
                    PlayerPrefs.Save();
                    OnJoystickToggled?.Invoke(value);
                    Debug.Log($"[SettingsManager] 🕹️ Joystick đã {(value ? "BẬT" : "TẮT")}");
                }
            }
        }

        /// <summary>
        /// Bật/Tắt Âm nhạc nền (Music / BGM).
        /// </summary>
        public bool IsMusicEnabled {
            get => _isMusicEnabled;
            set {
                if (_isMusicEnabled != value) {
                    _isMusicEnabled = value;
                    PlayerPrefs.SetInt(PREF_KEY_MUSIC, value ? 1 : 0);
                    PlayerPrefs.Save();
                    OnMusicToggled?.Invoke(value);
                    Debug.Log($"[SettingsManager] 🎵 Âm nhạc đã {(value ? "BẬT" : "TẮT")}");
                }
            }
        }

        /// <summary>
        /// Bật/Tắt Hiệu ứng âm thanh (SFX).
        /// </summary>
        public bool IsSfxEnabled {
            get => _isSfxEnabled;
            set {
                if (_isSfxEnabled != value) {
                    _isSfxEnabled = value;
                    PlayerPrefs.SetInt(PREF_KEY_SFX, value ? 1 : 0);
                    PlayerPrefs.Save();
                    OnSfxToggled?.Invoke(value);
                    Debug.Log($"[SettingsManager] 🔊 Hiệu ứng âm thanh đã {(value ? "BẬT" : "TẮT")}");
                }
            }
        }

        public event Action<bool> OnJoystickToggled;
        public event Action<bool> OnMusicToggled;
        public event Action<bool> OnSfxToggled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize() {
            GetOrCreateInstance();
        }

        public static SettingsManager GetOrCreateInstance() {
            if (Instance == null) {
                var found = FindAnyObjectByType<SettingsManager>();
                if (found != null) {
                    Instance = found;
                } else {
                    GameObject go = new GameObject("[SettingsManager]");
                    Instance = go.AddComponent<SettingsManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return Instance;
        }

        private void Awake() {
            if (Instance == null) {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            } else if (Instance != this) {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Tải lại toàn bộ cài đặt từ bộ nhớ PlayerPrefs.
        /// </summary>
        public void LoadSettings() {
            // Mặc định ban đầu đều là BẬT (1)
            _isJoystickEnabled = PlayerPrefs.GetInt(PREF_KEY_JOYSTICK, 1) == 1;
            _isMusicEnabled = PlayerPrefs.GetInt(PREF_KEY_MUSIC, 1) == 1;
            _isSfxEnabled = PlayerPrefs.GetInt(PREF_KEY_SFX, 1) == 1;
        }

        /// <summary>
        /// Khôi phục toàn bộ cài đặt về mặc định (Tất cả đều Bật).
        /// </summary>
        public void ResetToDefaults() {
            IsJoystickEnabled = true;
            IsMusicEnabled = true;
            IsSfxEnabled = true;
        }
    }
}
