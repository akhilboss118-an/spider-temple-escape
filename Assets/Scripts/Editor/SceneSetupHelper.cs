#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using Runner.CameraControl;
using Runner.Core;
using Runner.Effects;
using Runner.Monster;
using Runner.Pickups;
using Runner.Player;
using Runner.Track;
using Runner.UI;
using UnityEngine.UI;

namespace Runner.EditorTools
{
    /// <summary>
    /// Editor utility that:
    /// 1. Configures humanoid rigs for Spider-Man and Zombie FBXs
    /// 2. Generates Spider-Man and Zombie Animator Controllers with state machines
    /// 3. Builds the complete playable scene with 3D models and animations in 1 click!
    /// </summary>
    public static class SceneSetupHelper
    {
        private const string SPIDERMAN_PATH = "Assets/Models/SpiderMan/";
        private const string ZOMBIE_PATH = "Assets/Models/Zombie/";
        private const string ANIM_PATH = "Assets/Animations/";

        [MenuItem("Runner/Build Playable Scene (With 3D Models & Animations)")]
        public static void BuildFullScene()
        {
            Debug.Log("[SceneSetupHelper] Refreshing Assets & Setting up Humanoid Rigs...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // 0. Configure Humanoid Rigs & Loop Times on FBXs
            Avatar spiderAvatar = ConfigureHumanoidRig(SPIDERMAN_PATH + "run.fbx", loopTime: true);
            ConfigureHumanoidRig(SPIDERMAN_PATH + "Jump.fbx", loopTime: false, copyAvatar: spiderAvatar);
            ConfigureHumanoidRig(SPIDERMAN_PATH + "Running Slide.fbx", loopTime: true, copyAvatar: spiderAvatar);
            ConfigureHumanoidRig(SPIDERMAN_PATH + "Jogging Stumble.fbx", loopTime: false, copyAvatar: spiderAvatar);
            ConfigureHumanoidRig(SPIDERMAN_PATH + "Flying Back Death.fbx", loopTime: false, copyAvatar: spiderAvatar);
            ConfigureHumanoidRig(SPIDERMAN_PATH + "Dying Backwards.fbx", loopTime: false, copyAvatar: spiderAvatar);

            Avatar zombieAvatar = ConfigureHumanoidRig(ZOMBIE_PATH + "Zombie Running.fbx", loopTime: true);
            ConfigureHumanoidRig(ZOMBIE_PATH + "Zombie Attack.fbx", loopTime: false, copyAvatar: zombieAvatar);
            ConfigureHumanoidRig(ZOMBIE_PATH + "Zombie Scream.fbx", loopTime: false, copyAvatar: zombieAvatar);
            ConfigureHumanoidRig(ZOMBIE_PATH + "Zombie Idle.fbx", loopTime: true, copyAvatar: zombieAvatar);

            // 1. Setup Animator Controllers for Spider-Man & Zombie
            AnimatorController spiderAnimCtrl = SetupSpiderManAnimators();
            AnimatorController zombieAnimCtrl = SetupZombieAnimators();

            // 2. Clear existing scene objects
            ClearExistingScene();

            Debug.Log("[SceneSetupHelper] Building Playable Scene in 3rd Person View...");

            // 3. Directional Light & Atmosphere (Warm tropical sunlight, crisp obstacle shadows, high contrast)
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.90f);
            light.intensity = 1.20f;
            light.shadows = LightShadows.Soft;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.40f;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            lightObj.transform.rotation = Quaternion.Euler(46f, -38f, 0);

            // Sky & Ambient Setup (Trilight ambient for clear character/path separation & horizon fog)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.38f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45.0f;
            RenderSettings.fogEndDistance = 120.0f;
            RenderSettings.fogColor = new Color(0.20f, 0.34f, 0.28f);

            // 4. Managers
            GameObject coreObj = new GameObject("Core_Manager");
            coreObj.AddComponent<GameManager>();
            coreObj.AddComponent<PickupManager>();
            coreObj.AddComponent<Runner.Audio.AudioManager>();
            coreObj.AddComponent<Runner.Effects.BiomeManager>();
            coreObj.AddComponent<ImpactEffectManager>();
            UIManager uiManager = coreObj.AddComponent<UIManager>();

            // 5. Build Spider-Man Player
            GameObject playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, 0.05f, 0);

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.40f;
            cc.center = new Vector3(0, 1.0f, 0);

