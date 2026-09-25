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
                    EnsureExists();
                }
                return _instance;
            }
        }

        /// <summary>
        /// OnRenderImage only fires for components sitting on a Camera, so the manager
        /// always attaches itself to the main camera instead of a standalone object.
        /// </summary>
        public static ColorGradingManager EnsureExists()
        {
            if (_instance != null) return _instance;

            _instance = FindAnyObjectByType<ColorGradingManager>();
            if (_instance != null) return _instance;

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindAnyObjectByType<Camera>();
            }
            if (cam == null) return null;

            _instance = cam.GetComponent<ColorGradingManager>();
            if (_instance == null)
            {
                _instance = cam.gameObject.AddComponent<ColorGradingManager>();
            }
            return _instance;
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

        [Header("Environment Color Preset")]
        [SerializeField] private Color jungleTint = new Color(0.92f, 1.02f, 0.90f);

        // Runtime state
        public const string GRADING_SHADER_NAME = "Hidden/SpiderTempleEscape_ColorGrading";
        private bool gradingEnabled = true;
        private Material gradingMaterial;
        private RenderTexture sourceRT;
        private RenderTexture gradedRT;
        private Camera targetCamera;
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
            gradingMaterial = null;

            // Preferred: material asset shipped through Resources so the shader is
            // guaranteed to survive build-time shader stripping.
            Material loaded = Resources.Load<Material>("Materials/Mat_ColorGrading");
            if (loaded != null && loaded.shader != null && loaded.shader.name == GRADING_SHADER_NAME)
            {
                gradingMaterial = new Material(loaded);
                gradingMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (gradingMaterial == null)
            {
                Shader shader = Shader.Find(GRADING_SHADER_NAME);
                if (shader != null)
                {
                    gradingMaterial = new Material(shader);
                    gradingMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            if (gradingMaterial == null)
            {
                // No grading shader available: stay in pass-through mode instead of
                // blitting through an unrelated shader (which would corrupt the frame).
                gradingEnabled = false;
                Debug.LogWarning("[ColorGradingManager] Grading shader missing - post effects disabled");
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!gradingEnabled || gradingMaterial == null || !Application.isPlaying)
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
        }

        private void UpdateColorGradingParams()
        {
            if (gradingMaterial == null) return;

            gradingMaterial.SetFloat(PropSaturation, saturationBoost);
            gradingMaterial.SetFloat(PropContrast, contrastBoost);
            gradingMaterial.SetFloat(PropTemperature, temperatureShift);
            gradingMaterial.SetFloat(PropVignetteIntensity, currentVignetteIntensity);
            gradingMaterial.SetFloat(PropVignetteSmoothness, vignetteSmoothness);

            gradingMaterial.SetColor(PropBiomeTint, jungleTint);

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
