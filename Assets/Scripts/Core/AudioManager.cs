using System;
using UnityEngine;
using Runner.Core;

namespace Runner.Audio
{
    /// <summary>
    /// Centralized high-fidelity Audio Manager for Spider Temple Escape:
    /// - Dynamic Adaptive Jungle Chase BGM with speed-dependent tempo/pitch scaling (8 m/s -> 20 m/s).
    /// - Consecutive Coin / Heart Chimes with ascending 5-note pentatonic arpeggio (Subway Surfers feel).
    /// - Crisp physics movement SFX: Jump ballistic whoosh, gravel slide scrape, landing thud.
    /// - Power-up fanfares for Shield, Speedrun boost, Magnet, and Heart Revive.
    /// - Beast guardian proximity stomps and roar cues.
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

        [Header("Clips (Optional overrides)")]
        [SerializeField] private AudioClip customBgmClip;
        [SerializeField] private AudioClip customJumpClip;
        [SerializeField] private AudioClip customSlideClip;
        [SerializeField] private AudioClip customCoinClip;
        [SerializeField] private AudioClip customPowerUpClip;

        // Generated procedural clips
        private AudioClip proceduralBgmClip;
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

        // Coin arpeggio state
        private int consecutiveCoinIndex = 0;
        private float lastCoinCollectTime = -10f;
        private const float COIN_CHAIN_WINDOW = 1.25f;

        // BGM Pitch Scaling
        private float targetPitch = 1.0f;

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
                    break;
                case GameState.Playing:
                    targetPitch = 1.0f;
                    if (!bgmSource.isPlaying) bgmSource.Play();
                    break;
                case GameState.Paused:
                    targetPitch = 0.80f;
                    break;
                case GameState.GameOver:
                    targetPitch = 0.70f;
                    StopLoops();
                    break;
            }
        }

        private void HandleSpeedChanged(float speed)
        {
            // As speed scales from 8 m/s to 20 m/s, pitch ramps from 1.00x to 1.25x for intense adrenaline rush!
            float norm = Mathf.InverseLerp(8.0f, 20.0f, speed);
            targetPitch = Mathf.Lerp(1.0f, 1.26f, norm);
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
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public void StopLoops()
        {
            if (loopSource != null) loopSource.Stop();
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

            AudioClip clip = coinArpeggioClips[consecutiveCoinIndex];
            coinSource.pitch = 1.0f + (consecutiveCoinIndex * 0.04f);
            coinSource.PlayOneShot(clip, 0.95f);
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
        #endregion
    }
}
