using UnityEngine;
using UnityEditor;
using Runner.Effects;
using Runner.Track;
using Runner.Monster;

namespace Runner.EditorTools
{
    public static class MapVerificationTest
    {
        [MenuItem("Runner/Verify Jungle Map System")]
        public static void RunVerification()
        {
            Debug.Log("==================================================");
            Debug.Log("[MapVerificationTest] STARTING JUNGLE MAP VERIFICATION");
            Debug.Log("==================================================");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool allPassed = true;

            // 1. Verify Zombie Monster
            GameObject zombiePrefab = Resources.Load<GameObject>("Prefabs/Monster_Zombie");
            Debug.Log($"[VERIFY] Monster_Zombie prefab in Resources: {zombiePrefab != null}");
            if (zombiePrefab == null)
            {
                Debug.LogError("[FAIL] Monster_Zombie prefab could not be loaded!");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] Monster_Zombie prefab loaded successfully!");
            }

            // 2. Test BiomeManager - Jungle only
            BiomeManager biomeMgr = BiomeManager.Instance;

            biomeMgr.SetSelectedMap(SelectedMapMode.JungleCanopy);
            Debug.Log($"[VERIFY] SetSelectedMap(JungleCanopy) -> CurrentBiome = {biomeMgr.CurrentBiome} (Expected: JungleCanopy)");
            if (biomeMgr.CurrentBiome != BiomeType.JungleCanopy) allPassed = false;

            // Test Materials
            Material jungleMat = biomeMgr.GetFloorMaterialForBiome(BiomeType.JungleCanopy);
            Debug.Log($"[VERIFY] Jungle Floor Material: {jungleMat != null}");
            if (jungleMat == null)
            {
                Debug.LogError("[FAIL] Jungle floor material is null!");
                allPassed = false;
            }

            // 3. Test Spinning Blade Model
            GameObject bladeModel = Resources.Load<GameObject>("Obstacles/spinning_blade_model");
            Debug.Log($"[VERIFY] spinning_blade_model in Resources: {bladeModel != null}");
            if (bladeModel == null)
            {
                Debug.LogError("[FAIL] spinning_blade_model could not be loaded!");
                allPassed = false;
            }
            else
            {
                Debug.Log("[PASS] spinning_blade_model loaded successfully!");
            }

            // Clean up test objects
            Object.DestroyImmediate(biomeMgr.gameObject);

            Debug.Log("==================================================");
            if (allPassed)
            {
                Debug.Log("[MapVerificationTest] ALL JUNGLE MAP VERIFICATION CHECKS PASSED!");
            }
            else
            {
                Debug.LogError("[MapVerificationTest] VERIFICATION FAILED WITH ERRORS!");
            }
            Debug.Log("==================================================");
        }

        public static void RunBatchmodeVerification()
        {
            try
            {
                RunVerification();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MapVerificationTest] Exception during verification: " + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
