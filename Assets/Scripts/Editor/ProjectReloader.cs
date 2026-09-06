using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Runner.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectReloader
    {
        static ProjectReloader()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Debug.Log("[ProjectReloader] Automatically refreshing AssetDatabase and saving scenes...");
                AssetDatabase.Refresh();
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[ProjectReloader] All scenes and assets successfully reloaded and saved!");
            };
        }

        [MenuItem("Runner/Force Reload Project and Scenes")]
        public static void ForceReload()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.RequestScriptReload();
            Debug.Log("[ProjectReloader] Forced complete project reload complete!");
        }
    }
}
