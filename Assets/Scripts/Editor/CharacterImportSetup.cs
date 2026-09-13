using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Runner.Characters;

namespace Runner.Editor
{
    [InitializeOnLoad]
    public static class CharacterImportSetup
    {
        private class CharacterDef
        {
            public string id;
            public string name;
            public string title;
            public string description;
            public int heartUnlockCost;
            public float speedRating;
            public float shieldRating;
            public float agilityRating;
        }

        private static readonly CharacterDef[] Characters = new[]
        {
            new CharacterDef
            {
                id = "char_akhilboss",
                name = "Akhilboss",
                title = "Temple Master",
                description = "Elite temple runner with lightning reflexes, high speed, and fearless agility through ancient ruins.",
                heartUnlockCost = 20,
                speedRating = 1.25f,
                shieldRating = 1.10f,
                agilityRating = 1.20f
            },
            new CharacterDef
            {
                id = "char_harika",
                name = "Harika",
                title = "Jungle Voyager",
                description = "Agile jungle explorer capable of fluid dodges, swift barrier leaps, and balanced endurance.",
                heartUnlockCost = 35,
                speedRating = 1.15f,
                shieldRating = 1.05f,
                agilityRating = 1.30f
            },
            new CharacterDef
            {
                id = "char_nandini",
                name = "Nandini",
                title = "Mystic Scout",
                description = "Fleet-footed temple scout with sharp reflexes and supernatural recovery when navigating treacherous paths.",
                heartUnlockCost = 50,
                speedRating = 1.20f,
                shieldRating = 0.95f,
                agilityRating = 1.35f
            },
            new CharacterDef
            {
                id = "char_navaneeth",
                name = "Navaneeth",
                title = "Ruin Guardian",
                description = "Stalwart adventurer with rock-solid stability to withstand rough collisions and power through obstacles.",
                heartUnlockCost = 65,
                speedRating = 1.10f,
                shieldRating = 1.30f,
                agilityRating = 1.00f
            },
            new CharacterDef
            {
                id = "char_pavan",
                name = "Pavan",
                title = "Wind Striker",
                description = "High-velocity runner possessing explosive speed bursts and aerial maneuvers.",
                heartUnlockCost = 80,
                speedRating = 1.35f,
                shieldRating = 1.00f,
                agilityRating = 1.25f
            },
            new CharacterDef
            {
                id = "char_pravalika",
                name = "Pravalika",
                title = "Shadow Runner",
                description = "Graceful acrobatic athlete specialized in swift low slides and obstacle clearance.",
                heartUnlockCost = 95,
                speedRating = 1.20f,
                shieldRating = 1.15f,
                agilityRating = 1.30f
            },
            new CharacterDef
            {
                id = "char_srikar",
                name = "Srikar",
                title = "Expedition Leader",
                description = "Veteran temple explorer with masterclass attributes across speed, shield defense, and obstacle agility.",
                heartUnlockCost = 110,
                speedRating = 1.30f,
                shieldRating = 1.20f,
                agilityRating = 1.15f
            }
        };

        static CharacterImportSetup()
        {
            EditorApplication.delayCall += CheckAndAutoSetup;
        }

        private static void CheckAndAutoSetup()
        {
            bool needsSetup = false;
            foreach (var def in Characters)
            {
                string pPath = $"Assets/Characters/{def.name}/{def.name}_Prefab.prefab";
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (go == null)
                {
                    needsSetup = true;
                    break;
                }
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null)
                {
                    needsSetup = true;
                    break;
                }
            }

            if (needsSetup)
            {
                Debug.Log("<color=yellow>[CharacterImportSetup]</color> Missing or static character prefabs detected. Automatically running setup for all 7 characters from animation FBXs...");
                SetupAllCharacters();
            }
        }

