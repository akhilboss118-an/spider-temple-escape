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
        private const string BrainArtifactDir = "C:/Users/akhil/.gemini/antigravity-ide/brain/f91eff17-edb1-4296-a563-9eb1d8890a72";

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
                // Skybox Setup
                Material skyMat = Resources.Load<Material>("Materials/Mat_TempleSkybox");
                if (skyMat == null) skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TempleSkybox.mat");
                if (skyMat != null)
                {
                    RenderSettings.skybox = skyMat;
                }

                // Panoramic Tropical Blue Sky Vista Backdrop: Enclosing wide panoramic vista with fluffy clouds
                Texture2D skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/Tex_TempleSky.jpg");
                if (skyTex != null)
                {
                    Shader unlitShader = Shader.Find("Unlit/Texture") ?? Shader.Find("Mobile/Unlit (Supports Lightmap)") ?? MaterialHelper.GetSafeShader();
                    Material vistaMat = new Material(unlitShader);
                    vistaMat.mainTexture = skyTex;

                    // Center backdrop (spanning forward)
                    GameObject skyCenter = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    skyCenter.name = "Preview_SkyVistaBackdrop_C";
                    skyCenter.transform.SetParent(previewRoot.transform, false);
                    skyCenter.transform.position = new Vector3(0, 32.0f, 95.0f);
                    skyCenter.transform.localScale = new Vector3(280.0f, 110.0f, 1.0f);
                    skyCenter.transform.rotation = Quaternion.Euler(0, 0, 0);
                    UnityEngine.Object.DestroyImmediate(skyCenter.GetComponent<Collider>());
                    skyCenter.GetComponent<MeshRenderer>().sharedMaterial = vistaMat;

                    // Left wing backdrop (angled 45 degrees inwards)
                    GameObject skyLeft = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    skyLeft.name = "Preview_SkyVistaBackdrop_L";
                    skyLeft.transform.SetParent(previewRoot.transform, false);
                    skyLeft.transform.position = new Vector3(-130.0f, 32.0f, 50.0f);
                    skyLeft.transform.localScale = new Vector3(180.0f, 110.0f, 1.0f);
                    skyLeft.transform.rotation = Quaternion.Euler(0, 50.0f, 0);
                    UnityEngine.Object.DestroyImmediate(skyLeft.GetComponent<Collider>());
                    skyLeft.GetComponent<MeshRenderer>().sharedMaterial = vistaMat;

                    // Right wing backdrop (angled 45 degrees inwards)
                    GameObject skyRight = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    skyRight.name = "Preview_SkyVistaBackdrop_R";
                    skyRight.transform.SetParent(previewRoot.transform, false);
                    skyRight.transform.position = new Vector3(130.0f, 32.0f, 50.0f);
                    skyRight.transform.localScale = new Vector3(180.0f, 110.0f, 1.0f);
                    skyRight.transform.rotation = Quaternion.Euler(0, -50.0f, 0);
                    UnityEngine.Object.DestroyImmediate(skyRight.GetComponent<Collider>());
                    skyRight.GetComponent<MeshRenderer>().sharedMaterial = vistaMat;
                }

                // 1. Setup Directional Light & Environment Settings (Radiant golden tropical sun, high vibrant fill)
                GameObject lightObj = new GameObject("PreviewLight");
                lightObj.transform.SetParent(previewRoot.transform);
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1.0f, 0.98f, 0.92f);
                light.intensity = 1.60f;
                light.shadows = LightShadows.Soft;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.40f;
                lightObj.transform.rotation = Quaternion.Euler(52f, -34f, 0);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.82f, 0.92f, 1.0f);
                RenderSettings.ambientEquatorColor = new Color(0.72f, 0.80f, 0.68f);
                RenderSettings.ambientGroundColor = new Color(0.52f, 0.48f, 0.42f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 85.0f;
                RenderSettings.fogEndDistance = 180.0f;
                RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.98f);

                // 2. Setup Track Manager & Spawn sample chunks
                GameObject trackManagerObj = new GameObject("PreviewTrackManager");
                trackManagerObj.transform.SetParent(previewRoot.transform);
                TrackManager tm = trackManagerObj.AddComponent<TrackManager>();

                // Load materials & 3D models
                Material floorMat = Resources.Load<Material>("Materials/Mat_Track");
                if (floorMat == null) floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Track.mat");

                Material curbMat = Resources.Load<Material>("Materials/Mat_Curb");
                if (curbMat == null) curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Curb.mat");

                Material jungleGroundMat = Resources.Load<Material>("Materials/Mat_JungleGround");
                if (jungleGroundMat == null) jungleGroundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_JungleGround.mat");

                GameObject stoneGateModel = Resources.Load<GameObject>("Environment/stone_gate");
                if (stoneGateModel == null) stoneGateModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/stone_gate.obj");

                Material stoneGateMat = Resources.Load<Material>("Materials/Mat_AncientGate");
                if (stoneGateMat == null)
                {
                    Texture2D gateDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/stone_gate_tex_0.png");
                    Texture2D gateNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/stone_gate_tex_2.png");
                    stoneGateMat = MaterialHelper.CreateSafeMaterial(Color.white, gateDiff);
                    if (stoneGateMat != null && gateNorm != null && stoneGateMat.HasProperty("_BumpMap"))
                    {
                        stoneGateMat.SetTexture("_BumpMap", gateNorm);
                        stoneGateMat.EnableKeyword("_NORMALMAP");
                    }
                }

                Material mossyStoneMat = Resources.Load<Material>("Materials/Mat_MossyStone");
                if (mossyStoneMat == null) mossyStoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_MossyStone.mat");

                GameObject mossyModel = Resources.Load<GameObject>("Obstacles/mossy_stone");
                if (mossyModel == null) mossyModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/mossy_stone.obj");

                GameObject monsteraModel = Resources.Load<GameObject>("Environment/monstera3");
                if (monsteraModel == null) monsteraModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/monstera-tree/source/monstera3.fbx");

                GameObject pineModel = Resources.Load<GameObject>("Environment/pineTree");
                if (pineModel == null) pineModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/pine-tree/source/Tree.fbx");

                GameObject branchJumpModel = Resources.Load<GameObject>("Prefabs/Obstacle_TreeBranch_Jump");
                if (branchJumpModel == null) branchJumpModel = Resources.Load<GameObject>("Obstacles/tree_branch_jump");
                if (branchJumpModel == null) branchJumpModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_jump.obj");

                GameObject deadTreeModel = Resources.Load<GameObject>("Obstacles/dead_tree_obstacle");
                if (deadTreeModel == null) deadTreeModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/dead_tree_obstacle.obj");

                Material deadTreeMat = Resources.Load<Material>("Materials/Mat_DeadTree");
                if (deadTreeMat == null) deadTreeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_DeadTree.mat");
                if (deadTreeMat == null)
                {
                    Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/dead_tree_tex_0.png");
                    Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/dead_tree_tex_2.png");
                    deadTreeMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.78f, 0.70f), diff);
                    if (deadTreeMat != null && norm != null && deadTreeMat.HasProperty("_BumpMap"))
                    {
                        deadTreeMat.SetTexture("_BumpMap", norm);
                        deadTreeMat.EnableKeyword("_NORMALMAP");
                    }
                }

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
                Material monsteraMat = Resources.Load<Material>("Materials/Mat_Monstera");
                if (monsteraMat == null) monsteraMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Monstera.mat");

                Material canopyLeavesMat = Resources.Load<Material>("Materials/Mat_CanopyLeaves");
                if (canopyLeavesMat == null) canopyLeavesMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_CanopyLeaves.mat");

                Material canopyBarkMat = Resources.Load<Material>("Materials/Mat_CanopyBark");
                if (canopyBarkMat == null) canopyBarkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_CanopyBark.mat");
                if (canopyBarkMat == null)
                {
                    Texture2D trunkDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_basecolor.tga.png");
                    Texture2D trunkNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_normal.tga.png");
                    canopyBarkMat = MaterialHelper.CreateSafeMaterial(new Color(0.9f, 0.85f, 0.8f), trunkDiff);
                    if (canopyBarkMat != null && trunkNorm != null && canopyBarkMat.HasProperty("_BumpMap"))
                    {
                        canopyBarkMat.SetTexture("_BumpMap", trunkNorm);
                        canopyBarkMat.EnableKeyword("_NORMALMAP");
                    }
                }

                // Spawn 3 Chunks along Z: 0m, 10m, 20m
                for (int c = 0; c < 3; c++)
                {
                    float chunkZ = c * 10.0f;
                    GameObject chunkObj = new GameObject($"PreviewChunk_{c}");
                    chunkObj.transform.SetParent(previewRoot.transform);
                    chunkObj.transform.localPosition = new Vector3(0, 0, chunkZ);

                    // Floor (7.6m wide, 10.3m long) - Displays ancient Mayan rune stone flagstone
                    GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.SetParent(chunkObj.transform, false);
                    floor.transform.localPosition = new Vector3(0, -0.1f, 5.0f);
                    floor.transform.localScale = new Vector3(7.6f, 0.2f, 10.3f);
                    UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                    if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

                    // Colossal Stone Aqueduct Sub-Foundation & Bridge Buttress (Viaduct canyon aesthetic)
                    GameObject subFoundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    subFoundation.name = "Preview_Aqueduct_SubFoundation";
                    subFoundation.transform.SetParent(chunkObj.transform, false);
                    subFoundation.transform.localPosition = new Vector3(0, -2.6f, 5.0f);
                    subFoundation.transform.localScale = new Vector3(7.4f, 5.0f, 10.3f);
                    UnityEngine.Object.DestroyImmediate(subFoundation.GetComponent<Collider>());
                    if (curbMat != null) subFoundation.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject bridgePillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bridgePillar.name = "Preview_Aqueduct_BridgePillar";
                    bridgePillar.transform.SetParent(chunkObj.transform, false);
                    bridgePillar.transform.localPosition = new Vector3(0, -9.0f, 5.0f);
                    bridgePillar.transform.localScale = new Vector3(4.2f, 9.0f, 4.2f);
                    UnityEngine.Object.DestroyImmediate(bridgePillar.GetComponent<Collider>());
                    if (curbMat != null) bridgePillar.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    // Curbs & Stepped Stone Balustrade Pilasters
                    GameObject leftCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftCurb.transform.SetParent(chunkObj.transform, false);
                    leftCurb.transform.localPosition = new Vector3(-3.85f, 0.25f, 5.0f);
                    leftCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    UnityEngine.Object.DestroyImmediate(leftCurb.GetComponent<Collider>());
                    if (curbMat != null) leftCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject pilasterL1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pilasterL1.transform.SetParent(chunkObj.transform, false);
                    pilasterL1.transform.localPosition = new Vector3(-3.85f, 0.42f, 2.5f);
                    pilasterL1.transform.localScale = new Vector3(0.82f, 0.75f, 0.82f);
                    UnityEngine.Object.DestroyImmediate(pilasterL1.GetComponent<Collider>());
                    if (curbMat != null) pilasterL1.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject pilasterL2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pilasterL2.transform.SetParent(chunkObj.transform, false);
                    pilasterL2.transform.localPosition = new Vector3(-3.85f, 0.42f, 7.5f);
                    pilasterL2.transform.localScale = new Vector3(0.82f, 0.75f, 0.82f);
                    UnityEngine.Object.DestroyImmediate(pilasterL2.GetComponent<Collider>());
                    if (curbMat != null) pilasterL2.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject rightCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightCurb.transform.SetParent(chunkObj.transform, false);
                    rightCurb.transform.localPosition = new Vector3(3.85f, 0.25f, 5.0f);
                    rightCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    UnityEngine.Object.DestroyImmediate(rightCurb.GetComponent<Collider>());
                    if (curbMat != null) rightCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject pilasterR1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pilasterR1.transform.SetParent(chunkObj.transform, false);
                    pilasterR1.transform.localPosition = new Vector3(3.85f, 0.42f, 2.5f);
                    pilasterR1.transform.localScale = new Vector3(0.82f, 0.75f, 0.82f);
                    UnityEngine.Object.DestroyImmediate(pilasterR1.GetComponent<Collider>());
                    if (curbMat != null) pilasterR1.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    GameObject pilasterR2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pilasterR2.transform.SetParent(chunkObj.transform, false);
                    pilasterR2.transform.localPosition = new Vector3(3.85f, 0.42f, 7.5f);
                    pilasterR2.transform.localScale = new Vector3(0.82f, 0.75f, 0.82f);
                    UnityEngine.Object.DestroyImmediate(pilasterR2.GetComponent<Collider>());
                    if (curbMat != null) pilasterR2.GetComponent<MeshRenderer>().sharedMaterial = curbMat;

                    // Outer jungle terrain spanning wide to eliminate empty void borders
                    GameObject leftGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftGround.transform.SetParent(chunkObj.transform, false);
                    leftGround.transform.localPosition = new Vector3(-18.0f, -0.1f, 5.0f);
                    leftGround.transform.localScale = new Vector3(28.0f, 0.2f, 10.3f);
                    if (jungleGroundMat != null) leftGround.GetComponent<MeshRenderer>().sharedMaterial = jungleGroundMat;

                    GameObject rightGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightGround.transform.SetParent(chunkObj.transform, false);
                    rightGround.transform.localPosition = new Vector3(18.0f, -0.1f, 5.0f);
                    rightGround.transform.localScale = new Vector3(28.0f, 0.2f, 10.3f);
                    if (jungleGroundMat != null) rightGround.GetComponent<MeshRenderer>().sharedMaterial = jungleGroundMat;

                    // Flanking Ancient Fire Braziers
                    SpawnSnapshotBrazier(chunkObj.transform, new Vector3(-4.45f, 0f, 2.5f), curbMat);
                    SpawnSnapshotBrazier(chunkObj.transform, new Vector3(4.45f, 0f, 2.5f), curbMat);
                    SpawnSnapshotBrazier(chunkObj.transform, new Vector3(-4.45f, 0f, 7.5f), curbMat);
                    SpawnSnapshotBrazier(chunkObj.transform, new Vector3(4.45f, 0f, 7.5f), curbMat);

                    // Roadside Trees: Staggered 3 trees per side (X = ±5.8m to ±6.5m)
                    float[] zOffsetsL = { 1.8f, 5.0f, 8.2f };
                    for (int t = 0; t < zOffsetsL.Length; t++)
                    {
                        float zPos = zOffsetsL[t];
                        GameObject prefabL = (t % 2 == 0) ? (monsteraModel != null ? monsteraModel : pineModel) : (pineModel != null ? pineModel : monsteraModel);
                        if (prefabL != null)
                        {
                            GameObject treeL = UnityEngine.Object.Instantiate(prefabL, chunkObj.transform);
                            float xPosL = -5.8f - (t % 2) * 0.45f;
                            treeL.transform.localPosition = new Vector3(xPosL, 0f, zPos);
                            treeL.transform.localRotation = Quaternion.Euler(0, (t * 115f) % 360f, 0);
                            bool isM = (prefabL == monsteraModel);
                            float sc = isM ? 22.0f : 1.20f;
                            treeL.transform.localScale = Vector3.one * sc;
                            ApplyPreviewTreeMaterial(treeL, isM, monsteraMat, canopyBarkMat, canopyLeavesMat);
                        }
                    }

                    float[] zOffsetsR = { 2.2f, 5.4f, 8.6f };
                    for (int t = 0; t < zOffsetsR.Length; t++)
                    {
                        float zPos = zOffsetsR[t];
                        GameObject prefabR = (t % 2 == 0) ? (pineModel != null ? pineModel : monsteraModel) : (monsteraModel != null ? monsteraModel : pineModel);
                        if (prefabR != null)
                        {
                            GameObject treeR = UnityEngine.Object.Instantiate(prefabR, chunkObj.transform);
                            float xPosR = 5.8f + (t % 2) * 0.45f;
                            treeR.transform.localPosition = new Vector3(xPosR, 0f, zPos);
                            treeR.transform.localRotation = Quaternion.Euler(0, (t * 135f + 45f) % 360f, 0);
                            bool isM = (prefabR == monsteraModel);
                            float sc = isM ? 22.0f : 1.20f;
                            treeR.transform.localScale = Vector3.one * sc;
                            ApplyPreviewTreeMaterial(treeR, isM, monsteraMat, canopyBarkMat, canopyLeavesMat);
                        }
                    }

                    // On Chunk 0: Ancient Stone Gate & 3D Guide Hearts
                    if (c == 0)
                    {
                        if (stoneGateModel != null)
                        {
                            GameObject gateObj = UnityEngine.Object.Instantiate(stoneGateModel, chunkObj.transform);
                            gateObj.name = "Preview_AncientStoneGate";
                            gateObj.transform.localPosition = new Vector3(0, 0.0f, 1.0f);
                            gateObj.transform.localRotation = Quaternion.identity;
                            gateObj.transform.localScale = Vector3.one * 11.0f;
                            if (stoneGateMat != null)
                            {
                                foreach (var r in gateObj.GetComponentsInChildren<Renderer>())
                                {
                                    Material[] mats = new Material[r.sharedMaterials.Length];
                                    for (int m = 0; m < mats.Length; m++) mats[m] = stoneGateMat;
                                    r.sharedMaterials = mats;
                                }
                            }
                        }

                        // Clean, rewarding start runway: Golden hearts guiding player down the center
                        for (int k = 0; k < 6; k++)
                        {
                            float z = 1.5f + k * 1.4f;
                            GameObject heartC = new GameObject($"Preview_Heart_C_{k}");
                            heartC.transform.SetParent(chunkObj.transform, false);
                            heartC.transform.localPosition = new Vector3(0.0f, 0.85f, z);
                            heartC.tag = "Coin";
                            heartC.AddComponent<Coin>();
                        }

                        // Ancient Mystery Chest in left lane at Z = 6.5m (showcases barrel vault arched lid & golden bands)
                        GameObject puChest = new GameObject("Preview_MysteryChest");
                        puChest.transform.SetParent(chunkObj.transform, false);
                        puChest.transform.localPosition = new Vector3(-2.0f, 0.25f, 6.5f);
                        puChest.AddComponent<MysteryChest>();
                    }

                    // On Chunk 1: Minimized obstacle demonstration - single-lane tree branch jump hurdle in RIGHT lane only
                    // Leaving LEFT (X = -2) and CENTER (X = 0) completely wide open for comfortable running!
                    if (c == 1)
                    {
                        GameObject jumpRoot = new GameObject("Preview_TreeBranch_Jump");
                        jumpRoot.transform.SetParent(chunkObj.transform, false);
                        jumpRoot.transform.localPosition = new Vector3(2.0f, 0, 5.0f);

                        if (branchJumpModel != null)
                        {
                            GameObject visual = UnityEngine.Object.Instantiate(branchJumpModel, jumpRoot.transform);
                            visual.name = "JumpVisual_RightLaneOnly";
                            visual.transform.localPosition = Vector3.zero;
                            visual.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                            visual.transform.localScale = new Vector3(1.05f, 0.95f, 1.05f);

                            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
                            {
                                mr.sharedMaterial = jumpMat0 != null ? jumpMat0 : deadTreeMat;
                            }
                        }

                        // Rewarding coins through the open center and left lanes
                        for (int k = 0; k < 5; k++)
                        {
                            float z = 2.0f + k * 1.5f;
                            GameObject coinCenter = new GameObject($"Preview_Coin_C_{k}");
                            coinCenter.transform.SetParent(chunkObj.transform, false);
                            coinCenter.transform.localPosition = new Vector3(0.0f, 0.85f, z);
                            coinCenter.tag = "Coin";
                            coinCenter.AddComponent<Coin>();
                        }

                        // Speedrun Power-up (Monster Energy Can) in wide-open center lane at Z = 5.0m
                        GameObject puSpeed = new GameObject("Preview_Speedrun");
                        puSpeed.transform.SetParent(chunkObj.transform, false);
                        puSpeed.transform.localPosition = new Vector3(0.0f, 1.0f, 5.0f);
                        puSpeed.AddComponent<PowerUpItem>().Initialize(PowerUpType.Speedrun);
                    }

                    // On Chunk 2: Spawn Tree Branch Slide Obstacle (tree_branch (1).glb) with low slide hearts
                    if (c == 2)
                    {
                        // 1. Authentic 3D Broken Tree Slide Obstacle spanning full road (9.2m) at Z = 4.5m with clean slide clearance
                        GameObject slideRoot = new GameObject("Preview_BrokenTree_Slide");
                        slideRoot.transform.SetParent(chunkObj.transform, false);
                        slideRoot.transform.localPosition = new Vector3(0, 0, 4.5f);

                        if (deadTreeModel != null)
                        {
                            GameObject visual = UnityEngine.Object.Instantiate(deadTreeModel, slideRoot.transform);
                            visual.name = "BrokenTree_SlideVisual";
                            float targetSpan = 9.2f;
                            float scale = targetSpan / 100.26f;
                            visual.transform.localScale = Vector3.one * scale;
                            visual.transform.localRotation = Quaternion.Euler(-5f, 90f, 35f);
                            visual.transform.localPosition = new Vector3(-targetSpan * 0.5f, 2.50f, 0f);

                            // Anchored trunk root base to ground
                            GameObject rootAnchor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            rootAnchor.name = "Preview_Trunk_RootAnchor";
                            rootAnchor.transform.SetParent(slideRoot.transform, false);
                            rootAnchor.transform.localPosition = new Vector3(-targetSpan * 0.5f, 1.25f, 0f);
                            rootAnchor.transform.localRotation = Quaternion.Euler(0, 0, -6f);
                            rootAnchor.transform.localScale = new Vector3(1.3f, 1.35f, 1.3f);
                            UnityEngine.Object.DestroyImmediate(rootAnchor.GetComponent<Collider>());
                            if (deadTreeMat != null) rootAnchor.GetComponent<MeshRenderer>().sharedMaterial = deadTreeMat;
                            if (deadTreeMat != null)
                            {
                                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
                                    mr.sharedMaterial = deadTreeMat;
                            }
                        }
                        else if (branchSlideModel != null)
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

                // 3. Spawn Active Character (Akhilboss with running animation and high-res texture)
                GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Akhilboss/Akhilboss_Prefab.prefab");
                if (charPrefab == null) charPrefab = Resources.Load<GameObject>("Prefabs/Player_SpiderMan");
                if (charPrefab == null) charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SpiderMan/Player_SpiderMan.prefab");
                if (charPrefab != null)
                {
                    GameObject sp = UnityEngine.Object.Instantiate(charPrefab, previewRoot.transform);
                    sp.name = "Preview_Character";
                    sp.transform.localPosition = new Vector3(0, 0.05f, 4.0f);
                    sp.transform.localRotation = Quaternion.identity;

                    Texture2D charTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Akhilboss/Textures/Tex_Akhilboss.png");
                    if (charTex != null)
                    {
                        Material charMat = MaterialHelper.CreateSafeMaterial(Color.white, charTex);
                        foreach (var mr in sp.GetComponentsInChildren<Renderer>())
                            mr.sharedMaterial = charMat;
                    }
                    else
                    {
                        Material spiderMat = Resources.Load<Material>("Materials/Mat_Player");
                        if (spiderMat == null) spiderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
                        if (spiderMat != null)
                        {
                            foreach (var mr in sp.GetComponentsInChildren<Renderer>())
                                mr.sharedMaterial = spiderMat;
                        }
                    }

                    // Add Transparent Blue Shield Aura to Preview Character
                    GameObject shieldRoot = new GameObject("Preview_ShieldAura");
                    shieldRoot.transform.SetParent(previewRoot.transform, false);
                    shieldRoot.transform.position = sp.transform.position + new Vector3(0, 1.05f, 0);

                    GameObject coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    coreSphere.transform.SetParent(shieldRoot.transform, false);
                    coreSphere.transform.localPosition = Vector3.zero;
                    coreSphere.transform.localScale = new Vector3(1.35f, 2.05f, 1.35f);
                    UnityEngine.Object.DestroyImmediate(coreSphere.GetComponent<Collider>());

                    GameObject ring = new GameObject("Shield_OrbitRing");
                    ring.transform.SetParent(shieldRoot.transform, false);
                    ring.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);
                    Mesh ringMesh = new Mesh();
                    int rSegs = 32;
                    Vector3[] rVerts = new Vector3[rSegs * 2];
                    Vector2[] rUvs = new Vector2[rSegs * 2];
                    int[] rTris = new int[rSegs * 6];
                    for (int ri = 0; ri < rSegs; ri++)
                    {
                        float ra = (ri / (float)rSegs) * Mathf.PI * 2f;
                        rVerts[ri * 2] = new Vector3(Mathf.Cos(ra) * 0.76f, 0f, Mathf.Sin(ra) * 0.76f);
                        rVerts[ri * 2 + 1] = new Vector3(Mathf.Cos(ra) * 0.90f, 0f, Mathf.Sin(ra) * 0.90f);
                        rUvs[ri * 2] = new Vector2(ri / (float)rSegs, 0f);
                        rUvs[ri * 2 + 1] = new Vector2(ri / (float)rSegs, 1f);
                        int rn = (ri + 1) % rSegs;
                        int rtIdx = ri * 6;
                        rTris[rtIdx] = ri * 2; rTris[rtIdx + 1] = ri * 2 + 1; rTris[rtIdx + 2] = rn * 2 + 1;
                        rTris[rtIdx + 3] = ri * 2; rTris[rtIdx + 4] = rn * 2 + 1; rTris[rtIdx + 5] = rn * 2;
                    }
                    ringMesh.vertices = rVerts; ringMesh.uv = rUvs; ringMesh.triangles = rTris;
                    ringMesh.RecalculateNormals();
                    ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                    ring.AddComponent<MeshRenderer>();

                    Shader sShader = Shader.Find("Custom/AAA_ShieldAura");
                    if (sShader != null)
                    {
                        Material sMat = new Material(sShader);
                        sMat.SetColor("_AuraColor", new Color(0.04f, 0.40f, 1.0f, 0.05f));
                        sMat.SetColor("_RimColor", new Color(0.20f, 0.85f, 1.0f, 0.95f));
                        sMat.SetFloat("_RimPower", 3.5f);
                        sMat.SetFloat("_RimIntensity", 2.6f);
                        coreSphere.GetComponent<MeshRenderer>().sharedMaterial = sMat;
                        ring.GetComponent<MeshRenderer>().sharedMaterial = sMat;
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
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(0.35f, 0.65f, 0.95f);
                if (skyMat != null)
                {
                    Skybox sb = camObj.AddComponent<Skybox>();
                    sb.material = skyMat;
                }

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

                // 7. Render Direct Slide Hurdle Close-Up View (eye-level inspection of slide clearance)
                cam.transform.position = new Vector3(0, 1.7f, 19.0f);
                cam.transform.rotation = Quaternion.Euler(2f, 0, 0);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D shotCloseup = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shotCloseup.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shotCloseup.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;

                byte[] closeupBytes = shotCloseup.EncodeToPNG();
                string wsCloseupPath = Path.Combine(Application.dataPath, "../scene_slide_closeup.png");
                File.WriteAllBytes(wsCloseupPath, closeupBytes);

                if (Directory.Exists(BrainArtifactDir))
                {
                    string brainCloseupPath = Path.Combine(BrainArtifactDir, "scene_slide_closeup.png");
                    File.WriteAllBytes(brainCloseupPath, closeupBytes);
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
                foreach (var r in treeObj.GetComponentsInChildren<Renderer>())
                {
                    string rName = r.name.ToLower();
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++)
                    {
                        string matName = (r.sharedMaterials[m] != null) ? r.sharedMaterials[m].name.ToLower() : "";
                        if (rName.Contains("leav") || matName.Contains("leav") || rName.Contains("leaf") || matName.Contains("leaf"))
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

        private static void SpawnSnapshotBrazier(Transform parent, Vector3 localPos, Material stoneMat)
        {
            GameObject brazier = new GameObject("RoadsideBrazier");
            brazier.transform.SetParent(parent, false);
            brazier.transform.localPosition = localPos;

            GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/torch_brazier.obj")
                                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Environment/torch_brazier.obj");
            Material torchMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Torch.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Mat_Torch.mat");
            Material flameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Torch_Flame.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Mat_Torch_Flame.mat");

            if (torchPrefab != null)
            {
                GameObject torchVisual = UnityEngine.Object.Instantiate(torchPrefab, brazier.transform);
                torchVisual.name = "TorchBrazier_Visual";
                torchVisual.transform.localPosition = Vector3.zero;
                torchVisual.transform.localRotation = Quaternion.identity;
                torchVisual.transform.localScale = Vector3.one * 0.35f;

                foreach (var col in torchVisual.GetComponentsInChildren<Collider>())
                    UnityEngine.Object.DestroyImmediate(col);

                foreach (var r in torchVisual.GetComponentsInChildren<Renderer>())
                {
                    string rName = r.gameObject.name.ToLower();
                    if (rName.Contains("flame"))
                    {
                        if (flameMat != null) r.sharedMaterial = flameMat;
                    }
                    else if (rName.Contains("torch"))
                    {
                        if (torchMat != null) r.sharedMaterial = torchMat;
                    }
                    else
                    {
                        Material[] mats = r.sharedMaterials;
                        if (mats != null && mats.Length >= 2)
                        {
                            if (torchMat != null) mats[0] = torchMat;
                            if (flameMat != null) mats[1] = flameMat;
                            r.sharedMaterials = mats;
                        }
                        else if (torchMat != null)
                        {
                            r.sharedMaterial = torchMat;
                        }
                    }
                }
            }
            else
            {
                // Stone pedestal
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.transform.SetParent(brazier.transform, false);
                pedestal.transform.localPosition = new Vector3(0, 0.45f, 0);
                pedestal.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
                if (stoneMat != null) pedestal.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;

                // Fire bowl
                GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bowl.transform.SetParent(brazier.transform, false);
                bowl.transform.localPosition = new Vector3(0, 0.95f, 0);
                bowl.transform.localScale = new Vector3(0.70f, 0.35f, 0.70f);
                if (stoneMat != null) bowl.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;

                // Glowing ember core
                GameObject ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ember.transform.SetParent(brazier.transform, false);
                ember.transform.localPosition = new Vector3(0, 1.05f, 0);
                ember.transform.localScale = new Vector3(0.40f, 0.30f, 0.40f);
                Material emberMat = new Material(Shader.Find("Standard"));
                emberMat.color = new Color(1.0f, 0.45f, 0.10f);
                emberMat.EnableKeyword("_EMISSION");
                emberMat.SetColor("_EmissionColor", new Color(1.0f, 0.55f, 0.15f) * 2.5f);
                ember.GetComponent<MeshRenderer>().sharedMaterial = emberMat;
            }

            // Warm amber point light
            GameObject lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(brazier.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.25f, 0.05f);
            Light pLight = lightGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.65f, 0.20f);
            pLight.intensity = 2.8f;
            pLight.range = 8.5f;
            pLight.shadows = LightShadows.None;
        }
    }
}
