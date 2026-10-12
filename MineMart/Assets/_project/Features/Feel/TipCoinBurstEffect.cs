using System.Collections;
using System.Collections.Generic;
using MineMart.Core.Audio;
using UnityEngine;

namespace MineMart.Feel {
    /// <summary>
    /// Hiệu ứng các hạt vàng tròn (hoặc model tùy biến) tỏa ra và nổi lên
    /// tại vị trí TipAnchor khi người chơi / nhân viên giao block cho khách.
    /// Hỗ trợ:
    /// - Mặc định: Tự sinh các hạt vàng tròn 3D óng ánh (Golden Coins/Spheres).
    /// - Tùy biến: Cho phép gắn Prefab model 3D riêng của người dùng.
    /// - Quỹ đạo nảy & tỏa hình vòng cung (Radial Burst Arc), xoay nhẹ và tan biến êm ái.
    /// - Tích hợp phát tiếng tiền vàng leng keng qua AudioManager.
    /// </summary>
    public class TipCoinBurstEffect : MonoBehaviour {
        [Header("Settings")]
        [Tooltip("Số lượng hạt tỏa ra mỗi lần hoàn thành đơn hàng")]
        [SerializeField] private int _particleCount = 6;

        [Tooltip("Bán kính bung tỏa ra xung quanh (mét) - Tăng 50% để bung rộng rực rỡ")]
        [SerializeField] private float _burstRadius = 0.85f;

        [Tooltip("Độ cao nảy lên cực đại của hạt (mét) - Tăng 50%")]
        [SerializeField] private float _burstHeight = 1.28f;

        [Tooltip("Tổng thời gian hiệu ứng tỏa ra và nổi lên trước khi biến mất (giây)")]
        [SerializeField] private float _duration = 0.75f;

        [Tooltip("Kích thước hạt tròn vàng mặc định (mét) - Tăng 50%")]
        [SerializeField] private float _defaultParticleSize = 0.27f;

        [Tooltip("Hệ số phóng to kích thước của các hạt (áp dụng cho cả model tùy biến)")]
        [SerializeField] private float _particleScaleMultiplier = 1.5f;

        [Header("Audio (Âm Thanh Tiền Vàng)")]
        [Tooltip("Âm thanh tiếng tiền vàng kêu leng keng khi hạt bung ra (nếu để trống sẽ không phát hoặc dùng SFX mặc định)")]
        [SerializeField] private AudioClip _coinSfxClip;

        private static Material s_sharedGoldMaterial;
        public static AudioClip GlobalCoinSfxClip;

        /// <summary>
        /// Kích hoạt hiệu ứng hạt vàng tỏa ra tại một vị trí thế giới (thường là TipAnchor).
        /// Đã tinh chỉnh bung to và cao hơn 50% theo yêu cầu.
        /// </summary>
        public static void Play(Vector3 spawnPosition, GameObject customPrefab = null, int count = 6, float radius = 0.85f, float height = 1.28f, float duration = 0.75f, float scaleMultiplier = 1.5f, AudioClip sfx = null) {
            GameObject host = new GameObject("[TipCoinBurst]");
            host.transform.position = spawnPosition;
            var effect = host.AddComponent<TipCoinBurstEffect>();
            effect._particleCount = Mathf.Max(1, count);
            effect._burstRadius = radius;
            effect._burstHeight = height;
            effect._duration = duration;
            effect._particleScaleMultiplier = scaleMultiplier;
            effect._coinSfxClip = sfx != null ? sfx : GlobalCoinSfxClip;
            effect.StartBurst(customPrefab);
        }

        public void StartBurst(GameObject customPrefab) {
            StartCoroutine(BurstRoutine(customPrefab));
        }