        [MenuItem("Runner/Characters/Setup All 7 Custom Characters")]
        public static void SetupAllCharacters()
        {
            Debug.Log("<color=cyan>[CharacterImportSetup]</color> Starting setup for all 7 characters directly from animation FBXs...");

            List<string> logs = new List<string>();
            logs.Add($"=== Character Setup Started at {DateTime.Now} ===");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var def in Characters)
            {
                try
                {
                    SetupSingleCharacter(def, logs);
                }
                catch (Exception ex)
                {
                    logs.Add($"ERROR on {def.name}: {ex}");
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            logs.Add($"=== Character Setup Completed at {DateTime.Now} ===");
            try
            {
                File.WriteAllLines("char_setup_log.txt", logs);
            }
            catch { }

            Debug.Log("<color=green>[CharacterImportSetup]</color> All 7 characters and animation state machines configured successfully!");
        }

        private static void SetupSingleCharacter(CharacterDef def, List<string> logs)
        {
            string charFolder = $"Assets/Characters/{def.name}";
            string animDir = $"{charFolder}/Animations";

            if (!Directory.Exists(animDir))
            {
                logs.Add($"[Warning] Animations folder not found: {animDir}");
                return;
            }

            // Find the 5 animation files
            string runFbx = FindFbx(animDir, "run");
            string jumpFbx = FindFbx(animDir, "jump");
            string slideFbx = FindFbx(animDir, "slide");
            string stumbleFbx = FindFbx(animDir, "stumble");
            string dieFbx = FindFbx(animDir, "die");

            logs.Add($"[{def.name}] Run: {Path.GetFileName(runFbx)}, Jump: {Path.GetFileName(jumpFbx)}, Slide: {Path.GetFileName(slideFbx)}, Stumble: {Path.GetFileName(stumbleFbx)}, Die: {Path.GetFileName(dieFbx)}");

            if (string.IsNullOrEmpty(runFbx))
            {
                logs.Add($"[{def.name}] ERROR: Could not find Run FBX in {animDir}");
                return;
            }

            // 1. Configure the Run FBX as the Humanoid Avatar & Base Model
            Avatar runAvatar = ConfigureBaseRunImporter(runFbx);
            logs.Add($"[{def.name}] Run Avatar: {(runAvatar != null ? runAvatar.name : "null")}");

            string runMotionFbx = runFbx;
            string cleanRun = $"{animDir}/Akhil_Run_Clean.fbx";
            if (File.Exists(cleanRun))
            {
                runMotionFbx = cleanRun.Replace('\\', '/');
                ConfigureAnimationImporter(runMotionFbx, runAvatar, true);
                logs.Add($"[{def.name}] Using clean running motion from {Path.GetFileName(runMotionFbx)}");
            }

            // 2. Configure other animation FBXs using the Run Avatar
            if (!string.IsNullOrEmpty(jumpFbx)) ConfigureAnimationImporter(jumpFbx, runAvatar, false);
            if (!string.IsNullOrEmpty(slideFbx)) ConfigureAnimationImporter(slideFbx, runAvatar, false);
            if (!string.IsNullOrEmpty(stumbleFbx)) ConfigureAnimationImporter(stumbleFbx, runAvatar, false);
            if (!string.IsNullOrEmpty(dieFbx)) ConfigureAnimationImporter(dieFbx, runAvatar, false);

            // 3. Load Animation Clips
            AnimationClip runClip = LoadAnimationClip(runMotionFbx);
            AnimationClip jumpClip = LoadAnimationClip(jumpFbx);
            AnimationClip slideClip = LoadAnimationClip(slideFbx);
            AnimationClip stumbleClip = LoadAnimationClip(stumbleFbx);
            AnimationClip dieClip = LoadAnimationClip(dieFbx);

            // 4. Build or update Animator Controller
            string controllerPath = $"{charFolder}/{def.name}_Controller.controller";
            AnimatorController controller = BuildAnimatorController(def.name, controllerPath, runClip, jumpClip, slideClip, stumbleClip, dieClip);

            // Ensure motion GUIDs are written directly if in-memory assignment was deferred
            EnsureControllerMotions(controllerPath, runMotionFbx, jumpFbx, slideFbx, stumbleFbx, dieFbx);

            // 5. Create character Prefab directly from the Run FBX (contains full skinned mesh + skeleton)
            string prefabPath = $"{charFolder}/{def.name}_Prefab.prefab";
            CreateOrUpdatePrefab(def.name, runFbx, runAvatar, controller, prefabPath, logs);
        }

        private static string FindFbx(string dir, string action)
        {
            if (!Directory.Exists(dir)) return null;
            string[] files = Directory.GetFiles(dir, "*.fbx", SearchOption.TopDirectoryOnly);

            if (action == "run")
            {
                foreach (var f in files)
                {
                    string norm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (norm.Contains("clean")) continue;
                    if (norm.Contains("run") && !norm.Contains("slide"))
                        return f.Replace('\\', '/');
                }
            }
            else if (action == "slide")
            {
                foreach (var f in files)
                {
                    string norm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (norm.Contains("slide"))
                        return f.Replace('\\', '/');
                }
            }
            else if (action == "jump")
            {
                foreach (var f in files)
                {
                    string norm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (norm.Contains("jump"))
                        return f.Replace('\\', '/');
                }
            }
            else if (action == "stumble")
            {
                foreach (var f in files)
                {
                    string norm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (norm.Contains("stumble") || norm.Contains("jog"))
                        return f.Replace('\\', '/');
                }
            }
            else if (action == "die")
            {
                foreach (var f in files)
                {
                    string norm = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (norm.Contains("dy") || norm.Contains("die") || norm.Contains("death"))
                        return f.Replace('\\', '/');
                }
            }
            return null;
        }

        private static Avatar ConfigureBaseRunImporter(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return null;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                    clips[i].lockRootRotation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in subAssets)
            {
                if (asset is Avatar avatar) return avatar;
            }

            return null;
        }

