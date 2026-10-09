using System;
using MineMart.Core.Math;
using MineMart.Data;
using MineMart.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMart.UI.Binders {
    /// <summary>
    /// Thẻ hiển thị một loại mỏ trong danh sách cuộn ngang của Shop Ô Đất.
    /// Tự động hiển thị mô hình khối block 3D (Prefab3D) và nút Mua.
    /// </summary>
    public class PlotStallCardUI : MonoBehaviour {
        [Header("Card Visuals")]
        [SerializeField] private TextMeshProUGUI _stallNameText;
        [SerializeField] private TextMeshProUGUI _blockInfoText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Image _buttonBackground;

        [Header("3D Block Model Preview")]
        [Tooltip("Transform điểm neo để sinh ra model 3D của khối block (Prefab3D)")]
        [SerializeField] private Transform _blockPreviewHolder;

        [Tooltip("Tỉ lệ kích thước của khối block 3D trên thẻ UI (mặc định: 35)")]
        [SerializeField] private float _block3DScale = 35f;

        [Tooltip("Tự động xoay nhẹ khối block 3D")]
        [SerializeField] private bool _autoRotateBlock = true;
        [SerializeField] private float _rotateSpeed = 45f;

        private GameObject _spawnedBlockModel;
        private GameObject _currentBlockPrefab;
        private StallConfigOnMap _config;
        private Action<StallConfigOnMap> _onBuyCallback;
        private Color _canAffordColor;
        private Color _cannotAffordColor;

        private void Awake() {
            AutoFindReferences();
            SyncCardSize();
        }

        /// <summary>
        /// Đồng bộ kích thước từ RectTransform (ví dụ 200x200 do em đặt trong Prefab) vào LayoutElement
        /// để LayoutGroup không bao giờ tự ý bóp méo kích thước thẻ.
        /// </summary>
        public void SyncCardSize() {
            var rect = GetComponent<RectTransform>();
            var layoutElem = GetComponent<LayoutElement>();
            if (rect != null && layoutElem != null) {
                if (rect.sizeDelta.x > 10f) {
                    layoutElem.minWidth = rect.sizeDelta.x;
                    layoutElem.preferredWidth = rect.sizeDelta.x;
                }
                if (rect.sizeDelta.y > 10f) {
                    layoutElem.minHeight = rect.sizeDelta.y;
                    layoutElem.preferredHeight = rect.sizeDelta.y;
                }
            }
        }

        public StallConfigOnMap Config => _config;

        public void Bind(
            StallConfigOnMap config,
            Color canAffordColor,
            Color cannotAffordColor,
            Action<StallConfigOnMap> onBuyCallback) {

            _config = config;
            _canAffordColor = canAffordColor;
            _cannotAffordColor = cannotAffordColor;
            _onBuyCallback = onBuyCallback;

            SyncCardSize();

            if (_stallNameText != null) {
                _stallNameText.text = config.StallData != null ? config.StallData.StallName : "Mỏ Khai Thác";
            }

            if (_blockInfoText != null) {
                string blockName = config.StallData?.ProducedBlock != null
                    ? config.StallData.ProducedBlock.DisplayName
                    : "Khoáng Sản";
                _blockInfoText.text = blockName;
            }

            // TÁI SỬ DỤNG MODEL 3D: Nếu cùng loại Prefab thì giữ nguyên, không Destroy/Instantiate lại!
            GameObject targetPrefab = config.StallData?.ProducedBlock?.Prefab3D;
            if (targetPrefab != null) {
                if (_currentBlockPrefab != targetPrefab || _spawnedBlockModel == null) {
                    // Chỉ hủy và sinh mới nếu khác loại block
                    if (_spawnedBlockModel != null) {
                        Destroy(_spawnedBlockModel);
                        _spawnedBlockModel = null;
                    }

                    if (_blockPreviewHolder != null) {
                        _spawnedBlockModel = Instantiate(targetPrefab, _blockPreviewHolder);

                        // Gỡ toàn bộ Collider để không vướng physics/raycast
                        var colliders = _spawnedBlockModel.GetComponentsInChildren<Collider>();
                        for (int i = 0; i < colliders.Length; i++) {
                            Destroy(colliders[i]);
                        }

                        // Tắt hoàn toàn việc đổ bóng xuống map và nhận bóng
                        var renderers = _spawnedBlockModel.GetComponentsInChildren<Renderer>();
                        for (int i = 0; i < renderers.Length; i++) {
                            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderers[i].receiveShadows = false;
                        }

                        _spawnedBlockModel.transform.localPosition = new Vector3(0f, 0f, -15f);
                        _spawnedBlockModel.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);
                        _spawnedBlockModel.transform.localScale = Vector3.one * _block3DScale;

                        SetLayerRecursively(_spawnedBlockModel, _blockPreviewHolder.gameObject.layer);
                    }
                    _currentBlockPrefab = targetPrefab;
                } else {
                    // Nếu đúng loại cũ thì chỉ cần bật lên và reset góc xoay
                    _spawnedBlockModel.SetActive(true);
                    _spawnedBlockModel.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);
                }
            } else {
                if (_spawnedBlockModel != null) {
                    _spawnedBlockModel.SetActive(false);
                }
            }

            if (_costText != null) {
                if (config.BuyPrice <= 0) {
                    _costText.text = "Miễn phí";
                } else {
                    _costText.text = $"{NumberFormatter.Format(config.BuyPrice)} Vàng";
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

            double currentGold = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentGold : 0;
            bool canAfford = _config.BuyPrice <= 0 || currentGold >= _config.BuyPrice;

            if (_buyButton != null) {
                _buyButton.interactable = canAfford;
            }

            if (_buttonBackground != null) {
                _buttonBackground.color = canAfford ? _canAffordColor : _cannotAffordColor;
            }
        }

        private void HandleClick() {
            if (_config != null) {
                _onBuyCallback?.Invoke(_config);
            }
        }

        private void Update() {
            if (_autoRotateBlock && _spawnedBlockModel != null && _spawnedBlockModel.activeSelf) {
                _spawnedBlockModel.transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void OnDisable() {
            if (_spawnedBlockModel != null) {
                _spawnedBlockModel.SetActive(false);
            }
        }

        private void OnEnable() {
            if (_spawnedBlockModel != null && _config != null && _config.StallData?.ProducedBlock?.Prefab3D != null) {
                _spawnedBlockModel.SetActive(true);
            }
        }

        private void OnDestroy() {
            if (_spawnedBlockModel != null) {
                Destroy(_spawnedBlockModel);
                _spawnedBlockModel = null;
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer) {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Tự động dò tìm các component con nếu chưa được gán thủ công trên Inspector.
        /// </summary>
        public void AutoFindReferences() {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts) {
                string lower = t.gameObject.name.ToLower();
                if (_stallNameText == null && (lower.Contains("name") || lower.Contains("title") || lower.Contains("ten"))) _stallNameText = t;
                else if (_blockInfoText == null && (lower.Contains("block") || lower.Contains("info") || lower.Contains("desc") || lower.Contains("khoang"))) _blockInfoText = t;
                else if (_costText == null && (lower.Contains("cost") || lower.Contains("price") || lower.Contains("gold") || lower.Contains("coin") || lower.Contains("gia"))) _costText = t;
            }

            if (_blockPreviewHolder == null) {
                var found = transform.Find("BlockPreviewHolder");
                if (found != null) _blockPreviewHolder = found;
                else {
                    var allTransforms = GetComponentsInChildren<Transform>(true);
                    foreach (var tr in allTransforms) {
                        string lower = tr.gameObject.name.ToLower();
                        if (lower.Contains("preview") || lower.Contains("holder") || lower.Contains("model")) {
                            _blockPreviewHolder = tr;
                            break;
                        }
                    }
                }
            }

            if (_buyButton == null) {
                _buyButton = GetComponentInChildren<Button>(true);
            }

            if (_buyButton != null && _buttonBackground == null) {
                _buttonBackground = _buyButton.GetComponent<Image>();
            }
        }
    }
}
