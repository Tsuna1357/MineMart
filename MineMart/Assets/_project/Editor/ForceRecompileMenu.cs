using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MineMart.Editor {
    /// <summary>
    /// Công cụ hỗ trợ ép Unity biên dịch lại toàn bộ scripts và làm mới AssetDatabase.
    /// </summary>
    public static class ForceRecompileMenu {
        [MenuItem("MineMart/Làm Mới & Biên Dịch Lại Scripts (Recompile)", priority = 100)]
        public static void ForceRecompile() {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("<color=#10B981>[MineMart]</color> ✅ Đã kích hoạt Unity quét và biên dịch lại toàn bộ C# Scripts!");
        }
    }
}
