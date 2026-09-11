using System.Collections.Generic;
using UnityEngine;
using Runner.CameraControl;

namespace Runner.Effects
{
    /// <summary>
    /// AAA-quality Physical Collision Effects Manager:
    /// - Natural stone dust puffs and tumbling rock chip debris on impacts
    /// - Crystalline shield shatter bursts
    /// - Speedrun obstacle demolition shockwaves
    /// - Pink heart collection sparkles
    /// - Landing impact dust bursts
    /// - Shield aura energy field visual
    /// - Web/spider trail particles on high speed
    /// Completely replaces cartoonish effects with realistic physical feedback.
    /// </summary>
    public class ImpactEffectManager : MonoBehaviour
    {
        private static ImpactEffectManager _instance;
        public static ImpactEffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<ImpactEffectManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ImpactEffectManager");
                        _instance = go.AddComponent<ImpactEffectManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // Materials
        private Material rockDebrisMat;
        private Material dustPuffMat;
        private Material shieldShardMat;
        private Material heartSparkleMat;
        private Material landingDustMat;

        // Shield Aura
        private GameObject shieldAuraObject;
        private Renderer shieldAuraRenderer;
        private bool isShieldAuraActive = false;
        private float shieldAuraPulseTimer = 0f;

        // Audio
        private AudioSource audioSource;
        private AudioClip rockHitClip;
        private AudioClip shieldBreakClip;
        private AudioClip heartChimeClip;
        private AudioClip landingClip;

        // Particle pools
        private const int POOL_SIZE = 32;
        private readonly List<GameObject> debrisPool = new List<GameObject>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureMaterials();
            EnsureAudio();
            InitializeDebrisPool();
            InitializeShieldAura();
        }

        private void Update()
        {
            UpdateShieldAura();
        }

