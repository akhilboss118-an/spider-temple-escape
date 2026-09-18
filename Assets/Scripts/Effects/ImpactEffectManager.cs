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
        private readonly List<GameObject> sparkPool = new List<GameObject>();

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

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(obj); return; }
#endif
            Destroy(obj);
        }

        private static Mesh cachedStoneShardMesh = null;
        private static Mesh GetStoneShardMesh()
        {
            if (cachedStoneShardMesh != null) return cachedStoneShardMesh;
            cachedStoneShardMesh = new Mesh();
            cachedStoneShardMesh.name = "StoneShardMesh";
            // Irregular 7-vertex faceted chipped stone shard (never a raw cube!)
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.2f, -0.4f),
                new Vector3(0.55f, -0.3f, -0.2f),
                new Vector3(0.2f, -0.2f, 0.45f),
                new Vector3(-0.4f, -0.35f, 0.3f),
                new Vector3(0.0f, 0.55f, 0.0f),
                new Vector3(-0.25f, 0.35f, -0.25f),
                new Vector3(0.25f, 0.25f, 0.15f)
            };
            int[] triangles = new int[]
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                0, 5, 1,
                1, 6, 2,
                3, 2, 0,
                1, 0, 2
            };
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.2f, 0.8f),
                new Vector2(0.8f, 0.8f)
            };
            cachedStoneShardMesh.vertices = vertices;
            cachedStoneShardMesh.triangles = triangles;
            cachedStoneShardMesh.uv = uvs;
            cachedStoneShardMesh.RecalculateNormals();
            cachedStoneShardMesh.RecalculateBounds();
            return cachedStoneShardMesh;
        }

        private void InitializeDebrisPool()
        {
            EnsureMaterials();
            Mesh shardMesh = GetStoneShardMesh();

            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject debris = new GameObject("PooledDebris");
                debris.transform.SetParent(transform, false);
                debris.transform.localScale = Vector3.one * 0.18f;
                var mf = debris.AddComponent<MeshFilter>();
                mf.sharedMesh = shardMesh;
                var mr = debris.AddComponent<MeshRenderer>();
                if (rockDebrisMat != null) mr.sharedMaterial = rockDebrisMat;
                debris.SetActive(false);
                debrisPool.Add(debris);
            }

            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "PooledSpark";
                spark.transform.SetParent(transform, false);
                spark.transform.localScale = Vector3.one * 0.12f;
                SafeDestroy(spark.GetComponent<Collider>());
                var mr = spark.GetComponent<MeshRenderer>();
                if (mr != null && heartSparkleMat != null) mr.sharedMaterial = heartSparkleMat;
                var deb = spark.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                spark.SetActive(false);
                sparkPool.Add(spark);
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
            // Expand pool if needed (stone shard mesh, never raw cube)
            var extra = new GameObject("PooledDebris");
            extra.transform.SetParent(transform, false);
            extra.transform.localScale = Vector3.one * 0.18f;
            var mf = extra.AddComponent<MeshFilter>();
            mf.sharedMesh = GetStoneShardMesh();
            var extraMr = extra.AddComponent<MeshRenderer>();
            if (extraMr != null && rockDebrisMat != null) extraMr.sharedMaterial = rockDebrisMat;
            extra.SetActive(false);
            debrisPool.Add(extra);
            return extra;
        }

        private GameObject GetPooledSpark()
        {
            for (int i = sparkPool.Count - 1; i >= 0; i--)
            {
                GameObject s = sparkPool[i];
                if (s == null)
                {
                    sparkPool.RemoveAt(i);
                    continue;
                }
                if (!s.activeSelf) return s;
            }
            GameObject extra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            extra.name = "PooledSpark";
            extra.transform.SetParent(transform, false);
            extra.transform.localScale = Vector3.one * 0.12f;
            SafeDestroy(extra.GetComponent<Collider>());
            var mr = extra.GetComponent<MeshRenderer>();
            if (mr != null && heartSparkleMat != null) mr.sharedMaterial = heartSparkleMat;
            var deb = extra.AddComponent<PhysicalDebris>();
            deb.SetRecyclable(true);
            extra.SetActive(false);
            sparkPool.Add(extra);
            return extra;
        }

        private void EnsureMaterials()
        {
            if (rockDebrisMat == null)
            {
                Texture2D rockTex = Resources.Load<Texture2D>("Textures/Tex_Obstacle");
#if UNITY_EDITOR
                if (rockTex == null)
                    rockTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Obstacle.png")
                           ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_TempleCurb.jpg");
#endif
                rockDebrisMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.68f, 0.62f, 0.55f), rockTex);
                if (rockDebrisMat != null)
                {
                    rockDebrisMat.name = "RockDebrisMat";
                    if (rockDebrisMat.HasProperty("_Glossiness")) rockDebrisMat.SetFloat("_Glossiness", 0.35f);
                }
            }

            if (dustPuffMat == null)
            {
                Texture2D softDustTex = BiomeManager.GetSoftCircleTexture();
                dustPuffMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.78f, 0.72f, 0.62f, 0.50f), softDustTex);
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
                shieldShardMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.20f, 0.90f, 1.0f));
                if (shieldShardMat != null)
                {
                    shieldShardMat.name = "ShieldShardMat";
                    shieldShardMat.EnableKeyword("_EMISSION");
                    shieldShardMat.SetColor("_EmissionColor", new Color(0.20f, 0.90f, 1.0f) * 1.8f);
                }
            }

            if (heartSparkleMat == null)
            {
                heartSparkleMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.35f, 0.70f));
                if (heartSparkleMat != null)
                {
                    heartSparkleMat.name = "HeartSparkleMat";
                    heartSparkleMat.EnableKeyword("_EMISSION");
                    heartSparkleMat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.70f) * 1.5f);
                }
            }

            if (landingDustMat == null)
            {
                Texture2D softDustTex = BiomeManager.GetSoftCircleTexture();
                landingDustMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.65f, 0.58f, 0.48f, 0.45f), softDustTex);
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
                SafeDestroy(frag.GetComponent<Collider>());

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
                SafeDestroy(dust.GetComponent<Collider>());

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
                SafeDestroy(dust.GetComponent<Collider>());

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
            SafeDestroy(centerDust.GetComponent<Collider>());
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
                SafeDestroy(shard.GetComponent<Collider>());

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
                SafeDestroy(frag.GetComponent<Collider>());

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
        /// Pink heart collect: Emits subtle floating pink sparkles via pre-allocated pool (0 GC).
        /// </summary>
        public void PlayHeartCollect(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 6; i++)
            {
                GameObject spark = GetPooledSpark();
                if (spark == null) continue;
                spark.transform.position = position + Random.insideUnitSphere * 0.2f;
                float s = Random.Range(0.08f, 0.16f);
                spark.transform.localScale = Vector3.one * s;
                spark.SetActive(true);

                Vector3 upDrift = (Vector3.up * 2.5f + Random.insideUnitSphere * 0.6f);
                var deb = spark.GetComponent<PhysicalDebris>();
                if (deb == null) deb = spark.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(upDrift, Vector3.zero, 0.5f, expand: false, shrink: true);
            }

            if (audioSource != null && heartChimeClip != null)
            {
                audioSource.pitch = Random.Range(0.98f, 1.08f);
                audioSource.PlayOneShot(heartChimeClip, 0.5f);
            }
        }

        /// <summary>
        /// Mystery chest burst: Gold sparkles and coin rain via pre-allocated pool.
        /// </summary>
        public void PlayChestBurst(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 16; i++)
            {
                GameObject coinSpark = GetPooledSpark();
                if (coinSpark == null) continue;
                coinSpark.name = "ChestCoinSpark";
                coinSpark.transform.position = position + Random.insideUnitSphere * 0.3f;
                float s = Random.Range(0.12f, 0.25f);
                coinSpark.transform.localScale = new Vector3(s, s, s);
                coinSpark.SetActive(true);

                Vector3 blastVel = (Random.onUnitSphere + Vector3.up * 1.5f).normalized * Random.Range(4.0f, 9.0f);
                var deb = coinSpark.GetComponent<PhysicalDebris>();
                if (deb == null) deb = coinSpark.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(blastVel, Random.insideUnitSphere * 300f, 0.8f);
            }
        }

        /// <summary>
        /// Near-miss visual effect: Subtle screen flash and particle burst when narrowly avoiding an obstacle.
        /// </summary>
        public void PlayNearMissEffect(Vector3 position)
        {
            EnsureMaterials();

            // Brief white flash particles
            for (int i = 0; i < 4; i++)
            {
                GameObject flash = GetPooledSpark();
                if (flash == null) continue;
                flash.name = "NearMissFlash";
                flash.transform.position = position + Random.insideUnitSphere * 0.5f;
                float s = Random.Range(0.08f, 0.15f);
                flash.transform.localScale = Vector3.one * s;
                flash.SetActive(true);

                Vector3 drift = (Vector3.up * 3f + Random.insideUnitSphere * 0.5f);
                var deb = flash.GetComponent<PhysicalDebris>();
                if (deb == null) deb = flash.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(drift, Vector3.zero, 0.3f, expand: false, shrink: true);
            }

            // Audio
            if (audioSource != null)
            {
                Runner.Audio.AudioManager.Instance?.PlayNearMiss();
            }
        }

        /// <summary>
        /// Speed boost activation effect: Forward-rushing particle streaks.
        /// </summary>
        public void PlaySpeedBoostEffect(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 12; i++)
            {
                GameObject streak = GetPooledDebris();
                if (streak == null) continue;
                streak.name = "SpeedStreak";
                streak.transform.position = position + Random.insideUnitSphere * 0.3f + Vector3.forward * 2f;
                streak.SetActive(true);
                float s = Random.Range(0.05f, 0.12f);
                streak.transform.localScale = new Vector3(s * 0.3f, s * 0.3f, s * 3f);
                streak.GetComponent<MeshRenderer>().sharedMaterial = shieldShardMat;

                Vector3 streakVel = (Vector3.forward * 8f + Vector3.up * 0.5f) * Random.Range(1f, 2f);
                var deb = streak.GetComponent<PhysicalDebris>();
                if (deb == null) deb = streak.AddComponent<PhysicalDebris>();
                deb.SetRecyclable(true);
                deb.Initialize(streakVel, Vector3.zero, 0.4f);
            }

            // Camera effect
            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.15f, 0.1f);
            }

            // Audio
            Runner.Audio.AudioManager.Instance?.PlaySpeedBoost();
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
