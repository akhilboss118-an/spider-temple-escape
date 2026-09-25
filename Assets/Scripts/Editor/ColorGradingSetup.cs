using UnityEditor;
using UnityEngine;

namespace Runner.EditorTools
{
    /// <summary>
    /// Builds the color-grading material inside Resources so the grading shader is
    /// always included in player builds (Shader.Find alone gets stripped).
    /// </summary>
    public static class ColorGradingSetup
    {
        private const string MATERIAL_PATH = "Assets/Resources/Materials/Mat_ColorGrading.mat";

        [MenuItem("Runner/Setup Color Grading Material")]
        public static void EnsureMaterial()
        {
            Shader shader = Shader.Find(Runner.Effects.ColorGradingManager.GRADING_SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError("[ColorGradingSetup] Shader not found: " +
                               Runner.Effects.ColorGradingManager.GRADING_SHADER_NAME);
                return;
            }

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (existing != null && existing.shader == shader)
            {
                Debug.Log("[ColorGradingSetup] Material already up to date: " + MATERIAL_PATH);
                return;
            }

            Material mat = existing != null ? existing : new Material(shader);
            if (mat.shader != shader) mat.shader = shader;
            mat.hideFlags = HideFlags.None;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ColorGradingSetup] Created " + MATERIAL_PATH);
        }
    }
}
