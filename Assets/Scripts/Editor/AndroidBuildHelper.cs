#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Runner.EditorTools
{
    public static class AndroidBuildHelper
    {
        private const string APP_NAME = "Spider Temple Escape";
        private const string PACKAGE_NAME = "com.spidergames.templeescape";
        private const string SCENE_PATH = "Assets/Scenes/Main.unity";

        [MenuItem("Runner/Android/1. Configure Android Settings")]
        public static void ConfigureAndroidSettings()
        {
            ConfigureAndroidSettingsInternal(false);
        }

        public static void ConfigureAndroidSettingsInternal(bool silent)
        {
            Debug.Log("[AndroidBuildHelper] Configuring Android Player Settings...");

            PlayerSettings.productName = APP_NAME;
            PlayerSettings.companyName = "SpiderHero";
            PlayerSettings.bundleVersion = "1.2.0";
            PlayerSettings.Android.bundleVersionCode = 3;

            // Application ID / Package Name
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PACKAGE_NAME);

            // Portrait Lock
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Target SDK & Architecture (Android 8.0+ / IL2CPP ARM64 + ARMv7)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            
            // Set Scripting Backend to IL2CPP (Mandatory for modern 64-bit Android)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            // Managed Code Stripping & Engine Code Stripping
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);

            // Graphics API: OpenGLES3 ONLY (Strictly remove Vulkan to prevent GPU driver startup crashes)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
            });

            // Ensure APK is generated, NOT AAB
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;

            // Ensure Main scene is in build settings
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            EditorBuildSettings.scenes = scenes;

            AssetDatabase.SaveAssets();
            Debug.Log($"[AndroidBuildHelper] Android Settings Configured! Package: {PACKAGE_NAME}, Orientation: Portrait, Scene: {SCENE_PATH}, Backend: IL2CPP, Graphics: OpenGLES3");
            if (!Application.isBatchMode && !silent)
            {
                EditorUtility.DisplayDialog("Android Settings Configured",
                    $"Product: {APP_NAME}\nPackage: {PACKAGE_NAME}\nOrientation: Portrait Locked\nBackend: IL2CPP (64-bit ARM64)\nGraphics: OpenGLES3\n\nSettings are saved and ready for build.", "OK");
            }
        }

        [MenuItem("Runner/Android/2. Build Android APK")]
        public static void BuildAndroidApk()
        {
            ConfigureAndroidSettingsInternal(true);

            bool isAndroidSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            if (!isAndroidSupported)
            {
                string msg = "Android Build Support is not yet installed in your Unity Editor.\n\n" +
                             "To install it:\n" +
                             "1. Open Unity Hub\n" +
                             "2. Go to 'Installs' tab\n" +
                             "3. Click the Gear ⚙️ icon next to Unity 6000.6.0f1 -> 'Add modules'\n" +
                             "4. Check 'Android Build Support' (with Android SDK & NDK Tools and OpenJDK)\n" +
                             "5. Click 'Install', then restart Unity and click this button again!";
                Debug.LogWarning("[AndroidBuildHelper] " + msg);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Android Module Required", msg, "Got it");
                }
                else
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

            string apkPath = Path.Combine(buildDir, "SpiderTempleEscape.apk");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuildHelper] Starting Android APK build to: {apkPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuildHelper] APK BUILD SUCCEEDED! Output: {apkPath} ({summary.totalSize / (1024 * 1024)} MB)");
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(apkPath);
                    EditorUtility.DisplayDialog("Build Succeeded!", $"Your APK is ready:\n\n{apkPath}\n\nSize: {summary.totalSize / (1024 * 1024)} MB", "Awesome!");
                }
                else
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                Debug.LogError($"[AndroidBuildHelper] APK BUILD FAILED with result: {summary.result}. Total errors: {summary.totalErrors}.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        public static void BuildAndroidApkFromCommandLine()
        {
            BuildAndroidApk();
        }
    }
}
#endif
