using UnityEngine;

namespace MineMart.Data {
    /// <summary>
    /// Schema định nghĩa mốc đột phá doanh thu khi nâng cấp quầy hàng (Level 10, 25, 50...).
    /// Toàn bộ các trường dữ liệu đều là ô trống để nhà thiết kế điền thông tin trong Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Milestone_", menuName = "MineMart/Data/Milestone Rule")]
    public class MilestoneRuleSO : ScriptableObject {
        [SerializeField] private int _targetLevel;
        [SerializeField] private float _profitMultiplier;

        public int TargetLevel => _targetLevel;
        public float ProfitMultiplier => _profitMultiplier;
    }
}
