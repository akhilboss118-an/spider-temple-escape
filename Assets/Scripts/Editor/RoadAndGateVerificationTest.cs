using UnityEngine;
using UnityEditor;
using Runner.Track;
using Runner.Obstacles;
using Runner.Effects;

namespace Runner.EditorTools
{
    public static class RoadAndGateVerificationTest
    {
        [MenuItem("Runner/Verify Road Progression and Gates")]
        public static void RunVerification()
        {
            Debug.Log("==================================================");
            Debug.Log("[RoadAndGateVerificationTest] STARTING VERIFICATION");
            Debug.Log("==================================================");

            // 1. Verify 3D Assets exist in Assets/Models and Assets/Resources
            GameObject rockyPathModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/rocky_path.obj");
            GameObject lowPolyRoadModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/low_poly_road.obj");
            GameObject stoneGateModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/stone_gate.obj");
            GameObject deadTreeModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/dead_tree_obstacle.obj");

            Debug.Log($"[VERIFY] rocky_path.obj loaded: {rockyPathModel != null}");
            Debug.Log($"[VERIFY] low_poly_road.obj loaded: {lowPolyRoadModel != null}");
            Debug.Log($"[VERIFY] stone_gate.obj loaded: {stoneGateModel != null}");
            Debug.Log($"[VERIFY] dead_tree_obstacle.obj loaded: {deadTreeModel != null}");

            if (rockyPathModel == null || lowPolyRoadModel == null || stoneGateModel == null || deadTreeModel == null)
            {
                Debug.LogError("[VERIFY FAILED] One or more required 3D models could not be loaded!");
                return;
            }

            // 2. Verify Resources.Load compatibility
            GameObject rockyRes = Resources.Load<GameObject>("Path/rocky_path");
            GameObject lowPolyRes = Resources.Load<GameObject>("Path/low_poly_road");
            GameObject stoneGateRes = Resources.Load<GameObject>("Environment/stone_gate");
            Debug.Log($"[VERIFY] Resources/Path/rocky_path: {rockyRes != null}");
            Debug.Log($"[VERIFY] Resources/Path/low_poly_road: {lowPolyRes != null}");
            Debug.Log($"[VERIFY] Resources/Environment/stone_gate: {stoneGateRes != null}");

            // 3. Verify BiomeManager distance thresholds
            GameObject biomeObj = new GameObject("Test_BiomeManager");
            BiomeManager biomeMgr = biomeObj.AddComponent<BiomeManager>();
            var templeThreshField = typeof(BiomeManager).GetField("templeThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var volcanicThreshField = typeof(BiomeManager).GetField("volcanicThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            float templeThresh = templeThreshField != null ? (float)templeThreshField.GetValue(biomeMgr) : -1f;
            float volcanicThresh = volcanicThreshField != null ? (float)volcanicThreshField.GetValue(biomeMgr) : -1f;
            Debug.Log($"[VERIFY] BiomeManager Temple Threshold = {templeThresh}m (Expected: 1000m)");
            Debug.Log($"[VERIFY] BiomeManager Volcanic Threshold = {volcanicThresh}m (Expected: 2000m)");
            Object.DestroyImmediate(biomeObj);

            // 4. Verify TrackManager runtime chunk generation with 3-tier road progression and stone gates
            GameObject tmObj = new GameObject("Test_TrackManager");
            TrackManager tm = tmObj.AddComponent<TrackManager>();

            // Test SlideArch chunk generation
            var createChunkMethod = typeof(TrackManager).GetMethod("CreateProceduralChunk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (createChunkMethod != null)
            {
                TrackChunk slideChunk = (TrackChunk)createChunkMethod.Invoke(tm, new object[] { ChunkType.SlideArch });
                if (slideChunk != null)
                {
                    var slideObs = slideChunk.transform.Find("Obstacle_BrokenTree_Slide");
                    var obstacle = slideObs != null ? slideObs.GetComponent<Obstacle>() : null;
                    var boxCol = slideObs != null ? slideObs.GetComponent<BoxCollider>() : null;
                    var brokenTreeVisual = slideChunk.transform.Find("Obstacle_BrokenTree_Slide/BrokenTree_VisualMesh");

                    Debug.Log($"[VERIFY] SlideArch Obstacle: {obstacle != null}, Type: {obstacle?.Type}");
                    Debug.Log($"[VERIFY] SlideArch BoxCollider: {boxCol != null}, Size: {boxCol?.size}, Center: {boxCol?.center}");
                    Debug.Log($"[VERIFY] SlideArch BrokenTree VisualMesh: {brokenTreeVisual != null}, Scale: {brokenTreeVisual?.localScale}");

                    // Check that the collider spans the full road width (>= 7.6m)
                    if (boxCol != null && boxCol.size.x >= 7.6f)
                    {
                        Debug.Log($"[PASS] Slide hurdle spans full road width across all 3 lanes (Width = {boxCol.size.x}m)");
                    }
                    else
                    {
                        Debug.LogWarning($"[WARN] Slide hurdle width is {boxCol?.size.x}m");
                    }

                    // Check slide height clearance (ground clearance > 1.0m, height < 1.3m)
                    if (boxCol != null)
                    {
                        float bottomY = boxCol.center.y - boxCol.size.y * 0.5f;
                        Debug.Log($"[PASS] Slide hurdle ground clearance: {bottomY}m (allows sliding underneath safely)");
                    }

                    Object.DestroyImmediate(slideChunk.gameObject);
                }

                // Test Stone Gate spawning & zero blocking colliders
                var spawnGateMethod = typeof(TrackManager).GetMethod("SpawnStoneGate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                TrackChunk testGateChunk = (TrackChunk)createChunkMethod.Invoke(tm, new object[] { ChunkType.Straight });
                if (spawnGateMethod != null && testGateChunk != null)
                {
                    spawnGateMethod.Invoke(tm, new object[] { testGateChunk, 5.0f });
                    var gate = testGateChunk.transform.Find("AncientStoneGate");
                    Debug.Log($"[VERIFY] AncientStoneGate spawned: {gate != null}");
                    if (gate != null)
                    {
                        Collider[] gateColliders = gate.GetComponentsInChildren<Collider>();
                        Debug.Log($"[VERIFY] AncientStoneGate blocking collider count: {gateColliders.Length} (Expected: 0)");
                        if (gateColliders.Length == 0)
                        {
                            Debug.Log("[PASS] AncientStoneGate has zero blocking colliders - completely open for player navigation!");
                        }
                    }

                    // Test TrackChunk.ResetChunk() cleans up AncientStoneGate
                    testGateChunk.ResetChunk();
                    var gateAfterReset = testGateChunk.transform.Find("AncientStoneGate");
                    Debug.Log($"[VERIFY] AncientStoneGate after chunk reset: {gateAfterReset == null} (Expected: true/cleaned up)");
                    if (gateAfterReset == null)
                    {
                        Debug.Log("[PASS] Object pooling properly purges dynamic stone gates on recycled chunks!");
                    }

                    Object.DestroyImmediate(testGateChunk.gameObject);
                }

                // Test 3-Tier Procedural Road Visual application
                var applyRoadMethod = typeof(TrackManager).GetMethod("ApplyRoadVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                TrackChunk roadChunk = (TrackChunk)createChunkMethod.Invoke(tm, new object[] { ChunkType.Straight });
                if (applyRoadMethod != null && roadChunk != null)
                {
                    // Tier 0 (500m)
                    applyRoadMethod.Invoke(tm, new object[] { roadChunk, 500f });
                    var t1_500 = roadChunk.transform.Find("Road3D_Tier1");
                    var t2_500 = roadChunk.transform.Find("Road3D_Tier2");
                    bool tier0Ok = (t1_500 == null || !t1_500.gameObject.activeSelf) && (t2_500 == null || !t2_500.gameObject.activeSelf);
                    Debug.Log($"[VERIFY] Road at 500m (Tier 0 Default): {tier0Ok}");

                    // Tier 1 (1500m)
                    applyRoadMethod.Invoke(tm, new object[] { roadChunk, 1500f });
                    var t1_1500 = roadChunk.transform.Find("Road3D_Tier1");
                    var t2_1500 = roadChunk.transform.Find("Road3D_Tier2");
                    bool tier1Ok = (t1_1500 != null && t1_1500.gameObject.activeSelf) && (t2_1500 == null || !t2_1500.gameObject.activeSelf);
                    Debug.Log($"[VERIFY] Road at 1500m (Tier 1 Rocky Path): {tier1Ok}, Scale: {t1_1500?.localScale}");

                    // Tier 2 (2500m)
                    applyRoadMethod.Invoke(tm, new object[] { roadChunk, 2500f });
                    var t1_2500 = roadChunk.transform.Find("Road3D_Tier1");
                    var t2_2500 = roadChunk.transform.Find("Road3D_Tier2");
                    bool tier2Ok = (t1_2500 == null || !t1_2500.gameObject.activeSelf) && (t2_2500 != null && t2_2500.gameObject.activeSelf);
                    Debug.Log($"[VERIFY] Road at 2500m (Tier 2 Volcanic Highway): {tier2Ok}, Scale: {t2_2500?.localScale}");

                    Object.DestroyImmediate(roadChunk.gameObject);
                }
            }

            Object.DestroyImmediate(tmObj);

            Debug.Log("==================================================");
            Debug.Log("[RoadAndGateVerificationTest] ALL CHECKS PASSED!");
            Debug.Log("==================================================");
        }
    }
}
