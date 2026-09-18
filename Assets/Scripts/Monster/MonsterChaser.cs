using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Monster
{
    /// <summary>
    /// Controls the pursuing monster with progressive phases:
    /// Phase 1 (0-1000m): Normal beast, steady chase
    /// Phase 2 (1000-3000m): Enraged, faster lunges, occasional lunges even without stumble
    /// Phase 3 (3000m+): Demonic, glowing eyes, fire trail, very aggressive
    /// Dynamically interpolates chase distance and visual evolution.
    /// </summary>
    public class MonsterChaser : MonoBehaviour
    {
        public static MonsterChaser Instance { get; private set; }

        [Header("Chase Distance Parameters")]
        [Tooltip("Normal following distance in meters behind player")]
        [SerializeField] private float normalDistance = 4.0f;

        [Tooltip("Close-up menace distance when player stumbles")]
        [SerializeField] private float stumbleDistance = 1.8f;

        [Tooltip("Speed of monster position adjustment")]
        [SerializeField] private float chaseInterpolationSpeed = 4.0f;

        [Header("Procedural Visuals & Audio")]
        [SerializeField] private Transform monsterBody;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip screamClip;

        [Header("Phase Evolution")]
        [SerializeField] private Light monsterEyeGlow;
        [SerializeField] private ParticleSystem fireTrailParticles;

        private float currentFollowDistance = 4.0f;
        private float targetFollowDistance = 4.0f;
        private float gallopTimer = 0.0f;
        private float stompTimer = 0.0f;
        private bool isCatching = false;

        private int currentPhase = 1;
        private float lungeCooldown = 0f;
        private Color monsterBaseColor = new Color(0.3f, 0.45f, 0.3f);
        private Material monsterMaterial;

        private GameObject magmaGolemVisual;
        private Material magmaGolemMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            currentFollowDistance = normalDistance;
            targetFollowDistance = normalDistance;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            EnsureAudio();
        }

        private void EnsureZombieMaterial()
        {
            Material zombieMat = Resources.Load<Material>("Materials/Mat_Monster");
#if UNITY_EDITOR
            if (zombieMat == null)
                zombieMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Monster.mat");
#endif
            Texture2D zombieTex = Resources.Load<Texture2D>("Textures/Zombie_2_color");
#if UNITY_EDITOR
            if (zombieTex == null)
                zombieTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Zombie/textures/Zombie_2_color.jpeg");
#endif
            if (zombieMat == null && zombieTex != null)
            {
                zombieMat = Runner.Core.MaterialHelper.CreateSafeMaterial(Color.white, zombieTex);
            }

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                bool needsMaterial = (r.sharedMaterial == null || r.sharedMaterial.mainTexture == null);
                if (needsMaterial && zombieMat != null)
                {
                    r.sharedMaterial = zombieMat;
                }
            }

            monsterMaterial = zombieMat;
        }

        private void EnsureMagmaGolemMaterial(GameObject golemVisual)
        {
            if (magmaGolemMaterial == null)
            {
                Texture2D baseCol = Resources.Load<Texture2D>("Monster/VolcanoGolem/textures/Magmamonster_basecolor");
                Texture2D norm = Resources.Load<Texture2D>("Monster/VolcanoGolem/textures/Magmamonster_normal");
                Texture2D metal = Resources.Load<Texture2D>("Monster/VolcanoGolem/textures/Magmamonster_metallic");
                Texture2D rough = Resources.Load<Texture2D>("Monster/VolcanoGolem/textures/Magmamonster_roughness");

#if UNITY_EDITOR
                if (baseCol == null)
                    baseCol = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_basecolor.jpeg");
                if (norm == null)
                    norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_normal.jpeg");
                if (metal == null)
                    metal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_metallic.jpeg");
                if (rough == null)
                    rough = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Monster/VolcanoGolem/textures/Magmamonster_roughness.jpeg");
#endif

                magmaGolemMaterial = Runner.Core.MaterialHelper.CreatePBRMaterial(
                    new Color(0.90f, 0.70f, 0.50f),
                    albedo: baseCol,
                    normal: norm,
                    metallic: metal,
                    roughness: rough,
                    metallicValue: 0.35f,
                    smoothness: 0.65f,
                    emissionColor: new Color(1.0f, 0.35f, 0.05f) * 1.6f
                );
                if (magmaGolemMaterial != null)
                {
                    magmaGolemMaterial.name = "Mat_MagmaGolem";
                }
            }

            if (golemVisual != null && magmaGolemMaterial != null)
            {
                foreach (var r in golemVisual.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = magmaGolemMaterial;
                }
            }
        }

        public void ApplyBiomeMonster(Runner.Effects.BiomeType biome)
        {
            if (biome == Runner.Effects.BiomeType.VolcanicCaverns)
            {
                if (magmaGolemVisual == null)
                {
                    GameObject golemPrefab = Resources.Load<GameObject>("Monster/VolcanoGolem/source/Magma+monster");
#if UNITY_EDITOR
                    if (golemPrefab == null)
                        golemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Monster/VolcanoGolem/source/Magma+monster.fbx");
#endif
                    if (golemPrefab != null)
                    {
                        magmaGolemVisual = Instantiate(golemPrefab, transform);
                        magmaGolemVisual.name = "MagmaGolem_Model";
                        magmaGolemVisual.transform.localPosition = Vector3.zero;
                        magmaGolemVisual.transform.localRotation = Quaternion.identity;

                        foreach (var c in magmaGolemVisual.GetComponentsInChildren<Collider>())
                            Destroy(c);

                        Renderer[] rList = magmaGolemVisual.GetComponentsInChildren<Renderer>();
                        if (rList.Length > 0)
                        {
                            Bounds b = rList[0].bounds;
                            for (int i = 1; i < rList.Length; i++) b.Encapsulate(rList[i].bounds);
                            if (b.size.y > 0.1f)
                            {
                                float targetH = 2.4f;
                                float s = targetH / b.size.y;
                                magmaGolemVisual.transform.localScale = Vector3.one * s;
                            }
                        }

                        EnsureMagmaGolemMaterial(magmaGolemVisual);
                    }
                }

                if (magmaGolemVisual != null) magmaGolemVisual.SetActive(true);
                if (monsterBody != null) monsterBody.gameObject.SetActive(false);

                if (monsterEyeGlow != null)
                {
                    monsterEyeGlow.color = new Color(1.0f, 0.40f, 0.05f);
                    monsterEyeGlow.intensity = 2.2f;
                    monsterEyeGlow.range = 4.5f;
                }

                if (fireTrailParticles != null && !fireTrailParticles.isPlaying)
                {
                    fireTrailParticles.Play();
                }
            }
            else if (biome == Runner.Effects.BiomeType.FrostbiteCitadel)
            {
                if (magmaGolemVisual != null) magmaGolemVisual.SetActive(false);
                if (monsterBody != null) monsterBody.gameObject.SetActive(true);

                if (monsterEyeGlow != null)
                {
                    monsterEyeGlow.color = new Color(0.25f, 0.85f, 1.0f);
                    monsterEyeGlow.intensity = 1.8f;
                    monsterEyeGlow.range = 3.5f;
                }

                if (fireTrailParticles != null && fireTrailParticles.isPlaying)
                {
                    fireTrailParticles.Stop();
                }

                if (monsterMaterial != null)
                {
                    monsterMaterial.color = new Color(0.70f, 0.88f, 1.0f);
                }
            }
            else
            {
                // Jungle Canopy / Sunken Temple (Classic Beast)
                if (magmaGolemVisual != null) magmaGolemVisual.SetActive(false);
                if (monsterBody != null) monsterBody.gameObject.SetActive(true);

                if (monsterEyeGlow != null)
                {
                    monsterEyeGlow.color = new Color(0.2f, 0.8f, 0.2f);
                    monsterEyeGlow.intensity = 1.0f;
                    monsterEyeGlow.range = 3.0f;
                }

                if (fireTrailParticles != null && fireTrailParticles.isPlaying)
                {
                    fireTrailParticles.Stop();
                }

                if (monsterMaterial != null)
                {
                    monsterMaterial.color = Color.white;
                }
            }
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.0f;
            }

            if (screamClip == null)
            {
                screamClip = Resources.Load<AudioClip>("Audio/ZombieScream");
                #if UNITY_EDITOR
                if (screamClip == null)
                {
                    screamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ZombieScream.wav");
                }
                #endif
            }
        }

        public void PlayScreamAudio()
        {
            EnsureAudio();
            if (audioSource != null && screamClip != null)
            {
                audioSource.Stop();
                audioSource.volume = 1.0f;
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(screamClip, 1.0f);
            }
            if (animator != null)
            {
                animator.ResetTrigger("Scream");
                animator.SetTrigger("Scream");
            }
        }

        private void Start()
        {
            EnsureZombieMaterial();
            monsterBaseColor = new Color(0.3f, 0.45f, 0.3f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled += HandlePlayerStumbled;
                GameManager.Instance.OnStumbleRecovered += HandleStumbleRecovered;
                GameManager.Instance.OnGameOver += HandleGameOver;
            }

            if (monsterEyeGlow == null)
            {
                GameObject eyeObj = new GameObject("MonsterEyeGlow");
                eyeObj.transform.SetParent(transform, false);
                eyeObj.transform.localPosition = new Vector3(0, 1.8f, 0.3f);
                monsterEyeGlow = eyeObj.AddComponent<Light>();
                monsterEyeGlow.type = LightType.Point;
                monsterEyeGlow.color = new Color(0.2f, 0.8f, 0.2f);
                monsterEyeGlow.range = 3.0f;
                monsterEyeGlow.intensity = 0f;
                monsterEyeGlow.shadows = LightShadows.None;
            }

            if (fireTrailParticles == null)
            {
                GameObject trailObj = new GameObject("FireTrail");
                trailObj.transform.SetParent(transform, false);
                trailObj.transform.localPosition = new Vector3(0, 0.5f, -1.5f);
                fireTrailParticles = trailObj.AddComponent<ParticleSystem>();
                var main = fireTrailParticles.main;
                main.loop = true;
                main.startLifetime = 0.6f;
                main.startSpeed = 2.0f;
                main.startSize = 0.4f;
                main.startColor = new Color(1.0f, 0.4f, 0.05f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = fireTrailParticles.emission;
                emission.rateOverTime = 25f;
                var shape = fireTrailParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15f;
                shape.radius = 0.2f;
                var colors = fireTrailParticles.colorOverLifetime;
                colors.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.6f, 0.1f), 0f),
                    new GradientColorKey(new Color(1.0f, 0.2f, 0.0f), 1f)
                }, new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
                colors.color = grad;
                fireTrailParticles.Stop();
            }

            if (Runner.Effects.BiomeManager.Instance != null)
            {
                ApplyBiomeMonster(Runner.Effects.BiomeManager.Instance.CurrentBiome);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled -= HandlePlayerStumbled;
                GameManager.Instance.OnStumbleRecovered -= HandleStumbleRecovered;
                GameManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        private void Update()
        {
            if (PlayerController.Instance == null)
                return;

            float dt = Time.deltaTime;
            float dist = GameManager.Instance != null ? GameManager.Instance.DistanceTraveled : 0f;

            // Phase determination
            int newPhase = dist < 1000f ? 1 : dist < 3000f ? 2 : 3;
            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                OnPhaseChanged(currentPhase);
            }

            // 1. Determine Target Chase Distance (closer in later phases)
            float phaseNormalDist;
            float phaseStumbleDist;

            if (currentPhase == 1) { phaseNormalDist = 4.0f; phaseStumbleDist = 1.8f; }
            else if (currentPhase == 2) { phaseNormalDist = 3.2f; phaseStumbleDist = 1.2f; }
            else if (currentPhase == 3) { phaseNormalDist = 2.5f; phaseStumbleDist = 0.6f; }
            else { phaseNormalDist = 4.0f; phaseStumbleDist = 1.8f; }

            if (isCatching)
            {
                targetFollowDistance = 0.0f;
            }
            else if (GameManager.Instance != null && GameManager.Instance.IsStumbling)
            {
                targetFollowDistance = phaseStumbleDist;
            }
            else
            {
                targetFollowDistance = phaseNormalDist;
            }

            // Phase 2+: occasional random lunge
            if (currentPhase >= 2 && !isCatching && lungeCooldown <= 0f)
            {
                float lungeChance = currentPhase == 2 ? 0.003f : 0.008f;
                if (Random.value < lungeChance)
                {
                    targetFollowDistance = phaseStumbleDist * 0.7f;
                    lungeCooldown = currentPhase == 2 ? 4f : 2f;
                }
            }
            lungeCooldown -= dt;

            // Smoothly interpolate current distance
            currentFollowDistance = Mathf.Lerp(currentFollowDistance, targetFollowDistance, dt * chaseInterpolationSpeed);

            // 2. Position Monster Behind Player along Player's Forward Axis
            Vector3 playerPos = PlayerController.Instance.transform.position;
            Vector3 playerForward = PlayerController.Instance.ForwardDirection;
            Quaternion playerRot = PlayerController.Instance.transform.rotation;

            Vector3 targetPosition = playerPos - (playerForward * currentFollowDistance);
            targetPosition.y = playerPos.y;

            transform.position = Vector3.Lerp(transform.position, targetPosition, dt * 12.0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, playerRot, dt * 15.0f);

            // Update Animator if present
            if (animator != null)
            {
                float speed = GameManager.Instance != null ? GameManager.Instance.CurrentSpeed : 8.0f;
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsChasing", true);
            }

            // Proximity Beast Stomp Audio
            if (currentFollowDistance < 5.0f && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                stompTimer += dt * (GameManager.Instance.CurrentSpeed / 7.0f);
                if (stompTimer >= 0.42f)
                {
                    stompTimer = 0f;
                    float vol = Mathf.InverseLerp(5.0f, 1.2f, currentFollowDistance);
                    Runner.Audio.AudioManager.Instance?.PlayBeastStomp(vol * 0.75f);
                }

                // Monster roar on close proximity
                Runner.Audio.AudioManager.Instance?.PlayMonsterRoar(currentFollowDistance);
            }

            // 3. Procedural Gallop & Attack Animations
            UpdateProceduralAnimations(dt);
        }

        private void OnPhaseChanged(int phase)
        {
            if (phase >= 2 && monsterMaterial != null)
            {
                Color phaseColor = phase == 2
                    ? new Color(0.45f, 0.3f, 0.25f)   // Enraged red-brown
                    : new Color(0.35f, 0.15f, 0.15f);  // Demonic dark red
                monsterMaterial.color = phaseColor;
            }

            if (phase >= 2 && monsterEyeGlow != null)
            {
                monsterEyeGlow.intensity = phase == 2 ? 1.5f : 3.0f;
                monsterEyeGlow.range = phase == 2 ? 4.0f : 6.0f;
                monsterEyeGlow.color = phase == 2
                    ? new Color(0.9f, 0.3f, 0.1f)
                    : new Color(1.0f, 0.1f, 0.0f);
            }

            if (phase == 3 && fireTrailParticles != null && !fireTrailParticles.isPlaying)
            {
                fireTrailParticles.Play();
            }

            if (phase >= 2)
            {
                chaseInterpolationSpeed = phase == 2 ? 5.5f : 7.0f;
            }
        }

        private void UpdateProceduralAnimations(float dt)
        {
            if (animator != null) return;

            if (isCatching)
            {
                // Lunge and swipe attack pose
                if (monsterBody != null)
                {
                    monsterBody.localRotation = Quaternion.Euler(25.0f, 0, 0);
                }
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(75.0f, 20.0f, 0);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(75.0f, -20.0f, 0);
                return;
            }

            // Gallop cycle scaled by speed
            float speed = GameManager.Instance != null ? GameManager.Instance.CurrentSpeed : 8.0f;
            gallopTimer += dt * speed * 2.0f;

            // Body bounce
            float bounceY = Mathf.Abs(Mathf.Sin(gallopTimer)) * 0.25f;
            if (monsterBody != null)
            {
                monsterBody.localPosition = new Vector3(0, 1.0f + bounceY, 0);
                monsterBody.localRotation = Quaternion.Euler(12.0f + Mathf.Sin(gallopTimer) * 5.0f, 0, 0);
            }

            // Arm flailing
            float armAngle = Mathf.Sin(gallopTimer) * 40.0f;
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Euler(armAngle, 0, 0);
            }
            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(-armAngle, 0, 0);
            }
        }

        private void HandlePlayerStumbled(int count, float decayRemaining)
        {
            if (count >= 2)
            {
                // Caught! Lunge and attack!
                isCatching = true;
                chaseInterpolationSpeed = 12.0f;
                targetFollowDistance = 0.0f;
                if (animator != null) animator.SetTrigger("Attack");
                PlayScreamAudio();
            }
            else
            {
                // Zombie surges in right behind the stumbling player and screams!
                chaseInterpolationSpeed = 8.0f;
                targetFollowDistance = stumbleDistance;
                if (animator != null)
                {
                    animator.ResetTrigger("Scream");
                    animator.SetTrigger("Scream");
                }
                PlayScreamAudio();
            }
        }

        private void HandleStumbleRecovered()
        {
            chaseInterpolationSpeed = 3.5f;
            targetFollowDistance = normalDistance;
        }

        private void HandleGameOver(DeathType deathType, int score)
        {
            if (deathType == DeathType.CaughtByMonster)
            {
                isCatching = true;
                chaseInterpolationSpeed = 12.0f;
                targetFollowDistance = 0.0f;
                if (animator != null) animator.SetTrigger("Attack");
                PlayScreamAudio();
            }
        }
    }
}
