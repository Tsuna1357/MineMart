using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ tự động quét và loại bỏ các khối có BoxCollider chiều cao > 1m khỏi NavMesh.
    /// Đảm bảo cầu thang 1 block (<= 1m) đi được mượt mà, trong khi các khối tường/vách (> 1m)
    /// hoàn toàn không bị bake bất kỳ đường đi nào lên nóc.
    /// </summary>
    public static class NavMeshBlockFilterTool {
        private const float MAX_STEP_HEIGHT = 1.05f; // Ngưỡng 1 block tiêu chuẩn (1 mét)

        [MenuItem("MineMart/Navigation/Tự Động Chặn Bake Khối Cao > 1 Block & Re-Bake", false, 10)]
        public static void FilterBlocksAndBakeNavMesh() {
            int ignoredCount = 0;
            int walkableCount = 0;

            // 1. Quét toàn bộ BoxCollider trong Scene
            BoxCollider[] allColliders = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Filter Blocks Height > 1 for NavMesh");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (BoxCollider box in allColliders) {
                if (box == null || !box.enabled || box.isTrigger) continue;

                // Tính toán chiều cao thực tế theo mét trong thế giới 3D
                float worldHeight = box.size.y * Mathf.Abs(box.transform.lossyScale.y);

                NavMeshModifier modifier = box.GetComponent<NavMeshModifier>();

                if (worldHeight > MAX_STEP_HEIGHT) {
                    // Khối cao hơn 1m (Tường, vách, cột...): CẤM BAKE NAVMESH
                    if (modifier == null) {
                        modifier = Undo.AddComponent<NavMeshModifier>(box.gameObject);
                    } else {
                        Undo.RecordObject(modifier, "Update NavMeshModifier");
                    }

                    modifier.ignoreFromBuild = true;
                    EditorUtility.SetDirty(box.gameObject);
                    ignoredCount++;
                } else {
                    // Khối <= 1m (Bậc thang, sàn nhà, thềm cửa...): CHO PHÉP BAKE ĐƯỜNG ĐI
                    if (modifier != null && modifier.ignoreFromBuild) {
                        Undo.RecordObject(modifier, "Update NavMeshModifier");
                        modifier.ignoreFromBuild = false;
                        EditorUtility.SetDirty(box.gameObject);
                    }
                    walkableCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            // 2. Tìm NavMeshSurface trên Scene và kích hoạt chế độ Physics Colliders
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface != null) {
                Undo.RecordObject(surface, "Configure NavMeshSurface");
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // Bake theo BoxCollider thực tế
                EditorUtility.SetDirty(surface);

                // 3. Tiến hành Re-Bake NavMesh ngay lập tức
                surface.BuildNavMesh();
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);

                Debug.Log($"<color=#10B981>[NavMeshBlockFilter]</color> Đã xử lý {ignoredCount} khối cao > 1m (Loại bỏ khỏi NavMesh) và {walkableCount} khối bậc thang/sàn. Đã Re-Bake NavMesh thành công!");

                EditorUtility.DisplayDialog(
                    "Thành Công!",
                    $"Đã cấu hình NavMesh Surface sang Physics Colliders.\n\n" +
                    $"• Đã chặn bake trên: {ignoredCount} khối tường/vách (Chiều cao > 1m).\n" +
                    $"• Giữ nguyên đường đi cho: {walkableCount} khối bậc thang/sàn (Chiều cao <= 1m).\n\n" +
                    $"NavMesh đã được Re-Bake hoàn tất!",
                    "OK"
                );
            } else {
                Debug.LogWarning("[NavMeshBlockFilter] Không tìm thấy NavMeshSurface trên Scene!");
                EditorUtility.DisplayDialog(
                    "Thông Báo",
                    $"Đã gắn NavMeshModifier cho {ignoredCount} khối, nhưng chưa tìm thấy NavMeshSurface để Bake.",
                    "OK"
                );
            }
        }

        [MenuItem("MineMart/Navigation/Chỉ Bake Lại NavMesh", false, 11)]
        public static void BakeNavMeshOnly() {
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface != null) {
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                Debug.Log("<color=#10B981>[NavMeshBlockFilter]</color> Đã Re-Bake NavMesh thành công theo Physics Colliders!");
            }
        }
    }
}
