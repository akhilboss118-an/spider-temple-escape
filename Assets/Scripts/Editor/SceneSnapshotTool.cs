using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Runner.Core;
using Runner.Track;
using Runner.Pickups;

namespace Runner.EditorTools
{
    public static class SceneSnapshotTool
    {
        private const string BrainArtifactDir = "C:/Users/akhil/.gemini/antigravity-ide/brain/fc42aa3c-8218-4b66-a958-486379d97009";
        // Re-trigger snapshot timestamp: 2026-09-06T16:08:50

        [InitializeOnLoadMethod]
        public static void RegisterAutoCapture()
        {
            EditorApplication.delayCall += () =>
            {
                CaptureSceneSnapshot();
            };
        }

        public static void CaptureCommandLine()
        {
            Debug.Log("[SceneSnapshotTool] Starting CLI Snapshot Capture...");
            CaptureSceneSnapshot();
            Debug.Log("[SceneSnapshotTool] CLI Snapshot Capture complete.");
        }

        [MenuItem("Runner/Capture Scene Snapshot")]
        public static void CaptureSceneSnapshot()
        {
            Debug.Log("[SceneSnapshotTool] Rendering Scene Snapshot Preview...");

            GameObject previewRoot = new GameObject("_ScenePreviewRoot");
            Camera cam = null;
            RenderTexture rt = null;

            try
            {
                // 1. Setup Directional Light & Environment Settings
                GameObject lightObj = new GameObject("PreviewLight");
                lightObj.transform.SetParent(previewRoot.transform);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1.0f, 0.97f, 0.90f);
                light.intensity = 1.20f;
                light.shadows = LightShadows.Soft;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.40f;
                lightObj.transform.rotation = Quaternion.Euler(46f, -38f, 0);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.52f);
                RenderSettings.ambientEquatorColor = new Color(0.34f, 0.38f, 0.32f);
                RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.16f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 45.0f;
                RenderSettings.fogEndDistance = 120.0f;
                RenderSettings.fogColor = new Color(0.20f, 0.34f, 0.28f);

                // 2. Setup Track Manager & Spawn sample chunks
                GameObject trackManagerObj = new GameObject("PreviewTrackManager");
                trackManagerObj.transform.SetParent(previewRoot.transform);
                TrackManager tm = trackManagerObj.AddComponent<TrackManager>();

                // Load materials & 3D models
                Material floorMat = Resources.Load<Material>("Materials/Mat_Track");
                if (floorMat == null) floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Track.mat");

                Material curbMat = Resources.Load<Material>("Materials/Mat_Curb");
                if (curbMat == null) curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Curb.mat");

                Material mossyStoneMat = Resources.Load<Material>("Materials/Mat_MossyStone");
                if (mossyStoneMat == null) mossyStoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_MossyStone.mat");

                Material sidewalkMat = Resources.Load<Material>("Materials/Mat_Sidewalk");
                if (sidewalkMat == null) sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Sidewalk.mat");

                GameObject sidewalkModel = Resources.Load<GameObject>("Path/path_sidewalk");
                if (sidewalkModel == null) sidewalkModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/path_sidewalk.obj");

                GameObject mossyModel = Resources.Load<GameObject>("Obstacles/mossy_stone");
                if (mossyModel == null) mossyModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/mossy_stone.obj");

                GameObject monsteraModel = Resources.Load<GameObject>("Environment/monstera3");
                if (monsteraModel == null) monsteraModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/monstera-tree/source/monstera3.fbx");

                GameObject pineModel = Resources.Load<GameObject>("Environment/pineTree");
                if (pineModel == null) pineModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/pine-tree/source/Tree.fbx");

                GameObject branchJumpModel = Resources.Load<GameObject>("Prefabs/Obstacle_TreeBranch_Jump");
                if (branchJumpModel == null) branchJumpModel = Resources.Load<GameObject>("Obstacles/tree_branch_jump");
                if (branchJumpModel == null) branchJumpModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_jump.obj");

                GameObject branchSlideModel = Resources.Load<GameObject>("Prefabs/Obstacle_TreeBranch_Slide");
                if (branchSlideModel == null) branchSlideModel = Resources.Load<GameObject>("Obstacles/tree_branch_slide");
                if (branchSlideModel == null) branchSlideModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_slide.obj");

                Material jumpMat0 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_0.mat");
                Material jumpMat1 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_1.mat");
                Material jumpMat2 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_2.mat");

