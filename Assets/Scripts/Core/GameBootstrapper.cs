using UnityEngine;
using Runner.CameraControl;
using Runner.Effects;
using Runner.Monster;
using Runner.Pickups;
using Runner.Player;
using Runner.Track;
using Runner.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runner.Core
{
    /// <summary>
    /// Automatic Runtime Bootstrapper:
    /// Ensures that if the game is launched in an empty scene, all necessary
    /// GameObjects, components, materials, and links are instantiated immediately
    /// with full 3D Spider-Man, Zombie chaser, and Third-Person Camera!
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LockPortraitOrientation()
        {
            // Force portrait-only mode. Fires before any scene loads.
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameEntitiesExist()
        {
            // 0. Immediately destroy any leftover preview roots or cameras
            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go != null && (go.name == "_ScenePreviewRoot" || go.name == "PreviewCam" || go.name == "PreviewTrackManager" || go.name.StartsWith("Preview_")))
                {
                    Destroy(go);
                }
            }

            // If GameManager already exists, scene is already set up
            if (FindAnyObjectByType<GameManager>() != null)
                return;

            Debug.Log("[GameBootstrapper] Initializing Endless Runner Scene with 3D Models & 3rd-Person View...");

            // 1. Core Game Manager & Pickup Manager
            GameObject coreObj = new GameObject("Core_Manager");
            coreObj.AddComponent<GameManager>();
            coreObj.AddComponent<PickupManager>();
            coreObj.AddComponent<MissionManager>();
            coreObj.AddComponent<Runner.Audio.AudioManager>();
            coreObj.AddComponent<BiomeManager>();
            coreObj.AddComponent<ImpactEffectManager>();
            coreObj.AddComponent<UIManager>();

            // 2. Directional Light (Balanced warm sunlight & shadow fill)
            var existingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var l in existingLights) Destroy(l.gameObject);

            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1.0f, 0.96f, 0.86f);
            dirLight.intensity = 1.50f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowBias = 0.05f;
            dirLight.shadowNormalBias = 0.40f;
            lightObj.transform.rotation = Quaternion.Euler(46f, -38f, 0);

            // Skybox setup
            Material skyboxMat = Resources.Load<Material>("Materials/Mat_TempleSkybox");
#if UNITY_EDITOR
            if (skyboxMat == null)
                skyboxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TempleSkybox.mat");
