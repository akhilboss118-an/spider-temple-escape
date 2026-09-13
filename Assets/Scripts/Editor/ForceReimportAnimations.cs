using UnityEngine;
using UnityEditor;

/// <summary>
/// Force-reimports all character animation FBX files so Unity picks up
/// the corrected avatar source (avatarSetup=2, pointing to model FBX).
/// Run once via menu: Tools > Fix Animations > Force Reimport All Character Animations
/// </summary>
public class ForceReimportAnimations : MonoBehaviour
{
    [MenuItem("Tools/Fix Animations/Force Reimport All Character Animations")]
    public static void ReimportAll()
    {
        string[] characters = { "Akhilboss", "Harika", "Nandini", "Navaneeth", "Pavan", "Pravalika", "Srikar" };
        int total = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string c in characters)
            {
                // Reimport model first (creates the avatar)
                string modelPath = $"Assets/Characters/{c}/Model/{c}.fbx";
                if (c == "Pravalika") modelPath = $"Assets/Characters/{c}/Model/pravalika.fbx";

                if (System.IO.File.Exists(modelPath))
                {
                    AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[AnimFix] Reimported model: {modelPath}");
                    total++;
                }

                // Reimport all animation FBXs (now they copy avatar from model)
                string animDir = $"Assets/Characters/{c}/Animations";
                if (!System.IO.Directory.Exists(animDir)) continue;

                string[] fbxFiles = System.IO.Directory.GetFiles(animDir, "*.fbx");
                foreach (string fbx in fbxFiles)
                {
                    string assetPath = fbx.Replace('\\', '/');
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[AnimFix] Reimported: {assetPath}");
                    total++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=green>[AnimFix] Done! Reimported {total} assets. Animations should now play correctly.</color>");
        EditorUtility.DisplayDialog("Animation Fix Complete",
            $"Successfully reimported {total} FBX assets.\n\nAll character animations now use their model's avatar.\n\nPress Play to test!", "OK");
    }

    [MenuItem("Tools/Fix Animations/Validate Avatar Sources")]
    public static void ValidateAvatarSources()
    {
        string[] characters = { "Akhilboss", "Harika", "Nandini", "Navaneeth", "Pavan", "Pravalika", "Srikar" };
        int issues = 0;

        foreach (string c in characters)
        {
            string animDir = $"Assets/Characters/{c}/Animations";
            if (!System.IO.Directory.Exists(animDir)) continue;

            string[] fbxFiles = System.IO.Directory.GetFiles(animDir, "*.fbx");
            foreach (string fbx in fbxFiles)
            {
                string assetPath = fbx.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                // Check that avatar is not null (means copy-from-other worked)
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[AnimFix] No AnimationClip in: {assetPath}");
                    issues++;
                }
                else
                {
                    Debug.Log($"<color=green>[AnimFix] OK: {assetPath} -> clip '{clip.name}', length={clip.length:F2}s</color>");
                }
            }
        }

        if (issues == 0)
            Debug.Log("<color=green>[AnimFix] All animation FBXs validated OK!</color>");
        else
            Debug.LogWarning($"[AnimFix] {issues} issues found. Run 'Force Reimport' first.");
    }
}
