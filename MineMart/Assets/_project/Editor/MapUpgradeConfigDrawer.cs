using UnityEditor;
using UnityEngine;
using MineMart.Data;

namespace MineMart.Editor {
    /// <summary>
    /// Custom PropertyDrawer cho MapUpgradeConfig.
    /// Tự động ẩn/hiện các trường thông tin phù hợp theo UpgradeType đã chọn,
    /// đồng bộ chuẩn xác chiều cao từng trường để loại bỏ triệt để lỗi chữ đè chữ.
    /// </summary>
    [CustomPropertyDrawer(typeof(MapUpgradeConfig))]
    public class MapUpgradeConfigDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            if (!property.isExpanded) {
                return EditorGUIUtility.singleLineHeight;
            }

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float totalHeight = line + spacing; // Header foldout

            float PropH(string propName) {
                var p = property.FindPropertyRelative(propName);
                return p != null ? EditorGUI.GetPropertyHeight(p, true) + spacing : 0f;
            }

            // Identity fields
            totalHeight += PropH("_upgradeId");
            totalHeight += PropH("_displayName");
            totalHeight += PropH("_icon");
            totalHeight += PropH("_description");
            totalHeight += PropH("_upgradeType");

            var typeProp = property.FindPropertyRelative("_upgradeType");
            var upgradeType = typeProp != null ? (MapUpgradeType)typeProp.enumValueIndex : MapUpgradeType.AddWorkers;

            switch (upgradeType) {
                case MapUpgradeType.AddWorkers:
                    totalHeight += PropH("_workerRole");
                    totalHeight += PropH("_workersAddedPerLevel");
                    totalHeight += PropH("_workerPrefab");
                    totalHeight += (line * 2.2f) + spacing; // Helpbox
                    break;

                case MapUpgradeType.AddCustomers:
                    totalHeight += PropH("_customerBonusPerLevel");
                    totalHeight += PropH("_activateOutdoorTable");
                    var actProp = property.FindPropertyRelative("_activateOutdoorTable");
                    if (actProp != null && actProp.boolValue) {
                        totalHeight += PropH("_outdoorTableIndex");
                    }
                    totalHeight += (line * 2.2f) + spacing; // Helpbox for outdoor tables
                    break;

                case MapUpgradeType.MultiplyBlockPrice:
                    totalHeight += PropH("_targetScope");
                    var scopeProp = property.FindPropertyRelative("_targetScope");
                    if (scopeProp != null && scopeProp.enumValueIndex == (int)MapUpgradeScope.SingleStall) {
                        totalHeight += PropH("_targetStall");
                    }
                    totalHeight += PropH("_priceMultiplierValue");
                    break;

                case MapUpgradeType.IncreaseAISpeed:
                    totalHeight += PropH("_speedBonusPercent");
                    break;

                case MapUpgradeType.ReduceMiningTime:
                    totalHeight += PropH("_targetStall");
                    totalHeight += PropH("_miningTimeReduction");
                    break;
            }

            // Single fixed price
            totalHeight += PropH("_price");

            return totalHeight + 8f; // Extra padding at bottom
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var idProp = property.FindPropertyRelative("_upgradeId");
            var nameProp = property.FindPropertyRelative("_displayName");
            var typeProp = property.FindPropertyRelative("_upgradeType");

            string idStr = idProp != null ? idProp.stringValue : "";
            string nameStr = nameProp != null ? nameProp.stringValue : "";
            string typeStr = typeProp != null && typeProp.enumDisplayNames.Length > typeProp.enumValueIndex 
                ? typeProp.enumDisplayNames[typeProp.enumValueIndex] : "";

            string headerText = !string.IsNullOrEmpty(nameStr) 
                ? $"[{idStr}] {nameStr} ({typeStr})"
                : $"Upgrade ({typeStr})";

