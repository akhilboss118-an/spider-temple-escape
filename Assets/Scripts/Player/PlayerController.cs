using System;
using UnityEngine;
using Runner.Core;
using Runner.Effects;
using Runner.Obstacles;
using Runner.Track;

namespace Runner.Player
{
    public enum PlayerState
    {
        Running,
        Jumping,
        Sliding,
        Stumbling,
        Dead
    }

    /// <summary>
    /// Core Player Controller: Handles 3-lane movement, ballistic jump arc,
    /// dynamic capsule collider resizing for slides, 90-degree junction turns,
    /// procedural squash-and-stretch placeholder animations, and obstacle interactions.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(InputClassifier))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Lane Setup")]
        [Tooltip("Horizontal distance between lane centers in meters")]
        [SerializeField] private float laneDistance = 2.0f;

        [Tooltip("Speed of lane transitions")]
        [SerializeField] private float laneChangeSpeed = 15.0f;

        [Tooltip("Subtle horizontal lean offset from accelerometer tilt")]
        [SerializeField] private float tiltLeanMaxOffset = 0.45f;

        [Header("Jump Ballistics")]
        [Tooltip("Peak jump height in meters")]
        [SerializeField] private float jumpHeight = 2.2f;

        [Tooltip("Total jump duration in seconds")]
        [SerializeField] private float jumpDuration = 0.75f;

        [Header("Slide Parameters")]
        [Tooltip("Duration of slide crouch in seconds")]
        [SerializeField] private float slideDuration = 0.85f;

        [Header("Visual Mesh & Character Binding")]
        [Tooltip("Child transform containing visual model")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("Renderer of visual mesh for stumble color flashing")]
        [SerializeField] private MeshRenderer visualRenderer;

        // Lane tracking (-1 = Left, 0 = Center, +1 = Right)
        public int CurrentLane { get; private set; } = 0;
        private float currentLaneOffset = 0.0f;

        // State Machine
        public PlayerState State { get; private set; } = PlayerState.Running;

        // Components
        private CharacterController characterController;
        private InputClassifier inputClassifier;
        [SerializeField] private Animator animator;

        // Forward and Turn Tracking
        public Vector3 ForwardDirection { get; private set; } = Vector3.forward;
        public Vector3 RightDirection { get; private set; } = Vector3.right;
        private Quaternion targetRotation = Quaternion.identity;
        private float rotationSlerpSpeed = 18.0f;

        // Jump Physics Variables
        private float jumpTimer = 0.0f;
        private float jumpInitialVelocity;
        private float gravity;
        private float verticalVelocity = 0.0f;

        // Slide Variables
        private float slideTimer = 0.0f;
        private float originalColliderHeight = 2.0f;
        private Vector3 originalColliderCenter = new Vector3(0, 1.0f, 0);
        private float slideColliderHeight = 0.9f;
        private Vector3 slideColliderCenter = new Vector3(0, 0.45f, 0);

        // Stumble Visual Feedback
        private float stumbleVisualTimer = 0.0f;
        private Color originalVisualColor = Color.cyan;

        // Junction Turn Interaction
        public JunctionTrigger ActiveJunction { get; set; }
        private Vector3 corridorAnchor = Vector3.zero;
        private Vector3 corridorForward = Vector3.forward;
        private Vector3 corridorRight = Vector3.right;
        private float queuedTurnAngle = 0.0f;
        private float queuedTurnTimer = 0.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            characterController = GetComponent<CharacterController>();
            inputClassifier = GetComponent<InputClassifier>();

            originalColliderHeight = characterController.height;
            originalColliderCenter = characterController.center;

            // Calculate ballistic jump kinematics:
            // peak at t = duration/2: jumpHeight = v0 * (t_half) - 0.5 * g * (t_half)^2
            // v0 = g * t_half => jumpHeight = 0.5 * g * (t_half)^2 => g = 2*jumpHeight / (t_half^2)
            float tHalf = jumpDuration * 0.5f;
            gravity = (2.0f * jumpHeight) / (tHalf * tHalf);
            jumpInitialVelocity = gravity * tHalf;

