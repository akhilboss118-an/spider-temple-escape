using System;
using UnityEngine;
using Runner.Core;
using Runner.Player;
using Runner.Effects;

namespace Runner.Audio
{
    /// <summary>
    /// Centralized high-fidelity Audio Manager for Spider Temple Escape:
    /// - Dynamic Adaptive Jungle Chase BGM with speed-dependent tempo/pitch scaling (8 m/s -> 20 m/s).
    /// - Consecutive Coin / Heart Chimes with ascending 5-note pentatonic arpeggio (Subway Surfers feel).
    /// - Crisp physics movement SFX: Jump ballistic whoosh, gravel slide scrape, landing thud.
    /// - Power-up fanfares for Shield, Speedrun boost, Magnet, and Heart Revive.
    /// - Beast guardian proximity stomps and roar cues.
    /// - Environmental ambient layers: jungle, temple, volcanic sounds.
    /// - Dynamic music intensity layers based on monster proximity.
    /// - Procedural synthesis fallback ensures 100% functionality even without external asset files.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        _instance = go.AddComponent<AudioManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource coinSource;
        [SerializeField] private AudioSource beastSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource musicLayerSource;

        [Header("Clips (Optional overrides)")]
        [SerializeField] private AudioClip customBgmClip;
        [SerializeField] private AudioClip customJumpClip;
        [SerializeField] private AudioClip customSlideClip;
        [SerializeField] private AudioClip customCoinClip;
        [SerializeField] private AudioClip customPowerUpClip;

        // Generated procedural clips
        private AudioClip proceduralBgmClip;
        private AudioClip proceduralBgmIntenseClip;
        private AudioClip proceduralBgmVolcanicClip;
        private AudioClip[] coinArpeggioClips;
        private AudioClip jumpClip;
        private AudioClip slideClip;
        private AudioClip landClip;
        private AudioClip beastStompClip;
        private AudioClip powerUpClip;
        private AudioClip shieldBreakClip;
        private AudioClip uiClickClip;
        private AudioClip frenzyFanfareClip;
        private AudioClip mysteryChestClip;
        private AudioClip missionCompleteClip;

        // New environmental audio clips
        private AudioClip ambientJungleClip;
        private AudioClip ambientTempleClip;
        private AudioClip ambientVolcanicClip;
        private AudioClip speedBoostClip;
        private AudioClip nearMissClip;
        private AudioClip coinComboClip;

        // Coin arpeggio state
        private int consecutiveCoinIndex = 0;
        private float lastCoinCollectTime = -10f;
        private const float COIN_CHAIN_WINDOW = 1.25f;

        // BGM Pitch Scaling
        private float targetPitch = 1.0f;

        // Dynamic music layers
        private float currentMusicIntensity = 0f;
        private float targetMusicIntensity = 0f;

