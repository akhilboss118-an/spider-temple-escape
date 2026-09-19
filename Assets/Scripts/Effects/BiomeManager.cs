using System;
using UnityEngine;
using Runner.Core;
using Runner.CameraControl;
using Runner.Player;

namespace Runner.Effects
{
    public enum BiomeType
    {
        JungleCanopy = 0
    }

    public enum SelectedMapMode
    {
        JungleCanopy = 0
    }

    /// <summary>
    /// Dynamic Environmental Biome & Atmosphere Manager:
    /// - Smoothly transitions lighting, ambient color, and fog across distinct biomes based on distance traveled or map selection.
    /// - Provides dynamic biome-themed road and curb materials to TrackManager for newly spawned chunks.
    /// - Emits cinematic biome transition toasts and ambient sound transitions.
    /// - Adds speed wind particles and ambient jungle mist.
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        private static BiomeManager _instance;
        public static BiomeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<BiomeManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BiomeManager");
                        _instance = go.AddComponent<BiomeManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public const string PREFS_SELECTED_MAP = "SpiderRunner_SelectedMapMode";
        public static SelectedMapMode CurrentMapMode { get; private set; } = SelectedMapMode.JungleCanopy;

        [Header("Speed Wind Particle Settings")]

        [Header("Speed Wind Particle Settings")]
        [SerializeField] private float speedWindThreshold = 14.0f;

        public BiomeType CurrentBiome { get; private set; } = BiomeType.JungleCanopy;

        // Cached Lighting
        private Light directionalLight;

        // Biome Material Caches
        private Material jungleFloorMat;
        private Material jungleCurbMat;

        // Interpolation Targets
        private Color targetFogColor;
        private float targetFogStart;
        private float targetFogEnd;
        private Color targetAmbientSky;
        private Color targetAmbientGround;
        private Color targetLightColor;
        private float targetLightIntensity;

        // Speed Wind Lines Particle System
        private ParticleSystem speedWindParticles;

        // AAA Ambient Particle Systems
        private ParticleSystem ambientMotesParticles;    // Floating dust/spore motes
        private ParticleSystem groundMistParticles;       // Low ground fog/mist

        // Running Trail Effects
        private ParticleSystem dustTrailParticles;
        private ParticleSystem speedStreakTrail;

        // Biome-specific ambient color tints
        private Color currentAmbientTint = Color.white;

        // Day/Night Cycle based on distance traveled
        [Header("Day/Night Cycle")]
        [Tooltip("Meters for a full day-night cycle")]
        [SerializeField] private float dayNightCycleLength = 4000.0f;

        // Time-of-day phases: Dawn -> Day -> Dusk -> Night -> Dawn
        private static readonly Color dawnFogColor = new Color(0.85f, 0.65f, 0.45f);
        private static readonly Color dayFogColor = new Color(0.68f, 0.88f, 0.95f);
        private static readonly Color duskFogColor = new Color(0.75f, 0.45f, 0.35f);
        private static readonly Color nightFogColor = new Color(0.08f, 0.10f, 0.18f);

        private static readonly Color dawnLightColor = new Color(1.0f, 0.80f, 0.55f);
        private static readonly Color dayLightColor = new Color(1.0f, 0.96f, 0.88f);
        private static readonly Color duskLightColor = new Color(1.0f, 0.65f, 0.35f);
        private static readonly Color nightLightColor = new Color(0.25f, 0.30f, 0.55f);

