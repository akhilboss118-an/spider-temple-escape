using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Runner.Editor
{
    public static class AnimationUpdateTool
    {
        private class CharMap
        {
            public string folderName;
            public string sourceFileName;
            public string destFileName;
            public string oldFileNameToRemove;
        }

        private static readonly CharMap[] CharacterMaps = new[]
        {
            new CharMap { folderName = "Akhilboss", sourceFileName = "Akhil_Running.fbx", destFileName = "Akhil_Running.fbx", oldFileNameToRemove = null },
            new CharMap { folderName = "Harika", sourceFileName = "Harika_Running.fbx", destFileName = "Harika_Running.fbx", oldFileNameToRemove = null },
            new CharMap { folderName = "Nandini", sourceFileName = "nandini_Running.fbx", destFileName = "Nandini_Running.fbx", oldFileNameToRemove = null },
            new CharMap { folderName = "Navaneeth", sourceFileName = "Navaneeth_Running.fbx", destFileName = "Navaneeth_Running.fbx", oldFileNameToRemove = null },
            new CharMap { folderName = "Pavan", sourceFileName = "Pavan_Running.fbx", destFileName = "Pavan_Running.fbx", oldFileNameToRemove = null },
            new CharMap { folderName = "Pravalika", sourceFileName = "Pravalika_Running.fbx", destFileName = "Pravalika_Running.fbx", oldFileNameToRemove = "Pravalika_Fast Run.fbx" },
            new CharMap { folderName = "Srikar", sourceFileName = "Srikar_Running.fbx", destFileName = "Srikar_Running.fbx", oldFileNameToRemove = null },
        };

        [MenuItem("Tools/Characters/Replace Running Animations From Desktop")]
        public static void ExecuteAnimationReplacement()
        {
            string sourceDir = @"C:\Users\akhil\OneDrive\Desktop\runnning aniamtion";
            List<string> logs = new List<string>();
            logs.Add($"=== Running Animation Replacement Started at {DateTime.Now} ===");
            Debug.Log("<color=cyan>[AnimationUpdateTool]</color> Starting running animation replacement...");

            if (!Directory.Exists(sourceDir))
            {
                string err = $"ERROR: Source directory not found: {sourceDir}";
                Debug.LogError(err);
                logs.Add(err);
                WriteLogs(logs);
                return;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var map in CharacterMaps)
                {
                    string sourcePath = Path.Combine(sourceDir, map.sourceFileName);
                    if (!File.Exists(sourcePath))
                    {
                        string err = $"[{map.folderName}] Source file not found: {sourcePath}";
                        Debug.LogError(err);
                        logs.Add(err);
                        continue;
                    }

                    string animFolder = $"Assets/Characters/{map.folderName}/Animations";
                    if (!Directory.Exists(animFolder))
                    {
                        Directory.CreateDirectory(animFolder);
                    }

                    string destPath = Path.Combine(animFolder, map.destFileName).Replace('\\', '/');

                    // If there was an old file to remove (e.g. Pravalika_Fast Run.fbx)
                    if (!string.IsNullOrEmpty(map.oldFileNameToRemove))
                    {
                        string oldFbx = Path.Combine(animFolder, map.oldFileNameToRemove).Replace('\\', '/');
                        string oldMeta = oldFbx + ".meta";
                        string preservedGuid = null;

                        if (File.Exists(oldMeta))
                        {
                            // Preserve the existing GUID so controllers/prefabs/animations remain intact
                            foreach (var line in File.ReadAllLines(oldMeta))
                            {
                                if (line.StartsWith("guid: "))
                                {
                                    preservedGuid = line.Substring(6).Trim();
                                    break;
                                }
                            }
                        }

                        // Remove old fbx and meta
                        if (File.Exists(oldFbx)) File.Delete(oldFbx);
                        if (File.Exists(oldMeta)) File.Delete(oldMeta);

                        // Copy new FBX
                        File.Copy(sourcePath, destPath, true);

                        // If we preserved a GUID, create the destination meta with that GUID
                        if (!string.IsNullOrEmpty(preservedGuid))
                        {
                            string destMeta = destPath + ".meta";
                            string metaContent = $"fileFormatVersion: 2\nguid: {preservedGuid}\n";
                            File.WriteAllText(destMeta, metaContent);
                            logs.Add($"[{map.folderName}] Removed {map.oldFileNameToRemove}, replaced with {map.destFileName}, preserved GUID {preservedGuid}");
                        }
                    }
                    else
                    {
                        // Overwrite existing FBX file directly (preserves existing .meta GUID)
                        File.Copy(sourcePath, destPath, true);
                        logs.Add($"[{map.folderName}] Overwrote {destPath} with {map.sourceFileName}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            logs.Add("--- Re-importing and configuring Run FBXs as Humanoid Avatars ---");

            // Now configure each Run FBX importer
            Dictionary<string, Avatar> characterAvatars = new Dictionary<string, Avatar>();
            foreach (var map in CharacterMaps)
            {
                string runPath = $"Assets/Characters/{map.folderName}/Animations/{map.destFileName}";
                Avatar avatar = ConfigureRunFbxImporter(runPath, logs);
                if (avatar != null)
                {
                    characterAvatars[map.folderName] = avatar;
                    logs.Add($"[{map.folderName}] Configured Run Avatar: {avatar.name}");
                }
                else
                {
                    logs.Add($"[{map.folderName}] WARNING: Run avatar was null for {runPath}");
                }
            }

            logs.Add("--- Configuring remaining animation FBXs (Jump, Slide, Stumble, Die) with Run Avatar ---");

            // Configure remaining animations to copy the Run Avatar
            foreach (var map in CharacterMaps)
            {
                if (!characterAvatars.TryGetValue(map.folderName, out Avatar avatar) || avatar == null)
                    continue;

                string animFolder = $"Assets/Characters/{map.folderName}/Animations";
                string[] allFbx = Directory.GetFiles(animFolder, "*.fbx");
                foreach (var fbx in allFbx)
                {
                    string fbxNorm = Path.GetFileName(fbx).ToLowerInvariant();
                    // Skip the run animation itself
                    if (fbxNorm == map.destFileName.ToLowerInvariant()) continue;

                    bool isLooping = false; // Jump, slide, stumble, die are all non-looping action triggers
                    ConfigureRemainingAnimImporter(fbx.Replace('\\', '/'), avatar, isLooping, logs);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            logs.Add("--- Updating Animator Controllers and Prefabs for all 7 characters ---");

            // Run character import setup to rebuild state machines and prefabs
            CharacterImportSetup.SetupAllCharacters();

            // Validate all animations and characters
            logs.Add("--- Validating Final Animation Setup across all 7 Characters ---");
            ValidateAllCharacters(logs);

            logs.Add($"=== Running Animation Replacement Completed at {DateTime.Now} ===");
            WriteLogs(logs);
            Debug.Log("<color=green>[AnimationUpdateTool]</color> All 7 characters updated with new running animations successfully!");
        }

        private static Avatar ConfigureRunFbxImporter(string fbxPath, List<string> logs)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                logs.Add($"ERROR: Importer null for {fbxPath}");
                return null;
            }

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

            // Always take default clips to capture native frame length (e.g. 0-19)
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
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
            foreach (var a in subAssets)
            {
                if (a is Avatar av) return av;
            }

            return null;
        }

        private static void ConfigureRemainingAnimImporter(string fbxPath, Avatar baseAvatar, bool isLooping, List<string> logs)
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
                logs.Add($"[RemainingAnim] Reconfigured {Path.GetFileName(fbxPath)} with Avatar '{baseAvatar.name}'");
            }
        }

        private static void ValidateAllCharacters(List<string> logs)
        {
            foreach (var map in CharacterMaps)
            {
                string animDir = $"Assets/Characters/{map.folderName}/Animations";
                string controllerPath = $"Assets/Characters/{map.folderName}/{map.folderName}_Controller.controller";
                string prefabPath = $"Assets/Characters/{map.folderName}/{map.folderName}_Prefab.prefab";
                string resPrefabPath = $"Assets/Resources/Characters/{map.folderName}.prefab";

                // Check AnimatorController motions
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                int validMotions = 0;
                string motionSummary = "";
                if (controller != null && controller.layers.Length > 0)
                {
                    foreach (var state in controller.layers[0].stateMachine.states)
                    {
                        if (state.state.motion != null)
                        {
                            validMotions++;
                            motionSummary += $"[{state.state.name}: '{state.state.motion.name}'] ";
                        }
                        else
                        {
                            motionSummary += $"[{state.state.name}: MISSING] ";
                        }
                    }
                }

                // Check Prefabs
                GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var smr = charPrefab != null ? charPrefab.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                var anim = charPrefab != null ? charPrefab.GetComponent<Animator>() : null;

                bool prefabOk = smr != null && smr.sharedMesh != null && anim != null && anim.avatar != null && anim.runtimeAnimatorController != null;

                logs.Add($"[{map.folderName}] Motions ({validMotions}/5): {motionSummary} | Prefab Valid: {prefabOk} (Scale: {charPrefab?.transform.localScale.y:F1})");
            }
        }

        private static void WriteLogs(List<string> logs)
        {
            try
            {
                string logDir = "Logs";
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.WriteAllLines(Path.Combine(logDir, "anim_update_report.log"), logs);
            }
            catch { }
        }
    }
}
