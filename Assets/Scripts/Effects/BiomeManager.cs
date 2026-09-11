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
                        fogCol: new Color(0.72f, 0.86f, 0.98f),
                        fogStart: 85.0f, fogEnd: 180.0f,
                        ambientSky: new Color(0.82f, 0.92f, 1.0f),
                        ambientGround: new Color(0.52f, 0.48f, 0.42f),
                        lightCol: new Color(1.0f, 0.98f, 0.92f),
                        lightIntensity: 1.55f
                    );
                    break;

                case BiomeType.SunkenTemple:
                    toastIcon = "🏛️";
                    toastTitle = "Entering Sunken Temple (Ancient Rocky Path)";
                    SetAtmosphericTargets(
                        fogCol: new Color(0.85f, 0.76f, 0.60f),
                        fogStart: 70.0f, fogEnd: 170.0f,
                        ambientSky: new Color(0.78f, 0.68f, 0.52f),
                        ambientGround: new Color(0.42f, 0.32f, 0.22f),
                        lightCol: new Color(1.0f, 0.84f, 0.52f),
                        lightIntensity: 1.55f
                    );
                    break;

                case BiomeType.VolcanicCaverns:
                    toastIcon = "🔥";
                    toastTitle = "Entering Volcanic Highway Caverns";
                    SetAtmosphericTargets(
                        fogCol: new Color(0.65f, 0.25f, 0.18f),
                        fogStart: 60.0f, fogEnd: 160.0f,
                        ambientSky: new Color(0.68f, 0.28f, 0.22f),
                        ambientGround: new Color(0.38f, 0.15f, 0.12f),
                        lightCol: new Color(1.0f, 0.58f, 0.25f),
                        lightIntensity: 1.60f
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
                jungleFloorMat = MaterialHelper.CreateSafeMaterial(new Color(0.28f, 0.32f, 0.25f));
                jungleFloorMat.name = "Biome_Jungle_Floor";
            }
            jungleCurbMat = Resources.Load<Material>("Materials/Mat_Curb");
            if (jungleCurbMat == null)
            {
                jungleCurbMat = MaterialHelper.CreateSafeMaterial(new Color(0.18f, 0.22f, 0.16f));
                jungleCurbMat.name = "Biome_Jungle_Curb";
            }

            // 2. Sunken Temple Materials (Gilded Weathered Sandstone)
            templeFloorMat = MaterialHelper.CreateSafeMaterial(new Color(0.50f, 0.44f, 0.32f));
            templeFloorMat.name = "Biome_Temple_Floor";
            templeCurbMat = MaterialHelper.CreateSafeMaterial(new Color(0.32f, 0.28f, 0.20f));
            templeCurbMat.name = "Biome_Temple_Curb";

            // 3. Volcanic Caverns Materials (Dark Obsidian Basalt with Ember Sheen)
            volcanicFloorMat = MaterialHelper.CreateSafeMaterial(new Color(0.12f, 0.12f, 0.15f));
            volcanicFloorMat.name = "Biome_Volcanic_Floor";
            volcanicFloorMat.EnableKeyword("_EMISSION");
            volcanicFloorMat.SetColor("_EmissionColor", new Color(0.40f, 0.08f, 0.02f)); // Glowing magma veins

            volcanicCurbMat = MaterialHelper.CreateSafeMaterial(new Color(0.10f, 0.08f, 0.08f));
            volcanicCurbMat.name = "Biome_Volcanic_Curb";
            volcanicCurbMat.EnableKeyword("_EMISSION");
            volcanicCurbMat.SetColor("_EmissionColor", new Color(0.25f, 0.04f, 0.01f));
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

        #region Speed Wind Lines Particles
        private void InitializeSpeedWindParticles()
        {
            GameObject windObj = new GameObject("SpeedWindParticles");
            windObj.transform.SetParent(transform, false);

            speedWindParticles = windObj.AddComponent<ParticleSystem>();
            PrepareParticleSystemForSetup(speedWindParticles);
            var main = speedWindParticles.main;
            main.maxParticles = 60;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 35.0f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 1f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = speedWindParticles.emission;
            emission.rateOverTime = 0;

            var shape = speedWindParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.0f, 4.0f, 1.0f);

            var renderer = windObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.15f;
            renderer.lengthScale = 3.5f;
            renderer.sharedMaterial = MaterialHelper.CreateSafeMaterial(new Color(1f, 1f, 1f, 0.4f));
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
                emission.rateOverTime = Mathf.Lerp(15f, 60f, t);
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
            main.maxParticles = 40;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = 0.3f;
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.95f, 0.7f, 0.25f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ambientMotesParticles.emission;
            emission.rateOverTime = 8;

            var shape = ambientMotesParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 6f, 12f);

            var renderer = moteObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Material moteMat = MaterialHelper.CreateSafeMaterial(new Color(1f, 0.92f, 0.6f, 0.2f));
            moteMat.EnableKeyword("_ALPHABLEND_ON");
            renderer.sharedMaterial = moteMat;

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
            main.maxParticles = 25;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 5f;
            main.startSpeed = 0.15f;
            main.startSize = 0.8f;
            main.startColor = new Color(0.8f, 0.85f, 0.8f, 0.08f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = groundMistParticles.emission;
            emission.rateOverTime = 5;

            var shape = groundMistParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 1.5f, 14f);

            var renderer = mistObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Material mistMat = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.9f, 0.8f, 0.06f));
            renderer.sharedMaterial = mistMat;

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
            main.startLifetime = 3f;
            main.startSpeed = 1.2f;
            main.startSize = 0.06f;
            main.startColor = new Color(1f, 0.4f, 0.05f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = emberParticles.emission;
            emission.rateOverTime = 0;

            var shape = emberParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 1f, 12f);

            var renderer = emberObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 2f;

            Material emberMat = MaterialHelper.CreateSafeMaterial(new Color(1f, 0.35f, 0.05f, 0.6f));
            renderer.sharedMaterial = emberMat;
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