        private static void ConfigureAnimationImporter(string fbxPath, Avatar baseAvatar, bool isLooping)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (baseAvatar != null)
            {
                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || importer.sourceAvatar != baseAvatar)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = baseAvatar;
                    dirty = true;
                }
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].loopTime != isLooping)
                    {
                        clips[i].loopTime = isLooping;
                        dirty = true;
                    }
                }
                importer.clipAnimations = clips;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip LoadAnimationClip(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath)) return null;

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

        private static string GetAssetGuid(string path)
        {
            return AssetDatabase.AssetPathToGUID(path);
        }

        private static AnimatorController BuildAnimatorController(string charName, string controllerPath,
            AnimationClip runClip, AnimationClip jumpClip, AnimationClip slideClip, AnimationClip stumbleClip, AnimationClip dieClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            AddParamIfNotExists(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParamIfNotExists(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            AddParamIfNotExists(controller, "Jump", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "Slide", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "Stumble", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "DieFlying", AnimatorControllerParameterType.Trigger);
            AddParamIfNotExists(controller, "DieBackwards", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            AnimatorState runState = FindOrCreateState(rootStateMachine, "Run", runClip);
            AnimatorState jumpState = FindOrCreateState(rootStateMachine, "Jump", jumpClip);
            AnimatorState slideState = FindOrCreateState(rootStateMachine, "Slide", slideClip);
            AnimatorState stumbleState = FindOrCreateState(rootStateMachine, "Stumble", stumbleClip);
            AnimatorState dieBackState = FindOrCreateState(rootStateMachine, "DieBackwards", dieClip);

            rootStateMachine.defaultState = runState;

            // Clean transitions
            runState.transitions = new AnimatorStateTransition[0];
            jumpState.transitions = new AnimatorStateTransition[0];
            slideState.transitions = new AnimatorStateTransition[0];
            stumbleState.transitions = new AnimatorStateTransition[0];

            // 1. Run <-> Jump
            var toJump = runState.AddTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;

            var fromJump = jumpState.AddTransition(runState);
            fromJump.hasExitTime = true;
            fromJump.duration = 0.15f;

            // 2. Run <-> Slide
            var toSlide = runState.AddTransition(slideState);
            toSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            toSlide.hasExitTime = false;
            toSlide.duration = 0.08f;

            var fromSlide = slideState.AddTransition(runState);
            fromSlide.hasExitTime = true;
            fromSlide.duration = 0.15f;

            // 3. Run <-> Stumble
            var toStumble = runState.AddTransition(stumbleState);
            toStumble.AddCondition(AnimatorConditionMode.If, 0, "Stumble");
            toStumble.hasExitTime = false;
            toStumble.duration = 0.08f;

            var fromStumble = stumbleState.AddTransition(runState);
            fromStumble.hasExitTime = true;
            fromStumble.duration = 0.20f;

            // 4. Any -> DieBackwards
            bool hasDie = false;
            foreach (var t in rootStateMachine.anyStateTransitions)
            {
                if (t.destinationState == dieBackState) { hasDie = true; break; }
            }
            if (!hasDie)
            {
                var toDie = rootStateMachine.AddAnyStateTransition(dieBackState);
                toDie.AddCondition(AnimatorConditionMode.If, 0, "DieBackwards");
                toDie.hasExitTime = false;
                toDie.duration = 0.08f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void EnsureControllerMotions(string controllerPath, string runFbx, string jumpFbx, string slideFbx, string stumbleFbx, string dieFbx)
        {
            if (!File.Exists(controllerPath)) return;

            string runGuid = GetAssetGuid(runFbx);
            string jumpGuid = GetAssetGuid(jumpFbx);
            string slideGuid = GetAssetGuid(slideFbx);
            string stumbleGuid = GetAssetGuid(stumbleFbx);
            string dieGuid = GetAssetGuid(dieFbx);

            string text = File.ReadAllText(controllerPath);
            bool modified = false;

            // Replace empty motions for the 5 states
            var stateMap = new Dictionary<string, string>
            {
                { "Run", runGuid },
                { "Jump", jumpGuid },
                { "Slide", slideGuid },
                { "Stumble", stumbleGuid },
                { "DieBackwards", dieGuid }
            };

            foreach (var kvp in stateMap)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;

                // Match AnimatorState with m_Name: <State> and replace m_Motion: {fileID: 0}
                string statePattern = $"m_Name: {kvp.Key}";
                int idx = text.IndexOf(statePattern, StringComparison.Ordinal);
                if (idx > 0)
                {
                    int motionIdx = text.IndexOf("m_Motion: {fileID: 0}", idx, StringComparison.Ordinal);
                    int nextStateIdx = text.IndexOf("AnimatorState:", idx, StringComparison.Ordinal);

                    if (motionIdx > 0 && (nextStateIdx < 0 || motionIdx < nextStateIdx))
                    {
                        string replacement = $"m_Motion: {{fileID: -203655887218126122, guid: {kvp.Value}, type: 3}}";
                        text = text.Substring(0, motionIdx) + replacement + text.Substring(motionIdx + "m_Motion: {fileID: 0}".Length);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                File.WriteAllText(controllerPath, text);
            }
        }

        private static void CreateOrUpdatePrefab(string charName, string runFbxPath, Avatar avatar, AnimatorController controller, string prefabPath, List<string> logs)
        {
            GameObject runModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(runFbxPath);
            if (runModelAsset == null)
            {
                logs.Add($"[{charName}] ERROR: Failed to load Run FBX asset at {runFbxPath}");
                return;
            }

            GameObject instance = UnityEngine.Object.Instantiate(runModelAsset);
            instance.name = $"{charName}_Model";

            // Normalize scale to athletic human height (~1.75m, waist ~0.864m)
            Transform hips = null;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLowerInvariant().Contains("hips")) { hips = t; break; }
            }
            float s = 180.0f;
            if (charName == "Akhilboss") s = 180.0f;
            else if (charName == "Harika") s = 175.0f;
            else if (charName == "Nandini") s = 175.0f;
            else if (charName == "Navaneeth") s = 180.0f;
            else if (charName == "Pavan") s = 180.0f;
            else if (charName == "Pravalika") s = 2.081f;
            else if (charName == "Srikar") s = 180.0f;
            else if (hips != null && hips.localPosition.y > 0.0001f && hips.localPosition.y < 0.05f)
            {
                s = 0.864f / hips.localPosition.y;
            }
            instance.transform.localScale = Vector3.one * s;

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            Animator anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            string resDir = "Assets/Resources/Characters";
            if (!Directory.Exists(resDir)) Directory.CreateDirectory(resDir);
            string resPrefabPath = $"{resDir}/{charName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, resPrefabPath);

            var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            float meshHeight = (smrs != null && smrs.Length > 0 && smrs[0].sharedMesh != null) ? smrs[0].sharedMesh.bounds.size.y : 1.0f;
            float finalScaleY = instance.transform.localScale.y;
            UnityEngine.Object.DestroyImmediate(instance);
            logs.Add($"[{charName}] SUCCESS: Created SkinnedMesh prefab with {smrs?.Length ?? 0} SkinnedMeshRenderers, Height: {meshHeight:F2}, Scale: {finalScaleY:F4} -> {prefabPath}");
            Debug.Log($"[CharacterImportSetup] Successfully created character prefab from {runFbxPath}: {prefabPath} and {resPrefabPath}");
        }

        private static void AddParamIfNotExists(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(paramName, type);
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string stateName, AnimationClip clip)
        {
            foreach (var childState in sm.states)
            {
                if (childState.state.name == stateName)
                {
                    if (clip != null) childState.state.motion = clip;
                    return childState.state;
                }
            }
            var newState = sm.AddState(stateName);
            if (clip != null) newState.motion = clip;
            return newState;
        }
    }
}
