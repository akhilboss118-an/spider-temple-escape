using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Obstacles
{
    /// <summary>
    /// Collapsing floor segment that crumbles behind the player as they run.
    /// Floor tiles fall with a delay after the player passes, creating urgency.
    /// If the player lingers too long, the floor gives way and they fall.
    /// </summary>
    public class CollapsingFloor : MonoBehaviour
    {
        [Header("Collapse Settings")]
        [Tooltip("Delay in seconds after player passes before collapse starts")]
        [SerializeField] private float collapseDelay = 0.8f;

        [Tooltip("Speed at which tiles fall")]
        [SerializeField] private float fallSpeed = 6.0f;

        [Tooltip("Distance behind player to trigger collapse")]
        [SerializeField] private float triggerDistanceBehind = 2.0f;

        private FloorTile[] tiles;
        private bool[] tileTriggered;
        private bool playerPassed = false;
        private Transform playerTransform;

        [System.Serializable]
        public class FloorTile
        {
            public Transform transform;
            public Renderer renderer;
            public Collider collider;
            public float collapseTimer;
            public bool collapsed;
            public Vector3 originalPosition;
            public Quaternion originalRotation;
            public float fallDelay;
        }

        private void Awake()
        {
            InitializeTiles();
        }

        private void OnEnable()
        {
            playerPassed = false;
            ResetTiles();
        }

        private void InitializeTiles()
        {
            // Find all child floor tiles
            var tileTransforms = GetComponentsInChildren<Transform>();
            int tileCount = 0;

            // Count valid tiles (direct children that are not the root)
            foreach (var t in tileTransforms)
            {
                if (t != transform && t.parent == transform)
                {
                    tileCount++;
                }
            }

            if (tileCount == 0)
            {
                // Create procedural collapsing floor tiles
                CreateProceduralTiles();
                return;
            }

            tiles = new FloorTile[tileCount];
            tileTriggered = new bool[tileCount];

            int idx = 0;
            foreach (var t in tileTransforms)
            {
                if (t != transform && t.parent == transform)
                {
                    tiles[idx] = new FloorTile
                    {
                        transform = t,
                        renderer = t.GetComponentInChildren<Renderer>(),
                        collider = t.GetComponentInChildren<Collider>(),
                        originalPosition = t.localPosition,
                        originalRotation = t.localRotation,
                        collapsed = false,
                        collapseTimer = 0f,
                        fallDelay = collapseDelay + (idx * 0.15f) // Stagger collapse
                    };
                    tileTriggered[idx] = false;
                    idx++;
                }
            }
        }

        private void CreateProceduralTiles()
        {
            int rows = 3;
            int cols = 3;
            float tileSize = 2.5f;
            float gap = 0.15f;

            tiles = new FloorTile[rows * cols];
            tileTriggered = new bool[tiles.Length];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.transform.SetParent(transform, false);
                    tile.transform.localScale = new Vector3(tileSize, 0.3f, tileSize);
                    tile.transform.localPosition = new Vector3(
                        (c - (cols - 1) * 0.5f) * (tileSize + gap),
                        0f,
                        (r - (rows - 1) * 0.5f) * (tileSize + gap)
                    );

                    Renderer rend = tile.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        // Cracked stone appearance
                        Material mat = new Material(Shader.Find("Standard"));
                        mat.color = new Color(0.45f, 0.38f, 0.28f);
                        rend.material = mat;
                    }

                    // Mark tile with script component for identification
                    var marker = tile.AddComponent<CollapsingTileMarker>();

                    tiles[idx] = new FloorTile
                    {
                        transform = tile.transform,
                        renderer = rend,
                        collider = tile.GetComponent<Collider>(),
                        originalPosition = tile.transform.localPosition,
                        originalRotation = tile.transform.localRotation,
                        collapsed = false,
                        collapseTimer = 0f,
                        fallDelay = collapseDelay + (idx * 0.12f)
                    };
                    tileTriggered[idx] = false;
                }
            }
        }

        private void ResetTiles()
        {
            if (tiles == null) return;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].transform != null)
                {
                    tiles[i].transform.localPosition = tiles[i].originalPosition;
                    tiles[i].transform.localRotation = tiles[i].originalRotation;
                    tiles[i].collapsed = false;
                    tiles[i].collapseTimer = 0f;
                    if (tiles[i].renderer != null)
                    {
                        tiles[i].renderer.enabled = true;
                    }
                    if (tiles[i].collider != null)
                    {
                        tiles[i].collider.enabled = true;
                    }
                }
                tileTriggered[i] = false;
            }
            playerPassed = false;
        }

        private void Update()
        {
            if (PlayerController.Instance == null) return;
            if (tiles == null || tiles.Length == 0) return;

            playerTransform = PlayerController.Instance.transform;

            // Check if player has passed this chunk
            Vector3 toPlayer = playerTransform.position - transform.position;
            float distForward = Vector3.Dot(toPlayer, transform.forward);

            if (!playerPassed && distForward < -triggerDistanceBehind)
            {
                playerPassed = true;
            }

            // Update each tile
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null || tiles[i].transform == null) continue;
                if (tiles[i].collapsed) continue;

                if (playerPassed && !tileTriggered[i])
                {
                    tileTriggered[i] = true;
                    tiles[i].collapseTimer = tiles[i].fallDelay;
                }

                if (tileTriggered[i])
                {
                    tiles[i].collapseTimer -= Time.deltaTime;

                    // Shake before collapse
                    if (tiles[i].collapseTimer > 0f && tiles[i].collapseTimer < 0.3f)
                    {
                        float shakeIntensity = 0.05f * (tiles[i].collapseTimer / 0.3f);
                        Vector3 shakeOffset = new Vector3(
                            Random.Range(-shakeIntensity, shakeIntensity),
                            0f,
                            Random.Range(-shakeIntensity, shakeIntensity)
                        );
                        tiles[i].transform.localPosition = tiles[i].originalPosition + shakeOffset;

                        // Change color to indicate danger
                        if (tiles[i].renderer != null)
                        {
                            float dangerLerp = 1f - (tiles[i].collapseTimer / 0.3f);
                            tiles[i].renderer.material.color = Color.Lerp(
                                new Color(0.45f, 0.38f, 0.28f),
                                new Color(0.8f, 0.2f, 0.1f),
                                dangerLerp
                            );
                        }
                    }

                    // Collapse!
                    if (tiles[i].collapseTimer <= 0f)
                    {
                        tiles[i].collapsed = true;

                        // Disable collider so player can fall through
                        if (tiles[i].collider != null)
                        {
                            tiles[i].collider.enabled = false;
                        }

                        // Start falling animation
                        StartCoroutine(FallTile(tiles[i]));
                    }
                }
            }
        }

        private System.Collections.IEnumerator FallTile(FloorTile tile)
        {
            if (tile == null || tile.transform == null) yield break;

            float elapsed = 0f;
            float duration = 0.6f;
            Vector3 startPos = tile.transform.localPosition;
            Quaternion startRot = tile.transform.localRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Fall down and tilt
                tile.transform.localPosition = startPos + Vector3.down * (fallSpeed * t * t);
                tile.transform.localRotation = startRot * Quaternion.Euler(
                    Random.Range(-15f, 15f) * t,
                    0f,
                    Random.Range(-15f, 15f) * t
                );

                // Fade out
                if (tile.renderer != null)
                {
                    Color c = tile.renderer.material.color;
                    c.a = 1f - t;
                    tile.renderer.material.color = c;
                }

                yield return null;
            }

            // Disable tile entirely
            if (tile.transform != null)
            {
                tile.transform.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Marker component for procedural collapsing tiles
        /// </summary>
        private class CollapsingTileMarker : MonoBehaviour { }
    }
}
