using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Obstacles
{
    /// <summary>
    /// Boss entity that appears every 5000m. A large intimidating obstacle structure
    /// that the player must dodge through. Defeated by surviving the gauntlet.
    /// </summary>
    public class BossEntity : MonoBehaviour
    {
        [Header("Boss Settings")]
        [SerializeField] private float bossSpeed = 3.0f;
        [SerializeField] private float bossDuration = 8.0f;

        private float bossTimer = 0f;
        private bool bossActive = false;
        private Vector3 moveDirection;
        private float baseY;

        // Visual state
        private Renderer[] renderers;
        private Color bossBaseColor = new Color(0.6f, 0.15f, 0.1f);
        private float glowPulse = 0f;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            bossTimer = 0f;
            bossActive = true;
            moveDirection = transform.forward;
            baseY = transform.position.y;
            glowPulse = 0f;
        }

        private void Update()
        {
            if (!bossActive) return;

            bossTimer += Time.deltaTime;
            if (bossTimer >= bossDuration)
            {
                bossActive = false;
                gameObject.SetActive(false);
                return;
            }

            // Move boss alongside player
            transform.position += moveDirection * bossSpeed * Time.deltaTime;

            // Pulse glow effect
            glowPulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;

            if (renderers != null)
            {
                Color glowColor = Color.Lerp(bossBaseColor, Color.red, glowPulse * 0.5f);
                foreach (var r in renderers)
                {
                    if (r != null && r.material != null)
                    {
                        r.material.color = glowColor;
                        if (r.material.HasProperty("_EmissionColor"))
                        {
                            r.material.SetColor("_EmissionColor", Color.red * glowPulse * 2f);
                        }
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!bossActive) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null) player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.State == PlayerState.Dead) return;

            // Boss contact = instant death
            player.Die(DeathType.CaughtByMonster);
        }

        public void ConfigureBoss(int bossLevel)
        {
            // Scale boss difficulty with level
            float scaleFactor = 1f + (bossLevel - 1) * 0.3f;
            transform.localScale = Vector3.one * scaleFactor;
            bossSpeed = 3.0f + bossLevel * 0.5f;
            bossDuration = 6.0f + bossLevel * 1.0f;

            // Visual: bigger glow for higher levels
            bossBaseColor = Color.Lerp(
                new Color(0.6f, 0.15f, 0.1f),
                new Color(0.8f, 0.05f, 0.02f),
                Mathf.Clamp01((bossLevel - 1) / 4f)
            );
        }
    }
}
