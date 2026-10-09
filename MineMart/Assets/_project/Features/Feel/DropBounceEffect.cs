using System;
using System.Collections;
using UnityEngine;

namespace MineMart.Feel {
    /// <summary>
    /// Hiệu ứng Rơi nảy đàn hồi từ trên cao xuống (Drop & Bounce Effect).
    /// Áp dụng thống nhất chuẩn Game Feel Idle Tycoon cho:
    /// 1. Quầy mỏ mới sinh ra trên ô đất (MiningStation).
    /// 2. Khối block mỏ phụ khi mở thêm slot (MiningSlot VisualRoot).
    /// 3. Nhân viên mới được kích hoạt từ Pool (WorkerAgent).
    /// 4. Bàn ghế ngoài trời / Props của khách hàng khi mở cấp Map (Customer Outdoor Tables).
    /// </summary>
    public class DropBounceEffect : MonoBehaviour {
        /// <summary>
        /// Kích hoạt hiệu ứng rơi từ trên cao xuống một vị trí Thế Giới (World Position).
        /// </summary>
        public static DropBounceEffect Play(Transform target, Vector3 targetWorldPos, float dropHeight = 3.5f, float duration = 0.45f, Action onComplete = null) {
            if (target == null) return null;

            var runner = target.GetComponent<DropBounceEffect>();
            if (runner == null) {
                runner = target.gameObject.AddComponent<DropBounceEffect>();
            }

            runner.StopAllCoroutines();
            runner.StartCoroutine(runner.AnimateDropWorld(targetWorldPos, dropHeight, duration, onComplete));
            return runner;
        }

        /// <summary>
        /// Kích hoạt hiệu ứng rơi từ trên cao xuống vị trí Cục Bộ (Local Position) - dành cho các object con.
        /// </summary>
        public static DropBounceEffect PlayLocal(Transform target, Vector3 targetLocalPos, float dropHeight = 2.5f, float duration = 0.4f, Action onComplete = null) {
            if (target == null) return null;

            var runner = target.GetComponent<DropBounceEffect>();
            if (runner == null) {
                runner = target.gameObject.AddComponent<DropBounceEffect>();
            }

            runner.StopAllCoroutines();
            runner.StartCoroutine(runner.AnimateDropLocal(targetLocalPos, dropHeight, duration, onComplete));
            return runner;
        }

        private IEnumerator AnimateDropWorld(Vector3 targetPos, float dropHeight, float duration, Action onComplete) {
            Vector3 originalScale = transform.localScale;
            Vector3 startPos = targetPos + Vector3.up * dropHeight;
            transform.position = startPos;

            float elapsed = 0f;
            float fallDuration = duration * 0.58f;
            float bounceDuration = duration - fallDuration;

            // GIAI ĐOẠN 1: Rơi tự do từ trên cao xuống (Gia tốc trọng trường Ease-In Quad)
            while (elapsed < fallDuration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                // Ease-In Quad: t * t (rơi nhanh dần đều)
                float curve = t * t;
                transform.position = Vector3.Lerp(startPos, targetPos, curve);

                // Kéo dãn nhẹ theo chiều thẳng đứng khi đang rơi
                float stretchY = Mathf.Lerp(1f, 1.12f, t);
                float squashXZ = Mathf.Lerp(1f, 0.94f, t);
                transform.localScale = new Vector3(originalScale.x * squashXZ, originalScale.y * stretchY, originalScale.z * squashXZ);

                yield return null;
            }

            transform.position = targetPos;

            // GIAI ĐOẠN 2: Tiếp đất & Nảy đàn hồi (Squash & Damped Bounce)
            elapsed = 0f;
            float bouncePeak = Mathf.Max(0.2f, dropHeight * 0.16f);

            while (elapsed < bounceDuration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDuration);

                // Đường cong nảy: sin arc suy giảm (damped wave)
                float bounceArc = Mathf.Sin(t * Mathf.PI);
                float damping = 1f - t;
                float currentBounceY = bounceArc * bouncePeak * damping;
                transform.position = targetPos + Vector3.up * currentBounceY;

                // Biến dạng đàn hồi khi va chạm (Squash tại thời điểm t = 0 -> 0.3)
                float scaleT;
                if (t < 0.35f) {
                    float squashPhase = t / 0.35f;
                    // Lún xuống (squash)
                    float sY = Mathf.Lerp(0.82f, 1.08f, squashPhase);
                    float sXZ = Mathf.Lerp(1.15f, 0.96f, squashPhase);
                    transform.localScale = new Vector3(originalScale.x * sXZ, originalScale.y * sY, originalScale.z * sXZ);
                } else {
                    float settlePhase = (t - 0.35f) / 0.65f;
                    scaleT = Mathf.SmoothStep(0f, 1f, settlePhase);
                    transform.localScale = Vector3.Lerp(new Vector3(originalScale.x * 0.96f, originalScale.y * 1.08f, originalScale.z * 0.96f), originalScale, scaleT);
                }

                yield return null;
            }

            // Hoàn tất an toàn
            transform.position = targetPos;
            transform.localScale = originalScale;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateDropLocal(Vector3 targetLocalPos, float dropHeight, float duration, Action onComplete) {
            Vector3 originalScale = transform.localScale;
            Vector3 startLocalPos = targetLocalPos + Vector3.up * dropHeight;
            transform.localPosition = startLocalPos;

            float elapsed = 0f;
            float fallDuration = duration * 0.58f;
            float bounceDuration = duration - fallDuration;

            // GIAI ĐOẠN 1: Rơi cục bộ
            while (elapsed < fallDuration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                float curve = t * t;
                transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, curve);

                float stretchY = Mathf.Lerp(1f, 1.12f, t);
                float squashXZ = Mathf.Lerp(1f, 0.94f, t);
                transform.localScale = new Vector3(originalScale.x * squashXZ, originalScale.y * stretchY, originalScale.z * squashXZ);

                yield return null;
            }

            transform.localPosition = targetLocalPos;

            // GIAI ĐOẠN 2: Tiếp đất & Nảy
            elapsed = 0f;
            float bouncePeak = Mathf.Max(0.15f, dropHeight * 0.16f);

            while (elapsed < bounceDuration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDuration);

                float bounceArc = Mathf.Sin(t * Mathf.PI);
                float damping = 1f - t;
                float currentBounceY = bounceArc * bouncePeak * damping;
                transform.localPosition = targetLocalPos + Vector3.up * currentBounceY;

                if (t < 0.35f) {
                    float squashPhase = t / 0.35f;
                    float sY = Mathf.Lerp(0.82f, 1.08f, squashPhase);
                    float sXZ = Mathf.Lerp(1.15f, 0.96f, squashPhase);
                    transform.localScale = new Vector3(originalScale.x * sXZ, originalScale.y * sY, originalScale.z * sXZ);
                } else {
                    float settlePhase = (t - 0.35f) / 0.65f;
                    float scaleT = Mathf.SmoothStep(0f, 1f, settlePhase);
                    transform.localScale = Vector3.Lerp(new Vector3(originalScale.x * 0.96f, originalScale.y * 1.08f, originalScale.z * 0.96f), originalScale, scaleT);
                }

                yield return null;
            }

            transform.localPosition = targetLocalPos;
            transform.localScale = originalScale;
            onComplete?.Invoke();
        }
    }
}
