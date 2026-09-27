using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Data
{
    /// <summary>
    /// Schema định nghĩa hồ sơ kinh doanh và công thức nâng cấp của một Quầy Nghề.
    /// Toàn bộ các trường dữ liệu đều là ô trống để nhà thiết kế điền thông tin trong Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Stall_", menuName = "MineMart/Data/Stall Data")]
    public class StallDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _stallId;
        [SerializeField] private string _stallName;
        [SerializeField] private BlockDataSO _producedBlock;

        [Header("Economic Base Numbers")]
        [SerializeField] private double _buyPrice;
        [SerializeField] private double _baseUpgradePrice;
        [SerializeField] private float _priceMultiplier;

        [Header("Milestones")]
        [SerializeField] private List<MilestoneRuleSO> _milestoneRules = new List<MilestoneRuleSO>();

        public string StallId => _stallId;
        public string StallName => _stallName;
        public BlockDataSO ProducedBlock => _producedBlock;
        public double BuyPrice => _buyPrice;
        public double BaseUpgradePrice => _baseUpgradePrice;
        public float PriceMultiplier => _priceMultiplier;
        public IReadOnlyList<MilestoneRuleSO> MilestoneRules => _milestoneRules;

        /// <summary>
        /// Tính toán giá nâng cấp theo cấp độ: BasePrice * Multiplier^(Level - 1)
        /// </summary>
        public double CalculateUpgradeCost(int currentLevel)
        {
            if (currentLevel <= 0) return _baseUpgradePrice;
            float multiplier = _priceMultiplier > 0 ? _priceMultiplier : 1.15f;
            return _baseUpgradePrice * Math.Pow(multiplier, currentLevel - 1);
        }
    }
}
