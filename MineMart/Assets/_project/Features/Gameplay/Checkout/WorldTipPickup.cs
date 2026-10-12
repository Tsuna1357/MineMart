using System.Collections;
using MineMart.Economy;
using MineMart.Feel;
using MineMart.Gameplay.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MineMart.Gameplay.Checkout {
    /// <summary>
    /// Vật phẩm tiền Tip xuất hiện trong World Space tại quầy thanh toán (Checkout / Table).
    /// Hỗ trợ 2 cách nhặt:
    /// 1. Tự động hút về người chơi khi đến gần (CHỈ kích hoạt khi người chơi tự điều khiển tay, TẮT khi ở chế độ AI).
    /// 2. Bấm / chạm thẳng ngón tay hoặc chuột vào Tip trên màn hình -> Nhặt ngay lập tức từ xa (HOẠT ĐỘNG Ở MỌI THỜI ĐIỂM).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldTipPickup : MonoBehaviour, IPointerClickHandler {
        [Header("Tip Settings")]
        [Tooltip("Số tiền vàng người chơi nhận được khi nhặt tip")]
        [SerializeField] private double _tipAmount = 10;

        [Tooltip("Khoảng cách tối đa để nhân vật tự động hút tiền tip vào người (mét) - Chỉ kích hoạt ngoài AI mode")]
        [SerializeField] private float _pickupMagnetRadius = 1.85f;

        [Tooltip("Tốc độ bay hút về phía người chơi")]
        [SerializeField] private float _flySpeed = 12f;

        [Header("Visual & Animation")]
        [Tooltip("Transform chứa visual hiển thị để xoay tròn")]
        [SerializeField] private Transform _visualHolder;

        [Tooltip("Tốc độ xoay của đồng tiền tip")]
        [SerializeField] private float _rotateSpeed = 120f;

        [Tooltip("Biên độ bập bồng lên xuống")]
        [SerializeField] private float _bobAmplitude = 0.08f;

        [Tooltip("Tần số bập bồng")]
        [SerializeField] private float _bobFrequency = 3.5f;

        private Collider _collider;
        private Vector3 _initialLocalPos;
        private bool _isCollected = false;
        private bool _isFlyingToPlayer = false;

        public double TipAmount => _tipAmount;
        public bool IsCollected => _isCollected;

        private void Awake() {
            _collider = GetComponent<Collider>();
            if (_collider != null) {
                _collider.isTrigger = true;
            }
            if (_visualHolder == null) {
                _visualHolder = transform;
            }
            _initialLocalPos = _visualHolder.localPosition;
        }

        private void Start() {
            // Hiệu ứng nảy nhẹ lúc xuất hiện
            StartCoroutine(SpawnPopRoutine());
        }

        /// <summary>
        /// Khởi tạo số tiền tip và visual tùy biến (nếu có).
        /// </summary>
        public void Initialize(double amount) {
            _tipAmount = amount;
        }

        private IEnumerator SpawnPopRoutine() {
            Vector3 originalScale = transform.localScale;
            transform.localScale = Vector3.zero;

            float elapsed = 0f;
            float duration = 0.25f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.35f;
                transform.localScale = originalScale * (t * scale);
                yield return null;
            }
            transform.localScale = originalScale;
        }

        private void Update() {
            if (_isCollected) return;

            // 1. Hiệu ứng xoay tròn và bập bồng tại chỗ
            if (!_isFlyingToPlayer && _visualHolder != null) {
                _visualHolder.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime, Space.World);
                float bobOffset = Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
                _visualHolder.localPosition = _initialLocalPos + new Vector3(0f, bobOffset, 0f);
            }

            // 2. Cách 1: Tự động hút về người chơi khi đến gần (CHỈ KÍCH HOẠT KHI NGƯỜI CHƠI TỰ ĐIỀU KHIỂN TAY, TẮT KHI Ở CHẾ ĐỘ AI)
            if (PlayerController.Instance != null && !PlayerController.Instance.IsAutoPiloting) {
                float distSqr = (PlayerController.Instance.transform.position - transform.position).sqrMagnitude;
                if (distSqr <= _pickupMagnetRadius * _pickupMagnetRadius) {
                    Collect();
                    return;
                }
            }

            // 3. Cách 2: Bấm thẳng tay vào tip (quét qua New Input System và khoảng cách màn hình)
            CheckDirectPointerClick();
        }

        /// <summary>
        /// Bắt sự kiện EventSystem click (nếu có PhysicsRaycaster trên Camera).
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) {
            if (!_isCollected) {
                Collect();
            }
        }

        /// <summary>
        /// Cách 2: Bấm / tap trực tiếp vào Tip bằng chuột hoặc màn hình cảm ứng (hoạt động mọi lúc kể cả khi AI đang chạy).
        /// </summary>
        private void OnMouseDown() {
            if (!_isCollected) {
                Collect();
            }
        }

        private void CheckDirectPointerClick() {
            Vector2 pointerPos = Vector2.zero;
            bool isPointerDown = false;

            // Đọc Input từ New Input System
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0) {
                var touch = Touchscreen.current.touches[0];
                if (touch.press.wasPressedThisFrame) {
                    isPointerDown = true;
                    pointerPos = touch.position.ReadValue();
                }
            } else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
                isPointerDown = true;
                pointerPos = Mouse.current.position.ReadValue();
            }

            if (!isPointerDown) return;

            // Bỏ qua nếu bấm vào các nút UI trên Canvas
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            // 1. Quét khoảng cách trên màn hình (Screen Space Touch Radius):
            // Giúp người chơi bấm chuột hoặc chạm ngón tay cực kỳ dễ trúng, không lo hụt pixel
            Vector3 screenPoint = cam.WorldToScreenPoint(transform.position);
            if (screenPoint.z > 0) { // Nằm phía trước camera
                float screenDist = Vector2.Distance(pointerPos, new Vector2(screenPoint.x, screenPoint.y));
                if (screenDist <= 85f) { // Bán kính chạm 85 pixel rộng rãi, thoải mái
                    Collect();
                    return;
                }
            }

            // 2. Quét tia Raycast xuyên vật cản (RaycastAll ép buộc nhận Trigger Collider)
            Ray ray = cam.ScreenPointToRay(pointerPos);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++) {
                if (hits[i].collider == _collider || hits[i].transform == transform || hits[i].transform.IsChildOf(transform)) {
                    Collect();
                    return;
                }
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (_isCollected) return;

            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null) {
                // Chỉ cho phép hút tự động khi người chơi đang tự điều khiển bằng tay
                if (PlayerController.Instance != null && PlayerController.Instance.IsAutoPiloting) {
                    return;
                }
                Collect();
            }
        }

        /// <summary>
        /// Kích hoạt nhặt tiền tip: bay hút vào người chơi, cộng vàng và tiêu hủy.
        /// </summary>
        public void Collect() {
            if (_isCollected) return;
            _isCollected = true;

            StartCoroutine(FlyToPlayerAndRewardRoutine());
        }

        private IEnumerator FlyToPlayerAndRewardRoutine() {
            _isFlyingToPlayer = true;
            if (_collider != null) _collider.enabled = false;

            Transform targetTransform = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;

            float elapsed = 0f;
            float initialDist = targetTransform != null ? Vector3.Distance(startPos, targetTransform.position) : 1f;
            float duration = Mathf.Clamp(initialDist / Mathf.Max(1f, _flySpeed), 0.18f, 0.35f);

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t; // EaseInQuad

                Vector3 targetPos = targetTransform != null ? (targetTransform.position + Vector3.up * 1.0f) : startPos;
                transform.position = Vector3.Lerp(startPos, targetPos, ease);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);

                yield return null;
            }

            // Cộng tiền vàng cho người chơi
            if (EconomyManager.Instance != null && _tipAmount > 0) {
                EconomyManager.Instance.AddGold(_tipAmount);
            }

            // Hiệu ứng các hạt vàng tỏa ra nhỏ nhẹ
            TipCoinBurstEffect.Play(transform.position, null, 4);

            Destroy(gameObject);
        }

        /// <summary>
        /// Tạo một đối tượng tiền Tip trong Scene. Nếu chưa có Prefab, tự động tạo Model Fallback 3D có thể tùy chỉnh màu sắc.
        /// </summary>
        public static WorldTipPickup CreateTipInstance(Vector3 spawnPosition, double tipAmount, GameObject customPrefab = null, Color? customColor = null) {
            if (customPrefab != null) {
                GameObject instance = Instantiate(customPrefab, spawnPosition, Quaternion.identity);
                var pickup = instance.GetComponent<WorldTipPickup>();
                if (pickup == null) pickup = instance.AddComponent<WorldTipPickup>();
                pickup.Initialize(tipAmount);
                return pickup;
            }

            // Tạo model Tip Fallback 3D (Đồng xu nổi khối kèm biểu tượng kim cương ở tâm, tùy chỉnh màu sắc linh hoạt)
            GameObject tipRoot = new GameObject("WorldTip_" + (int)tipAmount);
            tipRoot.transform.position = spawnPosition;

            var boxCol = tipRoot.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(0.7f, 0.7f, 0.7f);

            GameObject visualHolder = new GameObject("VisualHolder");
            visualHolder.transform.SetParent(tipRoot.transform, false);

            Color baseColor = customColor.HasValue ? customColor.Value : new Color(1.0f, 0.85f, 0.15f, 1.0f); // Mặc định vàng gold ánh kim
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                         ?? Shader.Find("Standard") 
                         ?? Shader.Find("Sprites/Default");

            Material coinMat = new Material(shader);
            SetMaterialColor(coinMat, baseColor);

            // 1. Thân đồng tiền xu (Cylinder dẹt)
            GameObject coinBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinBody.name = "CoinBody";
            coinBody.transform.SetParent(visualHolder.transform, false);
            coinBody.transform.localScale = new Vector3(0.42f, 0.05f, 0.42f);
            coinBody.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // Nghiêng nhẹ để góc nhìn isometric thấy rõ độ dày 3D
            var coinCol = coinBody.GetComponent<Collider>();
            if (coinCol != null) Destroy(coinCol); // Dùng trigger của root

            var bodyRenderer = coinBody.GetComponent<Renderer>();
            if (bodyRenderer != null) bodyRenderer.material = coinMat;

            // 2. Biểu tượng hình thoi / kim cương nhô ở tâm đồng xu
            GameObject centerGem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerGem.name = "CenterSymbol";
            centerGem.transform.SetParent(coinBody.transform, false);
            centerGem.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            centerGem.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            centerGem.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var gemCol = centerGem.GetComponent<Collider>();
            if (gemCol != null) Destroy(gemCol);

            Material gemMat = new Material(shader);
            SetMaterialColor(gemMat, Color.Lerp(baseColor, Color.white, 0.4f)); // Màu sáng hơn tạo điểm nhấn
            var gemRenderer = centerGem.GetComponent<Renderer>();
            if (gemRenderer != null) gemRenderer.material = gemMat;

            var tipComp = tipRoot.AddComponent<WorldTipPickup>();
            tipComp._visualHolder = visualHolder.transform;
            tipComp.Initialize(tipAmount);

            return tipComp;
        }

        private static void SetMaterialColor(Material mat, Color col) {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            mat.color = col;
        }
    }
}
