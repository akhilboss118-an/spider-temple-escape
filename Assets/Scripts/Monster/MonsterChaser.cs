using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Monster
{
    /// <summary>
    /// Controls the pursuing monster.
    /// Dynamically interpolates chase distance:
    /// - Normal distance: 6.0m behind player
    /// - 1st Stumble: rushes in to 3.8m, stays aggressive during 5.0s stumble timer
    /// - Clean recovery: retreats smoothly back to 6.0m
    /// - 2nd Stumble: closes to 0.0m for catch/kill attack sequence
    /// Includes procedural placeholder animations (aggressive gallop, arm flailing, catch lunge).
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

        private float currentFollowDistance = 4.0f;
        private float targetFollowDistance = 4.0f;
        private float gallopTimer = 0.0f;
        private float stompTimer = 0.0f;
        private bool isCatching = false;

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
                zombieMat = MaterialHelper.CreateSafeMaterial(Color.white, zombieTex);
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

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled += HandlePlayerStumbled;
                GameManager.Instance.OnStumbleRecovered += HandleStumbleRecovered;
                GameManager.Instance.OnGameOver += HandleGameOver;
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

            // 1. Determine Target Chase Distance
            if (isCatching)
            {
                targetFollowDistance = 0.0f;
            }
            else if (GameManager.Instance != null && GameManager.Instance.IsStumbling)
            {
                targetFollowDistance = stumbleDistance;
            }
            else
            {
                targetFollowDistance = normalDistance;
            }

            // Smoothly interpolate current distance
            currentFollowDistance = Mathf.Lerp(currentFollowDistance, targetFollowDistance, dt * chaseInterpolationSpeed);

            // 2. Position Monster Behind Player along Player's Forward Axis
            Vector3 playerPos = PlayerController.Instance.transform.position;
            Vector3 playerForward = PlayerController.Instance.ForwardDirection;
            Quaternion playerRot = PlayerController.Instance.transform.rotation;

            Vector3 targetPosition = playerPos - (playerForward * currentFollowDistance);
            targetPosition.y = playerPos.y; // Match ground level

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
            }

            // 3. Procedural Gallop & Attack Animations
            UpdateProceduralAnimations(dt);
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