        private static readonly Color dawnAmbientSky = new Color(0.90f, 0.70f, 0.55f);
        private static readonly Color dayAmbientSky = new Color(0.78f, 0.95f, 1.0f);
        private static readonly Color duskAmbientSky = new Color(0.85f, 0.50f, 0.40f);
        private static readonly Color nightAmbientSky = new Color(0.10f, 0.12f, 0.25f);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitializeMaterials();
            InitializeSpeedWindParticles();
            InitializeAmbientMotes();
            InitializeGroundMist();
            InitializeDustTrail();
            InitializeSpeedStreakTrail();
        }

        private void Start()
        {
            FindDirectionalLight();
            CurrentMapMode = SelectedMapMode.JungleCanopy;
            SetBiomeInstant(BiomeType.JungleCanopy);
        }

        public void SetSelectedMap(SelectedMapMode mode)
        {
            CurrentMapMode = SelectedMapMode.JungleCanopy;
            PlayerPrefs.SetInt(PREFS_SELECTED_MAP, (int)CurrentMapMode);
            PlayerPrefs.Save();
            SetBiomeInstant(BiomeType.JungleCanopy);
            if (Runner.Monster.MonsterChaser.Instance != null)
            {
                Runner.Monster.MonsterChaser.Instance.ApplyBiomeMonster(BiomeType.JungleCanopy);
            }
        }

        public static BiomeType GetInitialBiomeForMode(SelectedMapMode mode)
        {
            return BiomeType.JungleCanopy;
        }

        private void FindDirectionalLight()
        {
            if (directionalLight == null)
            {
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        directionalLight = l;
                        break;
                    }
                }
            }
        }

        private static void PrepareParticleSystemForSetup(ParticleSystem ps)
        {
            if (ps == null) return;
            if (ps.isPlaying)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            var main = ps.main;
            main.playOnAwake = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                // Update Speed Wind Lines
                UpdateSpeedWindParticles(GameManager.Instance.CurrentSpeed);

                // Update Ambient Particles
                UpdateAmbientParticles(GameManager.Instance.CurrentSpeed, CurrentBiome);

                // Update Day/Night Cycle
                UpdateDayNightCycle(GameManager.Instance.DistanceTraveled);
            }

            // Smooth Atmospheric Lerp
            LerpAtmosphere(dt);
        }

        public BiomeType EvaluateBiomeForDistance(float distance)
        {
            return BiomeType.JungleCanopy;
        }

        private void TransitionToBiome(BiomeType newBiome)
        {
            CurrentBiome = newBiome;

            string toastIcon = "🌿";
            string toastTitle = "Entering Overgrown Jungle Canopy";

            SetAtmosphericTargets(
                fogCol: new Color(0.68f, 0.88f, 0.95f),
                fogStart: 80.0f, fogEnd: 175.0f,
                ambientSky: new Color(0.78f, 0.95f, 1.0f),
                ambientGround: new Color(0.48f, 0.52f, 0.38f),
                lightCol: new Color(1.0f, 0.96f, 0.88f),
                lightIntensity: 1.60f
            );

            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.ShowToast(toastIcon, toastTitle, 2.5f);
            }

            // Apply monster theme matching biome
            if (Runner.Monster.MonsterChaser.Instance != null)
            {
                Runner.Monster.MonsterChaser.Instance.ApplyBiomeMonster(newBiome);
            }

            // Skybox transitions per biome
            ApplyBiomeSkybox(newBiome);
        }

        private void ApplyBiomeSkybox(BiomeType biome)
        {
            Material jungleSky = Resources.Load<Material>("Materials/Mat_TempleSkybox");
            if (jungleSky != null)
            {
                RenderSettings.skybox = jungleSky;
            }
        }

        private Material CreateBiomeSkyboxMaterial(Color topColor, Color bottomColor, float blend)
        {
            Shader skyShader = Shader.Find("RenderFX/Skybox/Gradient");
            if (skyShader == null) skyShader = Shader.Find("Skybox/Gradient");
            if (skyShader == null) return RenderSettings.skybox;

            Material mat = new Material(skyShader);
            mat.SetColor("_TopColor", topColor);
            mat.SetColor("_BottomColor", bottomColor);
            mat.SetFloat("_Exponent", blend);
            return mat;
        }

        private void SetAtmosphericTargets(Color fogCol, float fogStart, float fogEnd, Color ambientSky, Color ambientGround, Color lightCol, float lightIntensity)
        {
            targetFogColor = fogCol;
            targetFogStart = fogStart;
            targetFogEnd = fogEnd;
            targetAmbientSky = ambientSky;
            targetAmbientGround = ambientGround;
            targetLightColor = lightCol;
            targetLightIntensity = lightIntensity;
        }

        private void SetBiomeInstant(BiomeType biome)
        {
            TransitionToBiome(biome);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = targetFogColor;
            RenderSettings.fogStartDistance = targetFogStart;
            RenderSettings.fogEndDistance = targetFogEnd;
            RenderSettings.ambientSkyColor = targetAmbientSky;
            RenderSettings.ambientGroundColor = targetAmbientGround;

            if (directionalLight != null)
            {
                directionalLight.color = targetLightColor;
                directionalLight.intensity = targetLightIntensity;
            }
        }

        private void UpdateDayNightCycle(float distance)
        {
            // Cycle through 4 phases: Dawn (0-25%), Day (25-50%), Dusk (50-75%), Night (75-100%)
            float cyclePosition = (distance % dayNightCycleLength) / dayNightCycleLength;
            float quarterDay = dayNightCycleLength * 0.25f;
            float phasePosition = (distance % quarterDay) / quarterDay;

            Color fogColor, lightColor, ambientColor;
            float lightIntensity;

            if (cyclePosition < 0.25f)
            {
                // Dawn -> Day
                fogColor = Color.Lerp(dawnFogColor, dayFogColor, phasePosition);
                lightColor = Color.Lerp(dawnLightColor, dayLightColor, phasePosition);
                ambientColor = Color.Lerp(dawnAmbientSky, dayAmbientSky, phasePosition);
                lightIntensity = Mathf.Lerp(1.2f, 1.6f, phasePosition);
            }
            else if (cyclePosition < 0.50f)
            {
                // Day -> Dusk
                fogColor = Color.Lerp(dayFogColor, duskFogColor, phasePosition);
                lightColor = Color.Lerp(dayLightColor, duskLightColor, phasePosition);
                ambientColor = Color.Lerp(dayAmbientSky, duskAmbientSky, phasePosition);
                lightIntensity = Mathf.Lerp(1.6f, 1.1f, phasePosition);
            }
            else if (cyclePosition < 0.75f)
            {
                // Dusk -> Night
                fogColor = Color.Lerp(duskFogColor, nightFogColor, phasePosition);
                lightColor = Color.Lerp(duskLightColor, nightLightColor, phasePosition);
                ambientColor = Color.Lerp(duskAmbientSky, nightAmbientSky, phasePosition);
                lightIntensity = Mathf.Lerp(1.1f, 0.4f, phasePosition);
            }
            else
            {
                // Night -> Dawn
                fogColor = Color.Lerp(nightFogColor, dawnFogColor, phasePosition);
                lightColor = Color.Lerp(nightLightColor, dawnLightColor, phasePosition);
                ambientColor = Color.Lerp(nightAmbientSky, dawnAmbientSky, phasePosition);
                lightIntensity = Mathf.Lerp(0.4f, 1.2f, phasePosition);
            }

            SetAtmosphericTargets(
                fogCol: fogColor,
                fogStart: 80.0f, fogEnd: 175.0f,
                ambientSky: ambientColor,
                ambientGround: new Color(0.48f, 0.52f, 0.38f),
                lightCol: lightColor,
                lightIntensity: lightIntensity
            );
        }

        private void LerpAtmosphere(float dt)
        {
            float lerpSpeed = dt * 1.2f;

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, lerpSpeed);
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, targetFogStart, lerpSpeed);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, targetFogEnd, lerpSpeed);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, targetAmbientSky, lerpSpeed);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, targetAmbientGround, lerpSpeed);

            if (directionalLight != null)
            {
                directionalLight.color = Color.Lerp(directionalLight.color, targetLightColor, lerpSpeed);
                directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, targetLightIntensity, lerpSpeed);
            }
        }

        #region Material Generation for Biomes
        private void InitializeMaterials()
        {
            // Jungle Canopy Materials (Lush Mossy Stone & Carved Relief Curb)
            jungleFloorMat = Resources.Load<Material>("Materials/Mat_Track");
            if (jungleFloorMat == null)
            {
                Texture2D jungleFloorTex = Resources.Load<Texture2D>("Textures/Tex_Track");
                jungleFloorMat = MaterialHelper.CreatePBRMaterial(
                    new Color(0.28f, 0.32f, 0.25f),
                    albedo: jungleFloorTex,
                    smoothness: 0.25f,
                    emissionColor: new Color(0.02f, 0.04f, 0.01f) * 0.5f
                );
                jungleFloorMat.name = "Biome_Jungle_Floor";
            }
            jungleCurbMat = Resources.Load<Material>("Materials/Mat_Curb");
            if (jungleCurbMat == null)
            {
                Texture2D jungleCurbTex = Resources.Load<Texture2D>("Textures/Tex_Curb");
                jungleCurbMat = MaterialHelper.CreatePBRMaterial(
                    new Color(0.18f, 0.22f, 0.16f),
                    albedo: jungleCurbTex,
                    smoothness: 0.20f
                );
                jungleCurbMat.name = "Biome_Jungle_Curb";
            }
        }

        public Material GetFloorMaterialForBiome(BiomeType biome)
        {
            if (jungleFloorMat == null)
            {
                InitializeMaterials();
            }
            return jungleFloorMat;
        }

        public Material GetCurbMaterialForBiome(BiomeType biome)
        {
            if (jungleCurbMat == null)
            {
                InitializeMaterials();
            }
            return jungleCurbMat;
        }
        #endregion

        #region Procedural Particle Textures & Materials
        private static Texture2D _cachedSoftCircleTex;
        private static Texture2D _cachedSoftCloudTex;
        private static Texture2D _cachedSpeedStreakTex;

        public static Texture2D GetSoftCircleTexture()
        {
            if (_cachedSoftCircleTex != null) return _cachedSoftCircleTex;

            int size = 64;
            _cachedSoftCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _cachedSoftCircleTex.name = "Procedural_SoftCircle";
            _cachedSoftCircleTex.wrapMode = TextureWrapMode.Clamp;
            _cachedSoftCircleTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    // Smooth cosine falloff
                    float alpha = 0.5f * (1.0f + Mathf.Cos(t * Mathf.PI));
                    if (t >= 1.0f) alpha = 0f;
                    alpha = Mathf.Pow(alpha, 1.6f); // Soft edge

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _cachedSoftCircleTex.SetPixels(pixels);
            _cachedSoftCircleTex.Apply();
            return _cachedSoftCircleTex;
        }

        public static Texture2D GetSoftCloudTexture()
        {
            if (_cachedSoftCloudTex != null) return _cachedSoftCloudTex;

            int size = 64;
            _cachedSoftCloudTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _cachedSoftCloudTex.name = "Procedural_SoftCloud";
            _cachedSoftCloudTex.wrapMode = TextureWrapMode.Clamp;
            _cachedSoftCloudTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, t);
                    alpha = Mathf.Pow(alpha, 2.2f); // Very soft wispy mist falloff

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _cachedSoftCloudTex.SetPixels(pixels);
            _cachedSoftCloudTex.Apply();
            return _cachedSoftCloudTex;
        }

        public static Texture2D GetSpeedStreakTexture()
        {
            if (_cachedSpeedStreakTex != null) return _cachedSpeedStreakTex;

            int width = 64;
            int height = 16;
            _cachedSpeedStreakTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _cachedSpeedStreakTex.name = "Procedural_SpeedStreak";
            _cachedSpeedStreakTex.wrapMode = TextureWrapMode.Clamp;
            _cachedSpeedStreakTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = Mathf.Abs((y - (height * 0.5f)) / (height * 0.5f));
                float yFalloff = Mathf.Clamp01(1.0f - ny * ny);

                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / (width - 1);
                    // Tapered aerodynamic speed line
                    float xFalloff = Mathf.Sin(nx * Mathf.PI);
                    float alpha = yFalloff * xFalloff;

                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _cachedSpeedStreakTex.SetPixels(pixels);
            _cachedSpeedStreakTex.Apply();
            return _cachedSpeedStreakTex;
        }

        public static Material CreateParticleMaterial(Color color, Texture2D texture, bool isAdditive = false)
        {
            Shader s = null;
            if (isAdditive)
            {
                s = Shader.Find("Mobile/Particles/Additive")
                 ?? Shader.Find("Particles/Additive")
                 ?? Shader.Find("Legacy Shaders/Particles/Additive")
                 ?? Shader.Find("Particles/Standard Unlit")
                 ?? Shader.Find("Sprites/Default");
            }
            else
            {
                s = Shader.Find("Mobile/Particles/Alpha Blended")
                 ?? Shader.Find("Particles/Alpha Blended")
                 ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                 ?? Shader.Find("Particles/Standard Unlit")
                 ?? Shader.Find("Sprites/Default");
            }

            if (s == null)
            {
                s = Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            }

            Material mat = new Material(s);
            mat.name = isAdditive ? "Particle_Additive" : "Particle_AlphaBlend";

            if (texture != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            }

            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            // If Standard or Standard Unlit, configure explicit transparency
            if (s.name.Contains("Standard"))
            {
                mat.SetFloat("_Mode", isAdditive ? 3 : 2); // 2 = Fade, 3 = Transparent
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.SrcAlpha));
                mat.SetInt("_DstBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            mat.renderQueue = 3000;
            return mat;
        }
        #endregion

        #region Speed Wind Lines Particles
        private void InitializeSpeedWindParticles()
        {
            GameObject windObj = new GameObject("SpeedWindParticles");
            windObj.transform.SetParent(transform, false);

            speedWindParticles = windObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(speedWindParticles);
            var main = speedWindParticles.main;
            main.maxParticles = 50;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 35.0f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.85f, 0.96f, 1.0f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = speedWindParticles.emission;
            emission.rateOverTime = 0;

            var shape = speedWindParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.0f, 4.0f, 1.0f);

            var renderer = windObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.18f;
            renderer.lengthScale = 4.0f;

            // Dedicated translucent speed streak material — zero solid white boxes!
            renderer.sharedMaterial = CreateParticleMaterial(new Color(0.85f, 0.98f, 1.0f, 0.45f), GetSpeedStreakTexture(), isAdditive: true);
        }

        private void UpdateSpeedWindParticles(float speed)
        {
            if (speedWindParticles == null || PlayerController.Instance == null) return;

            Transform playerT = PlayerController.Instance.transform;

            // Position particle emitter right in front of the player facing backward
            speedWindParticles.transform.position = playerT.position + playerT.forward * 8.0f + Vector3.up * 1.5f;
            speedWindParticles.transform.rotation = Quaternion.LookRotation(-playerT.forward, Vector3.up);

            var emission = speedWindParticles.emission;
            if (speed >= speedWindThreshold)
            {
                float t = Mathf.InverseLerp(speedWindThreshold, 20.0f, speed);
                emission.rateOverTime = Mathf.Lerp(15f, 50f, t);
                if (!speedWindParticles.isPlaying) speedWindParticles.Play();
            }
            else
            {
                emission.rateOverTime = 0;
            }
        }
        #endregion

        #region AAA Ambient Particle Systems
        private void InitializeAmbientMotes()
        {
            GameObject moteObj = new GameObject("AmbientMotes");
            moteObj.transform.SetParent(transform, false);

            ambientMotesParticles = moteObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(ambientMotesParticles);
            var main = ambientMotesParticles.main;
            main.maxParticles = 35;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 5f;
            main.startSpeed = 0.25f;
            main.startSize = 0.05f;
            main.startColor = new Color(1.0f, 0.90f, 0.50f, 0.50f); // Warm golden sunlit spores
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ambientMotesParticles.emission;
            emission.rateOverTime = 7;

            var shape = ambientMotesParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 5f, 10f);

            var renderer = moteObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Dedicated soft circular golden dust particle material — zero solid white squares!
            renderer.sharedMaterial = CreateParticleMaterial(new Color(1.0f, 0.88f, 0.45f, 0.60f), GetSoftCircleTexture(), isAdditive: true);

            // Start hidden until player starts
            ambientMotesParticles.Stop();
        }

        private void InitializeGroundMist()
        {
            GameObject mistObj = new GameObject("GroundMist");
            mistObj.transform.SetParent(transform, false);

            groundMistParticles = mistObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(groundMistParticles);
            var main = groundMistParticles.main;
            main.maxParticles = 20;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 5f;
            main.startSpeed = 0.12f;
            main.startSize = 1.6f;
            main.startColor = new Color(0.80f, 0.92f, 0.82f, 0.07f); // Wispy emerald canyon mist
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = groundMistParticles.emission;
            emission.rateOverTime = 4;

            var shape = groundMistParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 1.2f, 14f);

            var renderer = mistObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Dedicated soft radial cloud mist material — zero solid white squares!
            renderer.sharedMaterial = CreateParticleMaterial(new Color(0.82f, 0.92f, 0.85f, 0.08f), GetSoftCloudTexture(), isAdditive: false);

            groundMistParticles.Stop();
        }

        private void UpdateAmbientParticles(float speed, BiomeType biome)
        {
            if (ambientMotesParticles == null || groundMistParticles == null || PlayerController.Instance == null) return;

            Transform playerT = PlayerController.Instance.transform;

            // Keep ambient motes near the player
            ambientMotesParticles.transform.position = playerT.position + Vector3.up * 2.5f;
            groundMistParticles.transform.position = playerT.position + Vector3.up * 0.3f;

            // Ambient motes only when playing
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (!ambientMotesParticles.isPlaying) ambientMotesParticles.Play();
                if (!groundMistParticles.isPlaying) groundMistParticles.Play();
                UpdateDustTrail(speed, playerT);
                UpdateSpeedStreakTrail(speed, playerT);
            }
            else
            {
                if (ambientMotesParticles.isPlaying) ambientMotesParticles.Stop();
                if (groundMistParticles.isPlaying) groundMistParticles.Stop();
                if (dustTrailParticles != null && dustTrailParticles.isPlaying) dustTrailParticles.Stop();
                if (speedStreakTrail != null && speedStreakTrail.isPlaying) speedStreakTrail.Stop();
            }
        }

        #region Running Trail Effects
        private void InitializeDustTrail()
        {
            GameObject trailObj = new GameObject("DustTrail");
            trailObj.transform.SetParent(transform, false);
            dustTrailParticles = trailObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(dustTrailParticles);

            var main = dustTrailParticles.main;
            main.maxParticles = 25;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.2f;
            main.startSize = 0.35f;
            main.startColor = new Color(0.85f, 0.78f, 0.65f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = dustTrailParticles.emission;
            emission.rateOverTime = 15f;

            var shape = dustTrailParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.0f, 0.2f, 0.5f);

            var renderer = trailObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = CreateParticleMaterial(new Color(0.85f, 0.78f, 0.65f, 0.5f), GetSoftCircleTexture(), isAdditive: false);

            dustTrailParticles.Stop();
        }

        private void InitializeSpeedStreakTrail()
        {
            GameObject streakObj = new GameObject("SpeedStreakTrail");
            streakObj.transform.SetParent(transform, false);
            speedStreakTrail = streakObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(speedStreakTrail);

            var main = speedStreakTrail.main;
            main.maxParticles = 15;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 0.4f;
            main.startSpeed = 3.0f;
            main.startSize = 0.12f;
            main.startColor = new Color(1.0f, 0.95f, 0.85f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = speedStreakTrail.emission;
            emission.rateOverTime = 0f;

            var shape = speedStreakTrail.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 0.3f, 0.3f);

            var renderer = streakObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 3.0f;
            renderer.sharedMaterial = CreateParticleMaterial(new Color(1.0f, 0.95f, 0.85f, 0.5f), GetSpeedStreakTexture(), isAdditive: true);

            speedStreakTrail.Stop();
        }

        private void UpdateDustTrail(float speed, Transform playerT)
        {
            if (dustTrailParticles == null) return;
            dustTrailParticles.transform.position = playerT.position + Vector3.up * 0.1f - playerT.forward * 0.8f;

            var emission = dustTrailParticles.emission;
            float rate = Mathf.Lerp(5f, 30f, Mathf.InverseLerp(8f, 20f, speed));
            emission.rateOverTime = rate;

            var main = dustTrailParticles.main;
            main.startSpeed = Mathf.Lerp(0.5f, 2.0f, Mathf.InverseLerp(8f, 20f, speed));

            if (!dustTrailParticles.isPlaying) dustTrailParticles.Play();
        }

        private void UpdateSpeedStreakTrail(float speed, Transform playerT)
        {
            if (speedStreakTrail == null) return;
            speedStreakTrail.transform.position = playerT.position + Vector3.up * 1.0f + playerT.forward * 2.0f;
            speedStreakTrail.transform.rotation = Quaternion.LookRotation(-playerT.forward, Vector3.up);

            var emission = speedStreakTrail.emission;
            if (speed >= 14f)
            {
                float t = Mathf.InverseLerp(14f, 22f, speed);
                emission.rateOverTime = Mathf.Lerp(10f, 40f, t);
                if (!speedStreakTrail.isPlaying) speedStreakTrail.Play();
            }
            else
            {
                emission.rateOverTime = 0f;
            }
        }
        #endregion
        #endregion
    }
}
