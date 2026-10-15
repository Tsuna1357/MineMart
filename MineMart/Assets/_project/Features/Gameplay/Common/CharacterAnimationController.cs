using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Gameplay.Common {
    /// <summary>
    /// Điều khiển chuyển đổi Animation Run / Idle cho các nhân vật (Player, Worker, Customer).
    /// Tự động phát hiện cả hệ thống Animation cổ điển (Legacy UnityEngine.Animation)
    /// lẫn hệ thống Animator (Mecanim UnityEngine.Animator).
    /// Tự động đọc tốc độ di chuyển từ NavMeshAgent hoặc CharacterController,
    /// đồng thời hỗ trợ gọi thủ công qua hàm SetMoving(bool).
    /// </summary>
    public class CharacterAnimationController : MonoBehaviour {
        [Header("Animation Settings")]
        [Tooltip("Tên clip Idle (nếu để trống sẽ tự động dò tìm)")]
        [SerializeField] private string _idleClipName;

        [Tooltip("Tên clip Run (nếu để trống sẽ tự động dò tìm)")]
        [SerializeField] private string _runClipName;

        [Tooltip("Thời gian chuyển mượt (CrossFade) giữa các animation (giây)")]
        [SerializeField] private float _crossFadeDuration = 0.15f;

        [Tooltip("Tự động nhận diện di chuyển qua NavMeshAgent hoặc CharacterController")]
        [SerializeField] private bool _autoDetectMovement = true;

        [Tooltip("Ngưỡng vận tốc bình phương để coi là đang di chuyển")]
        [SerializeField] private float _moveSpeedThresholdSqr = 0.05f;

        [Header("Tốc Độ Animation (Đồng Bộ Vận Tốc Chạy)")]
        [Tooltip("Tự động đồng bộ tốc độ Animation Run theo vận tốc di chuyển")]
        [SerializeField] private bool _matchRunSpeedWithMovement = true;

        [Tooltip("Tốc độ di chuyển chuẩn (m/s) của animation gốc")]
        [SerializeField] private float _baseMoveSpeed = 3.0f;

        [Tooltip("Hệ số nhân tốc độ Animation Run để bước chân nhanh nhẹn, dứt khoát")]
        [SerializeField] private float _runAnimSpeedMultiplier = 1.35f;

        [Header("Bouncy Bobbing (Nhún Nhảy Khi Di Chuyển)")]
        [Tooltip("Bật hiệu ứng nhún nhún cơ thể vui nhộn khi di chuyển")]
        [SerializeField] private bool _enableBouncyBobbing = true;

        [Tooltip("Độ cao nảy lên xuống (mét) của mỗi bước nhún")]
        [SerializeField] private float _bobHeight = 0.12f;

        [Tooltip("Tần số nhún (tốc độ nhịp bước chân, nhún 2 lần mỗi chu kỳ chạy)")]
        [SerializeField] private float _bobFrequency = 14f;

        [Tooltip("Góc lắc lư nghiêng người trái/phải theo nhịp chạy (độ)")]
        [SerializeField] private float _tiltAngle = 4.5f;

        [Tooltip("Độ co giãn mềm mại (Squash & Stretch khi tiếp đất)")]
        [SerializeField] private float _squashFactor = 0.08f;

        private Animation _animation;
        private Animator _animator;
        private NavMeshAgent _navMeshAgent;
        private CharacterController _characterController;

        [Tooltip("Transform của visual/model hoặc bone gốc Base để chạy nhún nảy (để trống sẽ tự động dò tìm)")]
        [SerializeField] private Transform _modelTransform;
        private Vector3 _initialModelPos;
        private Quaternion _initialModelRot;
        private Vector3 _initialModelScale = Vector3.one;
        private bool _hasModelOriginalTransform = false;
        private float _positionScaleFactor = 1f;

        private float _bobTimer = 0f;
        private float _manualMoveSpeed = 0f;

        private bool _isMoving = false;
        private bool _isMining = false;
        private float _miningAnimTimer = 0f;
        private bool _isInitialized = false;
        private bool _hasPlayedInitial = false;

        public bool IsMoving => _isMoving;
        public bool IsMining => _isMining;
        public string IdleClipName => _idleClipName;
        public string RunClipName => _runClipName;

        public void SetMining(bool isMining) {
            _isMining = isMining;
            if (!isMining) {
                _miningAnimTimer = 0f;
            }
        }

        public Transform ModelTransform => _modelTransform;

        public void SetModelTransform(Transform model) {
            if (model == null || model == transform) return;
            _modelTransform = model;
            _initialModelPos = _modelTransform.localPosition;
            _initialModelRot = _modelTransform.localRotation;
            _initialModelScale = _modelTransform.localScale;
            _hasModelOriginalTransform = true;

            if (_modelTransform.parent != null && _modelTransform.parent.lossyScale.y > 0.0001f && _modelTransform.parent.lossyScale.y < 0.2f) {
                _positionScaleFactor = 1.0f / _modelTransform.parent.lossyScale.y;
            } else if (_initialModelPos.y > 10.0f) {
                _positionScaleFactor = 100.0f;
            } else {
                _positionScaleFactor = 1.0f;
            }

            Debug.Log($"<color=#38BDF8>[CharacterAnimationController]</color> {name}: SetModelTransform='{_modelTransform.name}', ScaleFactor={_positionScaleFactor:F1}, InitialPos={_initialModelPos}");
        }

        public bool AutoDetectMovement {
            get => _autoDetectMovement;
            set => _autoDetectMovement = value;
        }

        public bool EnableBouncyBobbing {
            get => _enableBouncyBobbing;
            set {
                if (_enableBouncyBobbing != value) {
                    _enableBouncyBobbing = value;
                    if (!_enableBouncyBobbing && _modelTransform != null && _modelTransform != transform && _hasModelOriginalTransform) {
                        _modelTransform.localPosition = _initialModelPos;
                        _modelTransform.localRotation = _initialModelRot;
                        _modelTransform.localScale = _initialModelScale;
                    }
                }
            }
        }

        private void Awake() {
            Initialize();
        }

        private void Start() {
            if (!_isInitialized) {
                Initialize();
            }
            // Mặc định ban đầu luôn phát Idle
            ApplyAnimationState(false, force: true);
        }

        private void OnEnable() {
            // Khi được kích hoạt từ Pool hoặc nạp lại, reset về Idle
            if (_isInitialized) {
                _hasPlayedInitial = false;
                ApplyAnimationState(false, force: true);
            }
        }

        private void OnDisable() {
            // Phục hồi nguyên trạng transform model ban đầu khi disable
            if (_hasModelOriginalTransform && _modelTransform != null) {
                _modelTransform.localPosition = _initialModelPos;
                _modelTransform.localRotation = _initialModelRot;
                _modelTransform.localScale = _initialModelScale;
            }
        }

        /// <summary>
        /// Khởi tạo và liên kết các component di chuyển cùng animation trên nhân vật.
        /// </summary>
        public void Initialize() {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _characterController = GetComponent<CharacterController>();

            RefreshModel();
            _isInitialized = true;
        }

        /// <summary>
        /// Quét lại các component Animation / Animator trong children (hữu ích khi đổi skin/model).
        /// </summary>
        public void RefreshModel() {
            // 1. Tìm component Animation (Legacy) - TUYỆT ĐỐI KHÔNG destroy hay move để giữ nguyên bindings của FBX
            _animation = GetComponentInChildren<Animation>();
            if (_animation != null) {
                DiscoverLegacyClips();
                if (_modelTransform == null && _animation.transform != transform) {
                    _modelTransform = _animation.transform;
                }
            }

            // 2. Tìm component Animator (Mecanim)
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null) {
                _animator.applyRootMotion = false;
                if (_modelTransform == null && _animator.transform != transform) {
                    _modelTransform = _animator.transform;
                }
            }

            // 3. Nếu là Worker hoặc Customer (có model container riêng nằm dưới transform)
            if (_modelTransform == null || _modelTransform == transform) {
                for (int i = 0; i < transform.childCount; i++) {
                    Transform child = transform.GetChild(i);
                    string childName = child.name.ToLowerInvariant();
                    if (childName.Contains("worker") || childName.Contains("customer") || (childName.Contains("model") && !childName.Contains("charactor"))) {
                        _modelTransform = child;
                        break;
                    }
                }
            }

            if (_modelTransform != null && _modelTransform != transform) {
                _initialModelPos = _modelTransform.localPosition;
                _initialModelRot = _modelTransform.localRotation;
                _initialModelScale = _modelTransform.localScale;
                _hasModelOriginalTransform = true;

                if (_modelTransform.parent != null && _modelTransform.parent.lossyScale.y > 0.0001f && _modelTransform.parent.lossyScale.y < 0.2f) {
                    _positionScaleFactor = 1.0f / _modelTransform.parent.lossyScale.y;
                } else if (_initialModelPos.y > 10.0f) {
                    _positionScaleFactor = 100.0f;
                } else {
                    _positionScaleFactor = 1.0f;
                }

                Debug.Log($"<color=#38BDF8>[CharacterAnimationController]</color> {name}: Model nhún='{_modelTransform.name}', ScaleFactor={_positionScaleFactor:F1}");
            } else {
                _hasModelOriginalTransform = false;
                _positionScaleFactor = 1.0f;
            }

            _hasPlayedInitial = false;
            ApplyAnimationState(false, force: true);
        }

        /// <summary>
        /// Tự động quét và cấu hình các clip Idle và Run trong component Animation (Legacy).
        /// </summary>
        public void DiscoverLegacyClips() {
            if (_animation == null) return;

            // 1. Dò tìm trực tiếp qua tên indexer phổ biến
            string[] idleCandidates = new string[] { "idle", "Idle", "IDLE", "stand", "Stand" };
            string[] runCandidates = new string[] { "Run", "run", "RUN", "walk", "Walk", "Walking" };

            if (string.IsNullOrEmpty(_idleClipName)) {
                foreach (string name in idleCandidates) {
                    if (_animation[name] != null) {
                        _idleClipName = name;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_runClipName)) {
                foreach (string name in runCandidates) {
                    if (_animation[name] != null) {
                        _runClipName = name;
                        break;
                    }
                }
            }

            // 2. Quét qua các clip đã được gán sẵn trong Animation component
            foreach (AnimationState state in _animation) {
                if (state.clip != null) {
                    state.wrapMode = WrapMode.Loop;
                }

                string lower = state.name.ToLowerInvariant();
                if (string.IsNullOrEmpty(_idleClipName) && (lower.Contains("idle") || lower.Contains("stand"))) {
                    _idleClipName = state.name;
                } else if (string.IsNullOrEmpty(_runClipName) && (lower.Contains("run") || lower.Contains("walk"))) {
                    _runClipName = state.name;
                }
            }

            // 3. Fallback: Nếu component có clip mặc định nhưng chưa gán
            if (string.IsNullOrEmpty(_idleClipName) && _animation.clip != null) {
                _idleClipName = _animation.clip.name;
            }

            // Cấu hình Loop cho các clip
            if (!string.IsNullOrEmpty(_idleClipName) && _animation[_idleClipName] != null) {
                _animation[_idleClipName].wrapMode = WrapMode.Loop;
            }
            if (!string.IsNullOrEmpty(_runClipName) && _animation[_runClipName] != null) {
                _animation[_runClipName].wrapMode = WrapMode.Loop;
            }

            Debug.Log($"<color=#38BDF8>[CharacterAnimationController]</color> {name} nhận diện: Idle='{_idleClipName}', Run='{_runClipName}'");
        }

        /// <summary>
        /// Cho phép script bên ngoài (PlayerController, AI Agent) gán trạng thái di chuyển thủ công.
        /// </summary>
        public void SetMoving(bool isMoving) {
            ApplyAnimationState(isMoving, force: false);
        }

        /// <summary>
        /// Cho phép script bên ngoài truyền tốc độ thực tế (ví dụ PlayerController truyền CurrentMoveSpeed).
        /// </summary>
        public void SetMoveSpeed(float speed) {
            _manualMoveSpeed = speed;
        }

        private float GetCurrentSpeed() {
            if (_manualMoveSpeed > 0.01f) {
                return _manualMoveSpeed;
            }
            if (_characterController != null && _characterController.enabled) {
                Vector3 vel = _characterController.velocity;
                vel.y = 0f;
                if (vel.sqrMagnitude > 0.01f) return vel.magnitude;
            }
            if (_navMeshAgent != null && _navMeshAgent.enabled) {
                if (_navMeshAgent.velocity.sqrMagnitude > 0.01f) return _navMeshAgent.velocity.magnitude;
                return _navMeshAgent.speed;
            }
            return _baseMoveSpeed * _runAnimSpeedMultiplier;
        }

        private void UpdateAnimationSpeed() {
            if (!_matchRunSpeedWithMovement) return;

            float speedRatio = 1.0f;
            if (_isMoving) {
                float currentSpeed = GetCurrentSpeed();
                // Tỉ lệ vận tốc: Tốc độ hiện tại / Tốc độ tham chiếu * Hệ số gia tốc (mặc định 1.35x)
                speedRatio = Mathf.Clamp((currentSpeed / Mathf.Max(_baseMoveSpeed, 0.1f)) * _runAnimSpeedMultiplier, 1.15f, 2.75f);
            }

            if (_animation != null) {
                if (_isMoving && !string.IsNullOrEmpty(_runClipName) && _animation[_runClipName] != null) {
                    _animation[_runClipName].speed = speedRatio;
                } else if (!_isMoving && !string.IsNullOrEmpty(_idleClipName) && _animation[_idleClipName] != null) {
                    _animation[_idleClipName].speed = 1.0f;
                }
            }

            if (_animator != null) {
                _animator.speed = _isMoving ? speedRatio : 1.0f;
            }
        }

        private void ApplyAnimationState(bool moving, bool force = false) {
            if (_isMoving == moving && _hasPlayedInitial && !force) return;
            _isMoving = moving;
            _hasPlayedInitial = true;

            string targetClip = _isMoving ? _runClipName : _idleClipName;

            // 1. Ưu tiên xử lý Legacy Animation nếu có
            if (_animation != null) {
                if (!string.IsNullOrEmpty(targetClip)) {
                    if (_animation[targetClip] != null) {
                        _animation[targetClip].wrapMode = WrapMode.Loop;
                    }
                    _animation.CrossFade(targetClip, _crossFadeDuration);
                } else {
                    // Nếu targetClip rỗng (chưa tìm ra clip Idle), dừng clip Run để không bị kẹt chạy tại chỗ
                    if (!_isMoving && !string.IsNullOrEmpty(_runClipName)) {
                        _animation.Stop(_runClipName);
                    }
                }
            }

            // 2. Xử lý Mecanim Animator nếu có Controller
            if (_animator != null && _animator.runtimeAnimatorController != null) {
                TrySetAnimatorBool("IsMoving", _isMoving);
                TrySetAnimatorBool("IsRunning", _isMoving);
                TrySetAnimatorFloat("Speed", _isMoving ? 1f : 0f);

                if (!string.IsNullOrEmpty(targetClip)) {
                    int hash = Animator.StringToHash(targetClip);
                    if (_animator.HasState(0, hash)) {
                        _animator.CrossFade(hash, _crossFadeDuration);
                    }
                }
            }

            UpdateAnimationSpeed();
        }

        private void TrySetAnimatorBool(string paramName, bool value) {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters) {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName) {
                    _animator.SetBool(paramName, value);
                    return;
                }
            }
        }

        private void TrySetAnimatorFloat(string paramName, float value) {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters) {
                if (p.type == AnimatorControllerParameterType.Float && p.name == paramName) {
                    _animator.SetFloat(paramName, value);
                    return;
                }
            }
        }

        private void Update() {
            if (_autoDetectMovement) {
                bool detectedMoving = false;

                // Kiểm tra vận tốc từ NavMeshAgent (AI Worker hoặc AI Customer hoặc Player Auto-Pilot)
                if (_navMeshAgent != null && _navMeshAgent.enabled) {
                    detectedMoving = _navMeshAgent.velocity.sqrMagnitude > _moveSpeedThresholdSqr
                                  || _navMeshAgent.desiredVelocity.sqrMagnitude > _moveSpeedThresholdSqr
                                  || (_navMeshAgent.hasPath && _navMeshAgent.remainingDistance > Mathf.Max(_navMeshAgent.stoppingDistance, 0.1f));
                }
                // Kiểm tra vận tốc từ CharacterController (Player điều khiển tay)
                else if (_characterController != null && _characterController.enabled) {
                    Vector3 vel = _characterController.velocity;
                    vel.y = 0f; // Bỏ qua trục Y (nhảy hoặc trọng lực rơi)
                    detectedMoving = vel.sqrMagnitude > _moveSpeedThresholdSqr;
                }

                ApplyAnimationState(detectedMoving, force: false);
            }

            // Liên tục cập nhật tốc độ phát animation thích ứng theo thời gian thực
            if (_isMoving) {
                UpdateAnimationSpeed();
            }
        }

        private void LateUpdate() {
            if (!_enableBouncyBobbing || !_hasModelOriginalTransform || _modelTransform == null || _modelTransform == transform) {
                return;
            }

            if (_isMoving) {
                float currentSpeed = GetCurrentSpeed();
                float speedFactor = Mathf.Clamp(currentSpeed / Mathf.Max(_baseMoveSpeed, 0.1f), 0.8f, 2.2f);
                _bobTimer += Time.deltaTime * speedFactor;

                // 1. Nhún nảy lên xuống theo nhịp bước chân (nhún 2 lần mỗi chu kỳ chạy)
                float bounceY = Mathf.Abs(Mathf.Sin(_bobTimer * _bobFrequency)) * (_bobHeight * _positionScaleFactor);

                // 2. Nghiêng người lắc lư nhẹ trái/phải theo nhịp 2 chân (waddle)
                float activeTiltAngle = Mathf.Max(_tiltAngle, 9.5f);
                float tiltZ = Mathf.Sin(_bobTimer * (_bobFrequency * 0.5f)) * activeTiltAngle;

                // 3. Độ dịch ngang hông (sway) bù trừ để giữ chân bám sàn và đẩy thân trên lắc lư rộng như Worker
                float swayX = Mathf.Sin(_bobTimer * (_bobFrequency * 0.5f)) * (0.07f * _positionScaleFactor);

                Quaternion tiltRot = Quaternion.Euler(0f, 0f, -tiltZ);
                Vector3 offset = new Vector3(swayX, bounceY, 0f);

                float squashWave = Mathf.Sin(_bobTimer * _bobFrequency);
                float scaleY = _initialModelScale.y * (1f - squashWave * _squashFactor);
                float scaleX = _initialModelScale.x * (1f + squashWave * (_squashFactor * 0.5f));
                float scaleZ = _initialModelScale.z * (1f + squashWave * (_squashFactor * 0.5f));

                _modelTransform.localPosition = _initialModelPos + offset;
                _modelTransform.localRotation = _initialModelRot * tiltRot;
                _modelTransform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            } else if (_isMining) {
                // Procedural Mining Chop
                _miningAnimTimer += Time.deltaTime;
                float swingFreq = 11f;
                float swingWave = Mathf.Sin(_miningAnimTimer * swingFreq);
                float chopStrength = Mathf.Max(0f, swingWave);

                float pitchForward = chopStrength * 16f;
                float dipY = -chopStrength * 0.08f * _positionScaleFactor;
                float squashY = 1f - chopStrength * 0.09f;
                float expandXZ = 1f + chopStrength * 0.05f;

                _modelTransform.localPosition = _initialModelPos + new Vector3(0f, dipY, 0f);
                _modelTransform.localRotation = _initialModelRot * Quaternion.Euler(pitchForward, 0f, 0f);
                _modelTransform.localScale = new Vector3(_initialModelScale.x * expandXZ, _initialModelScale.y * squashY, _initialModelScale.z * expandXZ);
                _bobTimer = 0f;
            } else {
                _modelTransform.localPosition = Vector3.Lerp(_modelTransform.localPosition, _initialModelPos, Time.deltaTime * 14f);
                _modelTransform.localRotation = Quaternion.Slerp(_modelTransform.localRotation, _initialModelRot, Time.deltaTime * 14f);
                _modelTransform.localScale = Vector3.Lerp(_modelTransform.localScale, _initialModelScale, Time.deltaTime * 14f);
                _bobTimer = 0f;
                _miningAnimTimer = 0f;
            }
        }

        public static Transform FindChildRecursive(Transform parent, string targetName) {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++) {
                Transform child = parent.GetChild(i);
                if (child.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase)) {
                    return child;
                }
                Transform found = FindChildRecursive(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
