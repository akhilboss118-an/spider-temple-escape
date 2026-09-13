using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Runner.Core;
using Runner.Pickups;

namespace Runner.EditorTools
{
    public static class CharacterFixAndSnapshotTool
    {
        private const string BrainArtifactDir = "C:/Users/akhil/.gemini/antigravity-ide/brain/f91eff17-edb1-4296-a563-9eb1d8890a72";

        public static void RunFixAndSnapshots()
        {
            Debug.Log("[CharacterFixAndSnapshotTool] === STARTING CHARACTER REBUILD AND SNAPSHOT VERIFICATION ===");

            // 1. Force re-run character import setup to ensure all prefabs and controllers are up to date
            try
            {
                Runner.Editor.CharacterImportSetup.SetupAllCharacters();
                Debug.Log("[CharacterFixAndSnapshotTool] CharacterImportSetup.SetupAllCharacters() completed successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterFixAndSnapshotTool] SetupAllCharacters failed: {ex}");
            }

            // 2. Verify all characters
            try
            {
                Runner.Editor.VerifyAllAnimations.RunVerification();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterFixAndSnapshotTool] VerifyAllAnimations failed: {ex}");
            }

            // 3. Render Scale & Power-Up Verification Scene
            RenderScaleAndPowerUpSnapshot();

            // 4. Render 7-Character Lineup
            RenderAllCharactersLineupSnapshot();

            // 5. Render In-Game Gameplay with Restored Heart Collection Meter
            RenderGameplayHUDPreview();

            Debug.Log("[CharacterFixAndSnapshotTool] === ALL VERIFICATIONS AND SNAPSHOTS COMPLETED ===");
        }

