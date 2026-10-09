using System;
using MineMart.Core.Math;
using MineMart.Data;
using MineMart.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Thẻ hiển thị một hạng mục Nâng Cấp Bản Đồ (Map Upgrade Card) trong danh sách cuộn dọc.
    /// Hiển thị Icon, Tên nâng cấp, Mô tả công dụng, Giá vàng và Nút Mua.
    /// Tự động đổi trạng thái sang "ĐÃ MUA" khi người chơi đã sở hữu.
    /// </summary>
    public class MapUpgradeCardUI : MonoBehaviour {
        [Header("Card Visuals")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Header("Buy Button & Cost")]
        [SerializeField] private Button _buyButton;
        [SerializeField] private Image _buttonBackground;
        [SerializeField] private TextMeshProUGUI _costText;

        [Header("Colors")]
        [SerializeField] private Color _canAffordColor = new Color(0.1f, 0.75f, 0.35f, 1f);     // Xanh lá sáng khi đủ tiền
        [SerializeField] private Color _cannotAffordColor = new Color(0.45f, 0.5f, 0.55f, 1f); // Xám khi thiếu tiền
        [SerializeField] private Color _purchasedColor = new Color(0.2f, 0.45f, 0.7f, 0.85f);  // Xanh dương trầm khi đã sở hữu

        private MapUpgradeConfig _config;
        private Action<MapUpgradeConfig> _onBuyCallback;
        private bool _isPurchased = false;

        public MapUpgradeConfig Config => _config;
        public bool IsPurchased => _isPurchased;

        public void Bind(
            MapUpgradeConfig config,
            bool isPurchased,
            Action<MapUpgradeConfig> onBuyCallback) {

            _config = config;
            _isPurchased = isPurchased;
            _onBuyCallback = onBuyCallback;

            if (_titleText != null) {
                _titleText.text = config != null ? config.DisplayName : "Nâng Cấp";
            }

            if (_descriptionText != null) {
                _descriptionText.text = config != null ? config.Description : "";
            }

            if (_iconImage != null) {
                if (config != null && config.Icon != null) {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.sprite = config.Icon;
                } else {
                    _iconImage.gameObject.SetActive(false);
                }
            }

            if (_buyButton != null) {
                _buyButton.onClick.RemoveAllListeners();
                _buyButton.onClick.AddListener(HandleClick);
            }

            RefreshAffordability();
        }

        public void RefreshAffordability() {
            if (_config == null) return;

            if (_isPurchased) {
                // Đã mua rồi -> Khóa nút và đổi text thành ĐÃ MUA
                if (_costText != null) {
                    _costText.text = "ĐÃ SỞ HỮU";
                }
                if (_buyButton != null) {
                    _buyButton.interactable = false;
                }
                if (_buttonBackground != null) {
                    _buttonBackground.color = _purchasedColor;
                }
                return;
            }

            // Chưa mua -> Kiểm tra số vàng hiện có
            double currentGold = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentGold : 0;
            double price = _config.Price;
            bool canAfford = price <= 0 || currentGold >= price;

            if (_costText != null) {
                if (price <= 0) {
                    _costText.text = "MIỄN PHÍ";
                } else {
                    _costText.text = $"{NumberFormatter.Format(price)} Vàng";
                }
            }

            if (_buyButton != null) {
                _buyButton.interactable = canAfford;
            }

            if (_buttonBackground != null) {
                _buttonBackground.color = canAfford ? _canAffordColor : _cannotAffordColor;
            }
        }

        public void MarkPurchased() {
            _isPurchased = true;
            RefreshAffordability();
        }

        private void HandleClick() {
            if (_config != null && !_isPurchased) {
                _onBuyCallback?.Invoke(_config);
            }
        }

        /// <summary>
        /// Tự động dò tìm các component con nếu chưa được gán thủ công trên Inspector.
        /// </summary>
        public void AutoFindReferences() {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts) {
                string lower = t.gameObject.name.ToLower();
                if (_titleText == null && (lower.Contains("title") || lower.Contains("name") || lower.Contains("ten"))) _titleText = t;
                else if (_descriptionText == null && (lower.Contains("desc") || lower.Contains("info") || lower.Contains("mota"))) _descriptionText = t;
                else if (_costText == null && (lower.Contains("cost") || lower.Contains("price") || lower.Contains("gold") || lower.Contains("gia"))) _costText = t;
            }

            if (_buyButton == null) {
                _buyButton = GetComponentInChildren<Button>(true);
            }

            if (_buyButton != null && _buttonBackground == null) {
                _buttonBackground = _buyButton.GetComponent<Image>();
            }

            if (_iconImage == null) {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images) {
                    string lower = img.gameObject.name.ToLower();
                    if (lower.Contains("icon") || lower.Contains("avatar") || lower.Contains("hinh")) {
                        _iconImage = img;
                        break;
                    }
                }
            }
        }
    }
}
