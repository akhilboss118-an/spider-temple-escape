using UnityEngine;
using Runner.Core;
using Runner.Effects;
using Runner.Player;

namespace Runner.Pickups
{
    /// <summary>
    /// Collectible 3D Pink Heart that replaces coins.
    /// Rotates smoothly, bobs up and down, flies toward player when Magnet is active,
    /// and grants hearts/score on collection.
    /// </summary>
    public class Coin : MonoBehaviour
    {
        [Header("Heart Floating & Rotation")]
        [SerializeField] private float rotationSpeed = 160.0f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 3.5f;

        [Header("Magnet Pull")]
        [SerializeField] private float magnetPullSpeed = 24.0f;

        private bool isCollected = false;
        private float baseLocalY;
        private float bobOffset;
        private bool hasInitializedY = false;
        private Vector3 originalLocalPos;
        private bool hasOriginalPos = false;

        public void SetOriginalLocalPosition(Vector3 pos)
        {
            originalLocalPos = pos;
            hasOriginalPos = true;
            baseLocalY = pos.y;
            transform.localPosition = pos;
        }

        private void Awake()
        {
            EnsureHeartMesh();
            if (!hasOriginalPos)
            {
                originalLocalPos = transform.localPosition;
                hasOriginalPos = true;
            }
        }

        private void EnsureHeartMesh()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = HeartMeshBuilder.GetHeartMesh();

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = HeartMeshBuilder.GetHeartMaterial();

            // Set appropriate scale for collectible heart (~0.65m wide)
            transform.localScale = new Vector3(0.68f, 0.68f, 0.68f);

            // Ensure trigger collider
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                Collider existing = GetComponent<Collider>();
                if (existing != null && !(existing is SphereCollider))
                {
                    Destroy(existing);
                }
                sc = gameObject.AddComponent<SphereCollider>();
            }
            sc.isTrigger = true;
            sc.radius = 0.65f;
            sc.center = new Vector3(0, 0.05f, 0);
        }

        private void OnEnable()
        {
            isCollected = false;
            if (hasOriginalPos)
            {
                transform.localPosition = originalLocalPos;
            }
            else
            {
                originalLocalPos = transform.localPosition;
                hasOriginalPos = true;
            }
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
            baseLocalY = originalLocalPos.y;
            hasInitializedY = true;
        }

        private void Update()
        {
            if (isCollected) return;

            float dt = Time.deltaTime;

            // 1. Continuous smooth rotation
            transform.Rotate(0, rotationSpeed * dt, 0, Space.World);

            // 2. Gentle sinusoidal vertical bobbing
            if (hasInitializedY)
            {
                float newY = baseLocalY + Mathf.Sin(Time.time * bobFrequency + bobOffset) * bobAmplitude;
                Vector3 curPos = transform.localPosition;
                transform.localPosition = new Vector3(curPos.x, newY, curPos.z);
            }

            // 3. Magnet attraction toward player
            if (PickupManager.Instance != null && PickupManager.Instance.IsMagnetActive)
            {
                if (PlayerController.Instance != null)
                {
                    Vector3 playerPos = PlayerController.Instance.transform.position + Vector3.up * 1.0f;
                    float dist = Vector3.Distance(transform.position, playerPos);

                    if (dist <= PickupManager.Instance.MagnetRadius)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, playerPos, magnetPullSpeed * dt);

                        if (dist < 0.65f)
                        {
                            Collect();
                        }
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null) player = other.GetComponentInParent<PlayerController>();

            if (player != null && player.State != PlayerState.Dead)
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(1);
            }

            // Play ascending coin chime
            Runner.Audio.AudioManager.Instance?.PlayCoinChime();

            // Spawn pink heart pickup sparkle effect
            if (ImpactEffectManager.Instance != null)
            {
                ImpactEffectManager.Instance.PlayHeartCollect(transform.position);
            }

            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Explicit alias for Coin representing the 3D Pink Heart collectible.
    /// </summary>
    public class PinkHeart : Coin
    {
    }
}
