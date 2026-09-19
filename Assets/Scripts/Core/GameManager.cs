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

        [Tooltip("After max speed reached, additional speed gain per minute")]
        [SerializeField] private float endlessSpeedGainPerMinute = 0.5f;

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

        // Coin Combo System
        private int coinComboCount = 0;
        private float coinComboTimer = 0f;
        private const float COMBO_TIMEOUT = 2.0f;
        public int CoinComboCount => coinComboCount;
        public int CoinComboMultiplier => coinComboCount >= 20 ? 5 : coinComboCount >= 10 ? 3 : coinComboCount >= 5 ? 2 : 1;
        public event Action<int, int> OnCoinCombo; // (comboCount, multiplier)

        // Character stat modifiers (applied from CharacterManager)
        private float charSpeedMultiplier = 1.0f;
        private float charAgilityMultiplier = 1.0f;
        private float charShieldBonus = 0f;
        public float CharSpeedMultiplier => charSpeedMultiplier;
        public float CharAgilityMultiplier => charAgilityMultiplier;

        // Character-specific ability state
        private float shieldRegenTimer = 0f;
        private float burstSpeedTimer = 0f;
        private float autoDodgeTimer = 0f;
        private float scoreSurgeTimer = 0f;
        private int activeCharacterIndex = -1;

        // Restart Cooldown
        private float restartCooldownTimer = 0f;
        private const float RESTART_COOLDOWN = 1.5f;

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
        public const string BEST_DISTANCE_KEY = "Runner_BestDistance";
        private const string TOTAL_COINS_KEY = "Runner_TotalCoins";
        public const string SUIT_KEY = "Runner_SelectedSuit";
        public const string SHIELD_LVL_KEY = "Runner_ShieldLvl";
        public const string SPEED_LVL_KEY = "Runner_SpeedLvl";
        public const string MAGNET_LVL_KEY = "Runner_MagnetLvl";
        public const string FRENZY_LVL_KEY = "Runner_FrenzyLvl";
        public const string MAX_LIVES_KEY = "Runner_MaxLives";
        public const string AUDIO_KEY = "Runner_AudioEnabled";
        public const string AUDIO_VOL_KEY = "Runner_AudioVol";
        public const string CONTROLS_KEY = "Runner_ControlScheme";
        public const string HAPTICS_KEY = "Runner_Haptics";

        public int SelectedSuitIndex { get; private set; }
        public float BestDistance { get; private set; }
        public int TotalBankedCoins { get; private set; }
        public int TotalBankedHearts => TotalBankedCoins;
        public int ShieldLevel { get; private set; }
        public int SpeedLevel { get; private set; }
        public int MagnetLevel { get; private set; }
        public int FrenzyLevel { get; private set; }
        public bool IsAudioEnabled { get; private set; }
        public float AudioVolume { get; private set; } = 1.0f;
        public int ControlScheme { get; private set; } = 0; // 0 = Swipe/Keys, 1 = Tilt
        public bool IsHapticsEnabled { get; private set; } = true;
        private float lastHapticTime = -1f;
        private const float hapticCooldown = 0.15f;

        public bool SpendBankedHearts(int amount)
        {
            if (TotalBankedCoins < amount) return false;
            TotalBankedCoins -= amount;
            PlayerPrefs.SetInt(TOTAL_COINS_KEY, TotalBankedCoins);
            PlayerPrefs.Save();
            return true;
        }

        private float scoreProgress = 0.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            HighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            BestDistance = PlayerPrefs.GetFloat(BEST_DISTANCE_KEY, 0f);
            if (BestDistance <= 0f && HighScore > 0)
            {
                BestDistance = HighScore;
            }
            TotalBankedCoins = PlayerPrefs.GetInt(TOTAL_COINS_KEY, 15);
            SelectedSuitIndex = PlayerPrefs.GetInt(SUIT_KEY, 0);
            ShieldLevel = PlayerPrefs.GetInt(SHIELD_LVL_KEY, 1);
            SpeedLevel = PlayerPrefs.GetInt(SPEED_LVL_KEY, 1);
            MagnetLevel = PlayerPrefs.GetInt(MAGNET_LVL_KEY, 1);
            FrenzyLevel = PlayerPrefs.GetInt(FRENZY_LVL_KEY, 1);
            MaxLives = Mathf.Clamp(PlayerPrefs.GetInt(MAX_LIVES_KEY, 10), 5, 10);
            CurrentLives = MaxLives;
            IsAudioEnabled = PlayerPrefs.GetInt(AUDIO_KEY, 1) == 1;
            AudioVolume = PlayerPrefs.GetFloat(AUDIO_VOL_KEY, 1.0f);
            ControlScheme = PlayerPrefs.GetInt(CONTROLS_KEY, 0);
            IsHapticsEnabled = PlayerPrefs.GetInt(HAPTICS_KEY, 1) == 1;
            AudioListener.volume = IsAudioEnabled ? AudioVolume : 0.0f;
            CurrentSpeed = baseSpeed;

            // Enforce smooth, stable 60 FPS on mobile with optimized rendering for smaller GPUs
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 35f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.realtimeReflectionProbes = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            CleanPreviewObjects();
            CleanDuplicateLightsAndAtmosphere();
        }

        public void CleanPreviewObjects()
        {
            var allGo = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var go in allGo)
            {
                if (go != null && (go.name == "_ScenePreviewRoot" || go.name == "PreviewCam" || go.name == "PreviewTrackManager" || go.name.StartsWith("Preview_")))
                {
                    Destroy(go);
                }
            }
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

            // Directional sun setup - bright, radiant tropical daylight
            if (mainSun == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                mainSun = lightObj.AddComponent<Light>();
                mainSun.type = LightType.Directional;
                mainSun.color = new Color(1.0f, 0.98f, 0.92f);
                mainSun.intensity = 1.45f;
                mainSun.shadows = LightShadows.Soft;
                lightObj.transform.rotation = Quaternion.Euler(55f, -35f, 0);
            }
            else
            {
                mainSun.color = new Color(1.0f, 0.98f, 0.92f);
                mainSun.intensity = 1.45f;
                mainSun.shadows = LightShadows.Soft;
            }

            // Radiant tropical daylight, warm lush ambient, and bright azure horizon fog (no dark swamp/night!)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.92f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.80f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.48f, 0.42f);

            Material skyboxMat = Resources.Load<Material>("Materials/Mat_TempleSkybox");
