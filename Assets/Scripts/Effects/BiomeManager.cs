using System;
using UnityEngine;
using Runner.Core;
using Runner.CameraControl;
using Runner.Player;

namespace Runner.Effects
{
    public enum BiomeType
    {
        JungleCanopy,    // 0m - 1000m: Lush mossy temple stones, tropical golden sun, emerald mist
        SunkenTemple,    // 1000m - 2000m: Weathered ancient Aztec sandstone & 3D Rocky Path
        VolcanicCaverns  // 2000m+: Dark obsidian basalt & 3D Low-Poly Highway
    }

    /// <summary>
    /// Dynamic Environmental Biome & Atmosphere Manager:
    /// - Smoothly transitions lighting, ambient color, and fog across 3 distinct biomes based on distance traveled.
    /// - Provides dynamic biome-themed road and curb materials to TrackManager for newly spawned chunks.
    /// - Emits cinematic biome transition toasts and ambient sound transitions.
    /// - Adds speed wind particles / streak effects at high running speeds.
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

        [Header("Biome Distance Thresholds (Meters)")]
        [SerializeField] private float templeThreshold = 1000f;
        [SerializeField] private float volcanicThreshold = 2000f;

        [Header("Speed Wind Particle Settings")]
        [SerializeField] private float speedWindThreshold = 14.0f;

        public BiomeType CurrentBiome { get; private set; } = BiomeType.JungleCanopy;

        // Cached Lighting
        private Light directionalLight;

        // Biome Material Caches
        private Material jungleFloorMat;
        private Material jungleCurbMat;

        private Material templeFloorMat;
        private Material templeCurbMat;

        private Material volcanicFloorMat;
        private Material volcanicCurbMat;

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
        private ParticleSystem emberParticles;            // Volcanic ember sparks

