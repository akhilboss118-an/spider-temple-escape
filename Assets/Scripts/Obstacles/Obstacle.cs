using UnityEngine;
using Runner.Core;
using Runner.Effects;
using Runner.Pickups;
using Runner.Player;

namespace Runner.Obstacles
{
    public enum ObstacleType
    {
        LowLog,         // Must jump over
        SlideArch,      // Must slide under
        LaneBlocker,    // Must switch lane
        DeadEndWall     // Missed turn at junction
    }

    /// <summary>
    /// Attached to obstacles in the runner.
    /// Evaluates collision against Player state to decide between a grazing stumble penalty
    /// and a fatal head-on collision.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        [SerializeField] private ObstacleType obstacleType = ObstacleType.LowLog;
        [SerializeField] private bool hasTriggered = false;

        public ObstacleType Type => obstacleType;

        public void SetObstacleType(ObstacleType type)
        {
            obstacleType = type;
        }

        private void OnEnable()
        {
            hasTriggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerController>();
            }

            if (player == null || player.State == PlayerState.Dead)
                return;

            EvaluateCollision(player);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasTriggered) return;

            PlayerController player = collision.collider.GetComponent<PlayerController>();
            if (player == null)
            {
                player = collision.collider.GetComponentInParent<PlayerController>();
            }

            if (player == null || player.State == PlayerState.Dead)
                return;

            EvaluateCollision(player);
        }

        public void EvaluateCollision(PlayerController player)
        {
            if (hasTriggered || player == null || player.State == PlayerState.Dead) return;

            // 1. Speedrun Invincibility: Player demolishes rock obstacles on contact!
            if (PickupManager.Instance != null && PickupManager.Instance.IsSpeedrunActive && obstacleType != ObstacleType.DeadEndWall)
            {
                hasTriggered = true;
                if (ImpactEffectManager.Instance != null)
                {
                    ImpactEffectManager.Instance.PlaySpeedrunSmash(transform.position);
                }
                gameObject.SetActive(false);
                return;
            }

            // Check if jump cleared low obstacle safely
            if (obstacleType == ObstacleType.LowLog && player.State == PlayerState.Jumping)
            {
                return;
            }

            // Check if slide ducked under slide arch safely
            if (obstacleType == ObstacleType.SlideArch && player.State == PlayerState.Sliding)
            {
                return;
            }

            // 2. Shield Absorption: Consumes shield and saves player from rock obstacle stumbles
            // Dead-End Walls are completely fatal and cannot be absorbed by a shield!
            if (obstacleType != ObstacleType.DeadEndWall && PickupManager.Instance != null && PickupManager.Instance.HasShield)
            {
                hasTriggered = true;
                Vector3 contactPoint = player.transform.position + player.ForwardDirection * 0.5f + Vector3.up * 1.0f;
                PickupManager.Instance.TryAbsorbCollision(contactPoint);
                Collider c = GetComponent<Collider>();
                if (c != null) c.enabled = false;
                return;
            }

            hasTriggered = true;

            // Only disable colliders on minor obstacles (boulders, hurdles) so player trips forward
            // DeadEndWall remains a solid physical boundary!
            if (obstacleType != ObstacleType.DeadEndWall)
            {
                foreach (var col in GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }

            switch (obstacleType)
            {
                case ObstacleType.LowLog:
                case ObstacleType.SlideArch:
                case ObstacleType.LaneBlocker:
                    // Natural stone dust & rock fragment impact
                    if (ImpactEffectManager.Instance != null)
                    {
                        ImpactEffectManager.Instance.PlayRockHit(player.transform.position + Vector3.up * 0.6f, -player.ForwardDirection);
                    }

                    // Always trigger stumble animation and sound
                    player.TriggerStumble();

                    // Check life pool
                    if (GameManager.Instance != null)
                    {
                        if (GameManager.Instance.CurrentLives > 1)
                        {
                            GameManager.Instance.LoseLife(1);
                        }
                        else
                        {
                            GameManager.Instance.LoseLife(1);
                            player.Die(DeathType.CaughtByMonster);
                        }
                    }
                    break;

                case ObstacleType.DeadEndWall:
                    if (ImpactEffectManager.Instance != null)
                    {
                        ImpactEffectManager.Instance.PlayRockHit(player.transform.position + Vector3.up * 1.2f, -player.ForwardDirection);
                    }
                    // Crashing into a dead-end wall is instantly fatal: drain all lives and trigger game over!
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.LoseLife(GameManager.Instance.CurrentLives);
                    }
                    player.Die(DeathType.MissedTurn);
                    break;
            }
        }
    }
}
