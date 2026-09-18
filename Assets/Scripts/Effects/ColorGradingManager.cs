using UnityEngine;
using Runner.Core;
using Runner.Effects;

namespace Runner.Effects
{
    /// <summary>
    /// Runtime Color Grading & Post-Processing Manager for Spider Temple Escape.
    /// Provides cinematic color correction, vignette, chromatic aberration, and speed blur
    /// without requiring URP/HDRP post-processing stack.
    /// Optimized for low-end 3GB RAM mobile devices.
    /// </summary>
    public class ColorGradingManager : MonoBehaviour
    {
        private static ColorGradingManager _instance;
        public static ColorGradingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ColorGradingManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ColorGradingManager");
                        _instance = go.AddComponent<ColorGradingManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Color Grading Settings")]
        [SerializeField] private float saturationBoost = 1.15f;
        [SerializeField] private float contrastBoost = 1.10f;
        [SerializeField] private float temperatureShift = 0.05f;

        [Header("Vignette Settings")]
        [SerializeField] private float vignetteIntensity = 0.35f;
        [SerializeField] private float vignetteSmoothness = 0.8f;

        [Header("Speed Effects")]
        [SerializeField] private float maxSpeedBlur = 0.3f;
        [SerializeField] private float maxChromaticAberration = 0.02f;

        [Header("Biome Color Presets")]
        [SerializeField] private Color jungleTint = new Color(0.92f, 1.02f, 0.90f);
        [SerializeField] private Color templeTint = new Color(1.02f, 0.90f, 0.78f);
        [SerializeField] private Color volcanicTint = new Color(1.05f, 0.82f, 0.70f);

        // Runtime state
        private Material gradingMaterial;
        private RenderTexture sourceRT;
        private RenderTexture gradedRT;
        private Camera targetCamera;
        private BiomeType currentBiome = BiomeType.JungleCanopy;
        private float targetVignetteIntensity;
        private float currentVignetteIntensity;
        private float speedEffectAmount;

        // Shader property IDs (cached for performance)
        private static readonly int PropSaturation = Shader.PropertyToID("_Saturation");
        private static readonly int PropContrast = Shader.PropertyToID("_Contrast");
        private static readonly int PropTemperature = Shader.PropertyToID("_Temperature");
        private static readonly int PropVignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int PropVignetteSmoothness = Shader.PropertyToID("_VignetteSmoothness");
        private static readonly int PropBiomeTint = Shader.PropertyToID("_BiomeTint");
        private static readonly int PropSpeedBlur = Shader.PropertyToID("_SpeedBlur");
        private static readonly int PropChromaticAberration = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            targetVignetteIntensity = vignetteIntensity;
            currentVignetteIntensity = vignetteIntensity;

            InitializeMaterial();
        }

        private void Start()
        {
            targetCamera = Camera.main;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSpeedChanged += OnSpeedChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSpeedChanged -= OnSpeedChanged;
            }

            if (gradingMaterial != null)
            {
                Destroy(gradingMaterial);
            }
            if (sourceRT != null) Destroy(sourceRT);
            if (gradedRT != null) Destroy(gradedRT);
        }

        private void InitializeMaterial()
        {
            Shader shader = Shader.Find("Hidden/SpiderTempleEscape_ColorGrading");
            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }

            if (shader != null)
            {
                gradingMaterial = new Material(shader);
                gradingMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                // Fallback: create a minimal pass-through shader
                gradingMaterial = CreateFallbackGradingMaterial();
            }
        }

        private Material CreateFallbackGradingMaterial()
        {
            // Use existing Unity shader as fallback since runtime shader compilation is not supported
            Shader fallbackShader = Shader.Find("Hidden/Internal-Colored");
            if (fallbackShader == null)
                fallbackShader = Shader.Find("Sprites/Default");
            
            Material mat = new Material(fallbackShader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            return mat;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (gradingMaterial == null || !Application.isPlaying)
            {
                Graphics.Blit(source, destination);
                return;
            }

            UpdateColorGradingParams();

            Graphics.Blit(source, destination, gradingMaterial);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Smooth vignette transition
            currentVignetteIntensity = Mathf.Lerp(currentVignetteIntensity, targetVignetteIntensity, Time.deltaTime * 3f);

            // Update biome tint
            if (BiomeManager.Instance != null && BiomeManager.Instance.CurrentBiome != currentBiome)
            {
                currentBiome = BiomeManager.Instance.CurrentBiome;
            }
        }

        private void UpdateColorGradingParams()
        {
            if (gradingMaterial == null) return;

            gradingMaterial.SetFloat(PropSaturation, saturationBoost);
            gradingMaterial.SetFloat(PropContrast, contrastBoost);
            gradingMaterial.SetFloat(PropTemperature, temperatureShift);
            gradingMaterial.SetFloat(PropVignetteIntensity, currentVignetteIntensity);
            gradingMaterial.SetFloat(PropVignetteSmoothness, vignetteSmoothness);

            Color biomeTint = jungleTint;
            switch (currentBiome)
            {
                case BiomeType.SunkenTemple:
                    biomeTint = templeTint;
                    break;
                case BiomeType.VolcanicCaverns:
                    biomeTint = volcanicTint;
                    break;
            }
            gradingMaterial.SetColor(PropBiomeTint, biomeTint);

            // Speed effects
            gradingMaterial.SetFloat(PropSpeedBlur, speedEffectAmount * maxSpeedBlur);
            gradingMaterial.SetFloat(PropChromaticAberration, speedEffectAmount * maxChromaticAberration);
        }

        private void OnSpeedChanged(float speed)
        {
            float norm = Mathf.InverseLerp(8f, 20f, speed);
            speedEffectAmount = norm;

            // Increase vignette intensity at high speed for tunnel vision effect
            targetVignetteIntensity = Mathf.Lerp(vignetteIntensity, vignetteIntensity * 1.5f, norm);
        }

        /// <summary>
        /// Trigger a flash effect (e.g., on power-up collect or biome transition)
        /// </summary>
        public void TriggerFlash(Color flashColor, float duration = 0.3f)
        {
            StartCoroutine(FlashCoroutine(flashColor, duration));
        }

        private System.Collections.IEnumerator FlashCoroutine(Color flashColor, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float flashAmount = Mathf.Sin(t * Mathf.PI) * 0.5f;
                if (gradingMaterial != null)
                {
                    gradingMaterial.SetFloat(PropSaturation, saturationBoost + flashAmount);
                }
                yield return null;
            }
            if (gradingMaterial != null)
            {
                gradingMaterial.SetFloat(PropSaturation, saturationBoost);
            }
        }
    }
}
