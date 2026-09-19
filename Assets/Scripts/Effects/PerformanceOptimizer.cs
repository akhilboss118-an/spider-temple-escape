using UnityEngine;
using Runner.Core;

namespace Runner.Effects
{
    /// <summary>
    /// Performance Optimizer for Spider Temple Escape.
    /// Automatically detects device capabilities and adjusts quality settings
    /// to ensure smooth 60 FPS on devices with 3GB RAM or less.
    /// Handles LOD management, texture streaming, draw call batching,
    /// and dynamic quality scaling based on real-time frame rate monitoring.
    /// </summary>
    public class PerformanceOptimizer : MonoBehaviour
    {
        private static PerformanceOptimizer _instance;
        public static PerformanceOptimizer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PerformanceOptimizer>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("PerformanceOptimizer");
                        _instance = go.AddComponent<PerformanceOptimizer>();
                    }
                }
                return _instance;
            }
        }

        [Header("Quality Tiers")]
        [SerializeField] private float lowEndRamThreshold = 3.0f;
        [SerializeField] private float midEndRamThreshold = 4.5f;

        [Header("Frame Rate Targets")]
        [SerializeField] private float targetFrameRate = 60f;
        [SerializeField] private float criticalFrameRate = 40f;

        [Header("Dynamic Quality Scaling")]
        [SerializeField] private bool enableDynamicScaling = true;
        [SerializeField] private float frameRateCheckInterval = 1.5f;
        [SerializeField] private float qualityStepDownCooldown = 3f;

        [Header("Dynamic Resolution Scaling")]
        [SerializeField] private bool enableDynamicResolution = true;
        [SerializeField] private float minResolutionScale = 0.65f;
        [SerializeField] private float maxResolutionScale = 1.0f;
        [SerializeField] private float resolutionStepSize = 0.05f;
        [SerializeField] private float resolutionCriticalFps = 35f;
        [SerializeField] private float resolutionRecoveryFps = 55f;

        // Device classification
        public enum DeviceTier { Low, Mid, High }
        public DeviceTier CurrentTier { get; private set; } = DeviceTier.High;

        // Current quality level (0 = lowest, 5 = ultra)
        private int currentQualityLevel;
        private float lastQualityAdjustTime;
        private float frameRateAccumulator;
        private int frameCount;
        private float avgFrameRate;

        // Cached settings
        private bool originalShadowsEnabled;
        private ShadowResolution originalShadowResolution;
        private int originalShadowCascades;
        private float originalShadowDistance;
        private bool originalSoftParticles;
        private bool originalAntiAliasing;
        private int originalTextureQuality;

        // Dynamic resolution scaling
        private float currentResolutionScale = 1.0f;
        private float lastResolutionAdjustTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            ClassifyDevice();
            ApplyInitialOptimizations();
        }

        private void Start()
        {
            Application.targetFrameRate = (int)targetFrameRate;
            QualitySettings.vSyncCount = 0;

            // Monitor frame rate for dynamic adjustments
            InvokeRepeating(nameof(MonitorFrameRate), 1f, frameRateCheckInterval);
        }

        /// <summary>
        /// Resets resolution to native when returning to menu (called by GameManager).
        /// </summary>
        public void ResetResolutionToNative()
        {
            if (!enableDynamicResolution) return;
            currentResolutionScale = maxResolutionScale;
            ApplyResolutionScale();
            Debug.Log("[PerformanceOptimizer] Resolution reset to native");
        }

        private void ClassifyDevice()
        {
            float systemRam = GetSystemRamGB();
            int systemMemoryMB = SystemInfo.systemMemorySize;

            if (systemMemoryMB > 0)
            {
                // Use actual system memory if available
                if (systemMemoryMB <= 3072)
                    CurrentTier = DeviceTier.Low;
                else if (systemMemoryMB <= 4608)
                    CurrentTier = DeviceTier.Mid;
                else
                    CurrentTier = DeviceTier.High;
            }
            else
            {
                // Fallback to estimated RAM
                if (systemRam <= lowEndRamThreshold)
                    CurrentTier = DeviceTier.Low;
                else if (systemRam <= midEndRamThreshold)
                    CurrentTier = DeviceTier.Mid;
                else
                    CurrentTier = DeviceTier.High;
            }

            Debug.Log($"[PerformanceOptimizer] Device Tier: {CurrentTier} | RAM: {systemMemoryMB}MB | GPU: {SystemInfo.graphicsDeviceName}");
        }

        private float GetSystemRamGB()
        {
            // Estimate based on device info (approximate)
            long totalMem = System.GC.GetTotalMemory(false);
            return totalMem / (1024f * 1024f * 1024f);
        }

        private void ApplyInitialOptimizations()
        {
            // Cache original settings
            originalShadowsEnabled = QualitySettings.shadows != ShadowQuality.Disable;
            originalShadowResolution = QualitySettings.shadowResolution;
            originalShadowCascades = QualitySettings.shadowCascades;
            originalShadowDistance = QualitySettings.shadowDistance;
            originalSoftParticles = QualitySettings.softParticles;

            switch (CurrentTier)
            {
                case DeviceTier.Low:
                    ApplyLowEndSettings();
                    break;
                case DeviceTier.Mid:
                    ApplyMidEndSettings();
                    break;
                case DeviceTier.High:
                    ApplyHighEndSettings();
                    break;
            }
        }

        private void ApplyLowEndSettings()
        {
            Debug.Log("[PerformanceOptimizer] Applying LOW-END optimizations for smooth 60 FPS");

            // Quality Level
            QualitySettings.SetQualityLevel(0, false);
            currentQualityLevel = 0;

            // Shadows: Disable or use lowest
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 10f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = ShadowResolution.Low;

            // Particles
            QualitySettings.softParticles = false;
            QualitySettings.particleRaycastBudget = 16;

            // Textures
            QualitySettings.globalTextureMipmapLimit = 2; // Half resolution
            QualitySettings.streamingMipmapsActive = false;

            // Rendering
            QualitySettings.antiAliasing = 0;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.billboardsFaceCameraPosition = false;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // LOD bias
            QualitySettings.lodBias = 0.4f;
            QualitySettings.maximumLODLevel = 1;

            // Audio
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 1024;
            config.numRealVoices = 16;
            config.numVirtualVoices = 32;
            AudioSettings.Reset(config);

            // Disable non-essential features
            Application.targetFrameRate = 60;
        }

        private void ApplyMidEndSettings()
        {
            Debug.Log("[PerformanceOptimizer] Applying MID-END optimizations");

            QualitySettings.SetQualityLevel(2, false);
            currentQualityLevel = 2;

            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 20f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = ShadowResolution.Medium;

            QualitySettings.softParticles = false;
            QualitySettings.particleRaycastBudget = 64;

            QualitySettings.globalTextureMipmapLimit = 1; // Quarter resolution
            QualitySettings.antiAliasing = 0;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            QualitySettings.lodBias = 0.7f;
            QualitySettings.maximumLODLevel = 0;

            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 512;
            config.numRealVoices = 24;
            config.numVirtualVoices = 48;
            AudioSettings.Reset(config);
        }

        private void ApplyHighEndSettings()
        {
            Debug.Log("[PerformanceOptimizer] Applying HIGH-END settings");

            QualitySettings.SetQualityLevel(4, false);
            currentQualityLevel = 4;

            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.High;

            QualitySettings.softParticles = true;
            QualitySettings.particleRaycastBudget = 256;

            QualitySettings.globalTextureMipmapLimit = 0; // Full resolution
            QualitySettings.antiAliasing = 2;
            QualitySettings.realtimeReflectionProbes = true;
            QualitySettings.skinWeights = SkinWeights.FourBones;

            QualitySettings.lodBias = 1.0f;
            QualitySettings.maximumLODLevel = 0;

            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 256;
            config.numRealVoices = 32;
            config.numVirtualVoices = 64;
            AudioSettings.Reset(config);
        }

        private void MonitorFrameRate()
        {
            if (!enableDynamicScaling) return;

            frameCount++;
            frameRateAccumulator += 1.0f / Time.unscaledDeltaTime;

            if (Time.time - lastQualityAdjustTime < qualityStepDownCooldown) return;

            avgFrameRate = frameRateAccumulator / frameCount;
            frameRateAccumulator = 0f;
            frameCount = 0;

            // Step down quality if frame rate is too low
            if (avgFrameRate < criticalFrameRate && currentQualityLevel > 0)
            {
                Debug.Log($"[PerformanceOptimizer] Frame rate critical ({avgFrameRate:F1} FPS) - stepping down quality");
                StepDownQuality();
                lastQualityAdjustTime = Time.time;
            }
            // Step up quality if frame rate is very high and we have room
            else if (avgFrameRate > targetFrameRate * 1.1f && currentQualityLevel < 4)
            {
                // Only step up occasionally to avoid oscillation
                if (Time.time - lastQualityAdjustTime > qualityStepDownCooldown * 2f)
                {
                    Debug.Log($"[PerformanceOptimizer] Frame rate excellent ({avgFrameRate:F1} FPS) - stepping up quality");
                    StepUpQuality();
                    lastQualityAdjustTime = Time.time;
                }
            }

            // Dynamic resolution scaling (runs independently of quality stepping)
            if (enableDynamicResolution)
            {
                UpdateDynamicResolution();
            }
        }

        private void UpdateDynamicResolution()
        {
            if (Time.time - lastResolutionAdjustTime < qualityStepDownCooldown * 0.5f) return;

            if (avgFrameRate < resolutionCriticalFps && currentResolutionScale > minResolutionScale)
            {
                currentResolutionScale = Mathf.Max(minResolutionScale, currentResolutionScale - resolutionStepSize);
                ApplyResolutionScale();
                Debug.Log($"[PerformanceOptimizer] Resolution scaled down to {currentResolutionScale * 100:F0}% ({avgFrameRate:F1} FPS)");
                lastResolutionAdjustTime = Time.time;
            }
            else if (avgFrameRate > resolutionRecoveryFps && currentResolutionScale < maxResolutionScale)
            {
                currentResolutionScale = Mathf.Min(maxResolutionScale, currentResolutionScale + resolutionStepSize * 0.5f);
                ApplyResolutionScale();
                Debug.Log($"[PerformanceOptimizer] Resolution scaled up to {currentResolutionScale * 100:F0}% ({avgFrameRate:F1} FPS)");
                lastResolutionAdjustTime = Time.time;
            }
        }

        private void ApplyResolutionScale()
        {
            float scale = currentResolutionScale;
            Screen.SetResolution(
                Mathf.Max(320, (int)(Screen.currentResolution.width * scale)),
                Mathf.Max(480, (int)(Screen.currentResolution.height * scale)),
                true
            );
        }

        private void StepDownQuality()
        {
            currentQualityLevel = Mathf.Max(0, currentQualityLevel - 1);
            QualitySettings.SetQualityLevel(currentQualityLevel, false);

            // Apply specific optimizations
            if (currentQualityLevel <= 1)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 10f;
                QualitySettings.softParticles = false;
                QualitySettings.globalTextureMipmapLimit = 2;
                QualitySettings.lodBias = 0.4f;
            }
            else if (currentQualityLevel <= 2)
            {
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 15f;
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.lodBias = 0.6f;
            }

            // Reduce particle counts if available
            ReduceParticleCounts();
        }

        private void StepUpQuality()
        {
            currentQualityLevel = Mathf.Min(4, currentQualityLevel + 1);
            QualitySettings.SetQualityLevel(currentQualityLevel, false);

            if (currentQualityLevel >= 3)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 25f;
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.lodBias = 0.9f;
            }
        }

        private void ReduceParticleCounts()
        {
            // Find and reduce particle systems for low-end
            var particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                int maxP = main.maxParticles;
                if (maxP > 20)
                {
                    main.maxParticles = Mathf.Max(10, maxP / 2);
                }
            }
        }

        /// <summary>
        /// Force quality settings for debugging or user override
        /// </summary>
        public void SetQualityLevel(int level)
        {
            currentQualityLevel = Mathf.Clamp(level, 0, 4);
            QualitySettings.SetQualityLevel(currentQualityLevel, false);

            switch (currentQualityLevel)
            {
                case 0: ApplyLowEndSettings(); break;
                case 1:
                case 2: ApplyMidEndSettings(); break;
                case 3:
                case 4: ApplyHighEndSettings(); break;
            }
        }

        /// <summary>
        /// Get current performance stats for debugging
        /// </summary>
        public string GetPerformanceStats()
        {
            return $"Tier: {CurrentTier} | Quality: {currentQualityLevel} | FPS: {avgFrameRate:F1} | RAM: {SystemInfo.systemMemorySize}MB | GPU: {SystemInfo.graphicsDeviceName}";
        }

        /// <summary>
        /// Optimize a specific renderer for performance (disable shadows, reduce shadow casting)
        /// </summary>
        public static void OptimizeRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Optimize a particle system for low-end devices
        /// </summary>
        public static void OptimizeParticleSystem(ParticleSystem ps, bool aggressive = false)
        {
            if (ps == null) return;

            var main = ps.main;
            int maxParticles = aggressive ? Mathf.Min(main.maxParticles, 15) : Mathf.Min(main.maxParticles, 30);
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            float currentRate = emission.rateOverTime.constant;
            emission.rateOverTime = currentRate * 0.6f;
        }
    }
}
