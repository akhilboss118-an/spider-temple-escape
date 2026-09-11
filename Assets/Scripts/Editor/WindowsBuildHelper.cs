#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Runner.EditorTools
{
    public static class WindowsBuildHelper
    {
        private const string APP_NAME = "Spider Temple Escape";
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";
        private const string WINDOWS_OUTPUT_DIR = "Builds/Windows";
        private const string EXE_NAME = "SpiderTempleEscape.exe";

        [MenuItem("Runner/Windows/1. Configure Windows Settings")]
        public static void ConfigureWindowsSettings()
        {
            ConfigureWindowsSettingsInternal(false);
        }

        public static void ConfigureWindowsSettingsInternal(bool silent)
        {
            Debug.Log("[WindowsBuildHelper] Configuring Windows Standalone Player Settings...");

            PlayerSettings.productName = APP_NAME;
            PlayerSettings.companyName = "SpiderHero";

            // Resolution & Fullscreen
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            // Use Mono for fast build (IL2CPP not needed for Windows standalone play-testing)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Managed stripping minimal for stable builds
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ManagedStrippingLevel.Minimal);

            // Graphics API: Use defaults (D3D11)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);

            // Target 60 FPS
            Application.targetFrameRate = 60;

            // Ensure Main scene is in build settings
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            EditorBuildSettings.scenes = scenes;

            AssetDatabase.SaveAssets();
            Debug.Log("[WindowsBuildHelper] Windows Standalone Settings configured successfully!");

            if (!Application.isBatchMode && !silent)
            {
                EditorUtility.DisplayDialog("Windows Settings Configured",
                    $"Product: {APP_NAME}\nResolution: 540x960 (Portrait Windowed)\nBackend: Mono\nGraphics: DirectX 11\n\nReady to build!", "OK");
            }
        }

        [MenuItem("Runner/Windows/2. Build Windows Standalone")]
        public static void BuildWindowsStandalone()
        {
            ConfigureWindowsSettingsInternal(true);

            bool isSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            if (!isSupported)
            {
                string msg = "Windows Standalone Build Support is not installed in your Unity Editor.\n\n" +
                             "Go to Unity Hub > Installs > Add modules > Windows Build Support (IL2CPP / Mono) and install it.";
                Debug.LogError("[WindowsBuildHelper] " + msg);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Windows Module Required", msg, "OK");
                }
                else
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildDir = Path.Combine(projectRoot, WINDOWS_OUTPUT_DIR);

            // Clean previous build
            if (Directory.Exists(buildDir))
            {
                try
                {
                    Directory.Delete(buildDir, true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[WindowsBuildHelper] Could not clear existing build dir: " + ex.Message);
                }
            }
            Directory.CreateDirectory(buildDir);

            string exePath = Path.Combine(buildDir, EXE_NAME);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log($"[WindowsBuildHelper] Starting Windows x64 build -> {exePath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                float sizeMB = summary.totalSize / (1024f * 1024f);
                Debug.Log($"[WindowsBuildHelper] BUILD SUCCEEDED! Output: {exePath} ({sizeMB:F1} MB), Duration: {summary.totalTime.TotalSeconds:F1}s");
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(exePath);
                    EditorUtility.DisplayDialog("Windows Build Complete!",
                        $"Your game is ready to play:\n\n{exePath}\n\nSize: {sizeMB:F1} MB\n\nDouble-click the .exe to play!", "Let's Go!");
                }
                else
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                Debug.LogError($"[WindowsBuildHelper] BUILD FAILED with {summary.totalErrors} errors!");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        public static void BuildWindowsStandaloneFromCommandLine()
        {
            Debug.Log("[WindowsBuildHelper] Executing command line Windows build...");
            BuildWindowsStandalone();
        }
    }
}
#endif
