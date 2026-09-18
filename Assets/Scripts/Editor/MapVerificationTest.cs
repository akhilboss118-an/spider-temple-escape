using UnityEngine;
using UnityEditor;
using Runner.Effects;
using Runner.Track;
using Runner.Monster;

namespace Runner.EditorTools
{
    public static class MapVerificationTest
    {
        [MenuItem("Runner/Verify Multi-Map System")]
        public static void RunVerification()
        {
            Debug.Log("==================================================");
            Debug.Log("[MapVerificationTest] STARTING MULTI-MAP VERIFICATION");
            Debug.Log("==================================================");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool allPassed = true;

            // 1. Verify pathway 3D Model Asset (obj / glb) & Textures
            GameObject pathwayModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/pathway.obj");
            if (pathwayModel == null)
                pathwayModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/pathway.glb");
            GameObject pathwayRes = Resources.Load<GameObject>("Path/pathway");
            Texture2D pathwayTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Path/pathway_tex_0.png");
            Texture2D pathwayNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Path/pathway_norm_0.png");

            Debug.Log($"[VERIFY] pathway model in Assets: {pathwayModel != null}");
            Debug.Log($"[VERIFY] pathway in Resources: {pathwayRes != null}");
            Debug.Log($"[VERIFY] pathway albedo texture: {pathwayTex != null}");
            Debug.Log($"[VERIFY] pathway normal texture: {pathwayNorm != null}");

            if (pathwayModel == null)
            {
                Debug.LogError("[FAIL] pathway 3D model could not be loaded!");
                allPassed = false;
            }
            else
            {
                Debug.Log($"[PASS] pathway 3D model loaded successfully! Name: {pathwayModel.name}");
            }

            // 2. Verify Magma Golem FBX Asset
            GameObject golemModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Monster/VolcanoGolem/source/Magma+monster.fbx");
            GameObject golemRes = Resources.Load<GameObject>("Monster/VolcanoGolem/source/Magma+monster");
            Debug.Log($"[VERIFY] Magma+monster.fbx in Assets: {golemModel != null}");
            Debug.Log($"[VERIFY] Magma+monster in Resources: {golemRes != null}");
            if (golemModel == null)
            {
                Debug.LogError("[FAIL] Magma+monster.fbx could not be loaded from Assets/Models/Monster/VolcanoGolem/source/Magma+monster.fbx!");
                allPassed = false;
            }
            else
            {
                Debug.Log($"[PASS] Magma+monster.fbx loaded successfully! Name: {golemModel.name}");
            }

            // 3. Verify Magma Golem Textures
            Texture2D baseCol = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_basecolor.jpeg");
            Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_normal.jpeg");
            Texture2D metal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_metallic.jpeg");
            Texture2D rough = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_roughness.jpeg");

            Debug.Log($"[VERIFY] Magma basecolor texture: {baseCol != null}");
            Debug.Log($"[VERIFY] Magma normal texture: {norm != null}");
            Debug.Log($"[VERIFY] Magma metallic texture: {metal != null}");
            Debug.Log($"[VERIFY] Magma roughness texture: {rough != null}");

            if (baseCol == null || norm == null || metal == null || rough == null)
            {
                Debug.LogError("[FAIL] One or more Magma Golem textures could not be loaded!");
                allPassed = false;
            }

            // 4. Test BiomeManager Map Selection & Material Caching
            BiomeManager biomeMgr = BiomeManager.Instance;

            // Test Map Mode Switching
            biomeMgr.SetSelectedMap(SelectedMapMode.VolcanicInferno);
            Debug.Log($"[VERIFY] SetSelectedMap(VolcanicInferno) -> CurrentBiome = {biomeMgr.CurrentBiome} (Expected: VolcanicCaverns)");
            if (biomeMgr.CurrentBiome != BiomeType.VolcanicCaverns) allPassed = false;

            biomeMgr.SetSelectedMap(SelectedMapMode.FrostbiteCitadel);
            Debug.Log($"[VERIFY] SetSelectedMap(FrostbiteCitadel) -> CurrentBiome = {biomeMgr.CurrentBiome} (Expected: FrostbiteCitadel)");
            if (biomeMgr.CurrentBiome != BiomeType.FrostbiteCitadel) allPassed = false;

            biomeMgr.SetSelectedMap(SelectedMapMode.JungleCanopy);
            Debug.Log($"[VERIFY] SetSelectedMap(JungleCanopy) -> CurrentBiome = {biomeMgr.CurrentBiome} (Expected: JungleCanopy)");
            if (biomeMgr.CurrentBiome != BiomeType.JungleCanopy) allPassed = false;

            // Test Materials
            Material jungleMat = biomeMgr.GetFloorMaterialForBiome(BiomeType.JungleCanopy);
            Material volcanoMat = biomeMgr.GetFloorMaterialForBiome(BiomeType.VolcanicCaverns);
            Material frostbiteMat = biomeMgr.GetFloorMaterialForBiome(BiomeType.FrostbiteCitadel);

            Debug.Log($"[VERIFY] Jungle Floor Material: {jungleMat != null}");
            Debug.Log($"[VERIFY] Volcanic Floor Material: {volcanoMat != null}");
            Debug.Log($"[VERIFY] Frostbite Floor Material: {frostbiteMat != null}");

            if (jungleMat == null || volcanoMat == null || frostbiteMat == null)
            {
                Debug.LogError("[FAIL] One or more biome floor materials are null!");
                allPassed = false;
            }

            // 5. Test TrackManager Chunk Generation across Biomes
            GameObject tmObj = new GameObject("Test_TrackManager");
            TrackManager tm = tmObj.AddComponent<TrackManager>();

            var createChunkMethod = typeof(TrackManager).GetMethod("CreateProceduralChunk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var applyRoadMethod = typeof(TrackManager).GetMethod("ApplyRoadVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createChunkMethod != null && applyRoadMethod != null)
            {
                TrackChunk straightChunk = (TrackChunk)createChunkMethod.Invoke(tm, new object[] { ChunkType.Straight });
                if (straightChunk != null)
                {
                    // Test Volcanic road visual
                    biomeMgr.SetSelectedMap(SelectedMapMode.VolcanicInferno);
                    applyRoadMethod.Invoke(tm, new object[] { straightChunk, 100f });
                    var volcanoRoad = straightChunk.transform.Find("Road3D_Volcano");
                    bool volcanoOk = (volcanoRoad != null && volcanoRoad.gameObject.activeSelf);
                    Debug.Log($"[VERIFY] Volcanic Chunk has Road3D_Volcano: {volcanoRoad != null}, Active: {volcanoRoad?.gameObject.activeSelf}");
                    if (!volcanoOk)
                    {
                        Debug.LogError("[FAIL] Volcanic road mesh was not spawned!");
                        allPassed = false;
                    }
                    else
                    {
                        Debug.Log("[PASS] Volcanic pathway.glb correctly instantiated and active on chunk!");
                    }

                    // Test Frostbite road visual
                    biomeMgr.SetSelectedMap(SelectedMapMode.FrostbiteCitadel);
                    applyRoadMethod.Invoke(tm, new object[] { straightChunk, 100f });
                    var regularRoad = straightChunk.transform.Find("Road3D_Tier2");
                    bool frostbiteOk = (regularRoad != null && regularRoad.gameObject.activeSelf && (volcanoRoad == null || !volcanoRoad.gameObject.activeSelf));
                    Debug.Log($"[VERIFY] Frostbite Chunk Road3D_Tier2 Active: {regularRoad?.gameObject.activeSelf}, Road3D_Volcano Active: {volcanoRoad?.gameObject.activeSelf}");
                    if (!frostbiteOk)
                    {
                        Debug.LogError("[FAIL] Frostbite road mesh state incorrect!");
                        allPassed = false;
                    }
                    else
                    {
                        Debug.Log("[PASS] Frostbite road correctly configured!");
                    }

                    Object.DestroyImmediate(straightChunk.gameObject);
                }
            }

            // Clean up test objects
            Object.DestroyImmediate(tmObj);

            Debug.Log("==================================================");
            if (allPassed)
            {
                Debug.Log("[MapVerificationTest] ALL MULTI-MAP VERIFICATION CHECKS PASSED!");
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
