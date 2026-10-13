using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Gameplay.Mining {
    /// <summary>
    /// Đại diện cho một "Ô Lớn Quy Hoạch" trên bản đồ cửa hàng.
    /// Mỗi ô có vị trí đặt mỏ (Snap Point) và hướng xoay cố định đã được tính toán kỹ,
    /// đảm bảo khối quầy không cản trở luồng di chuyển của Khách Hàng và Dân Làng.
    /// </summary>
    [SelectionBase]
    public class WorkstationPlot : MonoBehaviour {
        [Header("Plot Identification")]
        [Tooltip("Mã định danh duy nhất của ô đất quy hoạch (dùng để lưu/tải vị trí trạm mỏ)")]
        [SerializeField] private string _plotId;

        [Tooltip("Tên hiển thị khu vực (vd: Khu Mỏ Phía Tây, Khu Rừng Khai Thác...)")]
        [SerializeField] private string _plotName = "Khu Khai Thác";

        [Header("Anchors & Bounds")]
        [Tooltip("Điểm neo chuẩn xác để đặt trạm mỏ vào (vị trí + hướng xoay). Nếu để trống sẽ dùng chính Transform của Plot)")]
        [SerializeField] private Transform _snapAnchor;

        [Tooltip("Độ lệch vị trí (Local Offset) của trạm mỏ so với ô đất. Em có thể chỉnh X, Y, Z trên Inspector để đặt quầy lệch trái, lệch phải, thụt lùi... tùy ý từng khu.")]
        [SerializeField] private Vector3 _stationPositionOffset = Vector3.zero;

        [Tooltip("Góc xoay tùy chỉnh của trạm mỏ khi cắm vào ô đất này (vd: {0, 90, 0} xoay 90 độ, {0, 180, 0} xoay ngược lại...)")]
        [SerializeField] private Vector3 _stationRotationOffset = Vector3.zero;

        [Tooltip("Kích thước vùng nhận diện của ô đất khi rê chuột kéo trạm mỏ qua")]
        [SerializeField] private Vector3 _plotSize = new Vector3(2.5f, 0.2f, 2.5f);

        [Header("Current Station")]
        [Tooltip("Trạm mỏ đang cắm ở ô đất này")]
        [SerializeField] private MiningStation _currentStation;

        [Header("Visual Feedback (Optional)")]
        [Tooltip("GameObject hiển thị viền/mặt nền highlight khi đang kéo trạm mỏ đến gần")]
        [SerializeField] private GameObject _highlightRoot;
        [SerializeField] private Renderer _highlightRenderer;
        [SerializeField] private Color _validHighlightColor = new Color(0.2f, 0.9f, 0.3f, 0.25f);
        [SerializeField] private Color _swapHighlightColor = new Color(1f, 0.75f, 0.2f, 0.25f);

        [Header("Minecraft Particle Boundary (Hiệu Ứng Hạt Viền Vuông Minecraft)")]
        [Tooltip("Bật hiệu ứng hạt particle hình vuông phong cách Minecraft bao quanh viền ô đất thay thế mảng xanh đặc")]
        [SerializeField] private bool _useMinecraftParticleGuide = true;

        [Tooltip("Ẩn khối Quad/Cube mesh cũ để không che khuất sàn")]
        [SerializeField] private bool _hideLegacyHighlightMesh = true;

        private ParticleSystem _particleBoundary;
        private ParticleSystem.MainModule _psMain;
        private ParticleSystem.EmissionModule _psEmission;
        private ParticleSystem.ShapeModule _psShape;

        private bool _isGuideActive = false;
        private bool _isHovered = false;

        private static readonly Color COLOR_EMERALD_BRIGHT = new Color(0.18f, 0.98f, 0.42f, 0.95f);
        private static readonly Color COLOR_AMBER_SWAP = new Color(1.0f, 0.72f, 0.15f, 0.95f);
        private static readonly Color COLOR_RESTING_GUIDE = new Color(0.35f, 0.88f, 0.85f, 0.6f);

        private static readonly List<WorkstationPlot> s_allPlots = new List<WorkstationPlot>();
        public static IReadOnlyList<WorkstationPlot> AllPlots => s_allPlots;

        public string PlotId => _plotId;
        public string PlotName => _plotName;
        public Transform SnapAnchor => _snapAnchor != null ? _snapAnchor : transform;
        public Vector3 StationPositionOffset {
            get => _stationPositionOffset;
            set => _stationPositionOffset = value;
        }
        public Vector3 StationRotationOffset {
            get => _stationRotationOffset;
            set => _stationRotationOffset = value;
        }

        public Vector3 StationTargetPosition {
            get {
                Transform anchor = SnapAnchor;
                return anchor.position + anchor.TransformDirection(_stationPositionOffset);
            }
        }

        public Quaternion StationTargetRotation {
            get {
                Transform anchor = SnapAnchor;
                return anchor.rotation * Quaternion.Euler(_stationRotationOffset);
            }
        }

        public MiningStation CurrentStation => _currentStation;
        public bool IsEmpty => _currentStation == null;

        public event Action<MiningStation> OnStationPlaced;
        public event Action<MiningStation> OnStationRemoved;

        /// <summary>
        /// Sự kiện phát ra khi người chơi chạm/click vào một ô đất trống trên sàn.
        /// Dùng để kích hoạt Bong bóng Menu chọn mua mỏ từ MapManager.
        /// </summary>
        public static event Action<WorkstationPlot> OnPlotTapped;

        private void OnMouseDown() {
            if (IsEmpty) {
                OnPlotTapped?.Invoke(this);
            }
        }

        private void Awake() {
            if (string.IsNullOrEmpty(_plotId)) {
                _plotId = gameObject.name;
            }

            if (_snapAnchor == null) {
                Transform found = transform.Find("SnapAnchor");
                _snapAnchor = found != null ? found : transform;
            }

            if (_highlightRoot != null) {
                _highlightRoot.SetActive(false);
            }

            EnsureCollider();
        }

        private void EnsureCollider() {
            var col = GetComponent<Collider>();
            if (col == null) {
                var box = gameObject.AddComponent<BoxCollider>();
                float sx = _plotSize.x > 0.5f ? _plotSize.x : 2.5f;
                float sy = _plotSize.y > 0.2f ? _plotSize.y : 0.5f;
                float sz = _plotSize.z > 0.5f ? _plotSize.z : 2.5f;
                box.size = new Vector3(sx, sy, sz);
                box.center = new Vector3(0, sy * 0.5f, 0);
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            EnsureCollider();
        }

        private void Reset() {
            EnsureCollider();
        }
#endif

        private void OnEnable() {
            if (!s_allPlots.Contains(this)) {
                s_allPlots.Add(this);
            }
        }

        private void OnDisable() {
            s_allPlots.Remove(this);
        }

        private void Start() {
            // Tự động gán trạm mỏ con hoặc trạm mỏ đã cấu hình sẵn trong scene
            if (_currentStation != null) {
                SnapStation(_currentStation, false);
            } else {
                // Tự tìm mỏ con bên trong plot hoặc mỏ đứng sát vị trí của plot trong bán kính 2.5m
                MiningStation childStation = GetComponentInChildren<MiningStation>();
                if (childStation != null) {
                    SnapStation(childStation, false);
                } else {
                    for (int i = 0; i < MiningStation.AllStations.Count; i++) {
                        MiningStation st = MiningStation.AllStations[i];
                        if (st != null && st.CurrentPlot == null && Vector3.Distance(st.transform.position, StationTargetPosition) < 2.5f) {
                            SnapStation(st, false);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gán trạm mỏ vào ô quy hoạch này và đồng bộ vị trí.
        /// </summary>
        public void SnapStation(MiningStation station, bool animate = false) {
            if (station == null) return;

            // Nếu trạm trước đó đang ở plot khác, gỡ nó khỏi plot cũ
            if (station.CurrentPlot != null && station.CurrentPlot != this) {
                station.CurrentPlot.ClearStation();
            }

            _currentStation = station;
            station.CurrentPlot = this;

            Vector3 targetPos = StationTargetPosition;
            Quaternion targetRot = StationTargetRotation;

            if (animate) {
                // Hiệu ứng mượt mà khi thả vào ô
                StartCoroutine(AnimateSnap(station, targetPos, targetRot, 0.2f));
            } else {
                station.transform.position = targetPos;
                station.transform.rotation = targetRot;
                station.IsBeingDragged = false;
                station.NotifyPlacedDown();
            }

            OnStationPlaced?.Invoke(station);
        }

        /// <summary>
        /// Gỡ trạm mỏ ra khỏi ô quy hoạch này (chuẩn bị di dời hoặc hoán đổi).
        /// </summary>
        public void ClearStation() {
            if (_currentStation != null) {
                MiningStation old = _currentStation;
                _currentStation = null;
                if (old.CurrentPlot == this) {
                    old.CurrentPlot = null;
                }
                OnStationRemoved?.Invoke(old);
            }
        }

        /// <summary>
        /// Hoán đổi vị trí giữa 2 ô quy hoạch.
        /// </summary>
        public static void SwapStations(WorkstationPlot plotA, WorkstationPlot plotB, bool animate = true) {
            if (plotA == null || plotB == null || plotA == plotB) return;

            MiningStation stationA = plotA.CurrentStation;
            MiningStation stationB = plotB.CurrentStation;

            plotA.ClearStation();
            plotB.ClearStation();

            if (stationA != null) {
                plotB.SnapStation(stationA, animate);
            }
            if (stationB != null) {
                plotA.SnapStation(stationB, animate);
            }
        }

        /// <summary>
        /// Bật/Tắt hướng dẫn hiển thị đồng loạt cho tất cả các ô trên bản đồ khi người chơi nhấc một trạm mỏ lên.
        /// Giúp người chơi lập tức nhìn thấy các ô đích có thể thả hoặc swap vào!
        /// </summary>
        public static void ShowAllPlotGuides(bool show, MiningStation draggedStation = null) {
            for (int i = 0; i < s_allPlots.Count; i++) {
                WorkstationPlot p = s_allPlots[i];
                if (p != null) {
                    p.SetGuideActive(show, draggedStation);
                }
            }
        }

        /// <summary>
        /// Kích hoạt chế độ viền định hướng (Resting Guide) khi một trạm mỏ bất kỳ đang được kéo.
        /// </summary>
        public void SetGuideActive(bool active, MiningStation draggedStation = null) {
            _isGuideActive = active;
            _isHovered = false;

            if (!active) {
                if (_particleBoundary != null) {
                    _particleBoundary.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                if (_highlightRoot != null) {
                    _highlightRoot.SetActive(false);
                }
                return;
            }

            if (_useMinecraftParticleGuide) {
                EnsureParticleBoundary();
                if (_particleBoundary != null) {
                    _psMain.startColor = COLOR_RESTING_GUIDE;
                    _psEmission.rateOverTime = 14f;
                    if (!_particleBoundary.isPlaying) {
                        _particleBoundary.Play();
                    }
                }
            }

            // Nếu cho phép hiện mesh cũ thì bật ở mức độ trong suốt nhẹ
            if (!_hideLegacyHighlightMesh && _highlightRoot != null) {
                _highlightRoot.SetActive(true);
                if (_highlightRenderer != null) {
                    Color c = _validHighlightColor;
                    c.a = 0.12f;
                    _highlightRenderer.material.color = c;
                }
            } else if (_highlightRoot != null) {
                _highlightRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Bật/Tắt hiệu ứng khi người chơi rê trạm mỏ lướt trực tiếp qua ô này.
        /// </summary>
        public void SetHighlight(bool active, bool isSwap = false) {
            _isHovered = active;

            if (!active) {
                // Trở về trạng thái Resting Guide nếu vẫn đang trong lượt kéo thả
                if (_isGuideActive) {
                    if (_useMinecraftParticleGuide && _particleBoundary != null) {
                        _psMain.startColor = COLOR_RESTING_GUIDE;
                        _psEmission.rateOverTime = 14f;
                    }
                    if (!_hideLegacyHighlightMesh && _highlightRoot != null) {
                        Color c = _validHighlightColor;
                        c.a = 0.12f;
                        if (_highlightRenderer != null) _highlightRenderer.material.color = c;
                    } else if (_highlightRoot != null) {
                        _highlightRoot.SetActive(false);
                    }
                } else {
                    if (_particleBoundary != null) {
                        _particleBoundary.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    if (_highlightRoot != null) {
                        _highlightRoot.SetActive(false);
                    }
                }
                return;
            }

            // Đang Hover trực tiếp:
            if (_useMinecraftParticleGuide) {
                EnsureParticleBoundary();
                if (_particleBoundary != null) {
                    _psMain.startColor = isSwap ? COLOR_AMBER_SWAP : COLOR_EMERALD_BRIGHT;
                    _psEmission.rateOverTime = 40f; // Bùng nổ hạt rực rỡ báo hiệu chuẩn bị hạ cánh
                    if (!_particleBoundary.isPlaying) {
                        _particleBoundary.Play();
                    }
                }
            }

            if (!_hideLegacyHighlightMesh && _highlightRoot != null) {
                _highlightRoot.SetActive(true);
                if (_highlightRenderer != null) {
                    _highlightRenderer.material.color = isSwap ? _swapHighlightColor : _validHighlightColor;
                }
            } else if (_highlightRoot != null) {
                _highlightRoot.SetActive(false);
            }
        }

        private void EnsureParticleBoundary() {
            if (_particleBoundary != null) return;

            Transform existing = transform.Find("[MinecraftPlotBoundary]");
            if (existing != null) {
                _particleBoundary = existing.GetComponent<ParticleSystem>();
            }

            if (_particleBoundary == null) {
                GameObject pGo = new GameObject("[MinecraftPlotBoundary]");
                pGo.transform.SetParent(transform, false);
                pGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                pGo.transform.localRotation = Quaternion.identity;

                _particleBoundary = pGo.AddComponent<ParticleSystem>();
                var renderer = pGo.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = -5;

                _psMain = _particleBoundary.main;
                _psMain.loop = true;
                _psMain.playOnAwake = false;
                _psMain.duration = 1.0f;
                _psMain.startLifetime = 0.9f;
                _psMain.startSpeed = 0.08f;
                _psMain.startSize = 0.16f; // Hạt voxel vuông nhỏ xinh xắn
                _psMain.startColor = COLOR_RESTING_GUIDE;
                _psMain.simulationSpace = ParticleSystemSimulationSpace.World;
                _psMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
                _psMain.maxParticles = 60;

                _psShape = _particleBoundary.shape;
                _psShape.enabled = true;
                _psShape.shapeType = ParticleSystemShapeType.Box;
                _psShape.boxThickness = new Vector3(1f, 0f, 1f); // Chỉ phát hạt từ 4 mép viền ngoài!
                float sizeX = Mathf.Max(1.5f, _plotSize.x);
                float sizeZ = Mathf.Max(1.5f, _plotSize.z);
                _psShape.scale = new Vector3(sizeX, 0.05f, sizeZ);

                _psEmission = _particleBoundary.emission;
                _psEmission.enabled = true;
                _psEmission.rateOverTime = 14f;

                var colModule = _particleBoundary.colorOverLifetime;
                colModule.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0f, 1f) }
                );
                colModule.color = grad;
            } else {
                _psMain = _particleBoundary.main;
                _psEmission = _particleBoundary.emission;
                _psShape = _particleBoundary.shape;
            }
        }

        /// <summary>
        /// Tìm ô quy hoạch gần nhất với một vị trí trong không gian thế giới.
        /// </summary>
        /// <summary>
        /// Kiểm tra xem một tọa độ thế giới có nằm trong khu vực ô đất (Bounding Box của Plot) không.
        /// </summary>
        public bool IsInsidePlot(Vector3 worldPos) {
            Transform anchor = SnapAnchor;
            Vector3 center = anchor.position;
            Vector3 diff = worldPos - center;
            diff.y = 0;

            // Chuyển sang hệ tọa độ cục bộ của ô đất để tính đúng hướng xoay
            Vector3 localDiff = anchor.InverseTransformDirection(diff);
            float halfX = Mathf.Max(0.8f, _plotSize.x * 0.5f);
            float halfZ = Mathf.Max(0.8f, _plotSize.z * 0.5f);

            return Mathf.Abs(localDiff.x) <= halfX && Mathf.Abs(localDiff.z) <= halfZ;
        }

        /// <summary>
        /// Tìm ô quy hoạch tại một vị trí trong không gian thế giới.
        /// Ưu tiên nhận diện ngay khi con trỏ lướt vào khu vực (Bounding Box) của ô đất,
        /// sau đó mới xét đến khoảng cách gần nhất xung quanh mép ô.
        /// </summary>
        public static WorkstationPlot FindNearestPlot(Vector3 worldPos, float maxDistance = 2.2f) {
            // 1. Ưu tiên tuyệt đối ô mà con trỏ đang nằm TRỰC TIẾP bên trong phạm vi mặt nền của nó
            for (int i = 0; i < s_allPlots.Count; i++) {
                WorkstationPlot p = s_allPlots[i];
                if (p != null && p.IsInsidePlot(worldPos)) {
                    return p;
                }
            }

            // 2. Nếu con trỏ lướt sát mép ngoài ô đất, tìm ô gần nhất theo tâm ô đất (SnapAnchor)
            WorkstationPlot bestPlot = null;
            float minSqrDist = maxDistance * maxDistance;

            for (int i = 0; i < s_allPlots.Count; i++) {
                WorkstationPlot p = s_allPlots[i];
                if (p == null) continue;

                Vector3 diff = p.SnapAnchor.position - worldPos;
                diff.y = 0; // Chỉ tính khoảng cách 2D trên mặt sàn
                float sqrDist = diff.sqrMagnitude;

                if (sqrDist <= minSqrDist) {
                    minSqrDist = sqrDist;
                    bestPlot = p;
                }
            }

            return bestPlot;
        }

        /// <summary>
        /// Tìm ô quy hoạch theo PlotId hoặc tên GameObject (phục vụ load savegame).
        /// </summary>
        public static WorkstationPlot FindPlotById(string id) {
            if (string.IsNullOrEmpty(id)) return null;

            // Đảm bảo s_allPlots được nạp đầy đủ kể cả khi truy vấn trước OnEnable
            if (s_allPlots.Count == 0) {
                WorkstationPlot[] found = FindObjectsByType<WorkstationPlot>(FindObjectsInactive.Include);
                for (int i = 0; i < found.Length; i++) {
                    if (found[i] != null && !s_allPlots.Contains(found[i])) {
                        s_allPlots.Add(found[i]);
                    }
                }
            }

            // 1. Ưu tiên tìm theo tên GameObject (đảm bảo tính độc nhất trong Scene)
            for (int i = 0; i < s_allPlots.Count; i++) {
                if (s_allPlots[i] != null && s_allPlots[i].gameObject.name.Equals(id, StringComparison.OrdinalIgnoreCase)) {
                    return s_allPlots[i];
                }
            }

            // 2. Tìm theo PlotId
            for (int i = 0; i < s_allPlots.Count; i++) {
                if (s_allPlots[i] != null && !string.IsNullOrEmpty(s_allPlots[i].PlotId) && s_allPlots[i].PlotId.Equals(id, StringComparison.OrdinalIgnoreCase)) {
                    return s_allPlots[i];
                }
            }

            // 3. Fallback: Nếu id có chứa tên hoặc ngược lại
            for (int i = 0; i < s_allPlots.Count; i++) {
                if (s_allPlots[i] != null) {
                    string gName = s_allPlots[i].gameObject.name;
                    if (id.EndsWith(gName, StringComparison.OrdinalIgnoreCase) || gName.EndsWith(id, StringComparison.OrdinalIgnoreCase)) {
                        return s_allPlots[i];
                    }
                }
            }

            return null;
        }

        private System.Collections.IEnumerator AnimateSnap(MiningStation station, Vector3 endPos, Quaternion endRot, float duration) {
            if (station == null) yield break;
            Transform target = station.transform;
            Vector3 startPos = target.position;
            Quaternion startRot = target.rotation;
            float elapsed = 0f;

            while (elapsed < duration && target != null) {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                target.position = Vector3.Lerp(startPos, endPos, t);
                target.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            if (target != null) {
                target.position = endPos;
                target.rotation = endRot;
            }

            if (station != null) {
                station.IsBeingDragged = false;
                station.NotifyPlacedDown();
            }
        }

        private void OnDrawGizmos() {
            Transform anchor = SnapAnchor;
            Vector3 center = anchor.position + Vector3.up * (_plotSize.y * 0.5f);

            // Xanh lá = Ô đang trống, Vàng = Ô đã có mỏ
            Gizmos.color = IsEmpty ? new Color(0.1f, 0.9f, 0.3f, 0.4f) : new Color(1f, 0.8f, 0.1f, 0.4f);
            Gizmos.DrawCube(center, _plotSize);

            Gizmos.color = IsEmpty ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, _plotSize);

            // Vị trí mục tiêu của trạm mỏ (đã cộng offset tùy chỉnh của khu)
            Vector3 targetPos = StationTargetPosition;
            Quaternion targetRot = StationTargetRotation;

            // Khối preview vị trí trạm mỏ sẽ hạ cánh
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(targetPos + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));

            // Mũi tên chỉ hướng quay mặt của quầy mỏ
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(targetPos + Vector3.up * 0.5f, targetRot * Vector3.forward * 1.2f);
        }
    }
}
