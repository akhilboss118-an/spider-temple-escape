using System.Collections.Generic;
using UnityEngine;
using Runner.Obstacles;

namespace Runner.Track
{
    public enum ChunkType
    {
        Straight,
        Gap,
        LowObstacle,
        SlideArch,
        LaneBlocker,
        TJunctionLeft,
        TJunctionRight,
        TJunctionDouble,
        CoinRun,
        HeartRun
    }

    /// <summary>
    /// Represents an individual modular chunk in the endless runner track.
    /// Manages sockets, dimensions, entry/exit snap points, and reset logic for pooling.
    /// </summary>
    public class TrackChunk : MonoBehaviour
    {
        [Header("Chunk Properties")]
        [SerializeField] private ChunkType chunkType = ChunkType.Straight;
        [SerializeField] private float chunkLength = 10.0f;
        [SerializeField] private float trackWidth = 4.0f;

        [Header("Sockets & Snap Points")]
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Transform leftExitPoint;
        [SerializeField] private Transform rightExitPoint;

        [Header("Obstacles & Pickups Containers")]
        [SerializeField] private GameObject[] obstacleObjects;
        [SerializeField] private GameObject[] coinObjects;

        public ChunkType Type => chunkType;
        public float Length => chunkLength;
        public float Width => trackWidth;

        public Transform EntryPoint => entryPoint != null ? entryPoint : transform;
        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public Transform LeftExitPoint => leftExitPoint;
        public Transform RightExitPoint => rightExitPoint;

        public bool HasJumpObstacle => chunkType == ChunkType.LowObstacle || chunkType == ChunkType.Gap;
        public bool HasSlideObstacle => chunkType == ChunkType.SlideArch;
        public bool IsJunction => chunkType == ChunkType.TJunctionLeft ||
                                  chunkType == ChunkType.TJunctionRight ||
                                  chunkType == ChunkType.TJunctionDouble;

        /// <summary>
        /// Resets all dynamic child obstacles and pickups when re-spawned from the object pool.
        /// </summary>
        public void ResetChunk()
        {
            if (obstacleObjects != null)
            {
                foreach (var obj in obstacleObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }

            if (coinObjects != null)
            {
                foreach (var coin in coinObjects)
                {
                    if (coin != null) coin.SetActive(true);
                }
            }

            var obstacles = GetComponentsInChildren<Runner.Obstacles.Obstacle>(true);
            foreach (var obs in obstacles)
            {
                if (obs != null)
                {
                    obs.gameObject.SetActive(true);
                    foreach (var c in obs.GetComponentsInChildren<Collider>(true))
                    {
                        c.enabled = true;
                    }
                }
            }

            var powerUps = GetComponentsInChildren<Pickups.PowerUpItem>(true);
            foreach (var pu in powerUps)
            {
                if (pu != null) pu.gameObject.SetActive(true);
            }

            var chests = GetComponentsInChildren<Pickups.MysteryChest>(true);
            foreach (var chest in chests)
            {
                if (chest != null) chest.gameObject.SetActive(true);
            }

            var coins = GetComponentsInChildren<Runner.Pickups.Coin>(true);
            foreach (var coin in coins)
            {
                if (coin != null) coin.gameObject.SetActive(true);
            }

            var junc = GetComponentInChildren<JunctionTrigger>();
            if (junc != null)
            {
                junc.ResetJunction();
            }

            var dynamicGate = transform.Find("AncientStoneGate");
            if (dynamicGate != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(dynamicGate.gameObject);
                else
                    Destroy(dynamicGate.gameObject);
                #else
                Destroy(dynamicGate.gameObject);
                #endif
            }
        }

        public void SetChunkType(ChunkType type)
        {
            chunkType = type;
        }

        public void ApplyBiome(Material floorMat, Material curbMat)
        {
            if (floorMat == null) return;
            Transform floorT = transform.Find("Floor");
            if (floorT != null)
            {
                var r = floorT.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = floorMat;
            }

            if (curbMat != null)
            {
                foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r != null && (r.gameObject.name.Contains("Curb") || r.gameObject.name.Contains("Ground")))
                    {
                        r.sharedMaterial = curbMat;
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            // Visualize chunk bounds in Editor
            Gizmos.color = IsJunction ? Color.yellow : (chunkType == ChunkType.Gap ? Color.red : Color.green);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, 0, chunkLength * 0.5f), new Vector3(trackWidth, 0.2f, chunkLength));
        }
    }
}
