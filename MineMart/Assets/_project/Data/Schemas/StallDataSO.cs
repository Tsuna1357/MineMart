using System;
using System.Collections.Generic;
using UnityEngine;

namespace MineMart.Data {
    /// <summary>
    /// Schema định nghĩa hồ sơ kinh doanh và công thức nâng cấp của một Quầy Nghề.
    /// Toàn bộ các trường dữ liệu đều là ô trống để nhà thiết kế điền thông tin trong Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Stall_", menuName = "MineMart/Data/Stall Data")]
    public class StallDataSO : ScriptableObject {
        [Header("Identity")]
        [SerializeField] private string _stallId;
        [SerializeField] private string _stallName;
        [SerializeField] private BlockDataSO _producedBlock;

        public string StallId => _stallId;
        public string StallName => _stallName;
        public BlockDataSO ProducedBlock => _producedBlock;
    }
}
