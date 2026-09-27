using UnityEngine;

namespace MineMart.Data
{
    /// <summary>
    /// Schema định nghĩa thuộc tính hàng hóa vật lý và giá trị thương mại của một Khối Block.
    /// Toàn bộ các trường dữ liệu đều là ô trống để nhà thiết kế điền thông tin trong Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Block_", menuName = "MineMart/Data/Block Data")]
    public class BlockDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _blockId;
        [SerializeField] private string _displayName;

        [Header("Economics & Production")]
        [SerializeField] private double _baseValue;
        [SerializeField] private float _baseCraftTime;

        [Header("Visuals")]
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _prefab3D;

        public string BlockId => _blockId;
        public string DisplayName => _displayName;
        public double BaseValue => _baseValue;
        public double BasePrice => _baseValue;
        public float BaseCraftTime => _baseCraftTime;
        public Sprite Icon => _icon;
        public GameObject Prefab3D => _prefab3D;
    }
}
