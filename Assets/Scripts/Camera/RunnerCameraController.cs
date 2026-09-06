using UnityEngine;
using Runner.Core;
using Runner.Pickups;
using Runner.Player;

namespace Runner.CameraControl
{
    /// <summary>
    /// Behind-the-back third-person camera controller.
    /// Features:
    /// - Smooth lateral lane dampening (prevents motion sickness during lane shifts)
    /// - Dynamic FOV expansion with speed (60 deg at 8m/s -> 72 deg at 20m/s)
    /// - 90-degree corner slerp damping
    /// - Procedural screen shake on stumbles and fatal collisions
    /// </summary>
    public class RunnerCameraController : MonoBehaviour
    {
        public static RunnerCameraController Instance { get; private set; }

        [Header("Follow Offsets")]
        [Tooltip("Camera offset relative to player (X=lateral, Y=height, Z=distance behind)")]
        [SerializeField] private Vector3 followOffset = new Vector3(0, 3.4f, -5.8f);

        [Tooltip("Damping speed for position following")]
        [SerializeField] private float positionFollowSpeed = 14.0f;

        [Tooltip("Slerp speed for 90-degree corner rotation")]
        [SerializeField] private float rotationSlerpSpeed = 10.0f;

        [Header("Dynamic Speed FOV")]
        [Tooltip("Field of View at base speed (8 m/s)")]
        [SerializeField] private float baseFov = 60.0f;

        [Tooltip("Field of View at max speed (20 m/s)")]
        [SerializeField] private float maxFov = 72.0f;

        [Header("Screen Shake")]
        [SerializeField] private float stumbleShakeIntensity = 0.18f;
        [SerializeField] private float stumbleShakeDuration = 0.35f;
        [SerializeField] private float deathShakeIntensity = 0.45f;
        [SerializeField] private float deathShakeDuration = 0.60f;

        private Camera cam;

        private bool initializedPosition = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            SnapToPlayer();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled += HandleStumbled;
                GameManager.Instance.OnGameOver += HandleGameOver;
            }
        }

        public void SnapToPlayer()
        {
            if (PlayerController.Instance == null)
                return;

            Transform playerTransform = PlayerController.Instance.transform;
            Vector3 playerPos = playerTransform.position;
            Quaternion playerRot = playerTransform.rotation;

            Vector3 rotatedOffset = playerRot * followOffset;
            transform.position = playerPos + rotatedOffset;

            Quaternion lookRotation = Quaternion.LookRotation((playerPos + Vector3.up * 1.2f) - transform.position);
            transform.rotation = lookRotation;
            initializedPosition = true;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStumbled -= HandleStumbled;
                GameManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        private void LateUpdate()
        {
            if (PlayerController.Instance == null)
                return;

            if (!initializedPosition)
            {
                SnapToPlayer();
            }

            float dt = Time.deltaTime;

            Transform playerTransform = PlayerController.Instance.transform;
            Vector3 playerPos = playerTransform.position;
            Quaternion playerRot = playerTransform.rotation;

            // 1. Calculate Target Camera Position relative to player orientation
            Vector3 activeOffset = followOffset;
            float targetFov = baseFov;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Menu)
            {
                // Wider showcase angle of Spider Hero & Shrine Path in Main Menu
                activeOffset = new Vector3(0f, 4.2f, -7.5f);
                targetFov = 52.0f;
            }
            else if (GameManager.Instance != null)
            {
                // Dynamic FOV Scaling with Speed & Speedrun Powerup
                float speed = GameManager.Instance.CurrentSpeed;
                float t = Mathf.InverseLerp(8.0f, 20.0f, speed);
                targetFov = Mathf.Lerp(baseFov, maxFov, t);

                if (PickupManager.Instance != null && PickupManager.Instance.IsSpeedrunActive)
                {
                    targetFov = 78.0f;
                }
            }

            Vector3 rotatedOffset = playerRot * activeOffset;
            Vector3 desiredPosition = playerPos + rotatedOffset;

            // Interpolate position smoothly (zooms into 3rd-person sprint perspective on Start Run)
            transform.position = Vector3.Lerp(transform.position, desiredPosition, dt * positionFollowSpeed);

            // 2. Smoothly Slerp Rotation toward player heading + slight pitch down
            Quaternion lookRotation = Quaternion.LookRotation((playerPos + Vector3.up * 1.2f) - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, dt * rotationSlerpSpeed);

            // 3. Dynamic FOV Scaling
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, dt * 6.0f);
            }

            // 4. Natural Physical Camera Recoil (Directional kick & smooth spring-back)
            if (recoilTimer > 0.0f)
            {
                recoilTimer -= dt;
                float t = recoilTimer / recoilDuration;
                // Damped spring impulse along camera backward heading
                Vector3 recoilDisplacement = -transform.forward * (recoilIntensity * t * 0.7f) + Vector3.down * (recoilIntensity * t * 0.3f);
                transform.position += recoilDisplacement;
            }
        }

        private float recoilTimer = 0.0f;
        private float recoilDuration = 0.25f;
        private float recoilIntensity = 0.0f;

        public void TriggerPhysicalRecoil(float intensity, float duration)
        {
            recoilIntensity = intensity;
            recoilDuration = Mathf.Max(0.1f, duration);
            recoilTimer = recoilDuration;
        }

        public void TriggerScreenShake(float intensity, float duration)
        {
            TriggerPhysicalRecoil(intensity * 0.8f, duration);
        }

        private void HandleStumbled(int count, float decayTime)
        {
            TriggerPhysicalRecoil(stumbleShakeIntensity, stumbleShakeDuration);
        }

        private void HandleGameOver(DeathType deathType, int finalScore)
        {
            TriggerPhysicalRecoil(deathShakeIntensity, deathShakeDuration);
        }
    }
}