#if UNITY_EDITOR
            if (skyboxMat == null) skyboxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TempleSkybox.mat");
#endif
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85.0f;
            RenderSettings.fogEndDistance = 180.0f;
            RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.98f);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(0.40f, 0.70f, 0.98f);
            }
        }

        private static bool autoStartOnLoad = false;
        private static bool stayInMenuOnLoad = false;

        private void Start()
        {
            CleanDuplicateLightsAndAtmosphere();
            if (stayInMenuOnLoad)
            {
                stayInMenuOnLoad = false;
                autoStartOnLoad = false;
                CurrentState = GameState.Menu;
                Time.timeScale = 1.0f;
                OnGameStateChanged?.Invoke(GameState.Menu);
                if (Runner.Characters.CharacterManager.Instance != null && PlayerController.Instance != null)
                    Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
                else
                    PlayerController.Instance?.ApplySuit(SelectedSuitIndex);
            }
            else if (autoStartOnLoad)
            {
                autoStartOnLoad = false;
                StartGame();
            }
            else
            {
                // Always start in the Main Menu on launch
                autoStartOnLoad = false;
                stayInMenuOnLoad = false;
                CurrentState = GameState.Menu;
                Time.timeScale = 1.0f;
                OnGameStateChanged?.Invoke(GameState.Menu);
                if (Runner.Characters.CharacterManager.Instance != null && PlayerController.Instance != null)
                    Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
                else
                    PlayerController.Instance?.ApplySuit(SelectedSuitIndex);
            }
        }

        private void Update()
        {
            if (CurrentState != GameState.Playing)
                return;

            float dt = Time.deltaTime;
            runTimer += dt;

            // 1. Calculate Target Speed ramp: 8 m/s -> 20 m/s over 180s, then endless gain
            float rampRatio = Mathf.Clamp01(runTimer / speedRampDuration);
            float targetSpeed = Mathf.Lerp(baseSpeed, maxSpeed, rampRatio);

            // Endless scaling after max speed
            if (runTimer > speedRampDuration)
            {
                float extraMinutes = (runTimer - speedRampDuration) / 60f;
                targetSpeed += extraMinutes * endlessSpeedGainPerMinute;
            }

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

            // Wind rush audio at high speed
            Runner.Audio.AudioManager.Instance?.UpdateWindRush(CurrentSpeed);

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
            float comboMult = CoinComboMultiplier;
            float activeMultiplier = Multiplier * comboMult * (PickupManager.Instance != null && PickupManager.Instance.IsMultiplierFrenzyActive ? 3f : 1f);
            scoreProgress += CurrentSpeed * dt * 10f * activeMultiplier;
            Score = Mathf.FloorToInt(scoreProgress) + (CoinsCollected * 50);
            OnScoreChanged?.Invoke(Score);

            // 5. Coin Combo Decay
            if (coinComboCount > 0)
            {
                coinComboTimer -= dt;
                if (coinComboTimer <= 0f)
                {
                    coinComboCount = 0;
                    OnCoinCombo?.Invoke(0, 1);
                }
            }

            // 6. Restart Cooldown
            if (restartCooldownTimer > 0f)
            {
                restartCooldownTimer -= dt;
            }

            // 7. Character Ability Ticks
            TickCharacterAbilities(dt);
        }

        public void StartGame()
        {
            runTimer = 0.0f;
            DistanceTraveled = 0.0f;
            scoreProgress = 0.0f;
            CoinsCollected = 0;
            Score = 0;
            CurrentLives = MaxLives;
            stumbleTimer = 0.0f;
            stumbleSlowdownTimer = 0.0f;
            stumbleCount = 0;
            CurrentSpeed = baseSpeed;
            Multiplier = (SelectedSuitIndex == 3) ? 2 : 1;
            coinComboCount = 0;
            coinComboTimer = 0f;
            restartCooldownTimer = 0f;

            // Reset character ability timers
            shieldRegenTimer = 0f;
            burstSpeedTimer = 0f;
            autoDodgeTimer = 0f;
            scoreSurgeTimer = 0f;

            // Apply character stat ratings
            charSpeedMultiplier = 1.0f;
            charAgilityMultiplier = 1.0f;
            charShieldBonus = 0f;
            activeCharacterIndex = -1;

            if (Runner.Characters.CharacterManager.Instance != null)
            {
                var activeChar = Runner.Characters.CharacterManager.Instance.GetActiveCharacter();
                if (activeChar != null)
                {
                    activeCharacterIndex = Runner.Characters.CharacterManager.Instance.SelectedCharacterIndex;
                    charSpeedMultiplier = activeChar.speedRating;
                    charAgilityMultiplier = activeChar.agilityRating;
                    charShieldBonus = (activeChar.shieldRating - 1.0f) * 0.5f; // 50% of shield rating as damage reduction

                    // Character-specific abilities on game start
                    InitializeCharacterAbility(activeCharacterIndex);
                }
            }

            SetState(GameState.Playing);
            OnLivesChanged?.Invoke(CurrentLives, MaxLives);
            if (Runner.Characters.CharacterManager.Instance != null && PlayerController.Instance != null)
                Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
            else
                PlayerController.Instance?.ApplySuit(SelectedSuitIndex);

            // Level 2+ Shield or Iron Spider Suit (Suit 2): start run with shield ready!
            if (ShieldLevel >= 2 || SelectedSuitIndex == 2)
            {
                PickupManager.Instance?.ActivateShield();
            }
        }

        /// <summary>
        /// Initialize character-specific unique abilities.
        /// Index: 0=Spider-Man, 1=Naruto, 2=Nezuko, 3=Hinata, 4=Zoro, 5=Zenitsu, 6=Anya, 7=Sasuke
        /// </summary>
        private void InitializeCharacterAbility(int charIndex)
        {
            switch (charIndex)
            {
                case 1: // Naruto - Shadow Clone: magnet radius +50%
                    if (PickupManager.Instance != null)
                        PickupManager.Instance.MagnetRadiusBonus = 4.0f;
                    break;
                case 2: // Nezuko - Demon Regen: recover 1 life every 500m
                    // Handled in Update via DistanceTraveled
                    break;
                case 3: // Hinata - Byakugan: coins worth 2x during first 30s
                    scoreSurgeTimer = 30.0f;
                    break;
                case 4: // Zoro - Three-Sword Style: stumble recovery instant (no 2nd stumble penalty for 8s)
                    shieldRegenTimer = 8.0f;
                    break;
                case 5: // Zenitsu - Thunder Clap: speed burst every 45s for 3s
                    burstSpeedTimer = 45.0f;
                    break;
                case 6: // Anya - Waku Waku: mystery chest drop rate doubled
                    // Handled in PickupManager via a flag
                    if (PickupManager.Instance != null)
                        PickupManager.Instance.AnyaChestBonus = true;
                    break;
                case 7: // Sasuke - Sharingan: auto-dodge one obstacle every 30s
                    autoDodgeTimer = 30.0f;
                    break;
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

            // Reset combo on stumble
            coinComboCount = 0;
            coinComboTimer = 0f;
            OnCoinCombo?.Invoke(0, 1);

            if (stumbleCount >= 2)
            {
                TriggerHapticHeavy();
                TriggerGameOver(DeathType.CaughtByMonster);
                return;
            }

            stumbleTimer = stumbleDecayDuration;
            stumbleSlowdownTimer = stumbleSlowdownDuration;
            TriggerHaptic();
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

            // Combo tracking
            coinComboCount += amount;
            coinComboTimer = COMBO_TIMEOUT;
            OnCoinCombo?.Invoke(coinComboCount, CoinComboMultiplier);

            MissionManager.Instance?.ReportCoinsCollected(amount);
            OnCoinsChanged?.Invoke(CoinsCollected);
        }

        public void TriggerGameOver(DeathType deathType)
        {
            if (CurrentState == GameState.GameOver)
                return;

            SetState(GameState.GameOver);
            TriggerHapticHeavy();
            MissionManager.Instance?.ReportRunFinished(Score, DistanceTraveled);

            // Save High Score & Best Distance
            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HIGH_SCORE_KEY, HighScore);
                PlayerPrefs.Save();
            }

            if (DistanceTraveled > BestDistance)
            {
                BestDistance = DistanceTraveled;
                PlayerPrefs.SetFloat(BEST_DISTANCE_KEY, BestDistance);
                PlayerPrefs.Save();
            }

            // Death animation: slow-mo freeze frame
            StartCoroutine(DeathSequence());

            OnGameOver?.Invoke(deathType, Score);
        }

        private System.Collections.IEnumerator DeathSequence()
        {
            // Camera shake
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 originalPos = cam.transform.localPosition;
                float shakeDuration = 0.4f;
                float shakeMagnitude = 0.15f;
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    float x = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;
                    float y = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;
                    cam.transform.localPosition = originalPos + new Vector3(x, y, 0);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                cam.transform.localPosition = originalPos;
            }

            // Slow-mo death freeze
            Time.timeScale = 0.15f;
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            if (restartCooldownTimer > 0f) return;
            restartCooldownTimer = RESTART_COOLDOWN;

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

        private void TickCharacterAbilities(float dt)
        {
            if (activeCharacterIndex < 0) return;

            // Nezuko (2): Demon Regen - recover 1 life every 500m
            if (activeCharacterIndex == 2)
            {
                float prevDist = DistanceTraveled - CurrentSpeed * dt;
                int prevMilestones = Mathf.FloorToInt(prevDist / 500f);
                int currMilestones = Mathf.FloorToInt(DistanceTraveled / 500f);
                if (currMilestones > prevMilestones && CurrentLives < MaxLives)
                {
                    CurrentLives = Mathf.Min(CurrentLives + 1, MaxLives);
                    OnLivesChanged?.Invoke(CurrentLives, MaxLives);
                }
            }

            // Hinata (3): Byakugan score surge expires
            if (activeCharacterIndex == 3 && scoreSurgeTimer > 0f)
            {
                scoreSurgeTimer -= dt;
            }

            // Zoro (4): Shield regen expires (instant stumble recovery)
            if (activeCharacterIndex == 4 && shieldRegenTimer > 0f)
            {
                shieldRegenTimer -= dt;
            }

            // Zenitsu (5): Thunder burst cooldown
            if (activeCharacterIndex == 5)
            {
                burstSpeedTimer -= dt;
                if (burstSpeedTimer <= 0f)
                {
                    // Activate 3s burst
                    burstSpeedTimer = 45.0f;
                    StartCoroutine(ZenitsuThunderBurst());
                }
            }

            // Sasuke (7): Sharingan auto-dodge cooldown
            if (activeCharacterIndex == 7 && autoDodgeTimer > 0f)
            {
                autoDodgeTimer -= dt;
            }
        }

        private System.Collections.IEnumerator ZenitsuThunderBurst()
        {
            float burstDuration = 3.0f;
            CurrentSpeed *= 1.4f;
            yield return new WaitForSeconds(burstDuration);
        }

        /// <summary>
        /// Check if character can auto-dodge (Sasuke Sharingan).
        /// Called by PlayerController/Obstacle when about to take a hit.
        /// </summary>
        public bool TryCharacterAutoDodge()
        {
            if (activeCharacterIndex == 7 && autoDodgeTimer <= 0f)
            {
                autoDodgeTimer = 30.0f;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if stumble is negated (Zoro Three-Sword Style).
        /// </summary>
        public bool TryNegateStumble()
        {
            if (activeCharacterIndex == 4 && shieldRegenTimer > 0f)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if Hinata's Byakugan score surge is active (2x coin value).
        /// </summary>
        public bool IsScoreSurgeActive()
        {
            return activeCharacterIndex == 3 && scoreSurgeTimer > 0f;
        }

        public void ReturnToMenu()
        {
            autoStartOnLoad = false;
            stayInMenuOnLoad = true;
            SetState(GameState.Menu);
            Time.timeScale = 1.0f;
            Runner.Effects.PerformanceOptimizer.Instance?.ResetResolutionToNative();
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
            Runner.Audio.AudioManager.Instance?.UpdateVolumeSettings();
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
            Runner.Audio.AudioManager.Instance?.UpdateVolumeSettings();
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
            if (Time.unscaledTime - lastHapticTime < hapticCooldown) return;
            lastHapticTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        public void TriggerHapticLight()
        {
            if (!IsHapticsEnabled) return;
            if (Time.unscaledTime - lastHapticTime < hapticCooldown * 0.5f) return;
            lastHapticTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        public void TriggerHapticHeavy()
        {
            if (!IsHapticsEnabled) return;
            if (Time.unscaledTime - lastHapticTime < hapticCooldown * 0.3f) return;
            lastHapticTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private const string SUIT_UNLOCKED_PREFIX = "Runner_SuitUnlocked_";

        public bool IsSuitUnlocked(int suitIndex)
        {
            if (suitIndex == 0) return true; // Classic suit is always free & unlocked
            return PlayerPrefs.GetInt(SUIT_UNLOCKED_PREFIX + suitIndex, 0) == 1;
        }

        public bool UnlockSuit(int suitIndex, int cost)
        {
            if (IsSuitUnlocked(suitIndex)) return true;
            if (TotalBankedCoins < cost) return false;

            TotalBankedCoins -= cost;
            PlayerPrefs.SetInt(TOTAL_COINS_KEY, TotalBankedCoins);
            PlayerPrefs.SetInt(SUIT_UNLOCKED_PREFIX + suitIndex, 1);
            PlayerPrefs.Save();
            SelectSuit(suitIndex);
            return true;
        }

        public void SelectSuit(int suitIndex)
        {
            SelectedSuitIndex = Mathf.Clamp(suitIndex, 0, 3);
            PlayerPrefs.SetInt(SUIT_KEY, SelectedSuitIndex);
            PlayerPrefs.Save();
            if (Runner.Characters.CharacterManager.Instance != null && PlayerController.Instance != null)
                Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
            else
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
                case PowerUpType.MultiplierFrenzy:
                    FrenzyLevel++;
                    PlayerPrefs.SetInt(FRENZY_LVL_KEY, FrenzyLevel);
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
                case PowerUpType.MultiplierFrenzy: return FrenzyLevel;
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
