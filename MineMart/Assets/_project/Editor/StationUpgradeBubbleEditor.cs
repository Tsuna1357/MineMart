using MineMart.UI.Binders;
using UnityEditor;
using UnityEngine;

namespace MineMart.Editor {
    [CustomEditor(typeof(StationUpgradeBubble))]
    public class StationUpgradeBubbleEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var bubble = (StationUpgradeBubble)target;
            if (bubble == null) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("⭐ CÔNG CỤ THANH TIẾN TRÌNH MILESTONE", EditorStyles.boldLabel);

            var so = new SerializedObject(bubble);
            var sliderProp = so.FindProperty("_milestoneSlider");
            var fillProp = so.FindProperty("_milestoneFillImage");
            var containerProp = so.FindProperty("_milestoneBarContainer");

            bool hasSlider = sliderProp != null && sliderProp.objectReferenceValue != null;
            bool hasFill = fillProp != null && fillProp.objectReferenceValue != null;
            bool hasContainer = containerProp != null && containerProp.objectReferenceValue != null;

            if (hasSlider && hasFill && hasContainer) {
                EditorGUILayout.HelpBox(
                    "✅ Thanh tiến trình đã liên kết đầy đủ (Slider, Fill, Container)!\n" +
                    "• Bạn có thể dùng Rect Tool (T) trong Scene View để di chuyển vị trí, chỉnh chiều rộng/cao.\n" +
                    "• Đổi màu sắc: Chỉnh 2 ô màu 'Milestone Fill Color' và 'Milestone Bg Color' trong Inspector, thanh sẽ đổi màu ngay lập tức!",
                    MessageType.Info
                );
            } else {
                EditorGUILayout.HelpBox(
                    "⚠️ Mục 'Milestone Slider' đang trống hoặc chưa gắn component Slider.\n" +
                    "Bấm nút bên dưới để tự động tạo / gắn Slider vào 'Milestone_ProgressBar', sửa dứt điểm lỗi thanh bị đầy 100% màu vàng!",
                    MessageType.Warning
                );
            }

            if (GUILayout.Button("⚡ Tạo / Đồng Bộ Milestone Slider trong Scene", GUILayout.Height(36))) {
                bubble.CreateMilestoneBarInEditor();
            }
        }
    }
}