            Rect foldoutRect = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerText, true, EditorStyles.foldoutHeader);

            if (!property.isExpanded) {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float currentY = position.y + line + spacing;

            void DrawProp(string propName, string customLabel = null) {
                var prop = property.FindPropertyRelative(propName);
                if (prop == null) return;

                float h = EditorGUI.GetPropertyHeight(prop, true);
                Rect r = new Rect(position.x, currentY, position.width, h);
                if (customLabel != null) {
                    EditorGUI.PropertyField(r, prop, new GUIContent(customLabel), true);
                } else {
                    EditorGUI.PropertyField(r, prop, true);
                }
                currentY += h + spacing;
            }

            // Draw identity fields
            DrawProp("_upgradeId", "Mã Nâng Cấp (ID)");
            DrawProp("_displayName", "Tên Hiển Thị");
            DrawProp("_icon", "Icon");
            DrawProp("_description", "Mô Tả");
            DrawProp("_upgradeType", "Loại Nâng Cấp");

            var upgradeType = typeProp != null ? (MapUpgradeType)typeProp.enumValueIndex : MapUpgradeType.AddWorkers;

            // Draw context-specific fields
            switch (upgradeType) {
                case MapUpgradeType.AddWorkers: {
                    DrawProp("_workerRole", "Loại Thợ Kích Hoạt");
                    DrawProp("_workersAddedPerLevel", "+ Số Thợ Sinh Ra Mỗi Cấp");
                    DrawProp("_workerPrefab", "Prefab Thợ (Để trống = Mặc định)");
                    float helpH = line * 2.2f;
                    Rect helpRect = new Rect(position.x, currentY, position.width, helpH);
                    var roleProp = property.FindPropertyRelative("_workerRole");
                    string roleName = roleProp != null && roleProp.enumDisplayNames.Length > roleProp.enumValueIndex
                        ? roleProp.enumDisplayNames[roleProp.enumValueIndex] : "Worker";
                    EditorGUI.HelpBox(helpRect, $"ℹ️ Nâng cấp sẽ Instantiate số lượng Thợ loại '{roleName}' tại điểm WorkerSpawnPoint của MapManager.", MessageType.Info);
                    currentY += helpH + spacing;
                    break;
                }

                case MapUpgradeType.AddCustomers: {
                    DrawProp("_customerBonusPerLevel", "+ Khách Thêm Mỗi Cấp");
                    DrawProp("_activateOutdoorTable", "Kích Hoạt Bàn Ngoài Trời?");
                    var actProp = property.FindPropertyRelative("_activateOutdoorTable");
                    if (actProp != null && actProp.boolValue) {
                        DrawProp("_outdoorTableIndex", "Chỉ Số Bàn (-1 = Tự Động)");
                    }
                    float helpH = line * 2.2f;
                    Rect helpRect = new Rect(position.x, currentY, position.width, helpH);
                    EditorGUI.HelpBox(helpRect, "ℹ️ Chỉ khi tích 'Kích Hoạt Bàn Ngoài Trời' thì nâng cấp này mới SetActive(true) bàn tương ứng trong Scene. Nếu bỏ tích, chỉ tăng số lượng khách mà không thêm bàn.", MessageType.Info);
                    currentY += helpH + spacing;
                    break;
                }

                case MapUpgradeType.MultiplyBlockPrice: {
                    DrawProp("_targetScope", "Phạm Vi Áp Dụng");
                    var scopeProp = property.FindPropertyRelative("_targetScope");
                    if (scopeProp != null && scopeProp.enumValueIndex == (int)MapUpgradeScope.SingleStall) {
                        DrawProp("_targetStall", "Mỏ Mục Tiêu");
                    }
                    DrawProp("_priceMultiplierValue", "Hệ Số Nhân (x2, x3...)");
                    break;
                }

                case MapUpgradeType.IncreaseAISpeed:
                    DrawProp("_speedBonusPercent", "+ % Tốc Độ Chạy (AI & Player)");
                    break;

                case MapUpgradeType.ReduceMiningTime:
                    DrawProp("_targetStall", "Mỏ Mục Tiêu");
                    DrawProp("_miningTimeReduction", "Số Giây Giảm Mỗi Cấp (s)");
                    break;
            }

            // Draw single fixed price
            DrawProp("_price", "Giá Mua (Vàng)");

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
}
