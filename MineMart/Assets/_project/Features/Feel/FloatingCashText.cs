using System.Collections;
using System.Collections.Generic;
using MineMart.Core.Math;
using TMPro;
using UnityEngine;

namespace MineMart.Feel {
    /// <summary>
    /// Hiệu ứng số tiền nảy bay bổng (+$$$) hiển thị tại thế giới 3D (World Space),
    /// tự động hướng mặt về Camera (Billboard), nảy lên (Elastic Pop), lướt dần lên cao
    /// và mờ dần (Fade out). Tự động dọn dẹp không rác bộ nhớ (Zero-GC Pooling).
    /// </summary>
    public class FloatingCashText : MonoBehaviour {
        private static readonly Queue<FloatingCashText> s_pool = new Queue<FloatingCashText>();
        private static Transform s_poolContainer;

        private TextMeshPro _tmp;
        private Camera _mainCamera;
        private Coroutine _animateRoutine;

        private void Awake() {
            EnsureTextMeshPro();
        }

        private void EnsureTextMeshPro() {
            if (_tmp == null) {
                _tmp = GetComponent<TextMeshPro>();
                if (_tmp == null) {
                    _tmp = gameObject.AddComponent<TextMeshPro>();
                }
            }

            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.fontSize = 5.0f;
            _tmp.fontStyle = FontStyles.Bold;
            
            // Viền đen dày để nổi bật trên mọi nền gạch/đất/bàn
            _tmp.outlineWidth = 0.28f;
            _tmp.outlineColor = new Color32(20, 20, 20, 255);
        }

        /// <summary>
        /// Bắn số tiền bay lên tại tọa độ thế giới chỉ định.
        /// </summary>
        public static FloatingCashText Spawn(Vector3 worldPos, double amount, string prefix = "+$", Color? textColor = null) {
            string formattedAmount = NumberFormatter.Format(amount);
            Color col = textColor ?? new Color(1.0f, 0.92f, 0.22f, 1f); // Vàng tươi
            return SpawnText(worldPos, $"{prefix}{formattedAmount}", col);
        }

        /// <summary>
        /// Bắn một dòng text tùy biến bay lên (ví dụ: "+50$", "LEVEL UP!", "TIP!").
        /// </summary>
        public static FloatingCashText SpawnText(Vector3 worldPos, string content, Color textColor) {
            FloatingCashText item = GetFromPool();
            item.transform.position = worldPos + Vector3.up * 0.5f + new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
            item.Play(content, textColor);
            return item;
        }

        private static FloatingCashText GetFromPool() {
            while (s_pool.Count > 0) {
                var item = s_pool.Dequeue();
                if (item != null) {
                    item.gameObject.SetActive(true);
                    return item;
                }
            }

            if (s_poolContainer == null) {
                var go = new GameObject("[FloatingCashText_Pool]");
                DontDestroyOnLoad(go);
                s_poolContainer = go.transform;
            }

            GameObject host = new GameObject("FloatingCashText");
            host.transform.SetParent(s_poolContainer);
            return host.AddComponent<FloatingCashText>();
        }

        public void Play(string content, Color textColor) {
            EnsureTextMeshPro();

            _mainCamera = Camera.main;
            _tmp.text = content;
            _tmp.color = textColor;

            // Xoay mặt thẳng vào camera ngay frame đầu tiên
            if (_mainCamera != null) {
                transform.rotation = _mainCamera.transform.rotation;
            }

            if (_animateRoutine != null) {
                StopCoroutine(_animateRoutine);
            }
            _animateRoutine = StartCoroutine(AnimateRoutine(textColor));
        }

        private IEnumerator AnimateRoutine(Color baseColor) {
            float duration = 0.85f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + Vector3.up * 1.35f;

            transform.localScale = Vector3.zero;

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 1. Chuyển động vị trí: bay lên mượt mà theo đường cong Ease Out
                float posCurve = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.position = Vector3.Lerp(startPos, targetPos, posCurve);

                // 2. Co giãn nảy (Elastic Pop In): Phóng to 1.35x trong 20% đầu rồi về 1.0x
                float scale;
                if (t < 0.22f) {
                    float popT = t / 0.22f;
                    scale = Mathf.Sin(popT * Mathf.PI * 0.5f) * 1.35f;
                } else if (t < 0.45f) {
                    float settleT = (t - 0.22f) / 0.23f;
                    scale = Mathf.Lerp(1.35f, 1.0f, settleT);
                } else {
                    scale = 1.0f;
                }
                transform.localScale = Vector3.one * scale;

                // 3. Mờ dần (Fade out) ở 45% thời gian cuối
                float alpha = 1f;
                if (t > 0.55f) {
                    alpha = 1f - ((t - 0.55f) / 0.45f);
                }
                Color c = baseColor;
                c.a = alpha;
                _tmp.color = c;

                // 4. Billboard luôn hướng về camera
                if (_mainCamera != null) {
                    transform.rotation = _mainCamera.transform.rotation;
                }

                yield return null;
            }

            // Kết thúc: trả về pool
            gameObject.SetActive(false);
            s_pool.Enqueue(this);
            _animateRoutine = null;
        }

        private void LateUpdate() {
            if (_mainCamera != null && gameObject.activeSelf) {
                transform.rotation = _mainCamera.transform.rotation;
            }
        }
    }
}