                Material slideBranchMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Slide.mat");
                if (slideBranchMat == null)
                {
                    Texture2D slideTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_slide_tex_0.png");
                    Texture2D slideNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_slide_tex_2.png");
                    slideBranchMat = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.90f, 0.85f), slideTex);
                    if (slideBranchMat != null && slideNorm != null && slideBranchMat.HasProperty("_BumpMap"))
                    {
                        slideBranchMat.SetTexture("_BumpMap", slideNorm);
                        slideBranchMat.EnableKeyword("_NORMALMAP");
                    }
                }

                // Foliage Materials
                Texture2D monsteraDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/ALBEDO-monstera.png");
                Texture2D monsteraNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/SNormal-monstera3.png");
                Material monsteraMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.95f, 0.85f), monsteraDiff);
                if (monsteraMat != null && monsteraNorm != null && monsteraMat.HasProperty("_BumpMap"))
                {
                    monsteraMat.SetTexture("_BumpMap", monsteraNorm);
                    monsteraMat.EnableKeyword("_NORMALMAP");
                }

                Texture2D trunkDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_basecolor.tga.png");
                Texture2D trunkNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_normal.tga.png");
                Material canopyBarkMat = MaterialHelper.CreateSafeMaterial(new Color(0.9f, 0.85f, 0.8f), trunkDiff);
                if (canopyBarkMat != null && trunkNorm != null && canopyBarkMat.HasProperty("_BumpMap"))
                {
                    canopyBarkMat.SetTexture("_BumpMap", trunkNorm);
                    canopyBarkMat.EnableKeyword("_NORMALMAP");
                }

                Texture2D leavesDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Leavs_basecolor_.tga.png");
                Material canopyLeavesMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.95f, 0.85f), leavesDiff);

                // Spawn 3 Chunks along Z: 0m, 10m, 20m
                for (int c = 0; c < 3; c++)
                {
                    float chunkZ = c * 10.0f;
                    GameObject chunkObj = new GameObject($"PreviewChunk_{c}");
                    chunkObj.transform.SetParent(previewRoot.transform);
                    chunkObj.transform.localPosition = new Vector3(0, 0, chunkZ);

                    // Floor (7.6m wide, 10.3m long)
                    GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.SetParent(chunkObj.transform, false);
                    floor.transform.localPosition = new Vector3(0, -0.1f, 5.0f);
                    floor.transform.localScale = new Vector3(7.6f, 0.2f, 10.3f);
                    if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

                    // 3D Sidewalk
                    if (sidewalkModel != null)
                    {
                        GameObject sw = UnityEngine.Object.Instantiate(sidewalkModel, chunkObj.transform);
                        sw.transform.localPosition = new Vector3(0, -0.01f, 5.0f);
                        sw.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                        sw.transform.localScale = new Vector3(5.2f, 1.2f, 0.703f);
                        if (sidewalkMat != null)
                        {
                            foreach (var mr in sw.GetComponentsInChildren<MeshRenderer>())
                                mr.sharedMaterial = sidewalkMat;
                        }
                    }

                    // Curbs
                    GameObject leftCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftCurb.transform.SetParent(chunkObj.transform, false);
                    leftCurb.transform.localPosition = new Vector3(-3.85f, 0.25f, 5.0f);
                    leftCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    if (curbMat != null) leftCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject rightCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightCurb.transform.SetParent(chunkObj.transform, false);
                    rightCurb.transform.localPosition = new Vector3(3.85f, 0.25f, 5.0f);
                    rightCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    if (curbMat != null) rightCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    // Outer jungle terrain
                    GameObject leftGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftGround.transform.SetParent(chunkObj.transform, false);
                    leftGround.transform.localPosition = new Vector3(-8.85f, -0.1f, 5.0f);
                    leftGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    if (curbMat != null) leftGround.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject rightGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightGround.transform.SetParent(chunkObj.transform, false);
                    rightGround.transform.localPosition = new Vector3(8.85f, -0.1f, 5.0f);
                    rightGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    if (curbMat != null) rightGround.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    // Roadside Trees: Exactly 8 trees per line on BOTH sides (set back at X = ±6.0m to ±6.8m)
                    for (int t = 0; t < 8; t++)
                    {
                        float zPos = 0.6f + t * 1.25f;
                        // Left line (8 trees)
                        GameObject prefabL = (t % 2 == 0) ? (monsteraModel != null ? monsteraModel : pineModel) : (pineModel != null ? pineModel : monsteraModel);
                        if (prefabL != null)
                        {
                            GameObject treeL = UnityEngine.Object.Instantiate(prefabL, chunkObj.transform);
                            float xPosL = -6.0f - (t % 3) * 0.45f;
                            treeL.transform.localPosition = new Vector3(xPosL, 0f, zPos);
                            treeL.transform.localRotation = Quaternion.Euler(0, t * 45f, 0);
                            float sc = (prefabL == monsteraModel ? 1.35f : 1.15f);
                            treeL.transform.localScale = Vector3.one * sc;
                            ApplyPreviewTreeMaterial(treeL, prefabL == monsteraModel, monsteraMat, canopyBarkMat, canopyLeavesMat);
                        }
                        // Right line (8 trees)
                        GameObject prefabR = (t % 2 == 0) ? (pineModel != null ? pineModel : monsteraModel) : (monsteraModel != null ? monsteraModel : pineModel);
                        if (prefabR != null)
                        {
                            GameObject treeR = UnityEngine.Object.Instantiate(prefabR, chunkObj.transform);
                            float xPosR = 6.0f + (t % 3) * 0.45f;
                            treeR.transform.localPosition = new Vector3(xPosR, 0f, zPos);
                            treeR.transform.localRotation = Quaternion.Euler(0, t * 45f + 25f, 0);
                            float sc = (prefabR == monsteraModel ? 1.35f : 1.15f);
                            treeR.transform.localScale = Vector3.one * sc;
                            ApplyPreviewTreeMaterial(treeR, prefabR == monsteraModel, monsteraMat, canopyBarkMat, canopyLeavesMat);
                        }
                    }

                    // On Chunk 0: 3D Pink Hearts on EVERY side (one lane in each side: Left X = -2.0m, Right X = +2.0m)
                    if (c == 0)
                    {
                        for (int k = 0; k < 10; k++)
                        {
                            float z = 0.5f + k * 1.0f;
                            // Left lane hearts
                            GameObject heartL = new GameObject($"Preview_Heart_L_{k}");
                            heartL.transform.SetParent(chunkObj.transform, false);
                            heartL.transform.localPosition = new Vector3(-2.0f, 0.85f, z);
                            heartL.tag = "Coin";
                            heartL.AddComponent<Coin>();

                            // Right lane hearts
                            GameObject heartR = new GameObject($"Preview_Heart_R_{k}");
                            heartR.transform.SetParent(chunkObj.transform, false);
                            heartR.transform.localPosition = new Vector3(2.0f, 0.85f, z);
                            heartR.tag = "Coin";
                            heartR.AddComponent<Coin>();
                        }
                    }

                    // On Chunk 1: Spawn Tree Branch Jump Obstacle (tree_branch.glb) with arcing hearts and Speedrun Power-up
                    if (c == 1)
                    {
                        // 1. Tree Branch Jump Obstacle spanning all 3 lanes at Z = 5.0m
                        GameObject jumpRoot = new GameObject("Preview_TreeBranch_Jump");
                        jumpRoot.transform.SetParent(chunkObj.transform, false);
                        jumpRoot.transform.localPosition = new Vector3(0, 0, 5.0f);

                        if (branchJumpModel != null)
                        {
                            float[] logXCoords = { -2.0f, 0.0f, 2.0f };
                            for (int i = 0; i < 3; i++)
                            {
                                GameObject visual = UnityEngine.Object.Instantiate(branchJumpModel, jumpRoot.transform);
                                visual.name = $"JumpVisual_{i}";
                                visual.transform.localPosition = new Vector3(logXCoords[i], 0.02f, (i % 2 == 0 ? 0.05f : -0.05f));
                                visual.transform.localRotation = Quaternion.Euler(0, 90f + (i == 0 ? -4f : (i == 1 ? 2f : -3f)), 0);
                                visual.transform.localScale = new Vector3(1.05f, 0.95f, 1.05f);

                                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
                                {
                                    string rName = mr.gameObject.name.ToLower();
                                    Material chosen = jumpMat0;
                                    if (rName.Contains("1") || rName.Contains("scan_1")) chosen = jumpMat1;
                                    else if (rName.Contains("2") || rName.Contains("scan_2")) chosen = jumpMat2;
                                    mr.sharedMaterial = chosen != null ? chosen : jumpMat0;
                                }
                            }
                        }

                        // Arcing Pink Hearts leaping over the branch obstacle in center lane
                        float[] arcYs = { 0.85f, 1.65f, 0.85f };
                        float[] arcZs = { 3.0f, 5.0f, 7.0f };
                        for (int h = 0; h < 3; h++)
                        {
                            GameObject hObj = new GameObject($"ArcHeart_{h}");
                            hObj.transform.SetParent(chunkObj.transform, false);
                            hObj.transform.localPosition = new Vector3(0, arcYs[h], arcZs[h]);
                            hObj.tag = "Coin";
                            hObj.AddComponent<Coin>();
                        }

                        // Speedrun Power-up (Monster Energy Can) in center lane at Z = 1.5m
                        GameObject puSpeed = new GameObject("Preview_Speedrun");
                        puSpeed.transform.SetParent(chunkObj.transform, false);
                        puSpeed.transform.localPosition = new Vector3(0.0f, 1.0f, 1.5f);
                        puSpeed.AddComponent<PowerUpItem>().Initialize(PowerUpType.Speedrun);

                        // Mossy rock in right lane at Z = 8.0m
                        if (mossyModel != null)
                        {
                            GameObject rockRight = UnityEngine.Object.Instantiate(mossyModel, chunkObj.transform);
                            rockRight.transform.localScale = new Vector3(0.38f, 0.30f, 0.42f);
                            rockRight.transform.localPosition = new Vector3(2.0f, 0.53f, 8.5f);
                            if (mossyStoneMat != null)
                            {
                                foreach (var mr in rockRight.GetComponentsInChildren<MeshRenderer>())
                                    mr.sharedMaterial = mossyStoneMat;
                            }
                        }
                    }

                    // On Chunk 2: Spawn Tree Branch Slide Obstacle (tree_branch (1).glb) with low slide hearts
                    if (c == 2)
                    {
                        // 1. Tree Branch Slide Overhead Obstacle spanning all 3 lanes at Z = 4.5m
                        GameObject slideRoot = new GameObject("Preview_TreeBranch_Slide");
                        slideRoot.transform.SetParent(chunkObj.transform, false);
                        slideRoot.transform.localPosition = new Vector3(0, 0, 4.5f);

                        if (branchSlideModel != null)
                        {
                            GameObject visual = UnityEngine.Object.Instantiate(branchSlideModel, slideRoot.transform);
                            visual.name = "SlideVisual";
                            float scale = 0.123f;
                            visual.transform.localScale = Vector3.one * scale;
                            visual.transform.localPosition = new Vector3(11.90f * scale, 1.15f, 0);
                            visual.transform.localRotation = Quaternion.identity;
                            if (slideBranchMat != null)
                            {
                                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
                                    mr.sharedMaterial = slideBranchMat;
                            }
                        }

                        // Low Pink Hearts on ground to slide underneath
                        for (int h = 0; h < 3; h++)
                        {
                            GameObject hObj = new GameObject($"SlideHeart_{h}");
                            hObj.transform.SetParent(chunkObj.transform, false);
                            hObj.transform.localPosition = new Vector3(0, 0.35f, 2.5f + h * 2.0f);
                            hObj.tag = "Coin";
                            hObj.AddComponent<Coin>();
                        }

                        // Shield Power-up in right lane at Z = 7.0m
                        GameObject puShield = new GameObject("Preview_Shield");
                        puShield.transform.SetParent(chunkObj.transform, false);
                        puShield.transform.localPosition = new Vector3(2.0f, 1.0f, 7.0f);
                        puShield.AddComponent<PowerUpItem>().Initialize(PowerUpType.Shield);

                        // Magnet Power-up in left lane at Z = 2.0m
                        GameObject puMagnet = new GameObject("Preview_Magnet");
                        puMagnet.transform.SetParent(chunkObj.transform, false);
                        puMagnet.transform.localPosition = new Vector3(-2.0f, 1.0f, 2.0f);
                        puMagnet.AddComponent<PowerUpItem>().Initialize(PowerUpType.Magnet);
                    }
                }

                // 3. Spawn Spider-Man Player
                GameObject spiderPrefab = Resources.Load<GameObject>("Prefabs/Player_SpiderMan");
                if (spiderPrefab == null) spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SpiderMan/Player_SpiderMan.prefab");
                if (spiderPrefab != null)
                {
                    GameObject sp = UnityEngine.Object.Instantiate(spiderPrefab, previewRoot.transform);
                    sp.name = "Preview_SpiderMan";
                    sp.transform.localPosition = new Vector3(0, 0.05f, 4.0f);
                    sp.transform.localRotation = Quaternion.identity;

                    Material spiderMat = Resources.Load<Material>("Materials/Mat_Player");
                    if (spiderMat == null) spiderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
                    if (spiderMat != null)
                    {
                        foreach (var mr in sp.GetComponentsInChildren<Renderer>())
                            mr.sharedMaterial = spiderMat;
                    }
                }

                // 4. Spawn Zombie Chaser
                GameObject zombiePrefab = Resources.Load<GameObject>("Prefabs/Monster_Zombie");
                if (zombiePrefab == null) zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Zombie/Monster_Zombie.prefab");
                if (zombiePrefab != null)
                {
                    GameObject zb = UnityEngine.Object.Instantiate(zombiePrefab, previewRoot.transform);
                    zb.name = "Preview_Zombie";
                    zb.transform.localPosition = new Vector3(0, 0.05f, 0.5f);
                    zb.transform.localRotation = Quaternion.identity;

                    Material zombieMat = Resources.Load<Material>("Materials/Mat_Monster");
                    if (zombieMat == null) zombieMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Monster.mat");
                    if (zombieMat != null)
                    {
                        foreach (var mr in zb.GetComponentsInChildren<Renderer>())
                            mr.sharedMaterial = zombieMat;
                    }
                }

                // 5. Render Scene from Third-Person Gameplay Perspective
                GameObject camObj = new GameObject("PreviewCam");
                camObj.transform.SetParent(previewRoot.transform);
                cam = camObj.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 3.2f, -1.8f);
                cam.transform.rotation = Quaternion.Euler(16f, 0, 0);
                cam.fieldOfView = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.18f, 0.28f, 0.22f);

                rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shot.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;

                byte[] pngBytes = shot.EncodeToPNG();

                // Save to workspace root
                string wsPath = Path.Combine(Application.dataPath, "../scene_preview.png");
                File.WriteAllBytes(wsPath, pngBytes);
                Debug.Log($"[SceneSnapshotTool] Saved workspace preview to: {wsPath}");

                // Save to UI Textures
                string uiPath = Path.Combine(Application.dataPath, "Textures/UI/scene_preview.png");
                File.WriteAllBytes(uiPath, pngBytes);

                // Save to conversation brain artifacts folder
                if (Directory.Exists(BrainArtifactDir))
                {
                    string brainPath = Path.Combine(BrainArtifactDir, "scene_preview.png");
                    File.WriteAllBytes(brainPath, pngBytes);
                    Debug.Log($"[SceneSnapshotTool] Saved brain artifact to: {brainPath}");
                }

                // 6. Render Overview Angle
                cam.transform.position = new Vector3(0, 7.5f, 1.5f);
                cam.transform.rotation = Quaternion.Euler(32f, 0, 0);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D shotOverview = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shotOverview.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shotOverview.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;

                byte[] overviewBytes = shotOverview.EncodeToPNG();
                string wsOverviewPath = Path.Combine(Application.dataPath, "../scene_overview.png");
                File.WriteAllBytes(wsOverviewPath, overviewBytes);

                if (Directory.Exists(BrainArtifactDir))
                {
                    string brainOverviewPath = Path.Combine(BrainArtifactDir, "scene_overview.png");
                    File.WriteAllBytes(brainOverviewPath, overviewBytes);
                }

                Debug.Log("[SceneSnapshotTool] All scene snapshots successfully rendered and saved!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneSnapshotTool] Error rendering snapshot: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (previewRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewRoot);
                }
            }
        }

        private static void ApplyPreviewTreeMaterial(GameObject treeObj, bool isMonstera, Material monsteraMat, Material barkMat, Material leavesMat)
        {
            if (isMonstera)
            {
                if (monsteraMat == null) return;
                foreach (var r in treeObj.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = monsteraMat;
                    r.sharedMaterials = mats;
                }
            }
            else
            {
                if (barkMat == null && leavesMat == null) return;
                foreach (var r in treeObj.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++)
                    {
                        string matName = (r.sharedMaterials[m] != null) ? r.sharedMaterials[m].name.ToLower() : "";
                        if (matName.Contains("leaf") || matName.Contains("leaves") || m > 0)
                        {
                            mats[m] = leavesMat != null ? leavesMat : barkMat;
                        }
                        else
                        {
                            mats[m] = barkMat != null ? barkMat : leavesMat;
                        }
                    }
                    r.sharedMaterials = mats;
                }
            }
        }
    }
}
