using System.Collections.Generic;
using UnityEngine;
using Runner.CameraControl;

namespace Runner.Effects
{
    /// <summary>
    /// Manages natural, realistic physical collision effects:
    /// - Stone dust puffs and tumbling rock chip debris on impacts
    /// - Crystalline shield shatter bursts
    /// - Speedrun obstacle demolition shockwaves
    /// - Pink heart collection sparkles
    /// Completely replaces cartoonish red/orange dashes with natural physical feedback.
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

        private Material rockDebrisMat;
        private Material dustPuffMat;
        private Material shieldShardMat;
        private Material heartSparkleMat;

        private AudioSource audioSource;
        private AudioClip rockHitClip;
        private AudioClip shieldBreakClip;
        private AudioClip heartChimeClip;

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
                dustPuffMat = Runner.Core.MaterialHelper.CreateSafeMaterial(new Color(0.68f, 0.64f, 0.58f));
                if (dustPuffMat != null) dustPuffMat.name = "DustPuffMat";
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
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.0f; // 2D clean mobile audio
            }

            // Synthesize subtle procedural clips if not loaded from assets
            if (rockHitClip == null) rockHitClip = CreateSyntheticThud();
            if (shieldBreakClip == null) shieldBreakClip = CreateSyntheticShatter();
            if (heartChimeClip == null) heartChimeClip = CreateSyntheticChime();
        }

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
                GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.name = "RockFragment";
                frag.transform.position = hitPoint + Random.insideUnitSphere * 0.25f;
                float s = Random.Range(0.08f, 0.22f);
                frag.transform.localScale = new Vector3(s, s * Random.Range(0.7f, 1.3f), s);
                frag.GetComponent<MeshRenderer>().sharedMaterial = rockDebrisMat;
                Destroy(frag.GetComponent<Collider>());

                Vector3 ejectVelocity = (hitNormal + Random.insideUnitSphere * 0.8f + Vector3.up * 1.2f).normalized * Random.Range(3.5f, 6.5f);
                Vector3 tumbleTorque = Random.insideUnitSphere * 360f;
                var deb = frag.AddComponent<PhysicalDebris>();
                deb.Initialize(ejectVelocity, tumbleTorque, 0.8f);
            }

            // 2. Spawn expanding soft dust puff
            for (int d = 0; d < 3; d++)
            {
                GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dust.name = "DustPuff";
                dust.transform.position = hitPoint + Vector3.up * 0.2f + Random.insideUnitSphere * 0.15f;
                dust.transform.localScale = Vector3.one * Random.Range(0.35f, 0.55f);
                dust.GetComponent<MeshRenderer>().sharedMaterial = dustPuffMat;
                Destroy(dust.GetComponent<Collider>());

                Vector3 dustDrift = (Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f);
                var deb = dust.AddComponent<PhysicalDebris>();
                deb.Initialize(dustDrift, Vector3.zero, 0.55f, expand: true);
            }

            // 3. Camera physical jolt
            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.28f, 0.25f);
            }

            // 4. Heavy stone thud audio
            if (audioSource != null && rockHitClip != null)
            {
                audioSource.pitch = Random.Range(0.90f, 1.10f);
                audioSource.PlayOneShot(rockHitClip, 0.85f);
            }
        }

        /// <summary>
        /// Shield absorption: Shatters crystalline shield aura into cyan shards without harming player.
        /// </summary>
        public void PlayShieldBreak(Vector3 position)
        {
            EnsureMaterials();

            int shardCount = 12;
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "ShieldShard";
                shard.transform.position = position + Random.insideUnitSphere * 0.4f;
                float s = Random.Range(0.06f, 0.16f);
                shard.transform.localScale = new Vector3(s, s * 2.0f, s * 0.5f);
                shard.GetComponent<MeshRenderer>().sharedMaterial = shieldShardMat;
                Destroy(shard.GetComponent<Collider>());

                Vector3 burstDir = Random.onUnitSphere * Random.Range(4.0f, 8.0f);
                var deb = shard.AddComponent<PhysicalDebris>();
                deb.Initialize(burstDir, Random.insideUnitSphere * 400f, 0.65f);
            }

            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.18f, 0.20f);
            }

            if (audioSource != null && shieldBreakClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(shieldBreakClip, 0.90f);
            }
        }

        /// <summary>
        /// Speedrun demolition: Obliterates rock with massive debris explosion.
        /// </summary>
        public void PlaySpeedrunSmash(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 16; i++)
            {
                GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.transform.position = position + Random.insideUnitSphere * 0.5f;
                float s = Random.Range(0.12f, 0.32f);
                frag.transform.localScale = Vector3.one * s;
                frag.GetComponent<MeshRenderer>().sharedMaterial = rockDebrisMat;
                Destroy(frag.GetComponent<Collider>());

                Vector3 blastVel = (Random.onUnitSphere + Vector3.up * 0.8f + Vector3.forward * 1.5f).normalized * Random.Range(6.0f, 11.0f);
                var deb = frag.AddComponent<PhysicalDebris>();
                deb.Initialize(blastVel, Random.insideUnitSphere * 500f, 0.9f);
            }

            if (RunnerCameraController.Instance != null)
            {
                RunnerCameraController.Instance.TriggerPhysicalRecoil(0.35f, 0.30f);
            }

            if (audioSource != null && rockHitClip != null)
            {
                audioSource.pitch = 0.80f; // Deep explosion rumble
                audioSource.PlayOneShot(rockHitClip, 1.0f);
            }
        }

        /// <summary>
        /// Pink heart collect: Emits subtle floating pink sparkles.
        /// </summary>
        public void PlayHeartCollect(Vector3 position)
        {
            EnsureMaterials();

            for (int i = 0; i < 5; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.transform.position = position + Random.insideUnitSphere * 0.2f;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.14f);
                spark.GetComponent<MeshRenderer>().sharedMaterial = heartSparkleMat;
                Destroy(spark.GetComponent<Collider>());

                Vector3 upDrift = (Vector3.up * 2.2f + Random.insideUnitSphere * 0.6f);
                var deb = spark.AddComponent<PhysicalDebris>();
                deb.Initialize(upDrift, Vector3.zero, 0.45f, expand: false, shrink: true);
            }

            if (audioSource != null && heartChimeClip != null)
            {
                audioSource.pitch = Random.Range(0.98f, 1.05f);
                audioSource.PlayOneShot(heartChimeClip, 0.45f);
            }
        }
        #endregion

        #region Synthetic Audio Generators (Guaranteed in Memory)
        private static AudioClip CreateSyntheticThud()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.25f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(120f, 40f, t);
                float env = Mathf.Exp(-t * 14f);
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
            int samples = (int)(sampleRate * 0.30f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 10f);
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
            int samples = (int)(sampleRate * 0.20f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 8f);
                float chime = Mathf.Sin(2f * Mathf.PI * 1318.5f * (i / (float)sampleRate)) * 0.7f + // E6
                              Mathf.Sin(2f * Mathf.PI * 1760.0f * (i / (float)sampleRate)) * 0.4f;  // A6
                data[i] = chime * env;
            }
            AudioClip clip = AudioClip.Create("ProceduralChime", samples, 1, sampleRate, false);
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

        public void Initialize(Vector3 initialVelocity, Vector3 angularTorque, float duration, bool expand = false, bool shrink = true)
        {
            velocity = initialVelocity;
            torque = angularTorque;
            lifetime = duration;
            this.expand = expand;
            this.shrink = shrink;
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = elapsed / lifetime;

            // Apply gravity and velocity
            velocity += Vector3.down * (14.0f * dt);
            transform.position += velocity * dt;

            // Apply tumble
            transform.Rotate(torque * dt, Space.Self);

            // Scale transition
            if (expand)
            {
                transform.localScale = Vector3.Lerp(initialScale, initialScale * 2.2f, t);
            }
            else if (shrink)
            {
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t * t);
            }
        }
    }
}
