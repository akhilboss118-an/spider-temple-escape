using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Runner.EditorTools
{
    public static class ProjectReloader
    {
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
