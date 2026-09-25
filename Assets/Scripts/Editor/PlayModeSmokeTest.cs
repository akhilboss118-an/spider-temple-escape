using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Runner.EditorTools
{
    /// <summary>
    /// Batch-mode runtime smoke test.
    ///
    ///   Unity.exe -batchmode -projectPath . \
    ///     -executeMethod Runner.EditorTools.PlayModeSmokeTest.Run -logFile Logs/smoke.log
    ///
    /// Opens Main.unity, enters play mode, starts a run, plays for a few seconds and
    /// exits 0 when no Error/Exception was logged, 1 otherwise. Domain-reload safe:
    /// all state lives in SessionState and hooks are re-attached from the static ctor.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSmokeTest
    {
        private const string ActiveKey = "STE_Smoke.Active";
        private const string StartKey = "STE_Smoke.Start";
        private const string PlayKey = "STE_Smoke.PlayStart";
        private const string StartedRunKey = "STE_Smoke.RunStarted";
        private const string MapKey = "STE_Smoke.Map";
        private const string HudKey = "STE_Smoke.Hud";
        private const string CoinsKey = "STE_Smoke.Coins";
        private const string ShotKey = "STE_Smoke.Shot";
        private const string ChangedHudKey = "STE_Smoke.ChangedHud";
        private const string ErrorsKey = "STE_Smoke.Errors";

        private const double EnterPlayTimeoutSeconds = 180.0;
        private const double PlayDurationSeconds = 25.0;

        static PlayModeSmokeTest()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Attach();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(StartKey, Now().ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(PlayKey, "0");
            SessionState.SetBool(StartedRunKey, false);
            SessionState.SetString(ErrorsKey, string.Empty);
            SessionState.SetString(MapKey, ReadArg("-smokeMap"));
            SessionState.SetString(HudKey, ReadArg("-smokeHud"));
            SessionState.SetBool(CoinsKey, false);
            SessionState.SetBool(ShotKey, false);
            SessionState.SetBool(ChangedHudKey, false);

            Attach();

            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Debug.Log("[PlayModeSmokeTest] Entering play mode...");
            EditorApplication.EnterPlaymode();
        }

        private static void Attach()
        {
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
        }

        private static double Now()
        {
            return EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                Detach();
                return;
            }

            double started = double.Parse(SessionState.GetString(StartKey, "0"), CultureInfo.InvariantCulture);
            double elapsed = Now() - started;

            if (EditorApplication.isPlaying && SessionState.GetString(PlayKey, "0") == "0")
            {
                SessionState.SetString(PlayKey, Now().ToString(CultureInfo.InvariantCulture));
                Debug.Log("[PlayModeSmokeTest] Play mode entered.");
            }

            double playStart = double.Parse(SessionState.GetString(PlayKey, "0"), CultureInfo.InvariantCulture);
            bool inPlay = playStart > 0;

            if (inPlay && !SessionState.GetBool(StartedRunKey, false) && Now() - playStart > 3.0)
            {
                SessionState.SetBool(StartedRunKey, true);
                StartRun();
            }

            double playElapsed = inPlay ? (Now() - playStart) : 0;

            if (inPlay && playElapsed > 5.0 && !SessionState.GetBool(CoinsKey, false))
            {
                SessionState.SetBool(CoinsKey, true);
                InjectHearts();
            }

            if (inPlay && playElapsed > 10.0 && !SessionState.GetBool(ShotKey, false))
            {
                SessionState.SetBool(ShotKey, true);
                CaptureScreenshot();
            }

            if (inPlay && playStart > 0 && Now() - playStart >= PlayDurationSeconds)
            {
                Finish(0);
            }
            else if (elapsed >= EnterPlayTimeoutSeconds)
            {
                Debug.LogError("[PlayModeSmokeTest] Timed out waiting for play mode.");
                Finish(1);
            }
        }

        private static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return string.Empty;
        }

        private static void StartRun()
        {
            try
            {
                var gm = Runner.Core.GameManager.Instance;
                if (gm == null)
                {
                    Debug.LogError("[PlayModeSmokeTest] GameManager.Instance is null after entering play mode.");
                    return;
                }

                string mapArg = SessionState.GetString(MapKey, string.Empty);
                var biome = Runner.Effects.BiomeManager.Instance;
                if (!string.IsNullOrEmpty(mapArg) && biome != null)
                {
                    if (Enum.TryParse(mapArg, true, out Runner.Effects.SelectedMapMode mode))
                    {
                        biome.SetSelectedMap(mode);
                        Debug.Log("[PlayModeSmokeTest] Forced map mode: " + mode);
                    }
                    else
                    {
                        Debug.LogWarning("[PlayModeSmokeTest] Ignoring unknown -smokeMap value: " + mapArg);
                    }
                }

                string hudArg = SessionState.GetString(HudKey, string.Empty);
                if (!string.IsNullOrEmpty(hudArg))
                {
                    bool wantCanvas = !hudArg.Equals("classic", StringComparison.OrdinalIgnoreCase);
                    if (gm.UseCanvasHud != wantCanvas)
                    {
                        gm.SetHudStyle(wantCanvas);
                        SessionState.SetBool(ChangedHudKey, true);
                        Debug.Log("[PlayModeSmokeTest] HUD style forced to: " + hudArg);
                    }
                }

                Debug.Log("[PlayModeSmokeTest] State before StartGame: " + gm.CurrentState +
                          " | map=" + Runner.Effects.BiomeManager.CurrentMapMode +
                          " | biome=" + (biome != null ? biome.CurrentBiome.ToString() : "n/a"));

                if (gm.CurrentState == Runner.Core.GameState.Menu)
                {
                    gm.StartGame();
                    Debug.Log("[PlayModeSmokeTest] StartGame() called.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlayModeSmokeTest] StartGame threw: " + ex);
            }
        }

        /// <summary>Sets a mid-tier heart count so the collection meter shows lit pips.</summary>
        private static void InjectHearts()
        {
            try
            {
                var gm = Runner.Core.GameManager.Instance;
                if (gm == null) return;

                var prop = typeof(Runner.Core.GameManager).GetProperty("CoinsCollected");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(gm, 7);
                }
                else
                {
                    var field = typeof(Runner.Core.GameManager).GetField("coinsCollected",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null) field.SetValue(gm, 7);
                }

                Debug.Log("[PlayModeSmokeTest] Injected HeartsCollected = 7 for meter screenshot.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayModeSmokeTest] Could not inject hearts: " + ex.Message);
            }
        }

        private static void CaptureScreenshot()
        {
            try
            {
                string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                string hudTag = SessionState.GetString(HudKey, string.Empty);
                if (string.IsNullOrEmpty(hudTag)) hudTag = "canvas";
                string path = System.IO.Path.Combine(projectRoot, "Logs", "smoke_hearts_" + hudTag + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[PlayModeSmokeTest] Screenshot -> " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayModeSmokeTest] Screenshot failed: " + ex.Message);
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            string current = SessionState.GetString(ErrorsKey, string.Empty);
            SessionState.SetString(ErrorsKey,
                current + "\n[" + type + "] " + condition + "\n" + stackTrace);
        }

        private static void Finish(int code)
        {
            string errors = SessionState.GetString(ErrorsKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            Detach();

            if (SessionState.GetBool(ChangedHudKey, false))
            {
                SessionState.SetBool(ChangedHudKey, false);
                var gm = Runner.Core.GameManager.Instance;
                if (gm != null) gm.SetHudStyle(true);
                Debug.Log("[PlayModeSmokeTest] HUD style restored to Canvas.");
            }

            if (!string.IsNullOrEmpty(errors))
            {
                Debug.LogError("[PlayModeSmokeTest] FAILED - runtime errors captured:" + errors);
                code = 1;
            }
            else
            {
                Debug.Log("[PlayModeSmokeTest] PASSED - no runtime errors during " +
                          PlayDurationSeconds + "s of gameplay.");
            }

            EditorApplication.Exit(code);
        }
    }
}
