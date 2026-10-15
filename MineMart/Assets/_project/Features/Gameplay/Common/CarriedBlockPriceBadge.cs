using MineMart.Core.Math;
using MineMart.Data;
using MineMart.Gameplay.Mining;
using TMPro;
using UnityEngine;

namespace MineMart.Gameplay.Common {
    /// <summary>
    /// Huy hiệu nhãn giá tiền bồng bềnh đi theo khối Block khi được Người chơi hoặc Thợ vác trên tay.
    /// Tự động hiển thị chính xác giá bán của Block (bao gồm cả nhân hệ số cấp quầy & nâng cấp map).
    /// Luôn xoay mặt về phía Camera (Billboard) và nhấp nhô nhẹ nhàng theo nhịp bước đi.
    /// </summary>
    public class CarriedBlockPriceBadge : MonoBehaviour {
        private TextMeshPro _tmp;
        private Camera _mainCamera;
        private BlockDataSO _blockData;
        private const float DEFAULT_LOCAL_Y = 0.55f;

        private void Awake() {
            EnsureTextMeshPro();
            _mainCamera = Camera.main;
        }

        private void EnsureTextMeshPro() {
            if (_tmp == null) {
                _tmp = GetComponent<TextMeshPro>();
                if (_tmp == null) {
                    _tmp = gameObject.AddComponent<TextMeshPro>();
                }
            }

            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.fontSize = 3.6f;
            _tmp.fontStyle = FontStyles.Bold;
            _tmp.outlineWidth = 0.32f;
            _tmp.outlineColor = new Color32(15, 23, 42, 255); // Viền đen sẫm sắc nét
            _tmp.color = new Color(1.0f, 0.94f, 0.35f, 1f); // Màu vàng tươi óng ánh
        }

        /// <summary>
        /// Gắn nhãn giá tiền lên khối block đang mang vác.
        /// </summary>
        public static CarriedBlockPriceBadge Attach(GameObject blockInstance, BlockDataSO blockData) {
            if (blockInstance == null) return null;

            CarriedBlockPriceBadge badge = blockInstance.GetComponentInChildren<CarriedBlockPriceBadge>(true);
            if (badge == null) {
                GameObject badgeGo = new GameObject("CarriedPriceBadge");
                badgeGo.transform.SetParent(blockInstance.transform, false);
                badgeGo.transform.localPosition = new Vector3(0f, DEFAULT_LOCAL_Y, 0f);
                badgeGo.transform.localRotation = Quaternion.identity;
                badgeGo.transform.localScale = Vector3.one;

                badge = badgeGo.AddComponent<CarriedBlockPriceBadge>();
            }

            badge.SetBlockData(blockData);
            return badge;
        }

        /// <summary>
        /// Tháo gỡ nhãn giá tiền khi block được đặt xuống bàn trung chuyển hoặc giao hàng.
        /// </summary>
        public static void Detach(GameObject blockInstance) {
            if (blockInstance == null) return;
            CarriedBlockPriceBadge badge = blockInstance.GetComponentInChildren<CarriedBlockPriceBadge>(true);
            if (badge != null) {
                Destroy(badge.gameObject);
            }
        }

        public void SetBlockData(BlockDataSO blockData) {
            _blockData = blockData;
            UpdatePriceDisplay();
        }

        public void UpdatePriceDisplay() {
            EnsureTextMeshPro();
            if (_blockData == null) return;

            double price = MiningStation.GetSellPriceForBlock(_blockData);
            string formatted = NumberFormatter.Format(price);
            // Dùng rich text tạo pill tag nền tối bo nhẹ bảo vệ độ tương phản trên mọi màu sắc của block
            _tmp.text = $"<mark=#0f172ab0 padding=\"16,16,6,6\"><color=#FFE54C>${formatted}</color></mark>";
        }

        private void LateUpdate() {
            if (_mainCamera == null) {
                _mainCamera = Camera.main;
            }

            if (_mainCamera != null) {
                // Luôn xoay mặt vuông góc với góc nhìn camera
                transform.rotation = _mainCamera.transform.rotation;
            }

            // Nhấp nhô bồng bềnh nhẹ nhàng trên đầu block
            float floatY = DEFAULT_LOCAL_Y + Mathf.Sin(Time.time * 6.5f) * 0.04f;
            transform.localPosition = new Vector3(0f, floatY, 0f);
        }
    }
}
