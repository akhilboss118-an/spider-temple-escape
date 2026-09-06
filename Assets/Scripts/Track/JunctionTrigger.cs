using UnityEngine;
using Runner.Core;
using Runner.Player;
using Runner.Pickups;

namespace Runner.Track
{
    /// <summary>
    /// Monitors player approach toward a 90-degree junction.
    /// Strictly validates the [-4.0m, +1.5m] turn window.
    /// If player fails to turn before crossing +1.5m, triggers a dead-end fatal collision.
    /// </summary>
    public class JunctionTrigger : MonoBehaviour
    {
        [Header("Turn Permissions")]
        [SerializeField] private bool allowTurnLeft = true;
        [SerializeField] private bool allowTurnRight = false;

        [Header("Turn Window Parameters")]
        [Tooltip("Earliest distance in meters before junction center where turn swipe is accepted")]
        [SerializeField] private float entryDistance = -7.5f;

        [Tooltip("Latest distance in meters past junction center before fatal dead-end collision occurs")]
        [SerializeField] private float exitTolerance = 3.85f;

        [Tooltip("Transform representing the intersection snap center")]
        [SerializeField] private Transform snapCenter;

        private bool hasTurned = false;
        private bool isPlayerInWindow = false;

        public bool CanTurnLeft() => allowTurnLeft && !hasTurned;
        public bool CanTurnRight() => allowTurnRight && !hasTurned;

        public void Configure(bool canLeft, bool canRight)
        {
            allowTurnLeft = canLeft;
            allowTurnRight = canRight;
            hasTurned = false;
            isPlayerInWindow = false;
            enabled = true;
        }

        public Vector3 GetSnapCenter()
        {
            return snapCenter != null ? snapCenter.position : transform.position;
        }

        private void Update()
        {
            if (hasTurned || PlayerController.Instance == null || GameManager.Instance == null)
                return;

            if (GameManager.Instance.CurrentState != GameState.Playing)
                return;

            Vector3 junctionPos = GetSnapCenter();
            Vector3 playerPos = PlayerController.Instance.transform.position;

            // Project player position along this junction's coordinate frame
            Vector3 toPlayer = playerPos - junctionPos;
            float forwardDist = Vector3.Dot(toPlayer, transform.forward);
            float lateralDist = Mathf.Abs(Vector3.Dot(toPlayer, transform.right));

            // Only consider the player if they are within this track's corridor (<= 6.0m laterally)
            if (lateralDist > 6.0f)
            {
                if (isPlayerInWindow)
                {
                    isPlayerInWindow = false;
                    if (PlayerController.Instance.ActiveJunction == this)
                    {
                        PlayerController.Instance.ActiveJunction = null;
                    }
                }
                return;
            }

            // Turn window: between entryDistance (-7.5m) and exitTolerance (+3.85m)
            if (forwardDist >= entryDistance && forwardDist <= exitTolerance)
            {
                if (!isPlayerInWindow)
                {
                    isPlayerInWindow = true;
                    PlayerController.Instance.ActiveJunction = this;
                }

                // Super Boost auto-navigates junction turns
                if (PickupManager.Instance != null && PickupManager.Instance.IsSpeedrunActive && forwardDist >= -1.0f && !hasTurned)
                {
                    float autoTurnAngle = allowTurnLeft ? -90.0f : 90.0f;
                    PlayerController.Instance.ExecuteTurn(autoTurnAngle);
                    return;
                }
            }
            else if (forwardDist > 2.8f && !hasTurned)
            {
                // Failed to turn before crossing junction limit: instant fatal crash!
                isPlayerInWindow = false;
                hasTurned = true;
                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.ActiveJunction = null;
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.LoseLife(GameManager.Instance.CurrentLives);
                    }
                    PlayerController.Instance.Die(DeathType.MissedTurn);
                }
            }
            else if (forwardDist > exitTolerance)
            {
                isPlayerInWindow = false;
                if (PlayerController.Instance != null && PlayerController.Instance.ActiveJunction == this)
                {
                    PlayerController.Instance.ActiveJunction = null;
                }
            }
            else if (forwardDist < entryDistance && isPlayerInWindow)
            {
                isPlayerInWindow = false;
                if (PlayerController.Instance.ActiveJunction == this)
                {
                    PlayerController.Instance.ActiveJunction = null;
                }
            }
        }

        public void OnPlayerTurned(float turnAngle)
        {
            hasTurned = true;
            isPlayerInWindow = false;
            enabled = false; // Disable this trigger immediately so it never interferes again

            if (PlayerController.Instance != null && PlayerController.Instance.ActiveJunction == this)
            {
                PlayerController.Instance.ActiveJunction = null;
            }

            // Notify TrackManager with the exact turn angle (-90 or +90)
            if (TrackManager.Instance != null)
            {
                TrackManager.Instance.NotifyPlayerTurnedAtJunction(this, turnAngle);
            }
        }

        public void OnPlayerTurned()
        {
            OnPlayerTurned(allowTurnLeft ? -90.0f : 90.0f);
        }

        public void ResetJunction()
        {
            hasTurned = false;
            isPlayerInWindow = false;
            enabled = true;
            gameObject.SetActive(true);
        }

        private void OnDrawGizmos()
        {
            Vector3 center = GetSnapCenter();
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, 0.5f);

            // Draw window lines
            Gizmos.color = Color.yellow;
            Vector3 entry = center + transform.forward * entryDistance;
            Vector3 exit = center + transform.forward * exitTolerance;
            Gizmos.DrawLine(entry - transform.right * 2f, entry + transform.right * 2f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(exit - transform.right * 2f, exit + transform.right * 2f);
        }
    }
}