        private void InitializeDebrisPool()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.name = "PooledDebris";
                debris.transform.localScale = Vector3.one * 0.15f;
                var mr = debris.GetComponent<MeshRenderer>();
                if (mr != null && rockDebrisMat != null) mr.sharedMaterial = rockDebrisMat;
                debris.SetActive(false);
                debrisPool.Add(debris);
            }
        }

        private GameObject GetPooledDebris()
        {
            for (int i = debrisPool.Count - 1; i >= 0; i--)
            {
                GameObject d = debrisPool[i];
                if (d == null)
                {
                    debrisPool.RemoveAt(i);
                    continue;
                }
                if (!d.activeSelf) return d;
            }
            // Expand pool if needed
            var extra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extra.name = "PooledDebris";
            extra.transform.localScale = Vector3.one * 0.15f;
            var extraMr = extra.GetComponent<MeshRenderer>();
            if (extraMr != null && rockDebrisMat != null) extraMr.sharedMaterial = rockDebrisMat;
            extra.SetActive(false);
            debrisPool.Add(extra);
            return extra;
        }

        private void EnsureMaterials()
        {
            if (rockDebrisMat == null)
            {
                rockDebrisMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.48f, 0.46f, 0.42f));
                if (rockDebrisMat != null) rockDebrisMat.name = "RockDebrisMat";
            }

            if (dustPuffMat == null)
            {
                dustPuffMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.68f, 0.64f, 0.58f, 0.6f));
                if (dustPuffMat != null)
                {
                    dustPuffMat.name = "DustPuffMat";
                    dustPuffMat.SetFloat("_Mode", 3);
                    dustPuffMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    dustPuffMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    dustPuffMat.EnableKeyword("_ALPHABLEND_ON");
                    dustPuffMat.renderQueue = 3000;
                }
            }

            if (shieldShardMat == null)
            {
                shieldShardMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.25f, 0.85f, 1.0f));
                if (shieldShardMat != null) shieldShardMat.name = "ShieldShardMat";
            }

            if (heartSparkleMat == null)
            {
                heartSparkleMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.35f, 0.70f));
                if (heartSparkleMat != null) heartSparkleMat.name = "HeartSparkleMat";
            }

            if (landingDustMat == null)
            {
                landingDustMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.55f, 0.50f, 0.42f, 0.5f));
                if (landingDustMat != null)
                {
                    landingDustMat.name = "LandingDustMat";
                    landingDustMat.SetFloat("_Mode", 3);
                    landingDustMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    landingDustMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    landingDustMat.EnableKeyword("_ALPHABLEND_ON");
                    landingDustMat.renderQueue = 3000;
                }
            }
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.0f;
            }

            if (rockHitClip == null) rockHitClip = CreateSyntheticThud();
            if (shieldBreakClip == null) shieldBreakClip = CreateSyntheticShatter();
            if (heartChimeClip == null) heartChimeClip = CreateSyntheticChime();
            if (landingClip == null) landingClip = CreateSyntheticLanding();
        }

        #region Shield Aura System
        private void InitializeShieldAura()
        {
            if (shieldAuraObject != null) return;

            // Create shield aura sphere around player
            shieldAuraObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shieldAuraObject.name = "ShieldAura";
            shieldAuraObject.layer = LayerMask.NameToLayer("TransparentFX");

            // Apply the AAA ShieldAura shader
            Shader auraShader = Shader.Find("Custom/AAA_ShieldAura");
            if (auraShader == null)
            {
                // Fallback: use Unity's Particles/Additive
                auraShader = Shader.Find("Particles/Additive");
            }

            if (auraShader != null)
            {
                Material auraMat = new Material(auraShader);
                shieldAuraObject.GetComponent<Renderer>().sharedMaterial = auraMat;
            }
            else
            {
                Material auraMat = new Material(Shader.Find("Sprites/Default"));
                auraMat.color = new Color(0.3f, 0.85f, 1f, 0.3f);
                shieldAuraObject.GetComponent<Renderer>().sharedMaterial = auraMat;
            }

            shieldAuraRenderer = shieldAuraObject.GetComponent<Renderer>();
            shieldAuraObject.SetActive(false);

            // Scale to fit around player (CharacterController radius ~0.5, height ~2.0)
            shieldAuraObject.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
        }

        public void ActivateShieldAura(Vector3 playerPosition)
        {
            if (shieldAuraObject == null) InitializeShieldAura();
            if (shieldAuraObject == null) return;

            shieldAuraObject.transform.position = playerPosition + Vector3.up * 1.0f;
            shieldAuraObject.SetActive(true);
            isShieldAuraActive = true;
            shieldAuraPulseTimer = 0f;
        }

        public void DeactivateShieldAura()
        {
            if (shieldAuraObject != null)
            {
                shieldAuraObject.SetActive(false);
            }
            isShieldAuraActive = false;
        }

        private void UpdateShieldAura()
        {
            if (!isShieldAuraActive || shieldAuraObject == null) return;

            // Pulse and rotate the shield aura
            shieldAuraPulseTimer += Time.deltaTime;
            float pulse = 0.92f + 0.08f * Mathf.Sin(shieldAuraPulseTimer * 3.0f);
            float breathe = 1.0f + 0.03f * Mathf.Sin(shieldAuraPulseTimer * 1.5f);
            shieldAuraObject.transform.localScale = new Vector3(2.2f * breathe, 2.2f * breathe, 2.2f * breathe);
            shieldAuraObject.transform.Rotate(Vector3.up, Time.deltaTime * 45f, Space.World);

            // Follow player position
            if (Runner.Player.PlayerController.Instance != null)
            {
                Vector3 pPos = Runner.Player.PlayerController.Instance.transform.position;
                shieldAuraObject.transform.position = pPos + Vector3.up * 1.0f;
            }
        }
        #endregion

        #region Collision & Impact Effects
        /// <summary>
        /// Natural physical rock collision: Spawns stone fragments and dust puff, plus camera recoil.
        /// </summary>
        public void PlayRockHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            EnsureMaterials();

            // 1. Spawn tumbling rock fragments
            int fragmentCount = Random.Range(7, 11);
            for (int i = 0; i < fragmentCount; i++)
            {
                GameObject frag = GetPooledDebris();
                if (frag == null) continue;
                frag.name = "RockFragment";
                frag.transform.position = hitPoint + Random.insideUnitSphere * 0.25f;
                frag.SetActive(true);
                float s = Random.Range(0.08f, 0.22f);
                frag.transform.localScale = new Vector3(s, s * Random.Range(0.7f, 1.3f), s);
                frag.GetComponent<MeshRenderer>().sharedMaterial = rockDebrisMat;
                Destroy(frag.GetComponent<Collider>());

                Vector3 ejectVelocity = (hitNormal + Random.insideUnitSphere * 0.8f + Vector3.up * 1.2f).normalized * Random.Range(3.5f, 6.5f);
                Vector3 tumbleTorque = Random.insideUnitSphere * 360f;
                var deb = frag.GetComponent<PhysicalDebris>();
                if (deb == null) deb = frag.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(ejectVelocity, tumbleTorque, 0.8f);
            }

            // 2. Spawn expanding soft dust puff (AAA: more particles, wider spread)
            for (int d = 0; d < 5; d++)
            {
                GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dust.name = "DustPuff";
                dust.transform.position = hitPoint + Vector3.up * 0.2f + Random.insideUnitSphere * 0.15f;
                dust.transform.localScale = Vector3.one * Random.Range(0.35f, 0.65f);
                dust.GetComponent<MeshRenderer>().sharedMaterial = dustPuffMat;
                Destroy(dust.GetComponent<Collider>());

                Vector3 dustDrift = (Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f);
                var deb = dust.AddComponent<PhysicalDebris>();
                deb.Initialize(dustDrift, Vector3.zero, 0.55f, expand: true);
            }

            // 3. Camera physical jolt (AAA: stronger for more impact)
            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.32f, 0.28f);
            }

            // 4. Heavy stone thud audio
            if (audioSource != null && rockHitClip != null)
            {
                audioSource.pitch = Random.Range(0.88f, 1.12f);
                audioSource.PlayOneShot(rockHitClip, 0.9f);
            }
        }

        /// <summary>
        /// Play landing dust burst when Spider-Man lands from a jump.
        /// </summary>
        public void PlayLandingDust(Vector3 position)
        {
            EnsureMaterials();

            // Radial burst of dust particles
            for (int i = 0; i < 8; i++)
            {
                GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dust.name = "LandingDust";
                float angle = (float)i / 8f * Mathf.PI * 2f;
                dust.transform.position = position + Vector3.up * 0.05f + new Vector3(Mathf.Cos(angle) * 0.3f, 0, Mathf.Sin(angle) * 0.3f);
                dust.transform.localScale = Vector3.one * Random.Range(0.2f, 0.45f);
                dust.GetComponent<MeshRenderer>().sharedMaterial = landingDustMat;
                Destroy(dust.GetComponent<Collider>());

                Vector3 burstDir = new Vector3(Mathf.Cos(angle) * 1.5f, Random.Range(0.4f, 0.8f), Mathf.Sin(angle) * 1.5f);
                var deb = dust.AddComponent<PhysicalDebris>();
                deb.Initialize(burstDir, Vector3.zero, 0.45f, expand: true);
            }

            // Central soft puff
            GameObject centerDust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerDust.name = "LandingCentralDust";
            centerDust.transform.position = position + Vector3.up * 0.1f;
            centerDust.transform.localScale = Vector3.one * 0.5f;
            centerDust.GetComponent<MeshRenderer>().sharedMaterial = landingDustMat;
            Destroy(centerDust.GetComponent<Collider>());
            var centerDeb = centerDust.AddComponent<PhysicalDebris>();
            centerDeb.Initialize(Vector3.up * 0.6f, Vector3.zero, 0.5f, expand: true);

            // Audio
            if (audioSource != null && landingClip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(landingClip, 0.4f);
            }
        }

        /// <summary>
        /// Shield absorption: Shatters crystalline shield aura into cyan shards without harming player.
        /// </summary>
        public void PlayShieldBreak(Vector3 position)
        {
            DeactivateShieldAura();
            EnsureMaterials();

            int shardCount = 14;
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GetPooledDebris();
                if (shard == null) continue;
                shard.name = "ShieldShard";
                shard.transform.position = position + Random.insideUnitSphere * 0.4f;
                shard.SetActive(true);
                float s = Random.Range(0.06f, 0.18f);
                shard.transform.localScale = new Vector3(s, s * 2.0f, s * 0.5f);
                shard.GetComponent<MeshRenderer>().sharedMaterial = shieldShardMat;
                Destroy(shard.GetComponent<Collider>());

                Vector3 burstDir = Random.onUnitSphere * Random.Range(4.0f, 8.0f);
                var deb = shard.GetComponent<PhysicalDebris>();
                if (deb == null) deb = shard.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(burstDir, Random.insideUnitSphere * 400f, 0.65f);
            }

            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.20f, 0.22f);
            }

            if (audioSource != null && shieldBreakClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(shieldBreakClip, 0.95f);
            }
        }

        /// <summary>
        /// Speedrun demolition: Obliterates rock with massive debris explosion.
        /// </summary>
        public void PlaySpeedrunSmash(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 18; i++)
            {
                GameObject frag = GetPooledDebris();
                if (frag == null) continue;
                frag.transform.position = position + Random.insideUnitSphere * 0.5f;
                frag.SetActive(true);
                frag.name = "SpeedrunDebris";
                float s = Random.Range(0.12f, 0.35f);
                frag.transform.localScale = Vector3.one * s;
                frag.GetComponent<MeshRenderer>().sharedMaterial = rockDebrisMat;
                Destroy(frag.GetComponent<Collider>());

                Vector3 blastVel = (Random.onUnitSphere + Vector3.up * 0.8f + Vector3.forward * 1.5f).normalized * Random.Range(6.0f, 12.0f);
                var deb = frag.GetComponent<PhysicalDebris>();
                if (deb == null) deb = frag.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(blastVel, Random.insideUnitSphere * 500f, 0.9f);
            }

            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.40f, 0.32f);
            }

            if (audioSource != null && rockHitClip != null)
            {
                audioSource.pitch = 0.75f;
                audioSource.PlayOneShot(rockHitClip, 1.0f);
            }
        }

        /// <summary>
        /// Pink heart collect: Emits subtle floating pink sparkles.
        /// </summary>
        public void PlayHeartCollect(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 6; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.transform.position = position + Random.insideUnitSphere * 0.2f;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
                spark.GetComponent<MeshRenderer>().sharedMaterial = heartSparkleMat;
                Destroy(spark.GetComponent<Collider>());

                Vector3 upDrift = (Vector3.up * 2.5f + Random.insideUnitSphere * 0.6f);
                var deb = spark.AddComponent<PhysicalDebris>();
                deb.Initialize(upDrift, Vector3.zero, 0.5f, expand: false, shrink: true);
            }

            if (audioSource != null && heartChimeClip != null)
            {
                audioSource.pitch = Random.Range(0.98f, 1.08f);
                audioSource.PlayOneShot(heartChimeClip, 0.5f);
            }
        }

        /// <summary>
        /// Mystery chest burst: Gold sparkles and coin rain.
        /// </summary>
        public void PlayChestBurst(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 20; i++)
            {
                GameObject coinSpark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coinSpark.name = "ChestCoinSpark";
                coinSpark.transform.position = position + Random.insideUnitSphere * 0.3f;
                float s = Random.Range(0.12f, 0.25f);
                coinSpark.transform.localScale = new Vector3(s, s, s);
                Destroy(coinSpark.GetComponent<Collider>());
                if (heartSparkleMat != null)
                {
                    coinSpark.GetComponent<MeshRenderer>().sharedMaterial = heartSparkleMat;
                }

                Vector3 burstVelocity = (Vector3.up * Random.Range(3.5f, 7.0f)) + (Random.insideUnitSphere * Random.Range(1.5f, 4.0f));
                var deb = coinSpark.AddComponent<PhysicalDebris>();
                deb.Initialize(burstVelocity, Vector3.zero, Random.Range(0.6f, 1.0f), expand: false, shrink: true);
            }
        }
        #endregion

        #region Synthetic Audio Generators
        private static AudioClip CreateSyntheticThud()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.28f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(120f, 35f, t);
                float env = Mathf.Exp(-t * 12f);
                float noise = (Random.value * 2f - 1f) * 0.35f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate)) + noise) * env;
            }
            AudioClip clip = AudioClip.Create("ProceduralThud", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSyntheticShatter()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.32f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 9f);
                float wave = Mathf.Sin(2f * Mathf.PI * 880f * (i / (float)sampleRate)) * 0.5f +
                             Mathf.Sin(2f * Mathf.PI * 1420f * (i / (float)sampleRate)) * 0.3f;
                float glass = (Random.value * 2f - 1f) * 0.4f;
                data[i] = (wave + glass) * env;
            }
            AudioClip clip = AudioClip.Create("ProceduralShatter", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSyntheticChime()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.22f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 8f);
                float chime = Mathf.Sin(2f * Mathf.PI * 1318.5f * (i / (float)sampleRate)) * 0.7f +
                              Mathf.Sin(2f * Mathf.PI * 1760.0f * (i / (float)sampleRate)) * 0.4f;
                data[i] = chime * env;
            }
            AudioClip clip = AudioClip.Create("ProceduralChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSyntheticLanding()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 18f);
                float thud = Mathf.Sin(2f * Mathf.PI * 80f * (i / (float)sampleRate)) * 0.6f;
                float sand = (Random.value * 2f - 1f) * 0.4f;
                data[i] = (thud + sand) * env;
            }
            AudioClip clip = AudioClip.Create("ProceduralLanding", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }

    /// <summary>
    /// Helper component that animates physical debris (velocity, gravity, rotation, fade/shrink).
    /// </summary>
    public class PhysicalDebris : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 torque;
        private float lifetime;
        private float elapsed = 0f;
        private Vector3 initialScale;
        private bool expand = false;
        private bool shrink = true;
        private bool recycleOnExpiry = false;

        public void SetRecyclable(bool recyclable)
        {
            recycleOnExpiry = recyclable;
        }

        public void Initialize(Vector3 initialVelocity, Vector3 angularTorque, float duration, bool expand = false, bool shrink = true)
        {
            velocity = initialVelocity;
            torque = angularTorque;
            lifetime = duration;
            this.expand = expand;
            this.shrink = shrink;
            this.elapsed = 0f;
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            if (elapsed >= lifetime)
            {
                if (recycleOnExpiry)
                {
                    elapsed = 0f;
                    velocity = Vector3.zero;
                    torque = Vector3.zero;
                    gameObject.SetActive(false);
                    return;
                }
                Destroy(gameObject);
                return;
            }

            float t = elapsed / lifetime;

            // Apply gravity and velocity (AAA: slightly reduced gravity for floatier dust)
            velocity += Vector3.down * (10.0f * dt);
            transform.position += velocity * dt;

            // Apply tumble
            transform.Rotate(torque * dt, Space.Self);

            // Scale transition
            if (expand)
            {
                transform.localScale = Vector3.Lerp(initialScale, initialScale * 2.4f, t);
            }
            else if (shrink)
            {
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t * t);
            }
        }
    }
}