            playerObj.AddComponent<InputClassifier>();
            PlayerController player = playerObj.AddComponent<PlayerController>();

            AudioSource pAudio = playerObj.AddComponent<AudioSource>();
            pAudio.playOnAwake = false;
            pAudio.spatialBlend = 0.0f;
            AudioClip stumbleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PlayerStumble.wav");
            SetPrivateField(player, "audioSource", pAudio);
            SetPrivateField(player, "stumbleSoundClip", stumbleClip);

            // Instantiate Spider-Man 3D Model
            GameObject spiderModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SPIDERMAN_PATH + "run.fbx");
            GameObject playerVisual;
            if (spiderModelPrefab != null)
            {
                playerVisual = Object.Instantiate(spiderModelPrefab, playerObj.transform);
                playerVisual.name = "SpiderMan_Model";
                playerVisual.transform.localPosition = Vector3.zero;
                playerVisual.transform.localRotation = Quaternion.identity;

                // Normalize scale if needed (~1.8m height)
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

                // Assign Spider-Man Stark Enhanced suit texture with mobile-safe material
                Texture2D spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SPIDERMAN_PATH + "textures/M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");
                if (spiderTex == null)
                    spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SPIDERMAN_PATH + "textures/spiderman_texture_1.png");
                if (spiderTex == null)
                    spiderTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SPIDERMAN_PATH + "M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");
                if (spiderTex == null)
                    spiderTex = Resources.Load<Texture2D>("Textures/Tex_SpiderMan");

                Material spiderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
                if (spiderMat == null && spiderTex != null)
                {
                    spiderMat = MaterialHelper.CreateSafeMaterial(Color.white, spiderTex);
                }
                if (spiderMat != null)
                {
                    foreach (var r in rList)
                    {
                        r.sharedMaterial = spiderMat;
                    }
                }

                Animator pAnim = playerVisual.GetComponent<Animator>();
                if (pAnim == null) pAnim = playerVisual.AddComponent<Animator>();
                pAnim.avatar = spiderAvatar;
                pAnim.runtimeAnimatorController = spiderAnimCtrl;
                pAnim.applyRootMotion = false; // Code drives runner motion along tracks!

                SetPrivateField(player, "animator", pAnim);
                SetPrivateField(player, "visualTransform", playerVisual.transform);
            }
            else
            {
                // Fallback runner
                playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerVisual.name = "PlayerVisualMesh";
                playerVisual.transform.SetParent(playerObj.transform, false);
                playerVisual.transform.localPosition = new Vector3(0, 1.0f, 0);
                Object.DestroyImmediate(playerVisual.GetComponent<Collider>());
                Material pMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.15f, 0.15f));
                playerVisual.GetComponent<MeshRenderer>().material = pMat;
                SetPrivateField(player, "visualTransform", playerVisual.transform);
            }

            GameObject monsterObj = new GameObject("Monster");
            monsterObj.tag = "Monster";
            monsterObj.transform.position = new Vector3(0, 0, -4.0f);
            MonsterChaser monster = monsterObj.AddComponent<MonsterChaser>();

            AudioSource mAudio = monsterObj.AddComponent<AudioSource>();
            mAudio.playOnAwake = false;
            mAudio.spatialBlend = 0.0f;
            AudioClip screamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ZombieScream.wav");
            SetPrivateField(monster, "audioSource", mAudio);
            SetPrivateField(monster, "screamClip", screamClip);

            // Instantiate Zombie 3D Model
            GameObject zombieModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZOMBIE_PATH + "Zombie Running.fbx");
            if (zombieModelPrefab == null)
            {
                zombieModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZOMBIE_PATH + "source/Zombie_2.fbx");
            }

            GameObject monsterVisual;
            if (zombieModelPrefab != null)
            {
                monsterVisual = Object.Instantiate(zombieModelPrefab, monsterObj.transform);
                monsterVisual.name = "Zombie_Model";
                monsterVisual.transform.localPosition = Vector3.zero;
                monsterVisual.transform.localRotation = Quaternion.identity;

                // Normalize scale if needed (~2.0m height)
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

                // Assign Zombie Texture with mobile-safe material
                Texture2D zombieTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ZOMBIE_PATH + "textures/Zombie_2_color.jpeg");
                Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Monster.mat");
                if (mat == null && zombieTex != null)
                {
                    mat = MaterialHelper.CreateSafeMaterial(Color.white, zombieTex);
                }
                if (mat != null)
                {
                    Renderer[] renderers = monsterVisual.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        r.sharedMaterial = mat;
                    }
                }

                Animator mAnim = monsterVisual.GetComponent<Animator>();
                if (mAnim == null) mAnim = monsterVisual.AddComponent<Animator>();
                mAnim.avatar = zombieAvatar;
                mAnim.runtimeAnimatorController = zombieAnimCtrl;
                mAnim.applyRootMotion = false;

                SetPrivateField(monster, "animator", mAnim);
                SetPrivateField(monster, "monsterBody", monsterVisual.transform);
            }
            else
            {
                monsterVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                monsterVisual.name = "MonsterVisualMesh";
                monsterVisual.transform.SetParent(monsterObj.transform, false);
                monsterVisual.transform.localPosition = new Vector3(0, 1.2f, 0);
                monsterVisual.transform.localScale = new Vector3(1.4f, 1.8f, 1.2f);
                Object.DestroyImmediate(monsterVisual.GetComponent<Collider>());
                Material mMat = MaterialHelper.CreateSafeMaterial(new Color(0.20f, 0.28f, 0.18f));
                monsterVisual.GetComponent<MeshRenderer>().material = mMat;
                SetPrivateField(monster, "monsterBody", monsterVisual.transform);
            }

            // Save Character & Monster Prefabs to Resources/Prefabs for 100% reliable mobile loading
            string prefabDir = "Assets/Resources/Prefabs";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            if (playerVisual != null)
            {
                PrefabUtility.SaveAsPrefabAsset(playerVisual, prefabDir + "/Player_SpiderMan.prefab");
            }
            if (monsterVisual != null)
            {
                PrefabUtility.SaveAsPrefabAsset(monsterVisual, prefabDir + "/Monster_Zombie.prefab");
            }

            // 7. Track Manager
            GameObject trackObj = new GameObject("TrackManager");
            trackObj.AddComponent<TrackManager>();

            // 8. Main Camera with Third-Person Behind-The-Back Controller
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.45f, 0.70f);
            cam.transform.position = new Vector3(0, 3.4f, -5.8f);
            cam.transform.rotation = Quaternion.Euler(20f, 0, 0);
            RunnerCameraController rcc = cam.GetComponent<RunnerCameraController>();
            if (rcc == null)
            {
                rcc = cam.gameObject.AddComponent<RunnerCameraController>();
            }
            rcc.SnapToPlayer();

            // 9. Input EventSystem for Mobile Touch & Gestures
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 10. Save Scene to Assets/Scenes/Main.unity
            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }
            string scenePath = "Assets/Scenes/Main.unity";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),
                scenePath
            );
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            AssetDatabase.SaveAssets();

            Debug.Log("[SceneSetupHelper] SUCCESS! Scene built and saved to Assets/Scenes/Main.unity with 3rd-Person View, Spider-Man, Zombie, Textures, and rich UI!");
        }

        #region Humanoid Rig & Animation Configuration
        private static Avatar ConfigureHumanoidRig(string fbxPath, bool loopTime = false, Avatar copyAvatar = null)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SceneSetupHelper] Importer not found for {fbxPath}");
                return null;
            }

            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (copyAvatar != null)
            {
                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || importer.sourceAvatar != copyAvatar)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = copyAvatar;
                    dirty = true;
                }
            }
            else
            {
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    dirty = true;
                }
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                bool clipModified = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (loopTime != clips[i].loopTime)
                    {
                        clips[i].loopTime = loopTime;
                        clipModified = true;
                    }
                }
                if (clipModified)
                {
                    importer.clipAnimations = clips;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in subAssets)
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }
        #endregion

        #region Setup Spider-Man Animator Controller
        private static AnimatorController SetupSpiderManAnimators()
        {
            string controllerPath = ANIM_PATH + "SpiderMan_Controller.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (!Directory.Exists(ANIM_PATH)) Directory.CreateDirectory(ANIM_PATH);
            if (!Directory.Exists(ANIM_PATH)) Directory.CreateDirectory(ANIM_PATH);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            // Add Parameters
            AddParamIfNotExists(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParamIfNotExists(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            AddParamIfNotExists(controller, "Jump", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "Slide", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "Stumble", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "DieFlying", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "DieBackwards", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Load Animation Clips from FBXs
            AnimationClip runClip = LoadAnimationClip(SPIDERMAN_PATH + "run.fbx");
            AnimationClip jumpClip = LoadAnimationClip(SPIDERMAN_PATH + "Jump.fbx");
            AnimationClip slideClip = LoadAnimationClip(SPIDERMAN_PATH + "Running Slide.fbx");
            AnimationClip stumbleClip = LoadAnimationClip(SPIDERMAN_PATH + "Jogging Stumble.fbx");
            AnimationClip dieFlyClip = LoadAnimationClip(SPIDERMAN_PATH + "Flying Back Death.fbx");
            AnimationClip dieBackClip = LoadAnimationClip(SPIDERMAN_PATH + "Dying Backwards.fbx");

            // Build States
            AnimatorState runState = FindOrCreateState(rootStateMachine, "Run", runClip);
            AnimatorState jumpState = FindOrCreateState(rootStateMachine, "Jump", jumpClip);
            AnimatorState slideState = FindOrCreateState(rootStateMachine, "Slide", slideClip);
            AnimatorState stumbleState = FindOrCreateState(rootStateMachine, "Stumble", stumbleClip);
            AnimatorState dieFlyState = FindOrCreateState(rootStateMachine, "DieFlying", dieFlyClip);
            AnimatorState dieBackState = FindOrCreateState(rootStateMachine, "DieBackwards", dieBackClip);

            rootStateMachine.defaultState = runState;

            // Transitions: Run <-> Jump
            var toJump = runState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;

            var fromJump = jumpState.AddTransition(runState);
            fromJump.hasExitTime = true;
            fromJump.duration = 0.15f;

            // Transitions: Run <-> Slide
            var toSlide = runState.AddTransition(slideState);
            toSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            toSlide.hasExitTime = false;
            toSlide.duration = 0.08f;

            var fromSlide = slideState.AddTransition(runState);
            fromSlide.hasExitTime = true;
            fromSlide.duration = 0.15f;

            // From Jump -> Slide (Fast-fall slide)
            var jumpToSlide = jumpState.AddTransition(slideState);
            jumpToSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            jumpToSlide.hasExitTime = false;
            jumpToSlide.duration = 0.05f;

            // Any State -> Stumble (Triggered when hitting ground obstacle / tree root)
            var anyToStumble = rootStateMachine.AddAnyStateTransition(stumbleState);
            anyToStumble.AddCondition(AnimatorConditionMode.If, 0, "Stumble");
            anyToStumble.hasExitTime = false;
            anyToStumble.canTransitionToSelf = false;
            anyToStumble.duration = 0.08f;

            var fromStumble = stumbleState.AddTransition(runState);
            fromStumble.hasExitTime = true;
            fromStumble.duration = 0.2f;

            // Any State -> Deaths
            var anyToDieFly = rootStateMachine.AddAnyStateTransition(dieFlyState);
            anyToDieFly.AddCondition(AnimatorConditionMode.If, 0, "DieFlying");
            anyToDieFly.hasExitTime = false;
            anyToDieFly.duration = 0.08f;

            var anyToDieBack = rootStateMachine.AddAnyStateTransition(dieBackState);
            anyToDieBack.AddCondition(AnimatorConditionMode.If, 0, "DieBackwards");
            anyToDieBack.hasExitTime = false;
            anyToDieBack.duration = 0.08f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
        #endregion

        #region Setup Zombie Animator Controller
        private static AnimatorController SetupZombieAnimators()
        {
            string controllerPath = ANIM_PATH + "Zombie_Controller.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            // Add Parameters
            AddParamIfNotExists(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParamIfNotExists(controller, "IsChasing", AnimatorControllerParameterType.Bool);
            AddParamIfNotExists(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "Scream", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            AnimationClip zombieRun = LoadAnimationClip(ZOMBIE_PATH + "Zombie Running.fbx");
            AnimationClip zombieAttack = LoadAnimationClip(ZOMBIE_PATH + "Zombie Attack.fbx");
            AnimationClip zombieScream = LoadAnimationClip(ZOMBIE_PATH + "Zombie Scream.fbx");
            AnimationClip zombieIdle = LoadAnimationClip(ZOMBIE_PATH + "Zombie Idle.fbx");

            AnimatorState runState = FindOrCreateState(rootStateMachine, "ZombieRun", zombieRun);
            AnimatorState attackState = FindOrCreateState(rootStateMachine, "ZombieAttack", zombieAttack);
            AnimatorState screamState = FindOrCreateState(rootStateMachine, "ZombieScream", zombieScream);
            AnimatorState idleState = FindOrCreateState(rootStateMachine, "ZombieIdle", zombieIdle);

            rootStateMachine.defaultState = runState;

            // Any State -> Attack (Catch & kill player)
            var toAttack = rootStateMachine.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            // Any State -> Scream (Triggered when player stumbles)
            var toScream = rootStateMachine.AddAnyStateTransition(screamState);
            toScream.AddCondition(AnimatorConditionMode.If, 0, "Scream");
            toScream.hasExitTime = false;
            toScream.canTransitionToSelf = false;
            toScream.duration = 0.08f;

            var fromScream = screamState.AddTransition(runState);
            fromScream.hasExitTime = true;
            fromScream.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
        #endregion

        #region Utilities
        private static void AddParamIfNotExists(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ctrl.parameters)
            {
                if (p.name == name) return;
            }
            ctrl.AddParameter(name, type);
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            foreach (var state in sm.states)
            {
                if (state.state.name == name)
                {
                    if (clip != null) state.state.motion = clip;
                    return state.state;
                }
            }
            var newState = sm.AddState(name);
            if (clip != null) newState.motion = clip;
            return newState;
        }

        private static AnimationClip LoadAnimationClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void ClearExistingScene()
        {
            var managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);
            foreach (var m in managers) Object.DestroyImmediate(m.gameObject);

            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            foreach (var p in players) Object.DestroyImmediate(p.gameObject);

            var monsters = Object.FindObjectsByType<MonsterChaser>(FindObjectsInactive.Include);
            foreach (var m in monsters) Object.DestroyImmediate(m.gameObject);

            var tracks = Object.FindObjectsByType<TrackManager>(FindObjectsInactive.Include);
            foreach (var t in tracks) Object.DestroyImmediate(t.gameObject);

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var l in lights) Object.DestroyImmediate(l.gameObject);

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases) Object.DestroyImmediate(c.gameObject);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
        #endregion
    }

    /// <summary>
    /// Ensures that if Main.unity does not yet exist, it is auto-created on editor startup/recompile.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSetupAutoInitializer
    {
        static SceneSetupAutoInitializer()
        {
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();

                if (!File.Exists("Assets/Scenes/Main.unity") || File.ReadAllText("Assets/Scenes/Main.unity").Contains("MainCanvas"))
                {
                    Debug.Log("[SceneSetupAutoInitializer] Auto-generating Main.unity to purge legacy canvas and restore 3D runner...");
                    SceneSetupHelper.BuildFullScene();
                }

                Runner.EditorTools.MenuVerificationTest.VerifyCompilationAndSetup();
                Runner.EditorTools.SceneSnapshotTool.CaptureSceneSnapshot();
            };
        }
    }
}
#endif