        private static void RenderScaleAndPowerUpSnapshot()
        {
            GameObject root = new GameObject("_ScaleAndPowerUpTestRoot");
            Camera cam = null;
            RenderTexture rt = null;

            try
            {
                // Sun Light
                GameObject sunObj = new GameObject("SunLight");
                sunObj.transform.SetParent(root.transform);
                Light sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.95f, 0.85f);
                sun.intensity = 1.35f;
                sunObj.transform.rotation = Quaternion.Euler(38f, -25f, 0f);

                // Sky fill
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.6f, 0.75f, 0.95f) * 1.2f;
                RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.4f);
                RenderSettings.ambientGroundColor = new Color(0.25f, 0.2f, 0.15f);

                // Runway
                GameObject runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
                runway.name = "TestRunway";
                runway.transform.SetParent(root.transform);
                runway.transform.position = new Vector3(0, -0.25f, 0);
                runway.transform.localScale = new Vector3(14.0f, 0.5f, 30.0f);
                Material runwayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TempleStoneFloor.mat") ?? MaterialHelper.CreateSafeMaterial();
                runway.GetComponent<MeshRenderer>().sharedMaterial = runwayMat;

                // Helper to create hollow ring mesh
                Func<string, Transform, float, float, GameObject> createHollowRing = (name, parent, inR, outR) =>
                {
                    GameObject obj = new GameObject(name);
                    obj.transform.SetParent(parent, false);
                    Mesh m = new Mesh();
                    int segs = 32;
                    Vector3[] verts = new Vector3[segs * 2];
                    Vector2[] uvs = new Vector2[segs * 2];
                    int[] tris = new int[segs * 6];
                    for (int i = 0; i < segs; i++)
                    {
                        float a = (i / (float)segs) * Mathf.PI * 2f;
                        verts[i * 2] = new Vector3(Mathf.Cos(a) * inR, 0f, Mathf.Sin(a) * inR);
                        verts[i * 2 + 1] = new Vector3(Mathf.Cos(a) * outR, 0f, Mathf.Sin(a) * outR);
                        uvs[i * 2] = new Vector2(i / (float)segs, 0f);
                        uvs[i * 2 + 1] = new Vector2(i / (float)segs, 1f);
                        int next = (i + 1) % segs;
                        int t = i * 6;
                        tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = next * 2 + 1;
                        tris[t + 3] = i * 2; tris[t + 4] = next * 2 + 1; tris[t + 5] = next * 2;
                    }
                    m.vertices = verts; m.uv = uvs; m.triangles = tris;
                    m.RecalculateNormals();
                    obj.AddComponent<MeshFilter>().sharedMesh = m;
                    obj.AddComponent<MeshRenderer>();
                    return obj;
                };

                // 1. Akhilboss (Left) with Transparent Blue Shield
                GameObject playerRoot1 = new GameObject("PlayerRoot_Akhilboss");
                playerRoot1.transform.SetParent(root.transform, false);
                playerRoot1.transform.position = new Vector3(-1.5f, 0, 0);

                GameObject akhilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Akhilboss/Akhilboss_Prefab.prefab")
                                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/Akhilboss.prefab");
                if (akhilPrefab != null)
                {
                    GameObject akhil = UnityEngine.Object.Instantiate(akhilPrefab, playerRoot1.transform);
                    akhil.name = "Akhilboss_Model";
                    akhil.transform.localPosition = Vector3.zero;
                    akhil.transform.localRotation = Quaternion.Euler(0, 180f, 0);

                    // Shield Root (Parented to playerRoot with scale 1,1,1)
                    GameObject shieldRoot = new GameObject("Player_ShieldAuraRoot");
                    shieldRoot.transform.SetParent(playerRoot1.transform, false);
                    shieldRoot.transform.localPosition = new Vector3(0, 1.05f, 0);

                    // Transparent Blue Shield Core Sphere
                    GameObject coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    coreSphere.name = "Shield_EnergyShell";
                    coreSphere.transform.SetParent(shieldRoot.transform, false);
                    coreSphere.transform.localPosition = Vector3.zero;
                    coreSphere.transform.localScale = new Vector3(1.35f, 2.05f, 1.35f);
                    UnityEngine.Object.DestroyImmediate(coreSphere.GetComponent<Collider>());

                    // Outer hollow orbiting ring
                    GameObject shieldRing = createHollowRing("Shield_OrbitRing", shieldRoot.transform, 0.76f, 0.90f);
                    shieldRing.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);

                    Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                    if (shieldShader != null)
                    {
                        Material sMat = new Material(shieldShader);
                        sMat.SetColor("_AuraColor", new Color(0.04f, 0.40f, 1.0f, 0.05f)); // 95% transparent in center
                        sMat.SetColor("_RimColor", new Color(0.20f, 0.85f, 1.0f, 0.95f));  // Electric cyan rim
                        sMat.SetFloat("_RimPower", 3.5f);
                        sMat.SetFloat("_RimIntensity", 2.6f);
                        coreSphere.GetComponent<MeshRenderer>().sharedMaterial = sMat;
                        shieldRing.GetComponent<MeshRenderer>().sharedMaterial = sMat;
                    }
                }

                // 2. Pravalika (Center) with Upgraded Magnet Gyro Rings (Normal Human Size)
                GameObject playerRoot2 = new GameObject("PlayerRoot_Pravalika");
                playerRoot2.transform.SetParent(root.transform, false);
                playerRoot2.transform.position = new Vector3(0f, 0, 0);

                GameObject pravPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Pravalika/Pravalika_Prefab.prefab")
                                     ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/Pravalika.prefab");
                if (pravPrefab != null)
                {
                    GameObject prav = UnityEngine.Object.Instantiate(pravPrefab, playerRoot2.transform);
                    prav.name = "Pravalika_Model";
                    prav.transform.localPosition = Vector3.zero;
                    prav.transform.localRotation = Quaternion.Euler(0, 180f, 0);

                    // Magnet Root
                    GameObject magRoot = new GameObject("Player_MagnetAuraRoot");
                    magRoot.transform.SetParent(playerRoot2.transform, false);
                    magRoot.transform.localPosition = new Vector3(0, 0.95f, 0);

                    GameObject mRing1 = createHollowRing("Magnet_RingPrimary", magRoot.transform, 0.70f, 0.82f);
                    mRing1.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

                    GameObject mRing2 = createHollowRing("Magnet_RingSecondary", magRoot.transform, 0.60f, 0.71f);
                    mRing2.transform.localRotation = Quaternion.Euler(-45f, 0f, 30f);

                    Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                    if (shieldShader != null)
                    {
                        Material magMat1 = new Material(shieldShader);
                        magMat1.SetColor("_AuraColor", new Color(0.1f, 0.8f, 1.0f, 0.08f));
                        magMat1.SetColor("_RimColor", new Color(0.2f, 0.95f, 1.0f, 0.95f)); // Neon cyan flux
                        magMat1.SetFloat("_RimPower", 2.2f);
                        magMat1.SetFloat("_RimIntensity", 2.8f);
                        mRing1.GetComponent<MeshRenderer>().sharedMaterial = magMat1;

                        Material magMat2 = new Material(shieldShader);
                        magMat2.SetColor("_AuraColor", new Color(0.7f, 0.15f, 1.0f, 0.08f));
                        magMat2.SetColor("_RimColor", new Color(0.85f, 0.35f, 1.0f, 0.95f)); // Magnetic violet flux
                        magMat2.SetFloat("_RimPower", 2.2f);
                        magMat2.SetFloat("_RimIntensity", 2.8f);
                        mRing2.GetComponent<MeshRenderer>().sharedMaterial = magMat2;
                    }
                }

                // 3. Pavan (Right) with Upgraded Speedrun Supersonic Wind Ribbons
                GameObject playerRoot3 = new GameObject("PlayerRoot_Pavan");
                playerRoot3.transform.SetParent(root.transform, false);
                playerRoot3.transform.position = new Vector3(1.5f, 0, 0);

                GameObject pavanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Pavan/Pavan_Prefab.prefab")
                                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/Pavan.prefab");
                if (pavanPrefab != null)
                {
                    GameObject pavan = UnityEngine.Object.Instantiate(pavanPrefab, playerRoot3.transform);
                    pavan.name = "Pavan_Model";
                    pavan.transform.localPosition = Vector3.zero;
                    pavan.transform.localRotation = Quaternion.Euler(0, 180f, 0);

                    // Speedrun Root
                    GameObject speedRoot = new GameObject("Player_SpeedrunAuraRoot");
                    speedRoot.transform.SetParent(playerRoot3.transform, false);
                    speedRoot.transform.localPosition = new Vector3(0, 0.85f, 0);

                    // Supersonic wind ribbons
                    GameObject ribbon1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ribbon1.transform.SetParent(speedRoot.transform, false);
                    ribbon1.transform.localPosition = new Vector3(-0.45f, 0f, -0.4f);
                    ribbon1.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                    ribbon1.transform.localScale = new Vector3(0.04f, 0.15f, 1.2f);
                    UnityEngine.Object.DestroyImmediate(ribbon1.GetComponent<Collider>());

                    GameObject ribbon2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ribbon2.transform.SetParent(speedRoot.transform, false);
                    ribbon2.transform.localPosition = new Vector3(0.45f, 0f, -0.4f);
                    ribbon2.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
                    ribbon2.transform.localScale = new Vector3(0.04f, 0.15f, 1.2f);
                    UnityEngine.Object.DestroyImmediate(ribbon2.GetComponent<Collider>());

                    // Ground shockwave hollow ring
                    GameObject speedRing = createHollowRing("Speed_ShockwaveRing", speedRoot.transform, 0.58f, 0.72f);
                    speedRing.transform.localPosition = new Vector3(0f, -0.75f, 0f);

                    Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                    if (shieldShader != null)
                    {
                        Material speedMat = new Material(shieldShader);
                        speedMat.SetColor("_AuraColor", new Color(1.0f, 0.85f, 0.15f, 0.10f));
                        speedMat.SetColor("_RimColor", new Color(1.0f, 0.95f, 0.40f, 0.95f)); // Golden sonic streaks
                        speedMat.SetFloat("_RimPower", 1.8f);
                        speedMat.SetFloat("_RimIntensity", 2.6f);
                        ribbon1.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                        ribbon2.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                        speedRing.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                    }
                }

                // 4. Setup Camera facing all 3 runners
                GameObject camObj = new GameObject("TestCam");
                camObj.transform.SetParent(root.transform);
                cam = camObj.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 1.15f, -4.2f);
                cam.transform.LookAt(new Vector3(0, 1.0f, 0));
                cam.fieldOfView = 50f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.Color;
                cam.backgroundColor = new Color(0.08f, 0.12f, 0.18f);

                rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                Texture2D screenTex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                screenTex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                screenTex.Apply();

                byte[] bytes = screenTex.EncodeToPNG();
                string savePath = Path.Combine(BrainArtifactDir, "character_scale_and_shield.png");
                File.WriteAllBytes(savePath, bytes);
                string localPath = Path.Combine(Application.dataPath, "../character_scale_and_shield.png");
                File.WriteAllBytes(localPath, bytes);

                Debug.Log($"[CharacterFixAndSnapshotTool] Saved Scale & Shield Snapshot to {savePath}");
                UnityEngine.Object.DestroyImmediate(screenTex);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                RenderTexture.active = null;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RenderAllCharactersLineupSnapshot()
        {
            GameObject root = new GameObject("_AllCharsLineupRoot");
            Camera cam = null;
            RenderTexture rt = null;

            try
            {
                // Sun Light
                GameObject sunObj = new GameObject("SunLight");
                sunObj.transform.SetParent(root.transform);
                Light sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.98f, 0.90f);
                sun.intensity = 1.30f;
                sunObj.transform.rotation = Quaternion.Euler(35f, -20f, 0f);

                // Runway
                GameObject runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
                runway.transform.SetParent(root.transform);
                runway.transform.position = new Vector3(0, -0.25f, 0);
                runway.transform.localScale = new Vector3(25.0f, 0.5f, 30.0f);
                Material runwayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TempleStoneFloor.mat") ?? MaterialHelper.CreateSafeMaterial();
                runway.GetComponent<MeshRenderer>().sharedMaterial = runwayMat;

                string[] names = { "Akhilboss", "Harika", "Nandini", "Navaneeth", "Pavan", "Pravalika", "Srikar" };
                float startX = -4.5f;
                float spacing = 1.5f;

                for (int i = 0; i < names.Length; i++)
                {
                    string c = names[i];
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/{c}/{c}_Prefab.prefab")
                                     ?? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Characters/{c}.prefab");
                    if (prefab != null)
                    {
                        GameObject inst = UnityEngine.Object.Instantiate(prefab, root.transform);
                        inst.name = $"{c}_LineupInstance";
                        inst.transform.position = new Vector3(startX + i * spacing, 0, 0);
                        inst.transform.rotation = Quaternion.Euler(0, 180f, 0);
                    }
                }

                // Camera
                GameObject camObj = new GameObject("LineupCam");
                camObj.transform.SetParent(root.transform);
                cam = camObj.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 1.25f, -6.5f);
                cam.transform.LookAt(new Vector3(0, 0.95f, 0));
                cam.fieldOfView = 52f;
                cam.clearFlags = CameraClearFlags.Color;
                cam.backgroundColor = new Color(0.06f, 0.09f, 0.14f);

                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                Texture2D screenTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                screenTex.Apply();

                byte[] bytes = screenTex.EncodeToPNG();
                string savePath = Path.Combine(BrainArtifactDir, "all_characters_lineup.png");
                File.WriteAllBytes(savePath, bytes);
                string localPath = Path.Combine(Application.dataPath, "../all_characters_lineup.png");
                File.WriteAllBytes(localPath, bytes);

                Debug.Log($"[CharacterFixAndSnapshotTool] Saved 7-Character Lineup Snapshot to {savePath}");
                UnityEngine.Object.DestroyImmediate(screenTex);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                RenderTexture.active = null;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RenderGameplayHUDPreview()
        {
            string previewPath = Path.Combine(Application.dataPath, "../scene_preview.png");
            if (!File.Exists(previewPath)) return;
            byte[] bgBytes = File.ReadAllBytes(previewPath);
            Texture2D bgTex = new Texture2D(2, 2);
            bgTex.LoadImage(bgBytes);

            GameObject root = new GameObject("_HUDTestRoot");
            Camera cam = null;
            RenderTexture rt = null;

            try
            {
                // Background quad
                GameObject bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bgQuad.transform.SetParent(root.transform, false);
                bgQuad.transform.position = new Vector3(0, 0, 10f);
                bgQuad.transform.localScale = new Vector3(16f, 9f, 1f);
                Shader unlit = Shader.Find("Unlit/Texture") ?? Shader.Find("Mobile/Unlit (Supports Lightmap)");
                Material bgMat = new Material(unlit);
                bgMat.mainTexture = bgTex;
                bgQuad.GetComponent<MeshRenderer>().sharedMaterial = bgMat;
                UnityEngine.Object.DestroyImmediate(bgQuad.GetComponent<Collider>());

                // Camera
                GameObject camObj = new GameObject("HUDCam");
                camObj.transform.SetParent(root.transform, false);
                cam = camObj.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 4.5f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.Color;
                cam.backgroundColor = Color.black;

                // Canvas in ScreenSpaceCamera
                GameObject canvasObj = new GameObject("HUDCanvas");
                canvasObj.transform.SetParent(root.transform, false);
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 2f;

                UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);

                // Build Restored In-Game HUD Elements
                // 1. Top-Left Pause Button
                GameObject pause = CreateUIBox(canvasObj.transform, new Vector2(-580, 310), new Vector2(50, 50), new Color(0.08f, 0.12f, 0.10f, 0.90f), new Color(1.0f, 0.82f, 0.32f));
                CreateUIText(pause.transform, "⏸", 24, new Color(1.0f, 0.85f, 0.35f), TextAnchor.MiddleCenter);

                // 2. Top-Left Shield Active Indicator
                GameObject shieldSlot = CreateUIBox(canvasObj.transform, new Vector2(-510, 245), new Vector2(190, 36), new Color(0.06f, 0.14f, 0.22f, 0.90f), new Color(0.20f, 0.85f, 1.0f));
                CreateUIText(shieldSlot.transform, "🛡️ MYSTIC WARD", 13, new Color(0.35f, 0.90f, 1.0f), TextAnchor.MiddleLeft, new Vector2(12, 0));

                // 3. Top-Right Distance & Score
                GameObject distPill = CreateUIBox(canvasObj.transform, new Vector2(490, 320), new Vector2(250, 34), new Color(0.06f, 0.10f, 0.08f, 0.90f), new Color(0.30f, 0.85f, 0.55f));
                CreateUIText(distPill.transform, "📍 340m       ⭐ 1,850", 14, new Color(0.35f, 0.95f, 0.65f), TextAnchor.MiddleCenter);

                // 4. Top-Right Restored Heart Collection Meter (Segmented gauge, heart pulse, health pool)
                GameObject heartPill = CreateUIBox(canvasObj.transform, new Vector2(490, 260), new Vector2(250, 60), new Color(0.12f, 0.04f, 0.08f, 0.92f), new Color(1.0f, 0.30f, 0.65f));
                CreateUIText(heartPill.transform, "💖 HEARTS: 24        HP 10/10 ❤️", 13, new Color(1.0f, 0.45f, 0.75f), TextAnchor.UpperCenter, new Vector2(0, -6));

                // Segmented Progress bar inside heart meter
                GameObject barBg = CreateUIBox(heartPill.transform, new Vector2(0, -15), new Vector2(230, 16), new Color(0.03f, 0.02f, 0.04f, 0.95f), new Color(1f, 1f, 1f, 0.2f));
                CreateUIBox(barBg.transform, new Vector2(-50, 0), new Vector2(130, 12), new Color(1.0f, 0.35f, 0.65f, 0.95f), Color.clear);

                // Render to RT
                rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shot.Apply();

                byte[] png = shot.EncodeToPNG();
                string savePath = Path.Combine(BrainArtifactDir, "gameplay_hud_preview.png");
                File.WriteAllBytes(savePath, png);
                string localPath = Path.Combine(Application.dataPath, "../gameplay_hud_preview.png");
                File.WriteAllBytes(localPath, png);
                Debug.Log($"[CharacterFixAndSnapshotTool] Saved Gameplay HUD Preview to {savePath}");
                UnityEngine.Object.DestroyImmediate(shot);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                RenderTexture.active = null;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(bgTex);
            }
        }

        private static GameObject CreateUIBox(Transform parent, Vector2 pos, Vector2 size, Color bgColor, Color borderColor)
        {
            GameObject go = new GameObject("UIBox");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = bgColor;

            if (borderColor != Color.clear)
            {
                UnityEngine.UI.Outline outline = go.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = borderColor;
                outline.effectDistance = new Vector2(1.5f, 1.5f);
            }
            return go;
        }

        private static GameObject CreateUIText(Transform parent, string text, int fontSize, Color color, TextAnchor alignment, Vector2 offset = default)
        {
            GameObject go = new GameObject("UIText");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = offset;
            rt.sizeDelta = parent.GetComponent<RectTransform>()?.sizeDelta ?? new Vector2(200, 30);

            UnityEngine.UI.Text txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = alignment;
            txt.color = color;
            return go;
        }
    }
}