            if (visualRenderer != null && visualRenderer.material != null)
            {
                originalVisualColor = visualRenderer.material.color;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            targetRotation = transform.rotation;
            corridorForward = transform.forward;
            corridorRight = transform.right;
            ForwardDirection = corridorForward;
            RightDirection = corridorRight;
            corridorAnchor = transform.position;

            EnsureSpiderManSuitMaterial();
        }

        private void Start()
        {
            if (Runner.Characters.CharacterManager.Instance != null)
            {
                Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(gameObject);
            }
        }

        private void EnsureSpiderManSuitMaterial()
        {
            Material spiderMat = Resources.Load<Material>("Materials/Mat_Player");
#if UNITY_EDITOR
            if (spiderMat == null)
                spiderMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player.mat");
#endif
            Texture2D spiderTex = Resources.Load<Texture2D>("Textures/Tex_SpiderMan");
#if UNITY_EDITOR
            if (spiderTex == null)
                spiderTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/SpiderMan/textures/M-CoC_iOS_HERO_Peter_Parker_Spider-Man_Stark_Enhanced_Body_D.png");
#endif
            if (spiderMat == null && spiderTex != null)
            {
                spiderMat = MaterialHelper.CreateSafeMaterial(Color.white, spiderTex);
            }

            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                bool needsMaterial = (r.sharedMaterial == null || r.sharedMaterial.mainTexture == null);
                if (needsMaterial && spiderMat != null)
                {
                    r.sharedMaterial = spiderMat;
                }
            }
        }

        private void OnEnable()
        {
            if (inputClassifier == null)
            {
                inputClassifier = GetComponent<InputClassifier>();
            }

            if (inputClassifier != null)
            {
                inputClassifier.OnSwipeUp -= HandleSwipeUp;
                inputClassifier.OnSwipeDown -= HandleSwipeDown;
                inputClassifier.OnSwipeLeft -= HandleSwipeLeft;
                inputClassifier.OnSwipeRight -= HandleSwipeRight;

                inputClassifier.OnSwipeUp += HandleSwipeUp;
                inputClassifier.OnSwipeDown += HandleSwipeDown;
                inputClassifier.OnSwipeLeft += HandleSwipeLeft;
                inputClassifier.OnSwipeRight += HandleSwipeRight;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled -= HandleStumbleStarted;
                GameManager.Instance.OnStumbleRecovered -= HandleStumbleEnded;
                GameManager.Instance.OnGameOver -= HandleGameOver;

                GameManager.Instance.OnStumbled += HandleStumbleStarted;
                GameManager.Instance.OnStumbleRecovered += HandleStumbleEnded;
                GameManager.Instance.OnGameOver += HandleGameOver;
            }
        }

        private void OnDisable()
        {
            if (inputClassifier != null)
            {
                inputClassifier.OnSwipeUp -= HandleSwipeUp;
                inputClassifier.OnSwipeDown -= HandleSwipeDown;
                inputClassifier.OnSwipeLeft -= HandleSwipeLeft;
                inputClassifier.OnSwipeRight -= HandleSwipeRight;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled -= HandleStumbleStarted;
                GameManager.Instance.OnStumbleRecovered -= HandleStumbleEnded;
                GameManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        private void Update()
        {
            // ALWAYS update animator every frame — even before game starts — so Run anim plays on menu
            if (animator != null)
            {
                if (animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                }
                bool isPlaying = (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing);
                float forwardSpeed = isPlaying ? GameManager.Instance.CurrentSpeed : 1.0f;
                animator.SetFloat("Speed", forwardSpeed);
                animator.SetBool("IsGrounded", characterController.isGrounded);
            }

            // Keep visual model strictly centered without local offset drift
            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.zero;
                visualTransform.localRotation = Quaternion.identity;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;

            if (State == PlayerState.Dead)
                return;

            float dt = Time.deltaTime;

            // Direct guaranteed keyboard controls (guarantees instantaneous responsiveness)
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                HandleSwipeLeft();
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                HandleSwipeRight();
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
                HandleSwipeUp();
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                HandleSwipeDown();

            // 1. Process Queued Turn Input Buffer (0.25s responsiveness window)
            if (queuedTurnTimer > 0f)
            {
                queuedTurnTimer -= dt;
                if (ActiveJunction != null)
                {
                    if (queuedTurnAngle < 0f && ActiveJunction.CanTurnLeft())
                    {
                        queuedTurnTimer = 0f;
                        ExecuteTurn(queuedTurnAngle);
                    }
                    else if (queuedTurnAngle > 0f && ActiveJunction.CanTurnRight())
                    {
                        queuedTurnTimer = 0f;
                        ExecuteTurn(queuedTurnAngle);
                    }
                }
            }

            // 2. Smoothly Rotate toward current heading
            if (Quaternion.Angle(transform.rotation, targetRotation) > 0.05f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, dt * rotationSlerpSpeed);
            }
            else
            {
                transform.rotation = targetRotation;
            }
            ForwardDirection = corridorForward;
            RightDirection = corridorRight;

            // 3. Handle Jump Ballistics
            UpdateJump(dt);

            // 4. Handle Slide Timer & Collider
            UpdateSlide(dt);

            // 5. Handle Stumble Visuals
            UpdateStumble(dt);

            // 6. Calculate Movement Vector
            MoveCharacter(dt);

            // 7. Void Fall Check
            if (transform.position.y < -3.0f && State != PlayerState.Dead)
            {
                Die(DeathType.FallIntoVoid);
            }
        }

        private void LateUpdate()
        {
            // Keep visual model strictly centered without local offset drift after animation evaluation
            if (visualTransform != null && (State != PlayerState.Dead || animator != null))
            {
                visualTransform.localPosition = Vector3.zero;
                if (animator != null)
                {
                    visualTransform.localRotation = Quaternion.identity;
                }
            }
        }

        #region Movement & Kinematics
        private void MoveCharacter(float dt)
        {
            float forwardSpeed = GameManager.Instance.CurrentSpeed;

            // Forward displacement strictly along the corridor forward heading
            Vector3 forwardMove = corridorForward * (forwardSpeed * dt);

            // Lateral Lane Target Calculation (-1 = Left, 0 = Center, +1 = Right)
            CurrentLane = Mathf.Clamp(CurrentLane, -1, 1);
            float targetLateralOffset = CurrentLane * laneDistance;
            float tiltOffset = (inputClassifier != null && inputClassifier.CurrentTilt != 0f) 
                ? inputClassifier.CurrentTilt * tiltLeanMaxOffset : 0f;
            float desiredLateralOffset = targetLateralOffset + tiltOffset;

            // Measured lateral offset relative to fixed corridor centerline:
            // Projecting onto corridorRight eliminates any cross-product/lever-arm error over long running distances!
            Vector3 offsetFromCorridor = transform.position - corridorAnchor;
            float currentLateral = Vector3.Dot(offsetFromCorridor, corridorRight);

            // Smooth monotonic lane interpolation with a deadzone threshold to prevent micro-vibrations
            float lateralDiff = desiredLateralOffset - currentLateral;
            float lateralStep = 0f;
            if (Mathf.Abs(lateralDiff) > 0.0005f)
            {
                float nextLateral = Mathf.MoveTowards(currentLateral, desiredLateralOffset, laneChangeSpeed * dt);
                lateralStep = nextLateral - currentLateral;
            }

            currentLaneOffset = currentLateral + lateralStep;
            Vector3 lateralMove = corridorRight * lateralStep;

            // Vertical displacement
            Vector3 verticalMove = Vector3.up * (verticalVelocity * dt);

            // Total Move Vector
            Vector3 totalMovement = forwardMove + lateralMove + verticalMove;

            characterController.Move(totalMovement);
        }

        private void UpdateJump(float dt)
        {
            if (State == PlayerState.Jumping)
            {
                jumpTimer += dt;
                verticalVelocity -= gravity * dt;

                if (characterController.isGrounded && jumpTimer > 0.15f)
                {
                    // Landed — trigger AAA landing dust burst
                    verticalVelocity = -2.0f; // Small ground clamping force
                    State = PlayerState.Running;

                    // AAA: Landing dust effect at player feet
                    if (ImpactEffectManager.Instance != null)
                    {
                        ImpactEffectManager.Instance.PlayLandingDust(transform.position + Vector3.up * 0.1f);
                    }

                    Runner.Audio.AudioManager.Instance?.PlayLand();
                }
            }
            else
            {
                // Ground clamping
                if (characterController.isGrounded)
                {
                    verticalVelocity = -2.0f;
                }
                else
                {
                    // In freefall (e.g., ran off pit gap)
                    verticalVelocity -= gravity * dt;
                }
            }
        }

        private void UpdateSlide(float dt)
        {
            if (State == PlayerState.Sliding)
            {
                slideTimer -= dt;
                if (slideTimer <= 0.0f)
                {
                    // Revert collider to standard running height
                    characterController.height = originalColliderHeight;
                    characterController.center = originalColliderCenter;
                    State = PlayerState.Running;
                }
            }
        }
        #endregion

        #region Input Handling & Turns
        private int lastSwipeUpFrame = -1;
        private int lastSwipeDownFrame = -1;
        private int lastSwipeLeftFrame = -1;
        private int lastSwipeRightFrame = -1;

        private void HandleSwipeUp()
        {
            if (State == PlayerState.Dead) return;
            if (Time.frameCount == lastSwipeUpFrame) return;
            lastSwipeUpFrame = Time.frameCount;

            // Jump overrides slide immediately
            if (State == PlayerState.Sliding)
            {
                characterController.height = originalColliderHeight;
                characterController.center = originalColliderCenter;
            }

            if (characterController.isGrounded || State == PlayerState.Running || State == PlayerState.Sliding || State == PlayerState.Stumbling)
            {
                State = PlayerState.Jumping;
                jumpTimer = 0.0f;
                verticalVelocity = jumpInitialVelocity;

                if (animator != null)
                {
                    animator.SetTrigger("Jump");
                }
                Runner.Audio.AudioManager.Instance?.PlayJump();
                MissionManager.Instance?.ReportJump();
            }
        }

        private void HandleSwipeDown()
        {
            if (State == PlayerState.Dead) return;
            if (Time.frameCount == lastSwipeDownFrame) return;
            lastSwipeDownFrame = Time.frameCount;

            // Slide cancels jump immediately (fast-fall into slide)
            if (State == PlayerState.Jumping)
            {
                verticalVelocity = -jumpInitialVelocity * 1.5f;
            }

            State = PlayerState.Sliding;
            slideTimer = slideDuration;

            // Shrink collider to half height
            characterController.height = slideColliderHeight;
            characterController.center = slideColliderCenter;

            if (animator != null)
            {
                animator.SetTrigger("Slide");
            }
            Runner.Audio.AudioManager.Instance?.PlaySlide();
            MissionManager.Instance?.ReportSlide();
        }

        private void HandleSwipeLeft()
        {
            if (State == PlayerState.Dead) return;
            if (Time.frameCount == lastSwipeLeftFrame) return;
            lastSwipeLeftFrame = Time.frameCount;

            if (ActiveJunction != null && ActiveJunction.CanTurnLeft())
            {
                ExecuteTurn(-90.0f);
                return;
            }

            // Buffer turn command for 0.60 seconds in case entering a corner
            queuedTurnAngle = -90.0f;
            queuedTurnTimer = 0.60f;

            // Move to left lane (-1 <= CurrentLane <= 1)
            if (CurrentLane > -1)
            {
                CurrentLane--;
            }
        }

        private void HandleSwipeRight()
        {
            if (State == PlayerState.Dead) return;
            if (Time.frameCount == lastSwipeRightFrame) return;
            lastSwipeRightFrame = Time.frameCount;

            if (ActiveJunction != null && ActiveJunction.CanTurnRight())
            {
                ExecuteTurn(90.0f);
                return;
            }

            // Buffer turn command for 0.60 seconds in case entering a corner
            queuedTurnAngle = 90.0f;
            queuedTurnTimer = 0.60f;

            // Move to right lane (-1 <= CurrentLane <= 1)
            if (CurrentLane < 1)
            {
                CurrentLane++;
            }
        }

        public void ExecuteTurn(float angleDegrees)
        {
            queuedTurnTimer = 0f;

            if (ActiveJunction != null)
            {
                JunctionTrigger junc = ActiveJunction;

                // Align target and current rotation immediately to the 90-degree turn
                targetRotation = junc.transform.rotation * Quaternion.Euler(0, angleDegrees, 0);
                transform.rotation = targetRotation;
                ForwardDirection = transform.forward;
                RightDirection = transform.right;

                // Safely snap CharacterController position to junction center axis
                Vector3 snapPos = junc.GetSnapCenter();
                snapPos.y = junc.GetSnapCenter().y + 0.05f;
                verticalVelocity = -2.0f; // Firm ground clamping force

                // Cleanly reset player state to running
                State = PlayerState.Running;
                jumpTimer = 0.0f;
                slideTimer = 0.0f;
                characterController.height = originalColliderHeight;
                characterController.center = originalColliderCenter;

                if (characterController != null)
                {
                    characterController.enabled = false;
                    transform.position = snapPos;
                    characterController.enabled = true;
                }
                else
                {
                    transform.position = snapPos;
                }

                // Grid Snapping: set corridor anchor to junction snap point and clear lateral lane offsets
                corridorAnchor = snapPos;
                corridorForward = targetRotation * Vector3.forward;
                corridorRight = targetRotation * Vector3.right;
                ForwardDirection = corridorForward;
                RightDirection = corridorRight;
                currentLaneOffset = 0.0f;
                CurrentLane = 0;

                junc.OnPlayerTurned(angleDegrees);
            }
            else
            {
                targetRotation *= Quaternion.Euler(0, angleDegrees, 0);
                transform.rotation = targetRotation;
                corridorAnchor = transform.position;
                corridorForward = targetRotation * Vector3.forward;
                corridorRight = targetRotation * Vector3.right;
                ForwardDirection = corridorForward;
                RightDirection = corridorRight;
                currentLaneOffset = 0.0f;
                CurrentLane = 0;
            }
        }
        #endregion

        private AudioSource audioSource;
        private AudioClip stumbleSoundClip;

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

            if (stumbleSoundClip == null)
            {
                stumbleSoundClip = Resources.Load<AudioClip>("Audio/PlayerStumble");
                #if UNITY_EDITOR
                if (stumbleSoundClip == null)
                {
                    stumbleSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PlayerStumble.wav");
                }
                #endif
            }
        }

        public void TriggerStumble()
        {
            if (State == PlayerState.Dead) return;

            EnsureAudio();
            if (audioSource != null && stumbleSoundClip != null)
            {
                audioSource.PlayOneShot(stumbleSoundClip, 1.0f);
            }

            if (animator != null)
            {
                animator.ResetTrigger("Stumble");
                animator.SetTrigger("Stumble");
            }

            if (Monster.MonsterChaser.Instance != null)
            {
                Monster.MonsterChaser.Instance.PlayScreamAudio();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterStumble();
            }
        }

        private void UpdateStumble(float dt)
        {
            if (stumbleVisualTimer > 0.0f)
            {
                stumbleVisualTimer -= dt;

                // Only apply procedural rotation if no humanoid animator is present
                if (animator == null && visualTransform != null)
                {
                    visualTransform.localRotation = Quaternion.Euler(-15.0f, 0, 0);
                }

                if (stumbleVisualTimer <= 0.0f)
                {
                    if (animator == null && visualTransform != null)
                    {
                        visualTransform.localRotation = Quaternion.identity;
                    }
                    if (State == PlayerState.Stumbling)
                    {
                        State = PlayerState.Running;
                    }
                    if (visualRenderer != null)
                        visualRenderer.material.color = originalVisualColor;
                }
            }
        }

        #region Stumble & Death Callbacks
        private void HandleStumbleStarted(int count, float decayTime)
        {
            if (State == PlayerState.Dead) return;

            // Reset slide if stumbling
            if (State == PlayerState.Sliding)
            {
                characterController.height = originalColliderHeight;
                characterController.center = originalColliderCenter;
            }

            State = PlayerState.Stumbling;
            stumbleVisualTimer = 1.3f;

            if (animator != null)
            {
                animator.ResetTrigger("Stumble");
                animator.SetTrigger("Stumble");
            }

            if (Monster.MonsterChaser.Instance != null)
            {
                Monster.MonsterChaser.Instance.PlayScreamAudio();
            }
        }

        private void HandleStumbleEnded()
        {
            if (State == PlayerState.Stumbling)
            {
                State = PlayerState.Running;
            }
        }

        public void Die(DeathType deathType)
        {
            if (State == PlayerState.Dead) return;

            State = PlayerState.Dead;
            characterController.enabled = false;

            // Natural grounded collision fall (no cartoonish flying dash backward)
            if (animator != null)
            {
                animator.ResetTrigger("DieBackwards");
                animator.SetTrigger("DieBackwards");
            }
            else if (visualTransform != null)
            {
                // Topple naturally onto track
                visualTransform.localRotation = Quaternion.Euler(-85.0f, 0, 0);
                visualTransform.localPosition = new Vector3(0, 0.2f, -0.4f);
            }

            GameManager.Instance.TriggerGameOver(deathType);
        }

        private void HandleGameOver(DeathType deathType, int finalScore)
        {
            if (State != PlayerState.Dead)
            {
                Die(deathType);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (State == PlayerState.Dead) return;

            var obstacle = hit.collider.GetComponent<Obstacle>();
            if (obstacle == null)
                obstacle = hit.collider.GetComponentInParent<Obstacle>();

            if (obstacle != null)
            {
                obstacle.EvaluateCollision(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (State == PlayerState.Dead) return;

            var obstacle = other.GetComponent<Obstacle>();
            if (obstacle == null)
                obstacle = other.GetComponentInParent<Obstacle>();

            if (obstacle != null)
            {
                obstacle.EvaluateCollision(this);
            }
        }
        #endregion

        #region Hero Suits
        private TrailRenderer suitTrail;

        /// <summary>
        /// Applies suit visual styling and trail effects for 4 unique suits:
        /// 0: Classic Red & Blue
        /// 1: Symbiote / Shadow Stealth (Violet aura & trail)
        /// 2: Iron Spider / Gilded Armor (Gold aura & trail)
        /// 3: Cyber Spider 2099 (Neon cyan aura & trail)
        /// </summary>
        /// <summary>
        /// Equips the active character. For Spider-Man, preserves original authentic materials.
        /// When custom character models are uploaded, delegates instantiation to CharacterManager.
        /// </summary>
        public void ApplySuit(int characterIndex)
        {
            // Apply custom 3D character prefab if assigned in CharacterManager
            if (Runner.Characters.CharacterManager.Instance != null)
            {
                Runner.Characters.CharacterManager.Instance.ApplyCharacterModelToPlayer(gameObject);
            }

            // Restore pure authentic materials for Spider-Man ONLY (do not modify custom 3D models!)
            bool isSpiderMan = (Runner.Characters.CharacterManager.Instance == null || 
                                Runner.Characters.CharacterManager.Instance.SelectedCharacterIndex == 0);

            if (isSpiderMan)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null || r.gameObject.name.Contains("Oval") || r.gameObject.name.Contains("Shield") || r.gameObject.name.Contains("Custom")) continue;
                    foreach (var mat in r.materials)
                    {
                        if (mat == null) continue;
                        mat.color = Color.white;
                        mat.DisableKeyword("_EMISSION");
                    }
                }
            }

            if (suitTrail != null)
            {
                suitTrail.emitting = false;
                suitTrail.enabled = false;
            }
        }

        /// <summary>
        /// Dynamically re-binds active visual model and animator when custom characters are equipped.
        /// </summary>
        public void SetActiveVisualModel(Transform newVisualTransform, Animator newAnimator)
        {
            this.visualTransform = newVisualTransform;
            this.animator = newAnimator;
            if (newAnimator != null)
            {
                newAnimator.applyRootMotion = false;
            }
            if (newVisualTransform != null)
            {
                newVisualTransform.localPosition = Vector3.zero;
                newVisualTransform.localRotation = Quaternion.identity;
                this.visualRenderer = newVisualTransform.GetComponentInChildren<MeshRenderer>();
            }
        }

        /// <summary>
        /// Primes the animator Speed and IsGrounded params right after equip
        /// so the Run state starts immediately without waiting for first Update.
        /// </summary>
        public void SyncAnimatorSpeed(Animator anim)
        {
            if (anim == null) return;
            anim.applyRootMotion = false;
            float speed = (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
                ? GameManager.Instance.CurrentSpeed
                : 1.0f;
            anim.SetFloat("Speed", speed);
            anim.SetBool("IsGrounded", true);
            // Ensure the Run state is actively playing
            anim.Play("Run", 0, 0f);
        }
        #endregion
    }
}

