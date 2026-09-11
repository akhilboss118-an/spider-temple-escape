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

        [Header("Visual Mesh & Procedural Squash/Stretch")]
        [Tooltip("Child transform containing visual capsule/body mesh")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("MeshRenderer of visual mesh for stumble color flashing")]
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

        // Run Bobbing
        private float bobTimer = 0.0f;

        // Junction Turn Interaction
        public JunctionTrigger ActiveJunction { get; set; }
        private Vector3 corridorAnchor = Vector3.zero;
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

            targetRotation = transform.rotation;
            ForwardDirection = transform.forward;
            RightDirection = transform.right;
            corridorAnchor = transform.position;

            EnsureSpiderManSuitMaterial();
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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, dt * rotationSlerpSpeed);
            ForwardDirection = transform.forward;
            RightDirection = transform.right;

            // Update Animator parameters if present
            if (animator != null)
            {
                float forwardSpeed = GameManager.Instance.CurrentSpeed;
                animator.SetFloat("Speed", forwardSpeed);
                animator.SetBool("IsGrounded", characterController.isGrounded);
            }

            // 3. Handle Jump Ballistics
            UpdateJump(dt);

            // 4. Handle Slide Timer & Collider
            UpdateSlide(dt);

            // 5. Handle Stumble Visuals
            UpdateStumble(dt);

            // 6. Calculate Movement Vector
            MoveCharacter(dt);

            // 7. Procedural Animations (Squash, Stretch, Run Bob)
            UpdateProceduralVisuals(dt);

            // 8. Void Fall Check
            if (transform.position.y < -3.0f && State != PlayerState.Dead)
            {
                Die(DeathType.FallIntoVoid);
            }
        }

        #region Movement & Kinematics
        private void MoveCharacter(float dt)
        {
            float forwardSpeed = GameManager.Instance.CurrentSpeed;

            // Forward displacement along forward heading
            Vector3 forwardMove = ForwardDirection * (forwardSpeed * dt);

            // Lateral Lane Target Calculation (-1 = Left, 0 = Center, +1 = Right)
            CurrentLane = Mathf.Clamp(CurrentLane, -1, 1);
            float targetLateralOffset = CurrentLane * laneDistance;
            float tiltOffset = (inputClassifier != null && inputClassifier.CurrentTilt != 0f) 
                ? inputClassifier.CurrentTilt * tiltLeanMaxOffset : 0f;
            float desiredLateralOffset = targetLateralOffset + tiltOffset;

            // Smoothly advance currentLaneOffset toward desiredLateralOffset
            float prevLaneOffset = currentLaneOffset;
            currentLaneOffset = Mathf.MoveTowards(currentLaneOffset, desiredLateralOffset, laneChangeSpeed * dt);
            float laneDelta = currentLaneOffset - prevLaneOffset;

            // Closed-loop drift correction relative to current corridor centerline:
            // corridorAnchor is the snap center of the current track corridor.
            // This prevents phantom world-origin offsets on 90-degree branch turns!
            Vector3 offsetFromCorridor = transform.position - corridorAnchor;
            float actualLateral = Vector3.Dot(offsetFromCorridor, RightDirection);
            float driftError = currentLaneOffset - actualLateral;
            float maxCorrectionStep = laneChangeSpeed * dt * 1.5f;
            float driftCorrection = Mathf.Clamp(driftError, -maxCorrectionStep, maxCorrectionStep);

            Vector3 lateralMove = RightDirection * (laneDelta + driftCorrection);

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
                currentLaneOffset = 0.0f;
                CurrentLane = 0;

                junc.OnPlayerTurned(angleDegrees);
            }
            else
            {
                targetRotation *= Quaternion.Euler(0, angleDegrees, 0);
                transform.rotation = targetRotation;
                ForwardDirection = transform.forward;
                RightDirection = transform.right;
                corridorAnchor = transform.position;
                currentLaneOffset = 0.0f;
                CurrentLane = 0;
            }
        }
        #endregion

        #region Procedural Squash, Stretch & Bobbing
        private void UpdateProceduralVisuals(float dt)
        {
            if (visualTransform == null || animator != null) return;

            Vector3 baseScale = Vector3.one;
            Vector3 visualPosOffset = Vector3.zero;

            if (State == PlayerState.Jumping)
            {
                // Ballistic stretch (elongate vertically)
                baseScale = new Vector3(0.88f, 1.25f, 0.88f);
            }
            else if (State == PlayerState.Sliding)
            {
                // Slide squash (flatten and widen)
                baseScale = new Vector3(1.30f, 0.48f, 1.35f);
                visualPosOffset = new Vector3(0, -0.5f, 0);
            }
            else if (State == PlayerState.Running)
            {
                // Running bobbing sine wave
                float speed = GameManager.Instance.CurrentSpeed;
                bobTimer += dt * speed * 1.5f;
                float bobY = Mathf.Abs(Mathf.Sin(bobTimer)) * 0.12f;
                visualPosOffset = new Vector3(0, bobY, 0);
            }

            visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, baseScale, dt * 15.0f);
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, visualPosOffset, dt * 15.0f);
        }

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
        #endregion

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
        public void ApplySuit(int suitIndex)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Color suitTint = Color.white;
            float emissionBoost = 0f;
            Color emissionColor = Color.black;
            Color trailColor = Color.clear;

            switch (suitIndex)
            {
                case 0: // Classic Spider Suit
                    suitTint = Color.white;
                    emissionBoost = 0f;
                    trailColor = Color.clear;
                    break;
                case 1: // Symbiote / Shadow Stealth Suit (Midnight black with glowing violet accents)
                    suitTint = new Color(0.12f, 0.12f, 0.16f);
                    emissionBoost = 0.7f;
                    emissionColor = new Color(0.65f, 0.15f, 0.95f);
                    trailColor = new Color(0.65f, 0.15f, 0.95f, 0.5f);
                    break;
                case 2: // Iron Spider / Gilded Aztec Armor (Rich metallic gold & crimson)
                    suitTint = new Color(1.0f, 0.85f, 0.35f);
                    emissionBoost = 0.75f;
                    emissionColor = new Color(1.0f, 0.75f, 0.1f);
                    trailColor = new Color(1.0f, 0.80f, 0.2f, 0.55f);
                    break;
                case 3: // Spider-Man 2099 / Cyber Neon (Dark indigo with electric neon cyan)
                    suitTint = new Color(0.10f, 0.15f, 0.35f);
                    emissionBoost = 0.85f;
                    emissionColor = new Color(0.05f, 0.95f, 1.0f);
                    trailColor = new Color(0.05f, 0.95f, 1.0f, 0.6f);
                    break;
            }

            foreach (var r in renderers)
            {
                if (r == null || r.gameObject.name.Contains("Oval") || r.gameObject.name.Contains("Shield")) continue;
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    mat.color = suitTint;
                    if (emissionBoost > 0f)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", emissionColor * emissionBoost);
                    }
                    else
                    {
                        mat.DisableKeyword("_EMISSION");
                    }
                }
            }

            // Configure dynamic suit trail
            if (trailColor.a > 0.05f)
            {
                if (suitTrail == null)
                {
                    suitTrail = gameObject.GetComponent<TrailRenderer>();
                    if (suitTrail == null) suitTrail = gameObject.AddComponent<TrailRenderer>();
                    suitTrail.time = 0.35f;
                    suitTrail.startWidth = 0.40f;
                    suitTrail.endWidth = 0.02f;
                    suitTrail.material = Runner.Core.MaterialHelper.CreateSafeMaterial(trailColor);
                }
                suitTrail.startColor = trailColor;
                suitTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                suitTrail.enabled = true;
            }
            else if (suitTrail != null)
            {
                suitTrail.enabled = false;
            }
        }
        #endregion
    }
}