#endif
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.92f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.80f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.48f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85.0f;
            RenderSettings.fogEndDistance = 180.0f;
            RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.98f);

            // 3. Player GameObject (Spider-Man in 3rd Person)
            GameObject playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, 0.05f, 0);

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.40f;
            cc.center = new Vector3(0, 1.0f, 0);

            playerObj.AddComponent<InputClassifier>();
            PlayerController player = playerObj.AddComponent<PlayerController>();

            bool spiderLoaded = false;

            // Try loading baked prefab from Resources first (works on Android & Editor)
            GameObject spiderPrefab = Resources.Load<GameObject>("Prefabs/Player_SpiderMan");
            if (spiderPrefab != null)
            {
                GameObject playerVisual = Object.Instantiate(spiderPrefab, playerObj.transform);
                playerVisual.name = "SpiderMan_Model";
                playerVisual.transform.localPosition = Vector3.zero;
                playerVisual.transform.localRotation = Quaternion.identity;

                Animator pAnim = playerVisual.GetComponent<Animator>();
                var visualTransformField = typeof(PlayerController).GetField("visualTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var animatorField = typeof(PlayerController).GetField("animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                visualTransformField?.SetValue(player, playerVisual.transform);
                if (pAnim != null) animatorField?.SetValue(player, pAnim);

                // Ensure Spider-Man always has vibrant Stark Enhanced suit texture
                Material spiderMat = Resources.Load<Material>("Materials/Mat_Player");
#if UNITY_EDITOR
                if (spiderMat == null)
                    spiderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
#endif
                Texture2D spiderTex = Resources.Load<Texture2D>("Textures/Tex_SpiderMan");
#if UNITY_EDITOR
                if (spiderTex == null)
                    spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/SpiderMan/textures/M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");
#endif
                if (spiderMat == null && spiderTex != null)
                {
                    spiderMat = MaterialHelper.CreateSafeMaterial(Color.white, spiderTex);
                }

                if (spiderMat != null)
                {
                    foreach (var r in playerVisual.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = spiderMat;
                    }
                }

                spiderLoaded = true;
            }

#if UNITY_EDITOR
            if (!spiderLoaded)
            {
                GameObject spiderModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SpiderMan/run.fbx");
                RuntimeAnimatorController spiderAnimCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/SpiderMan_Controller.controller");
                if (spiderModelPrefab != null && spiderAnimCtrl != null)
                {
                    GameObject playerVisual = Object.Instantiate(spiderModelPrefab, playerObj.transform);
                    playerVisual.name = "SpiderMan_Model";
                    playerVisual.transform.localPosition = Vector3.zero;
                    playerVisual.transform.localRotation = Quaternion.identity;

                    var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/SpiderMan/run.fbx");
                    Avatar spiderAvatar = null;
                    foreach (var a in subAssets)
                    {
                        if (a is Avatar av) { spiderAvatar = av; break; }
                    }

                    Renderer[] rList = playerVisual.GetComponentsInChildren<Renderer>();
                    if (rList.Length > 0)
                    {
                        Bounds b = rList[0].bounds;
                        for (int i = 1; i < rList.Length; i++) b.Encapsulate(rList[i].bounds);
                        if (b.size.y > 5.0f || b.size.y < 0.5f)
                        {
                            float s = 1.8f / b.size.y;
                            playerVisual.transform.localScale = Vector3.one * s;
                        }
                    }

                    Texture2D spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/SpiderMan/textures/M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");
                    if (spiderTex == null)
                        spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/SpiderMan/M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");

                    if (spiderTex != null)
                    {
                        Material spiderMat = MaterialHelper.CreateSafeMaterial(Color.white, spiderTex);
                        foreach (var r in rList)
                        {
                            r.sharedMaterial = spiderMat;
                        }
                    }

                    Animator pAnim = playerVisual.GetComponent<Animator>();
                    if (pAnim == null) pAnim = playerVisual.AddComponent<Animator>();
                    pAnim.avatar = spiderAvatar;
                    pAnim.runtimeAnimatorController = spiderAnimCtrl;
                    pAnim.applyRootMotion = false;

                    var visualTransformField = typeof(PlayerController).GetField("visualTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var animatorField = typeof(PlayerController).GetField("animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    visualTransformField?.SetValue(player, playerVisual.transform);
                    animatorField?.SetValue(player, pAnim);
                    spiderLoaded = true;
                }
            }
#endif

            if (!spiderLoaded)
            {
                // Fallback runner mesh
                GameObject visualMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visualMesh.name = "PlayerVisualMesh";
                visualMesh.transform.SetParent(playerObj.transform, false);
                visualMesh.transform.localPosition = new Vector3(0, 1.0f, 0);
                visualMesh.transform.localScale = Vector3.one;
                Destroy(visualMesh.GetComponent<Collider>());

                MeshRenderer pRenderer = visualMesh.GetComponent<MeshRenderer>();
                Material playerMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.15f, 0.15f));
                if (playerMat != null)
                {
                    pRenderer.material = playerMat;
                }

                var visualTransformField = typeof(PlayerController).GetField("visualTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var visualRendererField = typeof(PlayerController).GetField("visualRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                visualTransformField?.SetValue(player, visualMesh.transform);
                visualRendererField?.SetValue(player, pRenderer);
            }

            // 4. Monster GameObject (Zombie chasing visibly 4.0m behind player)
            GameObject monsterObj = new GameObject("Monster");
            monsterObj.tag = "Monster";
            monsterObj.transform.position = new Vector3(0, 0, -4.0f);
            MonsterChaser monster = monsterObj.AddComponent<MonsterChaser>();

            bool zombieLoaded = false;

            // Try loading baked monster prefab from Resources
            GameObject zombiePrefab = Resources.Load<GameObject>("Prefabs/Monster_Zombie");
            if (zombiePrefab != null)
            {
                GameObject monsterVisual = Object.Instantiate(zombiePrefab, monsterObj.transform);
                monsterVisual.name = "Zombie_Model";
                monsterVisual.transform.localPosition = Vector3.zero;
                monsterVisual.transform.localRotation = Quaternion.identity;

                Animator mAnim = monsterVisual.GetComponent<Animator>();
                var monsterBodyField = typeof(MonsterChaser).GetField("monsterBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var animatorField = typeof(MonsterChaser).GetField("animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                monsterBodyField?.SetValue(monster, monsterVisual.transform);
                if (mAnim != null) animatorField?.SetValue(monster, mAnim);
                zombieLoaded = true;
            }

#if UNITY_EDITOR
            if (!zombieLoaded)
            {
                GameObject zombieModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Zombie/Zombie Running.fbx");
                RuntimeAnimatorController zombieAnimCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Zombie_Controller.controller");
                if (zombieModelPrefab != null && zombieAnimCtrl != null)
                {
                    GameObject monsterVisual = Object.Instantiate(zombieModelPrefab, monsterObj.transform);
                    monsterVisual.name = "Zombie_Model";
                    monsterVisual.transform.localPosition = Vector3.zero;
                    monsterVisual.transform.localRotation = Quaternion.identity;

                    Renderer[] rList = monsterVisual.GetComponentsInChildren<Renderer>();
                    if (rList.Length > 0)
                    {
                        Bounds b = rList[0].bounds;
                        for (int i = 1; i < rList.Length; i++) b.Encapsulate(rList[i].bounds);
                        if (b.size.y > 5.0f || b.size.y < 0.5f)
                        {
                            float s = 2.0f / b.size.y;
                            monsterVisual.transform.localScale = Vector3.one * s;
                        }
                    }

                    Texture2D zombieTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Zombie/textures/Zombie_2_color.jpeg");
                    if (zombieTex != null)
                    {
                        Material mat = MaterialHelper.CreateSafeMaterial(Color.white, zombieTex);
                        foreach (var r in rList)
                        {
                            r.sharedMaterial = mat;
                        }
                    }

                    var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Zombie/Zombie Running.fbx");
                    Avatar zombieAvatar = null;
                    foreach (var a in subAssets)
                    {
                        if (a is Avatar av) { zombieAvatar = av; break; }
                    }

                    Animator mAnim = monsterVisual.GetComponent<Animator>();
                    if (mAnim == null) mAnim = monsterVisual.AddComponent<Animator>();
                    mAnim.avatar = zombieAvatar;
                    mAnim.runtimeAnimatorController = zombieAnimCtrl;
                    mAnim.applyRootMotion = false;

                    var monsterBodyField = typeof(MonsterChaser).GetField("monsterBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var animatorField = typeof(MonsterChaser).GetField("animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    monsterBodyField?.SetValue(monster, monsterVisual.transform);
                    animatorField?.SetValue(monster, mAnim);
                    zombieLoaded = true;
                }
            }
#endif

            if (!zombieLoaded)
            {
                GameObject mBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mBody.name = "MonsterTorso";
                mBody.transform.SetParent(monsterObj.transform, false);
                mBody.transform.localPosition = new Vector3(0, 1.2f, 0);
                mBody.transform.localScale = new Vector3(1.4f, 1.8f, 1.2f);
                Destroy(mBody.GetComponent<Collider>());

                Material monsterMat = MaterialHelper.CreateSafeMaterial(new Color(0.20f, 0.28f, 0.18f));
                if (monsterMat != null)
                {
                    mBody.GetComponent<MeshRenderer>().material = monsterMat;
                }

                var mBodyField = typeof(MonsterChaser).GetField("monsterBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                mBodyField?.SetValue(monster, mBody.transform);
            }

            // 5. Track Manager
            GameObject trackManagerObj = new GameObject("TrackManager");
            trackManagerObj.AddComponent<TrackManager>();

            // 6. Camera Controller in 3rd Person View
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.backgroundColor = new Color(0.35f, 0.65f, 0.95f);
            if (skyboxMat != null)
            {
                Skybox sb = mainCam.GetComponent<Skybox>();
                if (sb == null) sb = mainCam.gameObject.AddComponent<Skybox>();
                sb.material = skyboxMat;
            }
            mainCam.transform.position = new Vector3(0, 3.4f, -5.8f);
            mainCam.transform.rotation = Quaternion.Euler(20f, 0, 0);

            RunnerCameraController rcc = mainCam.GetComponent<RunnerCameraController>();
            if (rcc == null)
            {
                rcc = mainCam.gameObject.AddComponent<RunnerCameraController>();
            }
            rcc.SnapToPlayer();

            // 7. Verify all 3D Models in Resources
            string[] modelsToCheck = new string[]
            {
                "Path/path_sidewalk",
                "Obstacles/mossy_stone",
                "Obstacles/dead_tree_obstacle",
                "Obstacles/gravestone_obstacle",
                "Obstacles/skull_obstacle",
                "Obstacles/tree_branch_jump",
                "Obstacles/tree_branch_slide",
                "PowerUps/shield",
                "PowerUps/speedrun",
                "PowerUps/magnet",
                "PowerUps/heart",
                "Environment/monstera3",
                "Environment/Tree"
            };
            foreach (var mPath in modelsToCheck)
            {
                GameObject obj = Resources.Load<GameObject>(mPath);
                Debug.Log($"[GameBootstrapper] 3D Model '{mPath}' loaded: {obj != null}");
            }

            Debug.Log("[GameBootstrapper] Scene initialized successfully in 3rd-Person View with full 3D models and textures!");
        }
    }
}