        private IEnumerator BurstRoutine(GameObject customPrefab) {
            Vector3 origin = transform.position;

            // Phát âm thanh tiếng tiền vàng nổ bung ra phong cách Eatventure (tự động lấy từ AudioManager hoặc slot riêng, ngẫu nhiên cao độ pitch để âm thanh giòn tan)
            AudioClip clipToPlay = _coinSfxClip != null ? _coinSfxClip : (GlobalCoinSfxClip != null ? GlobalCoinSfxClip : (AudioManager.Instance != null ? AudioManager.Instance.DefaultCoinBurstClip : null));
            if (clipToPlay != null && AudioManager.Instance != null) {
                float randomPitch = UnityEngine.Random.Range(0.96f, 1.14f);
                AudioManager.Instance.PlaySfx(clipToPlay, 0.95f, randomPitch);
            }
            List<Transform> particles = new List<Transform>();
            List<Vector3> targetOffsets = new List<Vector3>();
            List<float> heightOffsets = new List<float>();
            List<Vector3> spinAxes = new List<Vector3>();
            List<Vector3> baseScales = new List<Vector3>();

            // Khởi tạo các hạt
            for (int i = 0; i < _particleCount; i++) {
                GameObject item;
                Vector3 baseScale;

                if (customPrefab != null) {
                    item = Instantiate(customPrefab, origin, Quaternion.identity, transform);
                    baseScale = item.transform.localScale * _particleScaleMultiplier;
                } else {
                    item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    item.name = $"GoldCoin_{i + 1}";
                    item.transform.SetParent(transform, false);
                    item.transform.position = origin;
                    // Hạt vàng tròn hơi dẹt nhẹ như đồng xu vàng 3D (tăng 50% kích thước)
                    baseScale = new Vector3(_defaultParticleSize, _defaultParticleSize * 0.75f, _defaultParticleSize);
                    item.transform.localScale = Vector3.zero;

                    var mr = item.GetComponent<MeshRenderer>();
                    if (mr != null) {
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        mr.material = GetOrCreateGoldMaterial();
                    }
                }

                // Xóa Collider để không chặn click hay va chạm vật lý
                Collider col = item.GetComponent<Collider>();
                if (col != null) Destroy(col);
                for (int c = 0; c < item.transform.childCount; c++) {
                    Collider childCol = item.transform.GetChild(c).GetComponent<Collider>();
                    if (childCol != null) Destroy(childCol);
                }

                // Góc tỏa ra xung quanh (phân bố đều kết hợp ngẫu nhiên nhẹ)
                float angle = (i * (360f / Mathf.Max(1, _particleCount))) + UnityEngine.Random.Range(-15f, 15f);
                float rad = angle * Mathf.Deg2Rad;
                float r = UnityEngine.Random.Range(_burstRadius * 0.65f, _burstRadius * 1.15f);
                Vector3 horizontalOffset = new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);

                float h = UnityEngine.Random.Range(_burstHeight * 0.8f, _burstHeight * 1.25f);
                Vector3 spin = new Vector3(UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(180f, 360f), UnityEngine.Random.Range(-90f, 90f));

                particles.Add(item.transform);
                targetOffsets.Add(horizontalOffset);
                heightOffsets.Add(h);
                spinAxes.Add(spin);
                baseScales.Add(baseScale);
            }

            float elapsed = 0f;
            while (elapsed < _duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);

                // Đường cong bay nảy hình cung (EaseOut cho góc tỏa ngang, Arc Sin cho cao độ bay lên)
                float horizontalProgress = Mathf.Sqrt(t); // bung ra nhanh ở đầu rồi chậm dần
                float arc = Mathf.Sin(t * Mathf.PI);     // vồng lên cao nhất ở giữa rồi hạ nhẹ

                // Tỉ lệ scale: Pop nhanh 0 -> 1.18 ở 20% đầu, giữ nguyên, rồi thu nhỏ biến mất ở 30% cuối
                float scaleMultiplier;
                if (t < 0.2f) {
                    scaleMultiplier = Mathf.Lerp(0f, 1.18f, t / 0.2f);
                } else if (t < 0.7f) {
                    scaleMultiplier = Mathf.Lerp(1.18f, 1.0f, (t - 0.2f) / 0.5f);
                } else {
                    float fadeT = (t - 0.7f) / 0.3f;
                    // Co lại mượt mà kèm nhấp nháy tan biến
                    scaleMultiplier = Mathf.Lerp(1.0f, 0f, fadeT * fadeT);
                }

                for (int i = 0; i < particles.Count; i++) {
                    Transform p = particles[i];
                    if (p == null) continue;

                    Vector3 currentPos = origin 
                        + (targetOffsets[i] * horizontalProgress) 
                        + Vector3.up * (heightOffsets[i] * arc)
                        + Vector3.up * (t * 0.42f); // hiệu ứng trôi nhẹ lên trời (float up tăng 50%)

                    p.position = currentPos;
                    p.localScale = baseScales[i] * scaleMultiplier;
                    p.Rotate(spinAxes[i] * Time.deltaTime, Space.Self);
                }

                yield return null;
            }

            // Dọn dẹp đối tượng sau khi hoàn tất hiệu ứng
            Destroy(gameObject);
        }

        private static Material GetOrCreateGoldMaterial() {
            if (s_sharedGoldMaterial == null) {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") 
                             ?? Shader.Find("Sprites/Default") 
                             ?? Shader.Find("Standard");
                s_sharedGoldMaterial = new Material(shader) {
                    name = "M_TipGoldCoin_Shared",
                    color = new Color(1.0f, 0.85f, 0.12f, 1.0f) // Màu vàng hoàng kim rực rỡ
                };
                if (s_sharedGoldMaterial.HasProperty("_Color")) s_sharedGoldMaterial.SetColor("_Color", new Color(1.0f, 0.85f, 0.12f, 1.0f));
                if (s_sharedGoldMaterial.HasProperty("_BaseColor")) s_sharedGoldMaterial.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.12f, 1.0f));
            }
            return s_sharedGoldMaterial;
        }
    }
}