        // Combo tracking for enhanced coin sounds
        private int coinComboCount = 0;
        private float lastCoinTime = -10f;
        private const float COMBO_WINDOW = 0.8f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            GenerateProceduralAudioClips();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                GameManager.Instance.OnSpeedChanged += HandleSpeedChanged;
            }

            UpdateVolumeSettings();
            PlayBGM();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
            }
        }

        private void Update()
        {
            // Smoothly lerp BGM pitch to match runner speed
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.pitch = Mathf.Lerp(bgmSource.pitch, targetPitch, Time.unscaledDeltaTime * 2.0f);
            }

            // Reset coin chain index if idle
            if (Time.time - lastCoinCollectTime > COIN_CHAIN_WINDOW && consecutiveCoinIndex > 0)
            {
                consecutiveCoinIndex = 0;
            }

            // Reset combo counter if idle
            if (Time.time - lastCoinTime > COMBO_WINDOW)
            {
                coinComboCount = 0;
            }

            // Dynamic music intensity based on monster proximity
            UpdateDynamicMusicLayers();
        }

        private void InitializeAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.priority = 10;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.priority = 64;
            }

            if (coinSource == null)
            {
                coinSource = gameObject.AddComponent<AudioSource>();
                coinSource.loop = false;
                coinSource.playOnAwake = false;
                coinSource.priority = 40;
            }

            if (beastSource == null)
            {
                beastSource = gameObject.AddComponent<AudioSource>();
                beastSource.loop = false;
                beastSource.playOnAwake = false;
                beastSource.priority = 50;
            }

            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.loop = true;
                loopSource.playOnAwake = false;
                loopSource.priority = 70;
            }

            // New ambient audio source
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
                ambientSource.priority = 80;
                ambientSource.volume = 0.3f;
            }

            // New music layer source for dynamic intensity
            if (musicLayerSource == null)
            {
                musicLayerSource = gameObject.AddComponent<AudioSource>();
                musicLayerSource.loop = true;
                musicLayerSource.playOnAwake = false;
                musicLayerSource.priority = 15;
            }
        }

        public void UpdateVolumeSettings()
        {
            bool isAudioEnabled = GameManager.Instance == null || GameManager.Instance.IsAudioEnabled;
            float masterVol = GameManager.Instance != null ? GameManager.Instance.AudioVolume : 1.0f;

            if (bgmSource != null)
            {
                bgmSource.mute = !isAudioEnabled;
                bgmSource.volume = 0.55f * masterVol;
            }

            if (sfxSource != null)
            {
                sfxSource.mute = !isAudioEnabled;
                sfxSource.volume = 0.85f * masterVol;
            }

            if (coinSource != null)
            {
                coinSource.mute = !isAudioEnabled;
                coinSource.volume = 0.90f * masterVol;
            }

            if (beastSource != null)
            {
                beastSource.mute = !isAudioEnabled;
                beastSource.volume = 0.80f * masterVol;
            }

            if (loopSource != null)
            {
                loopSource.mute = !isAudioEnabled;
                loopSource.volume = 0.60f * masterVol;
            }

            if (ambientSource != null)
            {
                ambientSource.mute = !isAudioEnabled;
                ambientSource.volume = 0.30f * masterVol;
            }

            if (musicLayerSource != null)
            {
                musicLayerSource.mute = !isAudioEnabled;
                musicLayerSource.volume = 0.40f * masterVol;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            UpdateVolumeSettings();
            switch (state)
            {
                case GameState.Menu:
                    targetPitch = 1.0f;
                    if (bgmSource != null) bgmSource.pitch = 1.0f;
                    StopLoops();
                    StopAmbient();
                    break;
                case GameState.Playing:
                    targetPitch = 1.0f;
                    if (!bgmSource.isPlaying) bgmSource.Play();
                    PlayAmbientForBiome(Runner.Effects.BiomeManager.Instance != null ? 
                        Runner.Effects.BiomeManager.Instance.CurrentBiome : Runner.Effects.BiomeType.JungleCanopy);
                    break;
                case GameState.Paused:
                    targetPitch = 0.80f;
                    break;
                case GameState.GameOver:
                    targetPitch = 0.70f;
                    StopLoops();
                    StopAmbient();
                    break;
            }
        }

        private void HandleSpeedChanged(float speed)
        {
            // As speed scales from 8 m/s to 20 m/s, pitch ramps from 1.00x to 1.26x for intense adrenaline rush!
            float norm = Mathf.InverseLerp(8.0f, 20.0f, speed);
            targetPitch = Mathf.Lerp(1.0f, 1.26f, norm);
        }

        private void UpdateDynamicMusicLayers()
        {
            if (Runner.Monster.MonsterChaser.Instance == null || PlayerController.Instance == null) return;

            // Calculate monster proximity intensity
            float monsterDistance = Vector3.Distance(
                Runner.Monster.MonsterChaser.Instance.transform.position,
                PlayerController.Instance.transform.position
            );

            // Intensity: 0 = far away (calm), 1 = very close (intense)
            targetMusicIntensity = Mathf.InverseLerp(12f, 3f, monsterDistance);

            // Smooth transition
            currentMusicIntensity = Mathf.Lerp(currentMusicIntensity, targetMusicIntensity, Time.deltaTime * 2f);

            // Adjust music layer volume based on intensity
            if (musicLayerSource != null && musicLayerSource.isPlaying)
            {
                musicLayerSource.volume = currentMusicIntensity * 0.4f;
            }

            // BGM volume fades DOWN when zombie is close (inverse relationship)
            // This creates tension: music gets quieter, allowing monster sounds to dominate
            float masterVol = GameManager.Instance != null ? GameManager.Instance.AudioVolume : 1f;
            float bgmFadeMultiplier = Mathf.Lerp(1.0f, 0.25f, currentMusicIntensity);
            if (bgmSource != null)
            {
                bgmSource.volume = 0.55f * masterVol * bgmFadeMultiplier;
            }

            // Also slightly reduce ambient volume when zombie is close for extra tension
            if (ambientSource != null)
            {
                float ambientFade = Mathf.Lerp(1.0f, 0.3f, currentMusicIntensity);
                ambientSource.volume = 0.30f * masterVol * ambientFade;
            }
        }

        public void PlayBGM()
        {
            if (bgmSource == null) return;
            AudioClip clip = customBgmClip != null ? customBgmClip : proceduralBgmClip;
            if (clip != null && bgmSource.clip != clip)
            {
                bgmSource.clip = clip;
            }
            if (!bgmSource.isPlaying && (GameManager.Instance == null || GameManager.Instance.IsAudioEnabled))
            {
                bgmSource.Play();
            }

            // Start intense music layer (initially silent)
            if (proceduralBgmIntenseClip != null && musicLayerSource != null)
            {
                musicLayerSource.clip = proceduralBgmIntenseClip;
                musicLayerSource.volume = 0f;
                if (!musicLayerSource.isPlaying && (GameManager.Instance == null || GameManager.Instance.IsAudioEnabled))
                {
                    musicLayerSource.Play();
                }
            }
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
            if (musicLayerSource != null) musicLayerSource.Stop();
        }

        public void StopLoops()
        {
            if (loopSource != null) loopSource.Stop();
        }

        public void StopAmbient()
        {
            if (ambientSource != null) ambientSource.Stop();
        }

        public void PlayAmbientForBiome(Runner.Effects.BiomeType biome)
        {
            if (ambientSource == null) return;

            AudioClip ambientClip = null;
            switch (biome)
            {
                case Runner.Effects.BiomeType.JungleCanopy:
                    ambientClip = ambientJungleClip;
                    break;
            }

            if (ambientClip != null && ambientSource.clip != ambientClip)
            {
                ambientSource.clip = ambientClip;
                ambientSource.volume = 0.25f;
                if (!ambientSource.isPlaying && (GameManager.Instance == null || GameManager.Instance.IsAudioEnabled))
                {
                    ambientSource.Play();
                }
            }
        }

        #region Sound Effect Triggers
        public void PlayJump()
        {
            if (sfxSource == null) return;
            AudioClip clip = customJumpClip != null ? customJumpClip : jumpClip;
            sfxSource.PlayOneShot(clip, 0.75f);
        }

        public void PlaySlide()
        {
            if (sfxSource == null) return;
            AudioClip clip = customSlideClip != null ? customSlideClip : slideClip;
            sfxSource.PlayOneShot(clip, 0.80f);
        }

        public void PlayLand()
        {
            if (sfxSource == null || landClip == null) return;
            sfxSource.PlayOneShot(landClip, 0.50f);
        }

        public void PlayCoinChime()
        {
            if (coinSource == null || coinArpeggioClips == null || coinArpeggioClips.Length == 0) return;

            float now = Time.time;
            if (now - lastCoinCollectTime <= COIN_CHAIN_WINDOW)
            {
                consecutiveCoinIndex = (consecutiveCoinIndex + 1) % coinArpeggioClips.Length;
            }
            else
            {
                consecutiveCoinIndex = 0;
            }
            lastCoinCollectTime = now;

            // Combo tracking
            if (now - lastCoinTime <= COMBO_WINDOW)
            {
                coinComboCount++;
                if (coinComboCount >= 5 && coinComboCount % 5 == 0)
                {
                    // Play combo sound every 5 coins
                    PlayCoinCombo();
                }
            }
            else
            {
                coinComboCount = 1;
            }
            lastCoinTime = now;

            AudioClip clip = coinArpeggioClips[consecutiveCoinIndex];
            coinSource.pitch = 1.0f + (consecutiveCoinIndex * 0.04f);
            coinSource.PlayOneShot(clip, 0.95f);
        }

        public void PlayCoinCombo()
        {
            if (sfxSource == null || coinComboClip == null) return;
            sfxSource.PlayOneShot(coinComboClip, 0.7f);
        }

        public void PlayPowerUp()
        {
            if (sfxSource == null) return;
            AudioClip clip = customPowerUpClip != null ? customPowerUpClip : powerUpClip;
            sfxSource.PlayOneShot(clip, 0.90f);
        }

        public void PlayShieldBreak()
        {
            if (sfxSource == null || shieldBreakClip == null) return;
            sfxSource.PlayOneShot(shieldBreakClip, 1.0f);
        }

        public void PlayBeastStomp(float volumeScale = 1.0f)
        {
            if (beastSource == null || beastStompClip == null) return;
            beastSource.PlayOneShot(beastStompClip, Mathf.Clamp01(volumeScale));
        }

        public void PlayUIClick()
        {
            if (sfxSource == null || uiClickClip == null) return;
            sfxSource.PlayOneShot(uiClickClip, 0.60f);
        }

        public void PlayFrenzyFanfare()
        {
            if (sfxSource == null) return;
            AudioClip clip = frenzyFanfareClip != null ? frenzyFanfareClip : powerUpClip;
            sfxSource.PlayOneShot(clip, 1.0f);
        }

        public void PlayMysteryChestOpen()
        {
            if (sfxSource == null) return;
            AudioClip clip = mysteryChestClip != null ? mysteryChestClip : powerUpClip;
            sfxSource.PlayOneShot(clip, 1.0f);
        }

        public void PlayMissionComplete()
        {
            if (sfxSource == null) return;
            AudioClip clip = missionCompleteClip != null ? missionCompleteClip : powerUpClip;
            sfxSource.PlayOneShot(clip, 0.95f);
        }

        public void PlaySpeedBoost()
        {
            if (sfxSource == null || speedBoostClip == null) return;
            sfxSource.PlayOneShot(speedBoostClip, 0.85f);
        }

        public void PlayNearMiss()
        {
            if (sfxSource == null || nearMissClip == null) return;
            sfxSource.PlayOneShot(nearMissClip, 0.7f);
        }

        // Monster roar on proximity
        private AudioClip monsterRoarClip;
        private float lastRoarTime = 0f;
        private const float ROAR_COOLDOWN = 4.0f;

        public void PlayMonsterRoar(float proximity)
        {
            if (sfxSource == null || monsterRoarClip == null) return;
            if (Time.time - lastRoarTime < ROAR_COOLDOWN) return;

            float vol = Mathf.InverseLerp(6.0f, 1.5f, proximity) * 0.85f;
            if (vol > 0.1f)
            {
                sfxSource.PlayOneShot(monsterRoarClip, vol);
                lastRoarTime = Time.time;
            }
        }

        // Wind rush at high speed
        private AudioClip windRushClip;
        private float windRushVolume = 0f;

        public void UpdateWindRush(float speed)
        {
            if (loopSource == null || windRushClip == null) return;

            float targetVol = Mathf.InverseLerp(12f, 22f, speed) * 0.35f;
            windRushVolume = Mathf.Lerp(windRushVolume, targetVol, Time.deltaTime * 3f);

            if (windRushVolume > 0.01f)
            {
                if (loopSource.clip != windRushClip)
                {
                    loopSource.clip = windRushClip;
                    loopSource.loop = true;
                }
                loopSource.volume = windRushVolume;
                if (!loopSource.isPlaying) loopSource.Play();
            }
            else
            {
                if (loopSource.isPlaying && loopSource.clip == windRushClip)
                    loopSource.Stop();
            }
        }

        // Biome music crossfade
        private AudioClip jungleMusicClip;
        private AudioClip templeMusicClip;
        private AudioClip volcanicMusicClip;
        private const float CROSSFADE_DURATION = 2.0f;

        public void CrossfadeBGMForBiome(Runner.Effects.BiomeType biome)
        {
            if (bgmSource == null) return;

            AudioClip targetClip = biome switch
            {
                Runner.Effects.BiomeType.JungleCanopy => jungleMusicClip ?? proceduralBgmClip,
                _ => proceduralBgmClip
            };

            if (targetClip != null && bgmSource.clip != targetClip)
            {
                StartCoroutine(CrossfadeMusic(targetClip));
            }
        }

        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            float fadeTime = CROSSFADE_DURATION;
            float startVol = bgmSource.volume;

            // Fade out
            for (float t = 0; t < fadeTime * 0.5f; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / (fadeTime * 0.5f));
                yield return null;
            }

            bgmSource.clip = newClip;
            bgmSource.Play();

            // Fade in
            for (float t = 0; t < fadeTime * 0.5f; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, startVol, t / (fadeTime * 0.5f));
                yield return null;
            }
            bgmSource.volume = startVol;
        }
        #endregion

        #region Procedural Audio Synthesis
        private void GenerateProceduralAudioClips()
        {
            int sampleRate = 44100;

            // 1. Ascending Coin Chime Arpeggio (C5, D5, E5, G5, A5, C6)
            float[] frequencies = new float[] { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f, 1046.50f };
            coinArpeggioClips = new AudioClip[frequencies.Length];
            for (int i = 0; i < frequencies.Length; i++)
            {
                coinArpeggioClips[i] = CreateHarmonicToneClip($"Coin_{i}", frequencies[i], 0.22f, sampleRate);
            }

            // 2. Jump Whoosh (Pitch sweep upward with soft breath noise)
            jumpClip = CreateJumpWhooshClip("JumpWhoosh", sampleRate);

            // 3. Slide Gravel Scrape
            slideClip = CreateSlideGravelClip("SlideGravel", sampleRate);

            // 4. Land Thud
            landClip = CreateThudClip("LandThud", 110f, 0.12f, sampleRate);

            // 5. Beast Stomp
            beastStompClip = CreateThudClip("BeastStomp", 65f, 0.32f, sampleRate);

            // 6. Power-Up Fanfare (Crisp triad chord)
            powerUpClip = CreateFanfareClip("PowerUpFanfare", sampleRate);

            // 7. Shield Break (Glass shatter sweep)
            shieldBreakClip = CreateShatterClip("ShieldBreak", sampleRate);

            // 8. UI Ancient Stone Click
            uiClickClip = CreateClickClip("UIClick", sampleRate);

            // 9. Procedural Jungle Chase BGM Loop
            proceduralBgmClip = CreateTribalChaseBGMClip("TribalJungleChaseBGM", sampleRate);

            // 10. Multiplier Frenzy Fanfare (Exciting fast arpeggio)
            frenzyFanfareClip = CreateHarmonicToneClip("FrenzyFanfare", 880f, 0.40f, sampleRate);

            // 11. Mystery Chest Open Chime
            mysteryChestClip = CreateFanfareClip("MysteryChestChime", sampleRate);

            // 12. Mission / Achievement Complete Chime
            missionCompleteClip = CreateHarmonicToneClip("MissionCompleteChime", 1046.5f, 0.35f, sampleRate);

            // 13. Speed Boost whoosh (rising frequency sweep)
            speedBoostClip = CreateSpeedBoostClip("SpeedBoost", sampleRate);

            // 14. Near miss swoosh (quick pan effect)
            nearMissClip = CreateNearMissClip("NearMiss", sampleRate);

            // 15. Coin combo chime (higher pitched chime for combos)
            coinComboClip = CreateHarmonicToneClip("CoinCombo", 1318.5f, 0.25f, sampleRate);

            // 16. Environmental ambient loops
            ambientJungleClip = CreateJungleAmbientClip("JungleAmbient", sampleRate);
            ambientTempleClip = CreateTempleAmbientClip("TempleAmbient", sampleRate);
            ambientVolcanicClip = CreateVolcanicAmbientClip("VolcanicAmbient", sampleRate);

            // 17. Intense music layer for dynamic intensity
            proceduralBgmIntenseClip = CreateIntenseMusicLayerClip("IntenseMusicLayer", sampleRate);

            // 18. Monster roar (low guttural growl)
            monsterRoarClip = CreateMonsterRoarClip("MonsterRoar", sampleRate);

            // 19. Wind rush loop (filtered noise)
            windRushClip = CreateWindRushClip("WindRush", sampleRate);

            // 20. Biome music variations
            jungleMusicClip = proceduralBgmClip;
            templeMusicClip = CreateTempleMusicClip("TempleMusic", sampleRate);
            volcanicMusicClip = proceduralBgmVolcanicClip;
        }

        private AudioClip CreateHarmonicToneClip(string name, float freq, float duration, int sampleRate)
        {
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 14.0f); // Fast decay
                float fundamental = Mathf.Sin(2.0f * Mathf.PI * freq * t);
                float harmonic = 0.35f * Mathf.Sin(4.0f * Mathf.PI * freq * t);
                float shimmer = 0.15f * Mathf.Sin(6.0f * Mathf.PI * freq * t);
                data[i] = (fundamental + harmonic + shimmer) * envelope * 0.45f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateJumpWhooshClip(string name, int sampleRate)
        {
            float duration = 0.28f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float progress = (float)i / samples;
                float envelope = Mathf.Sin(progress * Mathf.PI); // Arc envelope
                float freq = Mathf.Lerp(250f, 950f, progress * progress);
                float tone = Mathf.Sin(2.0f * Mathf.PI * freq * ((float)i / sampleRate));
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.4f;
                data[i] = (tone * 0.6f + noise) * envelope * 0.45f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateSlideGravelClip(string name, int sampleRate)
        {
            float duration = 0.38f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(1337);
            float filterState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float progress = (float)i / samples;
                float envelope = Mathf.Pow(1.0f - progress, 0.7f) * Mathf.Min(progress * 10f, 1f);
                float rawNoise = (float)rnd.NextDouble() * 2f - 1f;
                filterState += (rawNoise - filterState) * 0.25f; // Low-pass
                data[i] = filterState * envelope * 0.55f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateThudClip(string name, float startFreq, float duration, int sampleRate)
        {
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / samples;
                float envelope = Mathf.Exp(-progress * 8.0f);
                float currentFreq = Mathf.Lerp(startFreq, 35.0f, progress);
                float tone = Mathf.Sin(2.0f * Mathf.PI * currentFreq * t);
                data[i] = tone * envelope * 0.75f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateFanfareClip(string name, int sampleRate)
        {
            float duration = 0.45f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            float[] chord = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C major chord

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 5.0f);
                float sum = 0f;
                foreach (float f in chord)
                {
                    sum += Mathf.Sin(2.0f * Mathf.PI * f * t);
                }
                data[i] = (sum / chord.Length) * envelope * 0.60f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateShatterClip(string name, int sampleRate)
        {
            float duration = 0.35f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(999);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 9.0f);
                float ring1 = Mathf.Sin(2.0f * Mathf.PI * 1800f * t);
                float ring2 = Mathf.Sin(2.0f * Mathf.PI * 2750f * t);
                float noise = (float)rnd.NextDouble() * 2f - 1f;
                data[i] = ((ring1 + ring2) * 0.35f + noise * 0.30f) * envelope * 0.65f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateClickClip(string name, int sampleRate)
        {
            float duration = 0.05f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 80.0f);
                data[i] = Mathf.Sin(2.0f * Mathf.PI * 1250f * t) * envelope * 0.40f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateTribalChaseBGMClip(string name, int sampleRate)
        {
            // 4-bar driving tribal chase drum beat (loop duration: ~3.2 seconds at 150 BPM)
            float bpm = 150f;
            float beatDuration = 60f / bpm;
            int totalBeats = 8;
            float totalDuration = totalBeats * beatDuration;
            int totalSamples = Mathf.RoundToInt(totalDuration * sampleRate);
            float[] data = new float[totalSamples];

            System.Random rnd = new System.Random(2026);

            for (int b = 0; b < totalBeats; b++)
            {
                float beatStartTime = b * beatDuration;
                int startSample = Mathf.RoundToInt(beatStartTime * sampleRate);

                // Kick Drum on beats 0, 2, 4, 6 and offbeat accents
                bool isKick = (b % 2 == 0) || (b == 3) || (b == 7);
                if (isKick)
                {
                    int kickSamples = Mathf.RoundToInt(0.18f * sampleRate);
                    for (int s = 0; s < kickSamples && (startSample + s) < totalSamples; s++)
                    {
                        float t = (float)s / sampleRate;
                        float env = Mathf.Exp(-t * 22f);
                        float freq = Mathf.Lerp(120f, 42f, (float)s / kickSamples);
                        data[startSample + s] += Mathf.Sin(2.0f * Mathf.PI * freq * t) * env * 0.50f;
                    }
                }

                // Snare/Tom on beats 1, 3, 5, 7
                bool isTom = (b % 2 == 1);
                if (isTom)
                {
                    int tomSamples = Mathf.RoundToInt(0.15f * sampleRate);
                    for (int s = 0; s < tomSamples && (startSample + s) < totalSamples; s++)
                    {
                        float t = (float)s / sampleRate;
                        float env = Mathf.Exp(-t * 18f);
                        float tone = Mathf.Sin(2.0f * Mathf.PI * 180f * t);
                        float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.4f;
                        data[startSample + s] += (tone + noise) * env * 0.40f;
                    }
                }

                // Shaker / Jungle Tambourine rhythmic 16th notes
                for (int sixteenth = 0; sixteenth < 4; sixteenth++)
                {
                    int subStart = startSample + Mathf.RoundToInt(sixteenth * (beatDuration * 0.25f) * sampleRate);
                    int shakerLen = Mathf.RoundToInt(0.04f * sampleRate);
                    for (int s = 0; s < shakerLen && (subStart + s) < totalSamples; s++)
                    {
                        float t = (float)s / sampleRate;
                        float env = Mathf.Exp(-t * 60f);
                        float noise = (float)rnd.NextDouble() * 2f - 1f;
                        data[subStart + s] += noise * env * 0.16f;
                    }
                }

                // Bass Drone pulse in A minor (110 Hz)
                int droneSamples = Mathf.RoundToInt(beatDuration * sampleRate);
                for (int s = 0; s < droneSamples && (startSample + s) < totalSamples; s++)
                {
                    float t = (float)s / sampleRate;
                    float env = 0.5f * (1.0f + Mathf.Sin(t * Mathf.PI * 4f));
                    float bass = Mathf.Sin(2.0f * Mathf.PI * 110f * t);
                    data[startSample + s] += bass * env * 0.14f;
                }
            }

            // Normalize slightly to prevent clipping
            float maxVal = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                if (Mathf.Abs(data[i]) > maxVal) maxVal = Mathf.Abs(data[i]);
            }
            if (maxVal > 0.95f)
            {
                float scale = 0.95f / maxVal;
                for (int i = 0; i < totalSamples; i++) data[i] *= scale;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateSpeedBoostClip(string name, int sampleRate)
        {
            float duration = 0.45f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(2026);

            for (int i = 0; i < samples; i++)
            {
                float progress = (float)i / samples;
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(progress * Mathf.PI) * 0.8f;
                float freq = Mathf.Lerp(200f, 1200f, progress * progress);
                float tone = Mathf.Sin(2.0f * Mathf.PI * freq * t);
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.3f * (1f - progress);
                data[i] = (tone * 0.5f + noise) * envelope * 0.55f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateNearMissClip(string name, int sampleRate)
        {
            float duration = 0.18f;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            System.Random rnd = new System.Random(777);

            for (int i = 0; i < samples; i++)
            {
                float progress = (float)i / samples;
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float freq = Mathf.Lerp(800f, 400f, progress);
                float tone = Mathf.Sin(2.0f * Mathf.PI * freq * t);
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * 0.5f;
                data[i] = (tone * 0.4f + noise * 0.6f) * envelope * 0.5f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateJungleAmbientClip(string name, int sampleRate)
        {
            // 8-second jungle ambient loop with birds, insects, and wind
            float duration = 8.0f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];
            System.Random rnd = new System.Random(3001);

            // Base wind/drone
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float wind = Mathf.Sin(2.0f * Mathf.PI * 80f * t) * 0.15f;
                wind += Mathf.Sin(2.0f * Mathf.PI * 120f * t + Mathf.Sin(t * 0.5f) * 2f) * 0.1f;
                data[i] = wind;
            }

            // Bird chirps (random high-frequency tones)
            for (int b = 0; b < 12; b++)
            {
                float chirpTime = (float)rnd.NextDouble() * duration;
                float chirpFreq = Mathf.Lerp(2000f, 4000f, (float)rnd.NextDouble());
                int chirpStart = Mathf.RoundToInt(chirpTime * sampleRate);
                int chirpLen = Mathf.RoundToInt(0.08f * sampleRate);

                for (int s = 0; s < chirpLen && (chirpStart + s) < totalSamples; s++)
                {
                    float ct = (float)s / chirpLen;
                    float env = Mathf.Sin(ct * Mathf.PI);
                    float freq = chirpFreq * (1f + Mathf.Sin(ct * 15f) * 0.1f);
                    data[chirpStart + s] += Mathf.Sin(2f * Mathf.PI * freq * ((float)(chirpStart + s) / sampleRate)) * env * 0.12f;
                }
            }

            // Insect buzzes
            for (int ins = 0; ins < 6; ins++)
            {
                float insTime = (float)rnd.NextDouble() * duration;
                int insStart = Mathf.RoundToInt(insTime * sampleRate);
                int insLen = Mathf.RoundToInt(0.5f * sampleRate);
                float insFreq = Mathf.Lerp(3500f, 5500f, (float)rnd.NextDouble());

                for (int s = 0; s < insLen && (insStart + s) < totalSamples; s++)
                {
                    float st = (float)s / insLen;
                    float env = Mathf.Sin(st * Mathf.PI) * 0.5f;
                    data[insStart + s] += Mathf.Sin(2f * Mathf.PI * insFreq * ((float)(insStart + s) / sampleRate)) * env * 0.06f;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateTempleAmbientClip(string name, int sampleRate)
        {
            // 8-second temple ambient with echoing drips and stone resonance
            float duration = 8.0f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];
            System.Random rnd = new System.Random(4001);

            // Low stone resonance drone
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float drone = Mathf.Sin(2.0f * Mathf.PI * 60f * t) * 0.2f;
                drone += Mathf.Sin(2.0f * Mathf.PI * 90f * t) * 0.12f;
                data[i] = drone;
            }

            // Water drip echoes
            for (int d = 0; d < 10; d++)
            {
                float dripTime = (float)rnd.NextDouble() * duration;
                int dripStart = Mathf.RoundToInt(dripTime * sampleRate);
                int dripLen = Mathf.RoundToInt(0.15f * sampleRate);

                for (int s = 0; s < dripLen && (dripStart + s) < totalSamples; s++)
                {
                    float dt_val = (float)s / dripLen;
                    float env = Mathf.Exp(-dt_val * 8f);
                    float freq = Mathf.Lerp(1800f, 600f, dt_val);
                    data[dripStart + s] += Mathf.Sin(2f * Mathf.PI * freq * ((float)(dripStart + s) / sampleRate)) * env * 0.15f;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateVolcanicAmbientClip(string name, int sampleRate)
        {
            // 8-second volcanic ambient with rumbling and crackling
            float duration = 8.0f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];
            System.Random rnd = new System.Random(5001);

            // Low volcanic rumble
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float rumble = Mathf.Sin(2.0f * Mathf.PI * 35f * t) * 0.25f;
                rumble += Mathf.Sin(2.0f * Mathf.PI * 50f * t + Mathf.Sin(t * 0.3f) * 3f) * 0.15f;
                float crackle = ((float)rnd.NextDouble() * 2f - 1f) * 0.08f;
                data[i] = rumble + crackle;
            }

            // Occasional lava pops
            for (int p = 0; p < 8; p++)
            {
                float popTime = (float)rnd.NextDouble() * duration;
                int popStart = Mathf.RoundToInt(popTime * sampleRate);
                int popLen = Mathf.RoundToInt(0.06f * sampleRate);

                for (int s = 0; s < popLen && (popStart + s) < totalSamples; s++)
                {
                    float pt = (float)s / popLen;
                    float env = Mathf.Exp(-pt * 15f);
                    data[popStart + s] += ((float)rnd.NextDouble() * 2f - 1f) * env * 0.2f;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateIntenseMusicLayerClip(string name, int sampleRate)
        {
            // 4-bar intense percussion layer (synced to 150 BPM)
            float bpm = 150f;
            float beatDuration = 60f / bpm;
            int totalBeats = 8;
            float totalDuration = totalBeats * beatDuration;
            int totalSamples = Mathf.RoundToInt(totalDuration * sampleRate);
            float[] data = new float[totalSamples];

            System.Random rnd = new System.Random(7001);

            for (int b = 0; b < totalBeats; b++)
            {
                float beatStartTime = b * beatDuration;
                int startSample = Mathf.RoundToInt(beatStartTime * sampleRate);

                // Fast hi-hats on every 16th note
                for (int sixteenth = 0; sixteenth < 4; sixteenth++)
                {
                    int subStart = startSample + Mathf.RoundToInt(sixteenth * (beatDuration * 0.25f) * sampleRate);
                    int hatLen = Mathf.RoundToInt(0.03f * sampleRate);
                    for (int s = 0; s < hatLen && (subStart + s) < totalSamples; s++)
                    {
                        float t = (float)s / sampleRate;
                        float env = Mathf.Exp(-t * 80f);
                        float noise = (float)rnd.NextDouble() * 2f - 1f;
                        data[subStart + s] += noise * env * 0.2f;
                    }
                }

                // Intense kick on downbeats
                if (b % 2 == 0)
                {
                    int kickLen = Mathf.RoundToInt(0.12f * sampleRate);
                    for (int s = 0; s < kickLen && (startSample + s) < totalSamples; s++)
                    {
                        float t = (float)s / sampleRate;
                        float env = Mathf.Exp(-t * 25f);
                        float freq = Mathf.Lerp(150f, 45f, (float)s / kickLen);
                        data[startSample + s] += Mathf.Sin(2.0f * Mathf.PI * freq * t) * env * 0.35f;
                    }
                }
            }

            // Normalize
            float maxVal = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                if (Mathf.Abs(data[i]) > maxVal) maxVal = Mathf.Abs(data[i]);
            }
            if (maxVal > 0.95f)
            {
                float scale = 0.95f / maxVal;
                for (int i = 0; i < totalSamples; i++) data[i] *= scale;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateMonsterRoarClip(string name, int sampleRate)
        {
            float duration = 1.2f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t / duration) * Mathf.Exp(-t * 1.5f);

                // Low guttural growl with harmonics
                float fundamental = Mathf.Sin(2.0f * Mathf.PI * 55f * t);
                float growl1 = 0.6f * Mathf.Sin(2.0f * Mathf.PI * 82f * t + Mathf.Sin(2.0f * Mathf.PI * 6f * t) * 2.0f);
                float growl2 = 0.3f * Mathf.Sin(2.0f * Mathf.PI * 110f * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;

                data[i] = (fundamental + growl1 + growl2 + noise) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateWindRushClip(string name, int sampleRate)
        {
            float duration = 4.0f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Filtered noise with slow modulation
                float noise = UnityEngine.Random.value * 2f - 1f;
                float mod = 0.5f + 0.5f * Mathf.Sin(2.0f * Mathf.PI * 0.3f * t);
                float filter = Mathf.Lerp(0.2f, 0.8f, mod);
                data[i] = noise * filter * 0.25f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateTempleMusicClip(string name, int sampleRate)
        {
            float duration = 8.0f;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[totalSamples];

            // Temple drone: A minor chord with stone percussion
            float[] chordFreqs = { 220f, 261.63f, 329.63f, 440f };
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 0.3f;

                float sample = 0f;
                foreach (float freq in chordFreqs)
                {
                    sample += Mathf.Sin(2.0f * Mathf.PI * freq * t) * 0.12f;
                }

                // Stone percussion hits
                float beatPhase = (t % 1.6f) / 1.6f;
                if (beatPhase < 0.05f)
                {
                    sample += Mathf.Sin(2.0f * Mathf.PI * 80f * t) * Mathf.Exp(-beatPhase * 60f) * 0.3f;
                }

                data[i] = sample * env;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }
}