        // Biome-specific ambient color tints
        private Color currentAmbientTint = Color.white;

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
            InitializeEmberParticles();
        }

        private void Start()
        {
            FindDirectionalLight();
            SetBiomeInstant(BiomeType.JungleCanopy);
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

            // 1. Evaluate Current Biome based on player distance
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                float dist = GameManager.Instance.DistanceTraveled;
                BiomeType evalBiome = EvaluateBiomeForDistance(dist);

                if (evalBiome != CurrentBiome)
                {
                    TransitionToBiome(evalBiome);
                }

                // 2. Update Speed Wind Lines
                UpdateSpeedWindParticles(GameManager.Instance.CurrentSpeed);

                // 3. Update Ambient Particles (motes, mist, embers)
                UpdateAmbientParticles(GameManager.Instance.CurrentSpeed, CurrentBiome);
            }

            // 4. Smooth Atmospheric Lerp
            LerpAtmosphere(dt);
        }

        public BiomeType EvaluateBiomeForDistance(float distance)
        {
            if (distance >= volcanicThreshold)
                return BiomeType.VolcanicCaverns;
            if (distance >= templeThreshold)
                return BiomeType.SunkenTemple;
            return BiomeType.JungleCanopy;
        }

        private void TransitionToBiome(BiomeType newBiome)
        {
            CurrentBiome = newBiome;

            string toastIcon = "";
            string toastTitle = "";

            switch (newBiome)
            {
                case BiomeType.JungleCanopy:
                    toastIcon = "🌿";
                    toastTitle = "Entering Overgrown Jungle Canopy";
                    SetAtmosphericTargets(
                        fogCol: new Color(0.68f, 0.88f, 0.95f),
                        fogStart: 80.0f, fogEnd: 175.0f,
                        ambientSky: new Color(0.78f, 0.95f, 1.0f),
                        ambientGround: new Color(0.48f, 0.52f, 0.38f),
                        lightCol: new Color(1.0f, 0.96f, 0.88f),
                        lightIntensity: 1.60f
                    );
                    break;

                case BiomeType.SunkenTemple:
                    toastIcon = "🏛️";
                    toastTitle = "Entering Sunken Temple (Ancient Rocky Path)";
                    SetAtmosphericTargets(
                        fogCol: new Color(0.88f, 0.78f, 0.58f),
                        fogStart: 65.0f, fogEnd: 165.0f,
                        ambientSky: new Color(0.82f, 0.72f, 0.50f),
                        ambientGround: new Color(0.45f, 0.35f, 0.22f),
                        lightCol: new Color(1.0f, 0.86f, 0.50f),
                        lightIntensity: 1.60f
                    );
                    break;

                case BiomeType.VolcanicCaverns:
                    toastIcon = "🔥";
                    toastTitle = "Entering Volcanic Highway Caverns";
                    SetAtmosphericTargets(
                        fogCol: new Color(0.68f, 0.22f, 0.15f),
                        fogStart: 55.0f, fogEnd: 155.0f,
                        ambientSky: new Color(0.72f, 0.25f, 0.18f),
                        ambientGround: new Color(0.42f, 0.12f, 0.08f),
                        lightCol: new Color(1.0f, 0.55f, 0.22f),
                        lightIntensity: 1.65f
                    );
                    break;
            }

            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.ShowToast(toastIcon, toastTitle, 2.5f);
            }
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
            // 1. Jungle Canopy Materials (Lush Mossy Stone & Carved Relief Curb)
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

            // 2. Sunken Temple Materials (Gilded Weathered Sandstone)
            Texture2D templeFloorTex = Resources.Load<Texture2D>("Textures/Tex_TempleRunway");
            templeFloorMat = MaterialHelper.CreatePBRMaterial(
                new Color(0.50f, 0.44f, 0.32f),
                albedo: templeFloorTex,
                metallicValue: 0.08f,
                smoothness: 0.35f,
                emissionColor: new Color(0.15f, 0.10f, 0.03f) * 0.8f
            );
            templeFloorMat.name = "Biome_Temple_Floor";

            Texture2D templeCurbTex = Resources.Load<Texture2D>("Textures/Tex_TempleCurb");
            templeCurbMat = MaterialHelper.CreatePBRMaterial(
                new Color(0.32f, 0.28f, 0.20f),
                albedo: templeCurbTex,
                metallicValue: 0.05f,
                smoothness: 0.30f
            );
            templeCurbMat.name = "Biome_Temple_Curb";

            // 3. Volcanic Caverns Materials (Dark Obsidian Basalt with Ember Sheen)
            Texture2D volcanicTex = Resources.Load<Texture2D>("Textures/Tex_Obstacle");
            volcanicFloorMat = MaterialHelper.CreatePBRMaterial(
                new Color(0.12f, 0.12f, 0.15f),
                albedo: volcanicTex,
                metallicValue: 0.45f,
                smoothness: 0.65f,
                emissionColor: new Color(0.40f, 0.08f, 0.02f)
            );
            volcanicFloorMat.name = "Biome_Volcanic_Floor";

            volcanicCurbMat = MaterialHelper.CreatePBRMaterial(
                new Color(0.10f, 0.08f, 0.08f),
                metallicValue: 0.35f,
                smoothness: 0.55f,
                emissionColor: new Color(0.25f, 0.04f, 0.01f)
            );
            volcanicCurbMat.name = "Biome_Volcanic_Curb";
        }

        public Material GetFloorMaterialForBiome(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.SunkenTemple => templeFloorMat,
                BiomeType.VolcanicCaverns => volcanicFloorMat,
                _ => jungleFloorMat
            };
        }

        public Material GetCurbMaterialForBiome(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.SunkenTemple => templeCurbMat,
                BiomeType.VolcanicCaverns => volcanicCurbMat,
                _ => jungleCurbMat
            };
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

        private void InitializeEmberParticles()
        {
            GameObject emberObj = new GameObject("EmberParticles");
            emberObj.transform.SetParent(transform, false);

            emberParticles = emberObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(emberParticles);
            var main = emberParticles.main;
            main.maxParticles = 30;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 2.5f;
            main.startSpeed = 1.4f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 0.55f, 0.10f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = emberParticles.emission;
            emission.rateOverTime = 0;

            var shape = emberParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 1f, 12f);

            var renderer = emberObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Dedicated soft fiery ember material — zero solid white squares!
            renderer.sharedMaterial = CreateParticleMaterial(new Color(1f, 0.55f, 0.12f, 0.85f), GetSoftCircleTexture(), isAdditive: true);
        }

        private void UpdateAmbientParticles(float speed, BiomeType biome)
        {
            if (ambientMotesParticles == null || groundMistParticles == null || PlayerController.Instance == null) return;

            Transform playerT = PlayerController.Instance.transform;

            // Keep ambient motes near the player
            ambientMotesParticles.transform.position = playerT.position + Vector3.up * 2.5f;
            groundMistParticles.transform.position = playerT.position + Vector3.up * 0.3f;

            // Ember particles follow in volcanic biome only
            if (emberParticles != null)
            {
                emberParticles.transform.position = playerT.position + Vector3.up * 0.5f;
                if (biome == BiomeType.VolcanicCaverns && speed > 8f)
                {
                    var emberEmission = emberParticles.emission;
                    emberEmission.rateOverTime = 12f;
                    if (!emberParticles.isPlaying) emberParticles.Play();
                }
                else
                {
                    var emberEmission = emberParticles.emission;
                    emberEmission.rateOverTime = 0f;
                }
            }

            // Ambient motes only when playing
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (!ambientMotesParticles.isPlaying) ambientMotesParticles.Play();
                if (!groundMistParticles.isPlaying) groundMistParticles.Play();
            }
            else
            {
                if (ambientMotesParticles.isPlaying) ambientMotesParticles.Stop();
                if (groundMistParticles.isPlaying) groundMistParticles.Stop();
                if (emberParticles != null && emberParticles.isPlaying) emberParticles.Stop();
            }
        }
        #endregion
    }
}
