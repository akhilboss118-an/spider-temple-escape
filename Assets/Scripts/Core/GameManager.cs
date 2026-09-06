using System;
using UnityEngine;
using Runner.Pickups;
using Runner.Player;

namespace Runner.Core
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver
    }

    public enum DeathType
    {
        HeadOnCollision,
        FallIntoVoid,
        CaughtByMonster,
        MissedTurn
    }

    /// <summary>
    /// Core Game Manager: Controls state machine, speed progression, scoring,
    /// stumble penalty timing, and game over sequences.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Speed Ramp Parameters")]
        [Tooltip("Initial running speed in m/s (default 8 m/s)")]
        [SerializeField] private float baseSpeed = 8.0f;

        [Tooltip("Maximum running speed cap in m/s (default 20 m/s)")]
        [SerializeField] private float maxSpeed = 20.0f;

        [Tooltip("Time in seconds to ramp from baseSpeed to maxSpeed (default 180s / 3 mins)")]
        [SerializeField] private float speedRampDuration = 180.0f;

        [Header("Stumble Rules")]
        [Tooltip("Stumble recovery window in seconds (default 5.0s)")]
        [SerializeField] private float stumbleDecayDuration = 5.0f;

        [Tooltip("Speed reduction percentage during stumble (0.20 = 20% slowdown)")]
        [SerializeField] private float stumbleSpeedPenaltyFraction = 0.20f;

        [Tooltip("Duration of stumble speed penalty in seconds (default 1.5s)")]
        [SerializeField] private float stumbleSlowdownDuration = 1.5f;

        // Current Run Variables
        public GameState CurrentState { get; private set; } = GameState.Menu;
        public float CurrentSpeed { get; private set; }
        public float DistanceTraveled { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public int CoinsCollected { get; private set; }
        public int Multiplier { get; private set; } = 1;

        // Player Lives Pool (up to 10/10)
        public int MaxLives { get; private set; } = 10;
        public int CurrentLives { get; private set; } = 10;

        // Run Timing & Stumble State
        private float runTimer = 0.0f;
        private float stumbleTimer = 0.0f;
        private float stumbleSlowdownTimer = 0.0f;
        private int stumbleCount = 0;

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnScoreChanged;
        public event Action<int> OnCoinsChanged;
        public event Action<float> OnSpeedChanged;
        public event Action<int, int> OnLivesChanged; // (current, max)
        public event Action<int, float> OnStumbled; // (stumbleCount, decayRemaining)
        public event Action OnStumbleRecovered;
        public event Action<DeathType, int> OnGameOver; // (deathType, finalScore)

        private const string HIGH_SCORE_KEY = "Runner_HighScore";
        private const string TOTAL_COINS_KEY = "Runner_TotalCoins";
        public const string SUIT_KEY = "Runner_SelectedSuit";
        public const string SHIELD_LVL_KEY = "Runner_ShieldLvl";
        public const string SPEED_LVL_KEY = "Runner_SpeedLvl";
        public const string MAGNET_LVL_KEY = "Runner_MagnetLvl";
        public const string MAX_LIVES_KEY = "Runner_MaxLives";
        public const string AUDIO_KEY = "Runner_AudioEnabled";
        public const string AUDIO_VOL_KEY = "Runner_AudioVol";
        public const string CONTROLS_KEY = "Runner_ControlScheme";
        public const string HAPTICS_KEY = "Runner_Haptics";

        public int SelectedSuitIndex { get; private set; }
        public int ShieldLevel { get; private set; }
        public int SpeedLevel { get; private set; }
        public int MagnetLevel { get; private set; }
        public bool IsAudioEnabled { get; private set; }
        public float AudioVolume { get; private set; } = 1.0f;
        public int ControlScheme { get; private set; } = 0; // 0 = Swipe/Keys, 1 = Tilt
        public bool IsHapticsEnabled { get; private set; } = true;
        public int TotalBankedCoins { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            HighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            TotalBankedCoins = PlayerPrefs.GetInt(TOTAL_COINS_KEY, 15);
            SelectedSuitIndex = PlayerPrefs.GetInt(SUIT_KEY, 0);
            ShieldLevel = PlayerPrefs.GetInt(SHIELD_LVL_KEY, 1);
            SpeedLevel = PlayerPrefs.GetInt(SPEED_LVL_KEY, 1);
            MagnetLevel = PlayerPrefs.GetInt(MAGNET_LVL_KEY, 1);
            MaxLives = Mathf.Clamp(PlayerPrefs.GetInt(MAX_LIVES_KEY, 10), 5, 10);
            CurrentLives = MaxLives;
            IsAudioEnabled = PlayerPrefs.GetInt(AUDIO_KEY, 1) == 1;
            AudioVolume = PlayerPrefs.GetFloat(AUDIO_VOL_KEY, 1.0f);
            ControlScheme = PlayerPrefs.GetInt(CONTROLS_KEY, 0);
            IsHapticsEnabled = PlayerPrefs.GetInt(HAPTICS_KEY, 1) == 1;
            AudioListener.volume = IsAudioEnabled ? AudioVolume : 0.0f;
            CurrentSpeed = baseSpeed;

            CleanDuplicateLightsAndAtmosphere();
        }

        public void CleanDuplicateLightsAndAtmosphere()
        {
            // Find and eliminate duplicate directional lights to prevent visual blowout (full whites)
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Light mainSun = null;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    if (mainSun == null)
                    {
                        mainSun = l;
                        mainSun.name = "Directional Light";
                        mainSun.color = new Color(1.0f, 0.96f, 0.88f);
                        mainSun.intensity = 0.90f;
                        mainSun.shadows = LightShadows.Soft;
                        mainSun.transform.rotation = Quaternion.Euler(50f, -30f, 0);
                    }
                    else
                    {
                        Destroy(l.gameObject);
                    }
                }
            }

            if (mainSun == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                mainSun = lightObj.AddComponent<Light>();
                mainSun.type = LightType.Directional;
                mainSun.color = new Color(1.0f, 0.96f, 0.88f);
                mainSun.intensity = 0.90f;
                mainSun.shadows = LightShadows.Soft;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0);
            }

            // Balanced tropical sunlight, rich jungle ambient and soft atmospheric canopy fog
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.28f, 0.25f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 35.0f;
            RenderSettings.fogEndDistance = 95.0f;
            RenderSettings.fogColor = new Color(0.18f, 0.32f, 0.25f);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.20f, 0.45f, 0.70f);
            }
        }

        private static bool autoStartOnLoad = false;

        private void Start()
        {
            CleanDuplicateLightsAndAtmosphere();
            if (autoStartOnLoad)
            {
                autoStartOnLoad = false;
                StartGame();
            }
            else
            {
                SetState(GameState.Menu);
                PlayerController.Instance?.ApplySuit(SelectedSuitIndex);
            }
        }

        private void Update()
        {
            if (CurrentState != GameState.Playing)
                return;

            float dt = Time.deltaTime;
            runTimer += dt;

            // 1. Calculate Target Speed ramp: 8 m/s -> 20 m/s over 180s
            float rampRatio = Mathf.Clamp01(runTimer / speedRampDuration);
            float targetSpeed = Mathf.Lerp(baseSpeed, maxSpeed, rampRatio);

            // Speedrun Powerup (3x speed boost)
            if (PickupManager.Instance != null && PickupManager.Instance.IsSpeedrunActive)
            {
                targetSpeed *= 3.0f;
            }

            // 2. Handle Stumble Slowdown (20% penalty for 1.5s)
            if (stumbleSlowdownTimer > 0.0f)
            {
                stumbleSlowdownTimer -= dt;
                targetSpeed *= (1.0f - stumbleSpeedPenaltyFraction);
            }

            CurrentSpeed = targetSpeed;
            OnSpeedChanged?.Invoke(CurrentSpeed);

            // 3. Stumble Decay Timer (5.0s)
            if (stumbleTimer > 0.0f)
            {
                stumbleTimer -= dt;
                if (stumbleTimer <= 0.0f)
                {
                    stumbleTimer = 0.0f;
                    stumbleCount = 0;
                    OnStumbleRecovered?.Invoke();
                }
            }

            // 4. Update Distance & Score
            DistanceTraveled += CurrentSpeed * dt;
            Score = Mathf.FloorToInt(DistanceTraveled * 10f * Multiplier) + (CoinsCollected * 50);
            OnScoreChanged?.Invoke(Score);
        }

        public void StartGame()
        {
            runTimer = 0.0f;
            DistanceTraveled = 0.0f;
            CoinsCollected = 0;
            Score = 0;
            CurrentLives = MaxLives;
            stumbleTimer = 0.0f;
            stumbleSlowdownTimer = 0.0f;
            stumbleCount = 0;
            CurrentSpeed = baseSpeed;

            SetState(GameState.Playing);
            OnLivesChanged?.Invoke(CurrentLives, MaxLives);
            PlayerController.Instance?.ApplySuit(SelectedSuitIndex);

            // Level 2+ Shield upgrade perk: start run with shield ready!
            if (ShieldLevel >= 2)
            {
                PickupManager.Instance?.ActivateShield();
            }
        }

        public void SetState(GameState newState)
        {
            if (CurrentState == newState)
                return;

            CurrentState = newState;
            Time.timeScale = (newState == GameState.Paused) ? 0.0f : 1.0f;
            OnGameStateChanged?.Invoke(CurrentState);
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                SetState(GameState.Paused);
                if (IsAudioEnabled)
                {
                    AudioListener.volume = AudioVolume * 0.30f; // Muffle/slow ambient audio
                }
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                SetState(GameState.Playing);
                if (IsAudioEnabled)
                {
                    AudioListener.volume = AudioVolume;
                }
            }
        }

        public void RestoreLife(int amount = 1)
        {
            CurrentLives = Mathf.Min(MaxLives, CurrentLives + amount);
            OnLivesChanged?.Invoke(CurrentLives, MaxLives);
            TriggerHaptic();
        }

        public void LoseLife(int amount = 1)
        {
            CurrentLives = Mathf.Max(0, CurrentLives - amount);
            OnLivesChanged?.Invoke(CurrentLives, MaxLives);
            TriggerHaptic();

            if (CurrentLives <= 0)
            {
                TriggerGameOver(DeathType.CaughtByMonster);
            }
        }

        /// <summary>
        /// Registers a side-clip obstacle stumble.
        /// 1st stumble: slowdown & monster closes in.
        /// 2nd stumble within 5 seconds: caught by monster -> Game Over!
        /// </summary>
        public void RegisterStumble()
        {
            if (CurrentState != GameState.Playing)
                return;

            stumbleCount++;

            if (stumbleCount >= 2)
            {
                // Second stumble within decay window -> caught by monster!
                TriggerGameOver(DeathType.CaughtByMonster);
                return;
            }

            // First stumble: set 5s decay timer and 1.5s slowdown
            stumbleTimer = stumbleDecayDuration;
            stumbleSlowdownTimer = stumbleSlowdownDuration;
            OnStumbled?.Invoke(stumbleCount, stumbleTimer);
        }

        public void AddCoins(int amount)
        {
            if (CurrentState != GameState.Playing)
                return;

            CoinsCollected += amount;
            TotalBankedCoins += amount;
            PlayerPrefs.SetInt(TOTAL_COINS_KEY, TotalBankedCoins);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(CoinsCollected);
        }

        public void TriggerGameOver(DeathType deathType)
        {
            if (CurrentState == GameState.GameOver)
                return;

            SetState(GameState.GameOver);

            // Save High Score
            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HIGH_SCORE_KEY, HighScore);
                PlayerPrefs.Save();
            }

            OnGameOver?.Invoke(deathType, Score);
        }

        public void RestartGame()
        {
            autoStartOnLoad = true;
            Time.timeScale = 1.0f;
            PlayerPrefs.Save();
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = "Main";
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        public void ReturnToMenu()
        {
            autoStartOnLoad = false;
            SetState(GameState.Menu);
            Time.timeScale = 1.0f;
            PlayerPrefs.Save();
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = "Main";
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        public void ToggleAudio()
        {
            IsAudioEnabled = !IsAudioEnabled;
            AudioListener.volume = IsAudioEnabled ? AudioVolume : 0.0f;
            PlayerPrefs.SetInt(AUDIO_KEY, IsAudioEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetAudioVolume(float vol)
        {
            AudioVolume = Mathf.Clamp01(vol);
            if (IsAudioEnabled)
            {
                AudioListener.volume = AudioVolume;
            }
            PlayerPrefs.SetFloat(AUDIO_VOL_KEY, AudioVolume);
            PlayerPrefs.Save();
        }

        public void SetControlScheme(int scheme)
        {
            ControlScheme = Mathf.Clamp(scheme, 0, 1);
            PlayerPrefs.SetInt(CONTROLS_KEY, ControlScheme);
            PlayerPrefs.Save();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            IsHapticsEnabled = enabled;
            PlayerPrefs.SetInt(HAPTICS_KEY, IsHapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void TriggerHaptic()
        {
            if (!IsHapticsEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        public void SelectSuit(int suitIndex)
        {
            SelectedSuitIndex = Mathf.Clamp(suitIndex, 0, 2);
            PlayerPrefs.SetInt(SUIT_KEY, SelectedSuitIndex);
            PlayerPrefs.Save();
            PlayerController.Instance?.ApplySuit(SelectedSuitIndex);
        }

        public bool UpgradePowerup(PowerUpType type, int cost = 5)
        {
            if (TotalBankedCoins < cost) return false;
            TotalBankedCoins -= cost;
            PlayerPrefs.SetInt(TOTAL_COINS_KEY, TotalBankedCoins);

            switch (type)
            {
                case PowerUpType.Shield:
                    ShieldLevel++;
                    PlayerPrefs.SetInt(SHIELD_LVL_KEY, ShieldLevel);
                    break;
                case PowerUpType.Speedrun:
                    SpeedLevel++;
                    PlayerPrefs.SetInt(SPEED_LVL_KEY, SpeedLevel);
                    break;
                case PowerUpType.Magnet:
                    MagnetLevel++;
                    PlayerPrefs.SetInt(MAGNET_LVL_KEY, MagnetLevel);
                    break;
            }

            PlayerPrefs.Save();
            return true;
        }

        public int GetPowerupLevel(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: return ShieldLevel;
                case PowerUpType.Speedrun: return SpeedLevel;
                case PowerUpType.Magnet: return MagnetLevel;
                default: return 1;
            }
        }

        public bool UpgradeHeartCapacity(int cost = 8)
        {
            if (MaxLives >= 10 || TotalBankedCoins < cost) return false;
            TotalBankedCoins -= cost;
            MaxLives = Mathf.Min(10, MaxLives + 1);
            CurrentLives = MaxLives;
            PlayerPrefs.SetInt(TOTAL_COINS_KEY, TotalBankedCoins);
            PlayerPrefs.SetInt(MAX_LIVES_KEY, MaxLives);
            PlayerPrefs.Save();
            OnLivesChanged?.Invoke(CurrentLives, MaxLives);
            return true;
        }

        public void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        public bool IsStumbling => stumbleTimer > 0.0f;
        public float StumbleTimeRemaining => stumbleTimer;
    }
}
