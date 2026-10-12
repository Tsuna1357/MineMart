using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Component quản lý hiển thị 1 ô nâng cấp (Card) dạng hình vuông trong Cây Nâng Cấp EUGT.
    /// - Hiển thị trong lưới 2 cột (GridLayoutGroup).
    /// - Khi khóa: Nền tối, hiện biểu tượng Móc Khóa 🔒 và chữ Khóa.
    /// - Khi mở khóa: Nền sáng, hiện biểu tượng Icon/Sprite, Tên và Cấp độ (Lv. X hoặc MAX).
    /// - Bấm vào ô mở khóa sẽ mở popup chi tiết để nâng cấp.
    /// - Dễ dàng mở Prefab trong Unity để kéo chỉnh RectTransform, Font, Màu sắc tùy ý.
    /// </summary>
    public class EmeraldUpgradeNodeItemUI : MonoBehaviour {
        [Header("Containers & Visuals")]
        [SerializeField] private Image _bgImage;
        [SerializeField] private Button _cardButton;

        [Header("Icon Box")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _iconText;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelBadgeText;
        [SerializeField] private Image _levelBadgeBg;

        [Header("Lock State")]
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private TextMeshProUGUI _lockText;

        [Header("Colors Preset")]
        [SerializeField] private Color _unlockedBgColor = new Color(0.14f, 0.18f, 0.25f, 0.95f);
        [SerializeField] private Color _lockedBgColor = new Color(0.08f, 0.10f, 0.14f, 0.85f);
        [SerializeField] private Color _activeLevelBadgeColor = new Color(0.12f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color _maxLevelBadgeColor = new Color(0.92f, 0.70f, 0.15f, 1f);
        [SerializeField] private Color _lockedBadgeColor = new Color(0.20f, 0.22f, 0.28f, 0.8f);

        private EmeraldUpgradeNode _currentNode;
        private Action<EmeraldUpgradeNode> _onClickCallback;
        private MineMart.Data.EmeraldUpgradeDataSO _currentData;
        private Action<MineMart.Data.EmeraldUpgradeDataSO> _onDataClickCallback;

        private void Awake() {
            if (_cardButton == null) _cardButton = GetComponent<Button>();
            if (_cardButton != null) {
                _cardButton.onClick.RemoveListener(HandleClick);
                _cardButton.onClick.AddListener(HandleClick);
            }
        }

        /// <summary>
        /// Gán dữ liệu cho ô nâng cấp hình vuông từ EmeraldUpgradeDataSO.
        /// </summary>
        public void Setup(MineMart.Data.EmeraldUpgradeDataSO data, bool isUnlocked, int level, Action<MineMart.Data.EmeraldUpgradeDataSO> onClick) {
            _currentData = data;
            _onDataClickCallback = onClick;
            _currentNode = null;
            _onClickCallback = null;

            if (data == null) return;

            bool isMax = level >= data.MaxLevel;

            if (_bgImage != null) {
                _bgImage.color = isUnlocked ? _unlockedBgColor : _lockedBgColor;
            }

            if (_iconImage != null) {
                if (data.CustomIcon != null) {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.sprite = data.CustomIcon;
                    if (_iconText != null) _iconText.gameObject.SetActive(false);
                } else {
                    _iconImage.gameObject.SetActive(false);
                    if (_iconText != null) {
                        _iconText.gameObject.SetActive(true);
                        _iconText.text = isUnlocked ? data.IconSymbol : "🔒";
                        _iconText.color = isUnlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);
                    }
                }
            } else if (_iconText != null) {
                _iconText.text = isUnlocked ? data.IconSymbol : "🔒";
                _iconText.color = isUnlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);
            }

            if (_nameText != null) {
                _nameText.text = isUnlocked ? data.DisplayName : "???";
                _nameText.color = isUnlocked ? Color.white : new Color(0.55f, 0.60f, 0.70f);
            }

            if (_levelBadgeText != null) {
                if (!isUnlocked) {
                    _levelBadgeText.text = "🔒 KHÓA";
                } else if (isMax) {
                    _levelBadgeText.text = "MAX";
                } else {
                    _levelBadgeText.text = $"Lv. {level}";
                }
            }

            if (_levelBadgeBg != null) {
                if (!isUnlocked) {
                    _levelBadgeBg.color = _lockedBadgeColor;
                } else if (isMax) {
                    _levelBadgeBg.color = _maxLevelBadgeColor;
                } else {
                    _levelBadgeBg.color = _activeLevelBadgeColor;
                }
            }

            if (_lockOverlay != null) {
                _lockOverlay.SetActive(!isUnlocked);
            }
            if (_lockText != null) {
                _lockText.gameObject.SetActive(!isUnlocked);
            }
        }

        /// <summary>
        /// Gán dữ liệu cho ô nâng cấp hình vuông (tương thích EmeraldUpgradeNode).
        /// </summary>
        public void Setup(EmeraldUpgradeNode node, bool isUnlocked, int level, Action<EmeraldUpgradeNode> onClick) {
            _currentNode = node;
            _onClickCallback = onClick;
            _currentData = null;
            _onDataClickCallback = null;

            if (node == null) return;

            bool isMax = level >= node.maxLevel;

            // 1. Màu nền
            if (_bgImage != null) {
                _bgImage.color = isUnlocked ? _unlockedBgColor : _lockedBgColor;
            }

            // 2. Icon hiển thị
            if (_iconImage != null) {
                if (node.customIcon != null) {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.sprite = node.customIcon;
                    if (_iconText != null) _iconText.gameObject.SetActive(false);
                } else {
                    _iconImage.gameObject.SetActive(false);
                    if (_iconText != null) {
                        _iconText.gameObject.SetActive(true);
                        _iconText.text = isUnlocked ? node.iconSymbol : "🔒";
                        _iconText.color = isUnlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);
                    }
                }
            } else if (_iconText != null) {
                _iconText.text = isUnlocked ? node.iconSymbol : "🔒";
                _iconText.color = isUnlocked ? Color.white : new Color(0.6f, 0.65f, 0.75f, 0.8f);
            }

            // 3. Tên nâng cấp
            if (_nameText != null) {
                _nameText.text = isUnlocked ? node.displayName : "???";
                _nameText.color = isUnlocked ? Color.white : new Color(0.55f, 0.60f, 0.70f);
            }

            // 4. Badge Level
            if (_levelBadgeText != null) {
                if (!isUnlocked) {
                    _levelBadgeText.text = "🔒 KHÓA";
                } else if (isMax) {
                    _levelBadgeText.text = "MAX";
                } else {
                    _levelBadgeText.text = $"Lv. {level}";
                }
            }

            if (_levelBadgeBg != null) {
                if (!isUnlocked) {
                    _levelBadgeBg.color = _lockedBadgeColor;
                } else if (isMax) {
                    _levelBadgeBg.color = _maxLevelBadgeColor;
                } else {
                    _levelBadgeBg.color = _activeLevelBadgeColor;
                }
            }

            // 5. Lock Overlay nếu có
            if (_lockOverlay != null) {
                _lockOverlay.SetActive(!isUnlocked);
            }
            if (_lockText != null) {
                _lockText.gameObject.SetActive(!isUnlocked);
            }
        }

        private void HandleClick() {
            if (_currentData != null) {
                _onDataClickCallback?.Invoke(_currentData);
                return;
            }
            if (_currentNode != null) {
                _onClickCallback?.Invoke(_currentNode);
            }
        }
    }
}
