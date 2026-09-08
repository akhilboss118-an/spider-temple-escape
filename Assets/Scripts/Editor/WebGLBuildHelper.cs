#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Runner.EditorTools
{
    public static class WebGLBuildHelper
    {
        private const string APP_NAME = "Spider Temple Escape";
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";
        private const string WEBGL_OUTPUT_PATH = "web/public/game";

        [MenuItem("Runner/WebGL/1. Configure WebGL Settings")]
        public static void ConfigureWebGLSettings()
        {
            ConfigureWebGLSettingsInternal(false);
        }

        public static void ConfigureWebGLSettingsInternal(bool silent)
        {
            Debug.Log("[WebGLBuildHelper] Configuring WebGL Player Settings...");

            PlayerSettings.productName = APP_NAME;
            PlayerSettings.companyName = "SpiderHero";

            // WebGL Memory & Canvas Settings
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // Uncompressed for universal compatibility with Next.js/Vercel
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            // Strip Engine Code minimal for fast stable build
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);

            // Ensure Main scene is in build list
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            EditorBuildSettings.scenes = scenes;

            AssetDatabase.SaveAssets();
            Debug.Log("[WebGLBuildHelper] WebGL Settings configured successfully!");

            if (!Application.isBatchMode && !silent)
            {
                EditorUtility.DisplayDialog("WebGL Settings Configured",
                    "WebGL settings have been optimized for browser embedding in the Next.js showcase web page.", "OK");
            }
        }

        [MenuItem("Runner/WebGL/2. Build WebGL Playable Demo")]
        public static void BuildWebGLPlayableDemo()
        {
            ConfigureWebGLSettingsInternal(true);

            bool isWebGLSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (!isWebGLSupported)
            {
                string msg = "WebGL Build Support is not installed in your Unity Editor.";
                Debug.LogError("[WebGLBuildHelper] " + msg);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("WebGL Module Required", msg, "OK");
                }
                else
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, WEBGL_OUTPUT_PATH);

            if (Directory.Exists(outputPath))
            {
                try
                {
                    Directory.Delete(outputPath, true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[WebGLBuildHelper] Could not clear existing output dir: " + ex.Message);
                }
            }
            Directory.CreateDirectory(outputPath);

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[WebGLBuildHelper] Launching WebGL Build -> {outputPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuildHelper] WebGL Build SUCCEEDED! Total size: {summary.totalSize / (1024 * 1024):F1} MB, Duration: {summary.totalTime.TotalSeconds:F1}s");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("WebGL Build Complete",
                        $"WebGL build created successfully in:\n{outputPath}\n\nSize: {summary.totalSize / (1024 * 1024):F1} MB", "OK");
                }
            }
            else
            {
                Debug.LogError($"[WebGLBuildHelper] WebGL Build FAILED with {summary.totalErrors} errors!");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        public static void BuildWebGLPlayableDemoFromCommandLine()
        {
            Debug.Log("[WebGLBuildHelper] Executing command line WebGL build...");
            BuildWebGLPlayableDemo();
        }
    }
}
#endif
