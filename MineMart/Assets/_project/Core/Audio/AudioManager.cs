using System.Collections;
using MineMart.Core.Settings;
using UnityEngine;

namespace MineMart.Core.Audio {
    /// <summary>
    /// Quản lý phát Âm nhạc (BGM) và Âm thanh hiệu ứng (SFX).
    /// Tự động lắng nghe trạng thái Bật/Tắt từ SettingsManager.
    /// Có cơ chế tự động tạo AudioSource và synthesize âm click nút bấm mặc định.
    /// </summary>
    public class AudioManager : MonoBehaviour {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Default Audio Clips (Tùy chọn)")]
        [Tooltip("Nhạc nền mặc định")]
        [SerializeField] private AudioClip _defaultBgmClip;

        [Tooltip("Âm thanh bấm nút UI mặc định")]
        [SerializeField] private AudioClip _buttonClickClip;

        [Tooltip("Âm thanh tiếng tiền vàng nổ mặc định (Tip Coin Burst SFX) khi đưa block cho khách")]
        [SerializeField] private AudioClip _defaultCoinBurstClip;

        public AudioClip DefaultCoinBurstClip => _defaultCoinBurstClip;

        [Header("Volume Scales")]
        [Range(0f, 1f)] [SerializeField] private float _musicVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1.0f;

        public AudioSource MusicSource => _musicSource;
        public AudioSource SfxSource => _sfxSource;

        public float MusicVolume {
            get => _musicVolume;
            set {
                _musicVolume = Mathf.Clamp01(value);
                if (_musicSource != null) _musicSource.volume = _musicVolume;
            }
        }

        public float SfxVolume {
            get => _sfxVolume;
            set {
                _sfxVolume = Mathf.Clamp01(value);
                if (_sfxSource != null) _sfxSource.volume = _sfxVolume;
            }
        }

        public static AudioManager GetOrCreateInstance() {
            if (Instance == null) {
                var found = FindAnyObjectByType<AudioManager>();
                if (found != null) {
                    Instance = found;
                } else {
                    GameObject go = new GameObject("[AudioManager]");
                    Instance = go.AddComponent<AudioManager>();
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
                SetupAudioSources();
            } else if (Instance != this) {
                // Nếu đã có instance cũ tồn tại, chuyển giao các AudioClip mới được kéo vào Inspector của Scene sang instance chính
                if (_defaultBgmClip != null) {
                    Instance._defaultBgmClip = _defaultBgmClip;
                    Instance.PlayMusic(_defaultBgmClip);
                }
                if (_buttonClickClip != null) Instance._buttonClickClip = _buttonClickClip;
                if (_defaultCoinBurstClip != null) Instance._defaultCoinBurstClip = _defaultCoinBurstClip;
                Destroy(gameObject);
            }
        }

        private void SetupAudioSources() {
            if (_musicSource == null) {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
                _musicSource.volume = _musicVolume;
            }

            if (_sfxSource == null) {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.volume = _sfxVolume;
            }
        }

        private void Start() {
            // Lắng nghe sự kiện bật/tắt từ SettingsManager
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnMusicToggled += HandleMusicToggled;
                SettingsManager.Instance.OnSfxToggled += HandleSfxToggled;

                ApplySettingsState(SettingsManager.Instance.IsMusicEnabled, SettingsManager.Instance.IsSfxEnabled);
            }

            if (_defaultBgmClip != null && _musicSource != null && !_musicSource.isPlaying) {
                PlayMusic(_defaultBgmClip);
            }
        }

        private void OnDestroy() {
            if (SettingsManager.Instance != null) {
                SettingsManager.Instance.OnMusicToggled -= HandleMusicToggled;
                SettingsManager.Instance.OnSfxToggled -= HandleSfxToggled;
            }
        }

        private void ApplySettingsState(bool musicEnabled, bool sfxEnabled) {
            if (_musicSource != null) {
                _musicSource.mute = !musicEnabled;
            }
            if (_sfxSource != null) {
                _sfxSource.mute = !sfxEnabled;
            }
        }

        private void HandleMusicToggled(bool enabled) {
            if (_musicSource != null) {
                _musicSource.mute = !enabled;
                if (enabled && !_musicSource.isPlaying && _defaultBgmClip != null) {
                    _musicSource.Play();
                }
            }
        }

        private void HandleSfxToggled(bool enabled) {
            if (_sfxSource != null) {
                _sfxSource.mute = !enabled;
            }
        }

        /// <summary>
        /// Phát bài nhạc nền BGM (tự động đổi bài khi sang map mới, nếu trùng bài thì không ngắt quãng).
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true) {
            if (clip == null || _musicSource == null) return;
            // Nếu đang phát đúng bài này thì tiếp tục phát mượt mà, không giật lại từ đầu
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = _musicVolume;
            _musicSource.mute = SettingsManager.Instance != null && !SettingsManager.Instance.IsMusicEnabled;
            _musicSource.Play();
        }

        /// <summary>
        /// Dừng phát nhạc nền.
        /// </summary>
        public void StopMusic() {
            if (_musicSource != null) {
                _musicSource.Stop();
            }
        }

        /// <summary>
        /// Phát một hiệu ứng âm thanh SFX (hỗ trợ điều chỉnh cao độ pitch để âm thanh nảy sống động).
        /// </summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1.0f, float pitch = 1.0f) {
            if (clip == null || _sfxSource == null) return;
            if (SettingsManager.Instance != null && !SettingsManager.Instance.IsSfxEnabled) return;

            _sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
            _sfxSource.PlayOneShot(clip, _sfxVolume * Mathf.Clamp01(volumeScale));
        }

        /// <summary>
        /// Phát âm thanh bấm nút UI.
        /// </summary>
        public void PlayButtonClick() {
            if (SettingsManager.Instance != null && !SettingsManager.Instance.IsSfxEnabled) return;

            if (_buttonClickClip != null) {
                PlaySfx(_buttonClickClip, 0.9f);
            }
        }
    }
}
