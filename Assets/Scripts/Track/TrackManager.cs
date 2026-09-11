using System.Collections.Generic;
using UnityEngine;
using Runner.Core;
using Runner.Obstacles;
using Runner.Pickups;
using Runner.Player;

namespace Runner.Track
{
    /// <summary>
    /// Manages track chunk pooling (25-30 active chunks), procedural generation,
    /// strict rule validation, and dynamic 90-degree branch redirection.
    /// </summary>
    public class TrackManager : MonoBehaviour
    {
        public static TrackManager Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Target number of chunks kept active in front of the player")]
        [SerializeField] private int activeChunkCount = 14;

        [Tooltip("Distance behind the player before a chunk is recycled")]
        [SerializeField] private float recycleDistanceBehindPlayer = 15.0f;

        [Header("Chunk Prefabs (9 Types)")]
        [SerializeField] private TrackChunk straightPrefab;
        [SerializeField] private TrackChunk gapPrefab;
        [SerializeField] private TrackChunk lowObstaclePrefab;
        [SerializeField] private TrackChunk slideArchPrefab;
        [SerializeField] private TrackChunk laneBlockerPrefab;
        [SerializeField] private TrackChunk tJunctionLeftPrefab;
        [SerializeField] private TrackChunk tJunctionRightPrefab;
        [SerializeField] private TrackChunk tJunctionDoublePrefab;
        [SerializeField] private TrackChunk coinRunPrefab;

        // Active track chunk queue
        private readonly Queue<TrackChunk> activeChunks = new Queue<TrackChunk>();

        // Object pools by ChunkType
        private readonly Dictionary<ChunkType, Queue<TrackChunk>> chunkPools = new Dictionary<ChunkType, Queue<TrackChunk>>();

        // Track generation cursor
        private Vector3 nextSpawnPosition = Vector3.zero;
        private Quaternion currentTrackRotation = Quaternion.identity;

        // Constraint history tracking
        private ChunkType lastSpawnedType = ChunkType.Straight;
        private float distanceSinceLastSlide = 100.0f;
        private float distanceSinceLastGap = 100.0f;
        private float distanceSinceLastJump = 100.0f;
        private int chunksSinceLastJunction = 0;

        // Procedural road progression & stone gate tracking
        private float cumulativeTrackDistance = 0.0f;
        private float nextStoneGateDistance = 120.0f;
        private bool gateSpawnedAt1000 = false;
        private bool gateSpawnedAt2000 = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePools();
        }

        private void Start()
        {
            SpawnInitialTrack();
        }

        private void Update()
        {
            if (PlayerController.Instance == null)
                return;

            Vector3 playerPos = PlayerController.Instance.transform.position;

            // Check if oldest chunk is far behind the player
            if (activeChunks.Count > 0)
            {
                TrackChunk oldest = activeChunks.Peek();
                // Vector from player to chunk
                Vector3 toChunk = oldest.transform.position - playerPos;
                // Distance along player's current forward heading
                float distAlongPlayerForward = Vector3.Dot(toChunk, PlayerController.Instance.ForwardDirection);

                // If chunk is behind the player by more than recycleDistanceBehindPlayer
                if (distAlongPlayerForward < -recycleDistanceBehindPlayer)
                {
                    RecycleOldestChunk();
                    SpawnNextChunk();
                }
            }
        }

        private void InitializePools()
        {
            foreach (ChunkType type in (ChunkType[])System.Enum.GetValues(typeof(ChunkType)))
            {
                chunkPools[type] = new Queue<TrackChunk>();
            }
        }

        private void SpawnInitialTrack()
        {
            chunksSinceLastJunction = 0;
            cumulativeTrackDistance = 0.0f;
            nextStoneGateDistance = 120.0f;
            gateSpawnedAt1000 = false;
            gateSpawnedAt2000 = false;

            // Start 20m behind player so road extends seamlessly under camera (-5.8m) & monster (-4.0m)
            nextSpawnPosition = new Vector3(0, 0, -20.0f);
            currentTrackRotation = Quaternion.identity;

            // Chunks behind starting line: -20 to -10m, -10 to 0m
            SpawnChunkOfType(ChunkType.Straight);
            SpawnChunkOfType(ChunkType.Straight);

            // Chunk at start line (0 - 10m): Safe straight runway with start gate archway
            TrackChunk startChunk = SpawnChunkOfType(ChunkType.Straight);
            if (startChunk != null)
            {
                SpawnStoneGate(startChunk, 1.0f);
            }

            // Chunk 1 (10 - 20m): Safe open sprint runway with ambient roadside braziers
            SpawnChunkOfType(ChunkType.Straight);

            // Chunk 2 (20 - 30m): Inviting Gold Coin trail
            SpawnChunkOfType(ChunkType.CoinRun);

            // Chunk 3 (30 - 40m): Safe straight runway allowing player to build momentum
            SpawnChunkOfType(ChunkType.Straight);

            // Chunk 4 (40 - 50m): 10-Heart sprint trail (pure reward, zero hazards)
            SpawnChunkOfType(ChunkType.HeartRun);

            // Chunk 5 (50 - 60m): Gentle single-lane obstacle introduction with wide open dodge lanes
            SpawnChunkOfType(ChunkType.LaneBlocker);

            // Chunk 6 (60 - 70m): Rewarding recovery coin run
            SpawnChunkOfType(ChunkType.CoinRun);

            // Chunks 7+ : Dynamically generated procedural world
            while (activeChunks.Count < activeChunkCount)
            {
                SpawnNextChunk();
            }
        }

        private void SpawnNextChunk()
        {
            ChunkType chosenType = SelectNextValidChunkType();
            SpawnChunkOfType(chosenType);
        }

        /// <summary>
        /// Balanced & Minimized Obstacle Generator:
        /// 1. Enforces strict obstacle cooldown guarantee: NEVER spawn two obstacle chunks consecutively.
        /// 2. Clean safe runs (Straight, CoinRun, HeartRun) make up ~70% of chunks.
        /// 3. Well-spaced single-lane obstacles make up ~30% of chunks.
        /// </summary>
        private ChunkType SelectNextValidChunkType()
        {
            float speed = GameManager.Instance != null ? GameManager.Instance.CurrentSpeed : 8.0f;
            float minJumpSpacing = 6.0f * (speed / 8.0f);

            // 1. Temple Run 90-degree Turn Junctions:
            // Spawn a junction only after 10 or more chunks of running
            if (chunksSinceLastJunction >= 10)
            {
                int juncRoll = Random.Range(0, 100);
                if (juncRoll < 35) return ChunkType.TJunctionLeft;
                else if (juncRoll < 70) return ChunkType.TJunctionRight;
                else return ChunkType.TJunctionDouble;
            }

            // 2. Obstacle Cooldown Guarantee:
            // If the previous chunk contained an obstacle, the next chunk is GUARANTEED to be a clean safe run!
            bool lastWasObstacle = (lastSpawnedType == ChunkType.LaneBlocker || lastSpawnedType == ChunkType.LowObstacle || lastSpawnedType == ChunkType.SlideArch);
            if (lastWasObstacle)
            {
                int safeRoll = Random.Range(0, 100);
                if (safeRoll < 45) return ChunkType.Straight;
                else if (safeRoll < 75) return ChunkType.CoinRun;
                else return ChunkType.HeartRun;
            }

            // 3. Balanced Procedural Distribution: ~70% safe runs, ~30% minimized hazards
            int roll = Random.Range(0, 100);
            ChunkType candidate;

            if (roll < 30) candidate = ChunkType.Straight;        // 30% clean straight
            else if (roll < 55) candidate = ChunkType.CoinRun;    // 25% coin run
            else if (roll < 70) candidate = ChunkType.HeartRun;   // 15% heart sprint
            else if (roll < 80) candidate = ChunkType.LaneBlocker;// 10% single-lane dodge
            else if (roll < 90) candidate = ChunkType.LowObstacle;// 10% jump hurdle
            else candidate = ChunkType.SlideArch;                 // 10% slide trunk

            // Spacing check: if spacing is too tight, fallback to safe straight/coin runway, NOT another hazard!
            if (candidate == ChunkType.LowObstacle && distanceSinceLastJump < minJumpSpacing)
            {
                return ChunkType.Straight;
            }
            if (candidate == ChunkType.SlideArch && distanceSinceLastSlide < minJumpSpacing)
            {
                return ChunkType.CoinRun;
            }

            return candidate;
        }

        private TrackChunk SpawnChunkOfType(ChunkType type)
        {
            TrackChunk chunk = GetOrCreateChunk(type);

            chunk.transform.position = nextSpawnPosition;
            chunk.transform.rotation = currentTrackRotation;
            chunk.gameObject.SetActive(true);
            chunk.ResetChunk();

            float chunkDistance = cumulativeTrackDistance;
            cumulativeTrackDistance += chunk.Length;

            if (Runner.Effects.BiomeManager.Instance != null)
            {
                var curBiome = Runner.Effects.BiomeManager.Instance.CurrentBiome;
                var floorMat = Runner.Effects.BiomeManager.Instance.GetFloorMaterialForBiome(curBiome);
                var curbMat = Runner.Effects.BiomeManager.Instance.GetCurbMaterialForBiome(curBiome);
                chunk.ApplyBiome(floorMat, curbMat);
            }

            // 3-Tier Procedural 3D Road Progression (0-1000m Default, 1000-2000m Rocky Path, 2000m+ Highway)
            ApplyRoadVisual(chunk, chunkDistance);

            // Ancient Stone Gate Integration (Milestone Transitions & Periodic Archways)
            CheckAndSpawnStoneGate(chunk, chunkDistance);

            // Configure junction triggers explicitly whether fresh or recycled from object pool
            if (chunk.IsJunction)
            {
                var junc = chunk.GetComponentInChildren<JunctionTrigger>();
                if (junc != null)
                {
                    bool canLeft = (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionDouble);
                    bool canRight = (type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble);
                    junc.Configure(canLeft, canRight);
                }
            }

            activeChunks.Enqueue(chunk);

            // Update constraint tracking distances
            float chunkLength = chunk.Length;
            distanceSinceLastSlide += chunkLength;
            distanceSinceLastGap += chunkLength;
            distanceSinceLastJump += chunkLength;
            chunksSinceLastJunction++;

            if (chunk.HasSlideObstacle) distanceSinceLastSlide = 0.0f;
            if (chunk.Type == ChunkType.Gap) distanceSinceLastGap = 0.0f;
            if (chunk.HasJumpObstacle) distanceSinceLastJump = 0.0f;
            if (chunk.IsJunction) chunksSinceLastJunction = 0;

            lastSpawnedType = type;

            // Advance spawn cursor forward along current orientation
            nextSpawnPosition += currentTrackRotation * (Vector3.forward * chunkLength);

            return chunk;
        }

        private TrackChunk GetOrCreateChunk(ChunkType type)
        {
            if (chunkPools.ContainsKey(type) && chunkPools[type].Count > 0)
            {
                return chunkPools[type].Dequeue();
            }

            // Instantiate from prefab if assigned
            TrackChunk prefab = GetPrefabForType(type);
            if (prefab != null)
            {
                TrackChunk instance = Instantiate(prefab, transform);
                instance.SetChunkType(type);
                return instance;
            }

            // Fallback: Generate procedural chunk primitive
            return CreateProceduralChunk(type);
        }

        private void RecycleOldestChunk()
        {
            if (activeChunks.Count == 0) return;

            TrackChunk oldest = activeChunks.Dequeue();
            oldest.gameObject.SetActive(false);

            if (!chunkPools.ContainsKey(oldest.Type))
            {
                chunkPools[oldest.Type] = new Queue<TrackChunk>();
            }
            chunkPools[oldest.Type].Enqueue(oldest);
        }

        public void NotifyPlayerTurnedAtJunction(JunctionTrigger junction, float turnAngle = -90.0f)
        {
            // Player turned at a junction (-90 for Left, +90 for Right):
            // Align currentTrackRotation with the exact branch heading
            Quaternion newRot = junction.transform.rotation * Quaternion.Euler(0, turnAngle, 0);
            currentTrackRotation = newRot;

            TrackChunk currentJunctionChunk = junction.GetComponentInParent<TrackChunk>();

            // Recycle all old chunks EXCEPT the junction chunk the player is currently standing on!
            List<TrackChunk> chunksToRecycle = new List<TrackChunk>();
            while (activeChunks.Count > 0)
            {
                TrackChunk oldChunk = activeChunks.Dequeue();
                if (oldChunk != currentJunctionChunk)
                {
                    chunksToRecycle.Add(oldChunk);
                }
            }

            foreach (var oldChunk in chunksToRecycle)
            {
                oldChunk.gameObject.SetActive(false);
                if (!chunkPools.ContainsKey(oldChunk.Type))
                {
                    chunkPools[oldChunk.Type] = new Queue<TrackChunk>();
                }
                chunkPools[oldChunk.Type].Enqueue(oldChunk);
            }

            // Keep the junction chunk active at the head of activeChunks so the floor doesn't disappear!
            if (currentJunctionChunk != null)
            {
                activeChunks.Enqueue(currentJunctionChunk);
            }

            // The branch floor on the junction chunk extends to 10m from junction center
            // Spawning at 10.0m ensures a 100% seamless transition with the new corridor chunks
            Vector3 branchDir = newRot * Vector3.forward;
            nextSpawnPosition = junction.GetSnapCenter() + (branchDir * 10.0f);

            // Populate the new corridor with full activeChunkCount (28 chunks)
            // First 4 guaranteed straight, followed by normal runner chunks
            for (int i = 0; i < activeChunkCount; i++)
            {
                ChunkType type = (i < 4) ? ChunkType.Straight : SelectNextValidChunkType();
                SpawnChunkOfType(type);
            }
        }

        private TrackChunk GetPrefabForType(ChunkType type)
        {
            switch (type)
            {
                case ChunkType.Straight: return straightPrefab;
                case ChunkType.Gap: return gapPrefab;
                case ChunkType.LowObstacle: return lowObstaclePrefab;
                case ChunkType.SlideArch: return slideArchPrefab;
                case ChunkType.LaneBlocker: return laneBlockerPrefab;
                case ChunkType.TJunctionLeft: return tJunctionLeftPrefab;
                case ChunkType.TJunctionRight: return tJunctionRightPrefab;
                case ChunkType.TJunctionDouble: return tJunctionDoublePrefab;
                case ChunkType.CoinRun: return coinRunPrefab;
                case ChunkType.HeartRun: return null;
                default: return straightPrefab;
            }
        }

        #region Procedural Fallback Builder (Temple Stone Pathway & Sockets)
        private Material trackMatCache;
        private Material curbMatCache;
        private Material jungleGroundMatCache;
        private Material obstacleMatCache;
        private Material coinMatCache;
        private Material sidewalkMatCache;
        private Material deadTreeMatCache;
        private Material mossyStoneMatCache;
        private Material skullMatCache;
        private Material treeBranchJumpMatCache;
        private Material treeBranchJumpMat0;
        private Material treeBranchJumpMat1;
        private Material treeBranchJumpMat2;
        private Material treeBranchSlideMatCache;
        private Material gravestoneMatCache;
        private Material monsteraMatCache;
        private Material canopyBarkMatCache;
        private Material canopyLeavesMatCache;
        private Material rockyPathMatCache;
        private Material lowPolyRoadMatCache;
        private Material stoneGateMatCache;
        private Material torchMatCache;
        private Material torchFlameMatCache;

        private GameObject pathSidewalkPrefab;
        private GameObject mossyStonePrefab;
        private GameObject deadTreePrefab;
        private GameObject gravestonePrefab;
        private GameObject skullPrefab;
        private GameObject monsteraTreePrefab;
        private GameObject pineTreePrefab;
        private GameObject treeBranchJumpPrefab;
        private GameObject treeBranchSlidePrefab;
        private GameObject rockyPathPrefab;
        private GameObject lowPolyRoadPrefab;
        private GameObject stoneGatePrefab;
        private GameObject torchBrazierPrefab;

        private void Ensure3DModels()
        {
            if (pathSidewalkPrefab == null)
            {
                pathSidewalkPrefab = Resources.Load<GameObject>("Path/path_sidewalk");
                #if UNITY_EDITOR
                if (pathSidewalkPrefab == null)
                    pathSidewalkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/path_sidewalk.obj");
                #endif
            }
            if (rockyPathPrefab == null)
            {
                rockyPathPrefab = Resources.Load<GameObject>("Path/rocky_path");
                #if UNITY_EDITOR
                if (rockyPathPrefab == null)
                    rockyPathPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/rocky_path.obj");
                #endif
                if (rockyPathPrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Rocky Path Model Loaded Successfully: " + rockyPathPrefab.name);
                }
            }
            if (lowPolyRoadPrefab == null)
            {
                lowPolyRoadPrefab = Resources.Load<GameObject>("Path/low_poly_road");
                #if UNITY_EDITOR
                if (lowPolyRoadPrefab == null)
                    lowPolyRoadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Path/low_poly_road.obj");
                #endif
                if (lowPolyRoadPrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Low-Poly Road Model Loaded Successfully: " + lowPolyRoadPrefab.name);
                }
            }
            if (stoneGatePrefab == null)
            {
                stoneGatePrefab = Resources.Load<GameObject>("Environment/stone_gate");
                #if UNITY_EDITOR
                if (stoneGatePrefab == null)
                    stoneGatePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/stone_gate.obj");
                #endif
                if (stoneGatePrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Ancient Stone Gate Model Loaded Successfully: " + stoneGatePrefab.name);
                }
            }
            if (mossyStonePrefab == null)
            {
                mossyStonePrefab = Resources.Load<GameObject>("Obstacles/mossy_stone");
                #if UNITY_EDITOR
                if (mossyStonePrefab == null)
                    mossyStonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/mossy_stone.obj");
                #endif
            }
            if (deadTreePrefab == null)
            {
                deadTreePrefab = Resources.Load<GameObject>("Obstacles/dead_tree_obstacle");
                #if UNITY_EDITOR
                if (deadTreePrefab == null)
                    deadTreePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/dead_tree_obstacle.obj");
                #endif
            }
            if (gravestonePrefab == null)
            {
                gravestonePrefab = Resources.Load<GameObject>("Obstacles/gravestone_obstacle");
                #if UNITY_EDITOR
                if (gravestonePrefab == null)
                    gravestonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/gravestone_obstacle.obj");
                #endif
            }
            if (skullPrefab == null)
            {
                skullPrefab = Resources.Load<GameObject>("Obstacles/skull_obstacle");
                #if UNITY_EDITOR
                if (skullPrefab == null)
                    skullPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/skull_obstacle.obj");
                #endif
            }
            if (monsteraTreePrefab == null)
            {
                monsteraTreePrefab = Resources.Load<GameObject>("Environment/monstera3");
                #if UNITY_EDITOR
                if (monsteraTreePrefab == null)
                    monsteraTreePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/monstera-tree/source/monstera3.fbx");
                #endif
            }
            if (pineTreePrefab == null)
            {
                pineTreePrefab = Resources.Load<GameObject>("Environment/Tree");
                #if UNITY_EDITOR
                if (pineTreePrefab == null)
                    pineTreePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Jungle/pine-tree/source/Tree.fbx");
                #endif
            }
            if (treeBranchJumpPrefab == null)
            {
                treeBranchJumpPrefab = Resources.Load<GameObject>("Prefabs/Obstacle_TreeBranch_Jump");
                if (treeBranchJumpPrefab == null)
                    treeBranchJumpPrefab = Resources.Load<GameObject>("Obstacles/tree_branch_jump");
                #if UNITY_EDITOR
                if (treeBranchJumpPrefab == null)
                    treeBranchJumpPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_jump.obj");
                if (treeBranchJumpPrefab == null)
                    treeBranchJumpPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Obstacles/tree_branch_jump.obj");
                if (treeBranchJumpPrefab == null)
                    treeBranchJumpPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_jump.glb");
                #endif
                if (treeBranchJumpPrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Tree Branch Jump Model Loaded Successfully: " + treeBranchJumpPrefab.name);
                }
            }
            if (treeBranchSlidePrefab == null)
            {
                treeBranchSlidePrefab = Resources.Load<GameObject>("Prefabs/Obstacle_TreeBranch_Slide");
                if (treeBranchSlidePrefab == null)
                    treeBranchSlidePrefab = Resources.Load<GameObject>("Obstacles/tree_branch_slide");
                #if UNITY_EDITOR
                if (treeBranchSlidePrefab == null)
                    treeBranchSlidePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_slide.obj");
                if (treeBranchSlidePrefab == null)
                    treeBranchSlidePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Obstacles/tree_branch_slide.obj");
                if (treeBranchSlidePrefab == null)
                    treeBranchSlidePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Obstacles/tree_branch_slide.glb");
                #endif
                if (treeBranchSlidePrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Tree Branch Slide Model Loaded Successfully: " + treeBranchSlidePrefab.name);
                }
            }
            if (torchBrazierPrefab == null)
            {
                torchBrazierPrefab = Resources.Load<GameObject>("Environment/torch_brazier");
                #if UNITY_EDITOR
                if (torchBrazierPrefab == null)
                    torchBrazierPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/torch_brazier.obj");
                if (torchBrazierPrefab == null)
                    torchBrazierPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Environment/torch_brazier.obj");
                #endif
                if (torchBrazierPrefab != null)
                {
                    Debug.Log("[TrackManager] 3D Torch Brazier Model Loaded Successfully: " + torchBrazierPrefab.name);
                }
            }
        }

        private void EnsureMaterials()
        {
            if (trackMatCache == null)
            {
                trackMatCache = Resources.Load<Material>("Materials/Mat_Track");
                #if UNITY_EDITOR
                if (trackMatCache == null)
                    trackMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Track.mat");
                #endif
                if (trackMatCache == null)
                {
                    Texture2D trackTex = Resources.Load<Texture2D>("Textures/Tex_Track");
                    #if UNITY_EDITOR
                    if (trackTex == null)
                        trackTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Track.png");
                    #endif
                    trackMatCache = MaterialHelper.CreateSafeMaterial(new Color(1.05f, 1.02f, 0.96f), trackTex);
                }
            }

            if (sidewalkMatCache == null)
            {
                sidewalkMatCache = Resources.Load<Material>("Materials/Mat_Sidewalk");
                #if UNITY_EDITOR
                if (sidewalkMatCache == null)
                    sidewalkMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Sidewalk.mat");
                #endif
                if (sidewalkMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Path/sidewalk_tex_0");
                    Texture2D norm = Resources.Load<Texture2D>("Path/sidewalk_tex_2");
                    sidewalkMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
                    if (sidewalkMatCache != null && norm != null && sidewalkMatCache.HasProperty("_BumpMap"))
                    {
                        sidewalkMatCache.SetTexture("_BumpMap", norm);
                        sidewalkMatCache.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            if (deadTreeMatCache == null)
            {
                deadTreeMatCache = Resources.Load<Material>("Materials/Mat_DeadTree");
                #if UNITY_EDITOR
                if (deadTreeMatCache == null)
                    deadTreeMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_DeadTree.mat");
                #endif
                if (deadTreeMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/dead_tree_tex_0");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/dead_tree_tex_0.png");
                    #endif
                    deadTreeMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.78f, 0.70f), diff);

                    Texture2D norm = Resources.Load<Texture2D>("Obstacles/dead_tree_tex_2");
                    #if UNITY_EDITOR
                    if (norm == null)
                        norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/dead_tree_tex_2.png");
                    #endif
                    if (deadTreeMatCache != null && norm != null && deadTreeMatCache.HasProperty("_BumpMap"))
                    {
                        deadTreeMatCache.SetTexture("_BumpMap", norm);
                        deadTreeMatCache.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            if (mossyStoneMatCache == null)
            {
                mossyStoneMatCache = Resources.Load<Material>("Materials/Mat_MossyStone");
                #if UNITY_EDITOR
                if (mossyStoneMatCache == null)
                    mossyStoneMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_MossyStone.mat");
                #endif
                if (mossyStoneMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/mossy_stone_tex_0");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/mossy_stone_tex_0.png");
                    #endif
                    mossyStoneMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
                }
            }

            if (skullMatCache == null)
            {
                skullMatCache = Resources.Load<Material>("Materials/Mat_Skull");
                #if UNITY_EDITOR
                if (skullMatCache == null)
                    skullMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Skull.mat");
                #endif
                if (skullMatCache == null)
                {
                    Texture2D skullDiff = Resources.Load<Texture2D>("Obstacles/skull_tex_0");
                    #if UNITY_EDITOR
                    if (skullDiff == null)
                        skullDiff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/skull_tex_0.png");
                    #endif
                    skullMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.50f, 0.47f, 0.42f), skullDiff);

                    Texture2D skullNorm = Resources.Load<Texture2D>("Obstacles/skull_tex_2");
                    #if UNITY_EDITOR
                    if (skullNorm == null)
                        skullNorm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/skull_tex_2.png");
                    #endif
                    if (skullMatCache != null && skullNorm != null && skullMatCache.HasProperty("_BumpMap"))
                    {
                        skullMatCache.SetTexture("_BumpMap", skullNorm);
                        skullMatCache.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            if (treeBranchJumpMat0 == null)
            {
                treeBranchJumpMat0 = Resources.Load<Material>("Materials/Mat_TreeBranch_Jump_0");
                #if UNITY_EDITOR
                if (treeBranchJumpMat0 == null)
                    treeBranchJumpMat0 = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_0.mat");
                #endif
                if (treeBranchJumpMat0 == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/tree_branch_jump_tex_0");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_jump_tex_0.jpg");
                    #endif
                    treeBranchJumpMat0 = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.88f, 0.82f), diff);
                }
            }

            if (treeBranchJumpMat1 == null)
            {
                treeBranchJumpMat1 = Resources.Load<Material>("Materials/Mat_TreeBranch_Jump_1");
                #if UNITY_EDITOR
                if (treeBranchJumpMat1 == null)
                    treeBranchJumpMat1 = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_1.mat");
                #endif
                if (treeBranchJumpMat1 == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/tree_branch_jump_tex_1");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_jump_tex_1.jpg");
                    #endif
                    treeBranchJumpMat1 = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.88f, 0.82f), diff);
                }
            }

            if (treeBranchJumpMat2 == null)
            {
                treeBranchJumpMat2 = Resources.Load<Material>("Materials/Mat_TreeBranch_Jump_2");
                #if UNITY_EDITOR
                if (treeBranchJumpMat2 == null)
                    treeBranchJumpMat2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_2.mat");
                #endif
                if (treeBranchJumpMat2 == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/tree_branch_jump_tex_2");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_jump_tex_2.jpg");
                    #endif
                    treeBranchJumpMat2 = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.88f, 0.82f), diff);
                }
            }

            if (treeBranchJumpMatCache == null)
            {
                treeBranchJumpMatCache = treeBranchJumpMat0;
            }

            if (treeBranchSlideMatCache == null)
            {
                treeBranchSlideMatCache = Resources.Load<Material>("Materials/Mat_TreeBranch_Slide");
                #if UNITY_EDITOR
                if (treeBranchSlideMatCache == null)
                    treeBranchSlideMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Slide.mat");
                #endif
                if (treeBranchSlideMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/tree_branch_slide_tex_0");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_slide_tex_0.png");
                    #endif
                    treeBranchSlideMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.90f, 0.85f), diff);

                    Texture2D norm = Resources.Load<Texture2D>("Obstacles/tree_branch_slide_tex_2");
                    #if UNITY_EDITOR
                    if (norm == null)
                        norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/tree_branch_slide_tex_2.png");
                    #endif
                    if (treeBranchSlideMatCache != null && norm != null && treeBranchSlideMatCache.HasProperty("_BumpMap"))
                    {
                        treeBranchSlideMatCache.SetTexture("_BumpMap", norm);
                        treeBranchSlideMatCache.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            if (gravestoneMatCache == null)
            {
                gravestoneMatCache = Resources.Load<Material>("Materials/Mat_Gravestone");
                #if UNITY_EDITOR
                if (gravestoneMatCache == null)
                    gravestoneMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Gravestone.mat");
                #endif
                if (gravestoneMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Obstacles/gravestone_tex_0");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Obstacles/gravestone_tex_0.png");
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Obstacles/gravestone_tex_0.png");
                    #endif
                    gravestoneMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.48f, 0.45f, 0.40f), diff);
                }
            }

            if (curbMatCache == null)
            {
                curbMatCache = Resources.Load<Material>("Materials/Mat_Curb");
                #if UNITY_EDITOR
                if (curbMatCache == null)
                    curbMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Curb.mat");
                #endif
                if (curbMatCache == null)
                {
                    Texture2D curbTex = Resources.Load<Texture2D>("Textures/Tex_TempleCurb") ?? Resources.Load<Texture2D>("Textures/Tex_Curb");
                    #if UNITY_EDITOR
                    if (curbTex == null)
                        curbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_TempleCurb.jpg") 
                               ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Curb.png");
                    #endif
                    curbMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.90f, 0.86f, 0.80f), curbTex);
                    if (curbMatCache != null && curbTex != null)
                    {
                        curbMatCache.mainTextureScale = new Vector2(1.0f, 3.0f);
                    }
                }
            }

            if (jungleGroundMatCache == null)
            {
                jungleGroundMatCache = Resources.Load<Material>("Materials/Mat_JungleGround");
                #if UNITY_EDITOR
                if (jungleGroundMatCache == null)
                    jungleGroundMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_JungleGround.mat");
                #endif
                if (jungleGroundMatCache == null)
                {
                    Texture2D gTex = Resources.Load<Texture2D>("Textures/Tex_JungleGround");
                    #if UNITY_EDITOR
                    if (gTex == null)
                        gTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_JungleGround.jpg");
                    #endif
                    jungleGroundMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.95f, 0.82f), gTex);
                    if (jungleGroundMatCache != null && gTex != null)
                    {
                        jungleGroundMatCache.mainTextureScale = new Vector2(2.0f, 4.0f);
                    }
                }
            }

            if (obstacleMatCache == null)
            {
                Texture2D obsTex = Resources.Load<Texture2D>("Textures/Tex_Obstacle");
                #if UNITY_EDITOR
                if (obsTex == null)
                    obsTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Obstacle.png");
                #endif

                obstacleMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.45f, 0.32f, 0.22f), obsTex);
                if (obstacleMatCache != null && obsTex != null)
                {
                    obstacleMatCache.mainTextureScale = new Vector2(2.0f, 2.0f);
                }
            }

            if (coinMatCache == null)
            {
                coinMatCache = Resources.Load<Material>("Materials/Mat_Coin");
                #if UNITY_EDITOR
                if (coinMatCache == null)
                    coinMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Coin.mat");
                #endif
                if (coinMatCache == null)
                {
                    Texture2D coinTex = Resources.Load<Texture2D>("Textures/Tex_Coin");
                    #if UNITY_EDITOR
                    if (coinTex == null)
                        coinTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Coin.png");
                    #endif
                    coinMatCache = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.82f, 0.15f), coinTex);
                }
            }

            if (monsteraMatCache == null)
            {
                monsteraMatCache = Resources.Load<Material>("Materials/Mat_Monstera");
                #if UNITY_EDITOR
                if (monsteraMatCache == null)
                    monsteraMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Monstera.mat");
                #endif
                if (monsteraMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Environment/monstera_cutout") ?? Resources.Load<Texture2D>("Environment/ALBEDO-monstera");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/monstera_cutout.png")
                            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/ALBEDO-monstera.png");
                    #endif
                    monsteraMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.90f, 1.05f, 0.88f), diff);
                }
            }

            if (canopyBarkMatCache == null)
            {
                Texture2D diff = Resources.Load<Texture2D>("Environment/Trank_basecolor.tga");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_basecolor.tga.png");
                #endif
                Texture2D norm = Resources.Load<Texture2D>("Environment/Trank_normal.tga");
                #if UNITY_EDITOR
                if (norm == null)
                    norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Trank_normal.tga.png");
                #endif
                canopyBarkMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.9f, 0.85f, 0.8f), diff);
                if (canopyBarkMatCache != null && norm != null && canopyBarkMatCache.HasProperty("_BumpMap"))
                {
                    canopyBarkMatCache.SetTexture("_BumpMap", norm);
                    canopyBarkMatCache.EnableKeyword("_NORMALMAP");
                }
            }

            if (canopyLeavesMatCache == null)
            {
                canopyLeavesMatCache = Resources.Load<Material>("Materials/Mat_CanopyLeaves");
                #if UNITY_EDITOR
                if (canopyLeavesMatCache == null)
                    canopyLeavesMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_CanopyLeaves.mat");
                #endif
                if (canopyLeavesMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Environment/Leavs_basecolor_cutout") ?? Resources.Load<Texture2D>("Environment/Leavs_basecolor_.tga");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Leavs_basecolor_cutout.png")
                            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Leavs_basecolor_.tga.png");
                    #endif
                    canopyLeavesMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 1.10f, 0.85f), diff);
                }
            }

            if (rockyPathMatCache == null)
            {
                Texture2D diff = Resources.Load<Texture2D>("Path/rocky_path_tex_0");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Path/rocky_path_tex_0.jpg");
                #endif
                rockyPathMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);

                Texture2D norm = Resources.Load<Texture2D>("Path/rocky_path_tex_3");
                #if UNITY_EDITOR
                if (norm == null)
                    norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Path/rocky_path_tex_3.jpg");
                #endif
                if (rockyPathMatCache != null && norm != null && rockyPathMatCache.HasProperty("_BumpMap"))
                {
                    rockyPathMatCache.SetTexture("_BumpMap", norm);
                    rockyPathMatCache.EnableKeyword("_NORMALMAP");
                }
            }

            if (lowPolyRoadMatCache == null)
            {
                Texture2D diff = Resources.Load<Texture2D>("Path/low_poly_road_tex_0");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Path/low_poly_road_tex_0.png");
                #endif
                lowPolyRoadMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
            }

            if (stoneGateMatCache == null)
            {
                Texture2D diff = Resources.Load<Texture2D>("Environment/stone_gate_tex_0");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/stone_gate_tex_0.png");
                #endif
                stoneGateMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);

                Texture2D norm = Resources.Load<Texture2D>("Environment/stone_gate_tex_2");
                #if UNITY_EDITOR
                if (norm == null)
                    norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/stone_gate_tex_2.png");
                #endif
                if (stoneGateMatCache != null && norm != null && stoneGateMatCache.HasProperty("_BumpMap"))
                {
                    stoneGateMatCache.SetTexture("_BumpMap", norm);
                    stoneGateMatCache.EnableKeyword("_NORMALMAP");
                }
            }

            if (torchMatCache == null)
            {
                torchMatCache = Resources.Load<Material>("Materials/Mat_Torch");
                #if UNITY_EDITOR
                if (torchMatCache == null)
                    torchMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Torch.mat");
                #endif
                if (torchMatCache == null)
                {
                    Texture2D diff = Resources.Load<Texture2D>("Environment/tex_torch_albedo");
                    #if UNITY_EDITOR
                    if (diff == null)
                        diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/tex_torch_albedo.png");
                    #endif
                    Texture2D norm = Resources.Load<Texture2D>("Environment/tex_torch_normal");
                    #if UNITY_EDITOR
                    if (norm == null)
                        norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/tex_torch_normal.png");
                    #endif
                    torchMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
                    if (torchMatCache != null && norm != null && torchMatCache.HasProperty("_BumpMap"))
                    {
                        torchMatCache.SetTexture("_BumpMap", norm);
                        torchMatCache.EnableKeyword("_NORMALMAP");
                    }
                }
            }

            if (torchFlameMatCache == null)
            {
                torchFlameMatCache = Resources.Load<Material>("Materials/Mat_Torch_Flame");
                #if UNITY_EDITOR
                if (torchFlameMatCache == null)
                    torchFlameMatCache = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Torch_Flame.mat");
                #endif
                if (torchFlameMatCache == null)
                {
                    Texture2D flameDiff = Resources.Load<Texture2D>("Environment/tex_torch_flame_albedo");
                    #if UNITY_EDITOR
                    if (flameDiff == null)
                        flameDiff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/tex_torch_flame_albedo.png");
                    #endif
                    Texture2D flameEmis = Resources.Load<Texture2D>("Environment/tex_torch_flame_emissive");
                    #if UNITY_EDITOR
                    if (flameEmis == null)
                        flameEmis = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/tex_torch_flame_emissive.png");
                    #endif

                    Shader s = Shader.Find("Particles/Standard Unlit") 
                            ?? Shader.Find("Mobile/Particles/Alpha Blended") 
                            ?? Shader.Find("Unlit/Transparent") 
                            ?? Shader.Find("Standard");
                    torchFlameMatCache = MaterialHelper.CreateSafeMaterial(Color.white, flameDiff, s);
                    if (torchFlameMatCache != null)
                    {
                        torchFlameMatCache.color = new Color(1.1f, 1.0f, 0.9f);
                        if (torchFlameMatCache.HasProperty("_EmissionColor"))
                        {
                            torchFlameMatCache.EnableKeyword("_EMISSION");
                            torchFlameMatCache.SetColor("_EmissionColor", new Color(1.0f, 0.55f, 0.08f) * 3.5f);
                            if (flameEmis != null && torchFlameMatCache.HasProperty("_EmissionMap"))
                            {
                                torchFlameMatCache.SetTexture("_EmissionMap", flameEmis);
                            }
                        }
                        if (torchFlameMatCache.HasProperty("_Cull"))
                        {
                            torchFlameMatCache.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                        }
                    }
                }
            }
        }

        private void SpawnRandomPowerUp(Transform parent, Vector3 localPos)
        {
            GameObject puObj = new GameObject("PowerUp_Item");
            puObj.transform.SetParent(parent, false);
            puObj.transform.localPosition = localPos;

            var puItem = puObj.AddComponent<PowerUpItem>();
            // Equal 25% distribution across ALL 4 power-ups: Speedrun, Shield, Magnet, MultiplierFrenzy
            int rollType = Random.Range(0, 4);
            PowerUpType pType = rollType switch
            {
                0 => PowerUpType.Speedrun,        // Monster energy speedboost
                1 => PowerUpType.Shield,          // Invulnerability shield
                2 => PowerUpType.Magnet,          // 3D Pink Heart magnet
                _ => PowerUpType.MultiplierFrenzy // Radiant Frenzy Totem
            };
            puItem.Initialize(pType);
        }

        private void SpawnMysteryChest(Transform parent, Vector3 localPos)
        {
            GameObject chestObj = new GameObject("MysteryChest");
            chestObj.transform.SetParent(parent, false);
            chestObj.transform.localPosition = localPos;
            chestObj.AddComponent<Pickups.MysteryChest>();
        }

        private GameObject SpawnCoinHeart(Transform parent, Vector3 localPos)
        {
            GameObject heartObj = new GameObject("CoinHeart");
            heartObj.transform.SetParent(parent, false);
            heartObj.transform.localPosition = localPos;
            heartObj.tag = "Coin";
            var coin = heartObj.AddComponent<Pickups.Coin>();
            coin.SetOriginalLocalPosition(localPos);
            return heartObj;
        }

        private void SpawnHeart(Transform parent, Vector3 localPos)
        {
            SpawnCoinHeart(parent, localPos);
        }

        private GameObject SpawnRockObstacle(Transform parent, Vector3 localPos, Vector3 targetScale, ObstacleType obstacleType, float yRotation = 0f)
        {
            Ensure3DModels();
            EnsureMaterials();

            GameObject rockObj;
            float minY = -1.8276f;
            Vector3 meshCenter = Vector3.zero;
            Vector3 meshSize = new Vector3(2.79f, 3.665f, 2.04f);

            if (mossyStonePrefab != null)
            {
                rockObj = Instantiate(mossyStonePrefab, parent);
                rockObj.name = $"MossyRock_{obstacleType}";

                MeshFilter mf = rockObj.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    minY = mf.sharedMesh.bounds.min.y;
                    meshCenter = mf.sharedMesh.bounds.center;
                    meshSize = mf.sharedMesh.bounds.size;
                }

                if (mossyStoneMatCache != null)
                {
                    Renderer[] rList = rockObj.GetComponentsInChildren<Renderer>();
                    foreach (var r in rList)
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = mossyStoneMatCache;
                        r.sharedMaterials = mats;
                    }
                }
            }
            else
            {
                // Organic rounded mossy boulder fallback textured with temple stone (never a white cube!)
                rockObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rockObj.name = $"MossyRockPrimitive_{obstacleType}";
                rockObj.transform.SetParent(parent, false);
                Material stoneMat = mossyStoneMatCache ?? curbMatCache;
                if (stoneMat != null) rockObj.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;
                minY = -0.5f;
                meshCenter = Vector3.zero;
                meshSize = Vector3.one;
            }

            // Remove any pre-existing colliders from imported 3D asset
            foreach (var col in rockObj.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            // Apply scale and orientation
            rockObj.transform.localScale = targetScale;
            rockObj.transform.localRotation = Quaternion.Euler(0, yRotation, 0);

            // Exact mathematical placement flush on the road surface:
            // Sinking 0.02m guarantees zero floating light gaps from any camera angle.
            float flushY = (-minY * targetScale.y) - 0.02f;
            rockObj.transform.localPosition = new Vector3(localPos.x, flushY, localPos.z);

            // Add accurate BoxCollider configured for runner obstacle detection:
            // Tall vertical box ensures player CANNOT jump or slide through the rock!
            BoxCollider bc = rockObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(meshCenter.x, meshCenter.y + (meshSize.y * 0.40f), meshCenter.z);
            bc.size = new Vector3(meshSize.x * 0.90f, meshSize.y * 1.80f, meshSize.z * 0.88f);

            var obs = rockObj.AddComponent<Obstacle>();
            obs.SetObstacleType(obstacleType);
            SafeSetTag(rockObj, "Obstacle");

            return rockObj;
        }

        /// <summary>
        /// Ancient Carved Temple Pillar / Stone lane blocker.
        /// </summary>
        private GameObject SpawnGravestoneObstacle(Transform parent, Vector3 localPos, float uniformScale, ObstacleType obstacleType, float yRotation = 0f)
        {
            Ensure3DModels();
            EnsureMaterials();

            // Delegate to authentic mossy temple rock obstacle to ensure rich textures and zero white cubes
            return SpawnRockObstacle(parent, localPos, Vector3.one * uniformScale * 0.85f, obstacleType, yRotation);
        }

        /// <summary>
        /// Lane-blocker helper: spawns authentic ancient mossy stone boulders
        /// that fit the jungle temple escape aesthetic with rich textures.
        /// </summary>
        private GameObject SpawnLaneBlockerVariant(Transform parent, float laneX, float localZ)
        {
            float rotY = Random.Range(0f, 360f);
            return SpawnRockObstacle(parent, new Vector3(laneX, 0, localZ), new Vector3(0.70f, 0.70f, 0.70f), ObstacleType.LaneBlocker, rotY);
        }

        /// <summary>
        /// Overhanging jungle canopy arch built from the tree_branch_slide 3D model.
        /// Disabled to prevent floating branches in mid-air.
        /// </summary>
        private void SpawnCanopyBranchArch(Transform parent, float localZ)
        {
            // No-op: eliminates mid-air floating branches
        }

        /// <summary>
        /// Roadside tombstones disabled to eliminate random white cubes / untextured objects from the track.
        /// Atmosphere is richly decorated by temple braziers, lush trees, monstera foliage, and stone gates.
        /// </summary>
        private void SpawnRoadsideTombstones(Transform parent)
        {
            // No-op: eliminates roadside white cubes completely
        }

        private GameObject SpawnSkullJumpObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1)
        {
            return SpawnSkullJumpObstacle(parent, localPos, out _, laneMode, targetLane);
        }

        private GameObject SpawnSkullJumpObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1)
        {
            Ensure3DModels();
            EnsureMaterials();

            GameObject skullRoot = new GameObject("Obstacle_Skull_Jump");
            skullRoot.transform.SetParent(parent, false);
            skullRoot.transform.localPosition = localPos;

            // Determine lane configuration (minimized obstacle footprint):
            // laneMode: 0 = Single Lane (85%), 1 = Dual Lanes (15%), 0% all 3 lanes
            if (laneMode < 0)
            {
                int r = Random.Range(0, 100);
                if (r < 85) laneMode = 0;
                else laneMode = 1;
            }

            float[] laneXCoords = { -2.0f, 0.0f, 2.0f };
            float centerX = 0f;
            float spanWidth = 7.2f;

            List<float> skullPositionsX = new List<float>();

            if (laneMode == 0)
            {
                // Single Lane (0 = Left, 1 = Center, 2 = Right)
                if (targetLane < 0 || targetLane > 2) targetLane = Random.Range(0, 3);
                blockedMask = 1 << targetLane;
                centerX = laneXCoords[targetLane];
                spanWidth = 1.90f;
                skullPositionsX.Add(centerX);
            }
            else if (laneMode == 1)
            {
                // Dual Lanes (0 = Left + Center, 1 = Center + Right)
                if (targetLane < 0 || targetLane > 1) targetLane = Random.Range(0, 2);
                if (targetLane == 0)
                {
                    blockedMask = (1 << 0) | (1 << 1); // Left + Center
                    centerX = -1.0f;
                    skullPositionsX.Add(-2.0f);
                    skullPositionsX.Add(0.0f);
                }
                else
                {
                    blockedMask = (1 << 1) | (1 << 2); // Center + Right
                    centerX = 1.0f;
                    skullPositionsX.Add(0.0f);
                    skullPositionsX.Add(2.0f);
                }
                spanWidth = 3.90f;
            }
            else
            {
                // All 3 Lanes
                blockedMask = (1 << 0) | (1 << 1) | (1 << 2);
                centerX = 0f;
                spanWidth = 7.20f;
                skullPositionsX.Add(-2.0f);
                skullPositionsX.Add(0.0f);
                skullPositionsX.Add(2.0f);
            }

            // 1. Primary 3D jump hurdle visuals: cursed skulls and fallen branches
            // 1. Primary 3D jump hurdle visuals: authentic jungle timber log hurdles (tree_branch_jump.obj)
            GameObject jumpPrefab = treeBranchJumpPrefab ?? treeBranchSlidePrefab;
            if (jumpPrefab != null)
            {
                for (int i = 0; i < skullPositionsX.Count; i++)
                {
                    float posX = skullPositionsX[i];
                    GameObject hurdleVisual = Instantiate(jumpPrefab, skullRoot.transform);
                    hurdleVisual.name = $"Branch_JumpMesh_{i}";

                    MeshFilter mf = hurdleVisual.GetComponentInChildren<MeshFilter>();
                    float minY = -29.31f;
                    Vector3 meshCenter = new Vector3(32.22f, 0.03f, 2.28f);
                    Vector3 meshSize = new Vector3(73.78f, 58.33f, 77.55f);

                    if (mf != null && mf.sharedMesh != null)
                    {
                        minY = mf.sharedMesh.bounds.min.y;
                        meshCenter = mf.sharedMesh.bounds.center;
                        meshSize = mf.sharedMesh.bounds.size;
                    }

                    // Target obstacle height ~0.70m flush on ground
                    float targetHeight = 0.70f;
                    float scale = meshSize.y > 0 ? (targetHeight / meshSize.y) : 0.012f;

                    foreach (var col in hurdleVisual.GetComponentsInChildren<Collider>()) Destroy(col);

                    Material jumpMat = treeBranchJumpMatCache ?? deadTreeMatCache ?? curbMatCache;
                    if (jumpMat != null)
                    {
                        foreach (var r in hurdleVisual.GetComponentsInChildren<Renderer>())
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int m = 0; m < mats.Length; m++) mats[m] = jumpMat;
                            r.sharedMaterials = mats;
                        }
                    }

                    // Fallen branch lies across the lane (long axis Z -> X) with a slight natural dip
                    float rotY = 90f + Random.Range(-10f, 10f);
                    hurdleVisual.transform.localScale = Vector3.one * scale;
                    hurdleVisual.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), rotY, Random.Range(-7f, 7f));

                    // Flush placement on the road surface
                    float flushY = (-minY * scale) - 0.02f;
                    hurdleVisual.transform.localPosition = new Vector3(posX, flushY, Random.Range(-0.04f, 0.04f));
                }
            }
            else
            {
                // Fallback: horizontal fallen jungle log cylinder textured with timber bark (never a raw white cube!)
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fallback.name = "FallenJungleTimberLog";
                fallback.transform.SetParent(skullRoot.transform, false);
                fallback.transform.localPosition = new Vector3(centerX, 0.28f, 0);
                fallback.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                fallback.transform.localScale = new Vector3(0.55f, spanWidth * 0.50f, 0.55f);
                Destroy(fallback.GetComponent<Collider>());
                Material logMat = deadTreeMatCache ?? treeBranchJumpMatCache ?? curbMatCache;
                if (logMat != null) fallback.GetComponent<MeshRenderer>().sharedMaterial = logMat;
            }

            // 2. Accurate LowLog BoxCollider for Jump clearance (knee height 0.55m matching blocked lane width)
            BoxCollider sbc = skullRoot.AddComponent<BoxCollider>();
            sbc.center = new Vector3(centerX, 0.28f, 0);
            sbc.size = new Vector3(spanWidth, 0.56f, 1.1f);

            var sobs = skullRoot.AddComponent<Obstacle>();
            sobs.SetObstacleType(ObstacleType.LowLog);
            SafeSetTag(skullRoot, "Obstacle");

            return skullRoot;
        }

        private GameObject SpawnBrokenTreeSlideObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1)
        {
            return SpawnBrokenTreeSlideObstacle(parent, localPos, out _, laneMode, targetLane);
        }

        private GameObject SpawnBrokenTreeSlideObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1)
        {
            Ensure3DModels();
            EnsureMaterials();

            GameObject archParent = new GameObject("Obstacle_BrokenTree_Slide");
            archParent.transform.SetParent(parent, false);
            archParent.transform.localPosition = localPos;

            Material slideMat = deadTreeMatCache != null ? deadTreeMatCache : mossyStoneMatCache;

            // Full-Road Broken Tree Slide Hurdle:
            // Spans across all 3 lanes so player must slide under it regardless of which lane they are in
            if (laneMode < 0)
            {
                laneMode = 2; // Always full road width by default
            }

            float[] laneXCoords = { -2.0f, 0.0f, 2.0f };
            float centerX = 0f;
            float spanWidth = 8.0f;

            if (laneMode == 0)
            {
                // Single Lane (0 = Left, 1 = Center, 2 = Right)
                if (targetLane < 0 || targetLane > 2) targetLane = Random.Range(0, 3);
                blockedMask = 1 << targetLane;
                centerX = laneXCoords[targetLane];
                spanWidth = 2.40f;
            }
            else if (laneMode == 1)
            {
                // Dual Lanes (0 = Left + Center, 1 = Center + Right)
                if (targetLane < 0 || targetLane > 1) targetLane = Random.Range(0, 2);
                if (targetLane == 0)
                {
                    blockedMask = (1 << 0) | (1 << 1);
                    centerX = -1.0f;
                }
                else
                {
                    blockedMask = (1 << 1) | (1 << 2);
                    centerX = 1.0f;
                }
                spanWidth = 4.60f;
            }
            else
            {
                // All 3 Lanes (Spans full 7.6m roadway + curbs = 8.6m)
                blockedMask = (1 << 0) | (1 << 1) | (1 << 2);
                centerX = 0f;
                spanWidth = 8.6f;
            }

            // 1. 3D Broken / Fallen Tree Model for sliding (dead_tree_obstacle.obj)
            if (deadTreePrefab != null)
            {
                GameObject treeVisual = Instantiate(deadTreePrefab, archParent.transform);
                treeVisual.name = "BrokenTree_VisualMesh";

                MeshFilter mf = treeVisual.GetComponentInChildren<MeshFilter>();
                Vector3 meshSize = new Vector3(79.36f, 74.92f, 100.26f);

                if (mf != null && mf.sharedMesh != null)
                {
                    meshSize = mf.sharedMesh.bounds.size;
                }

                // Scale tree so its trunk length spans the target width across lanes (9.2m for full road)
                float targetSpan = (laneMode == 2) ? 9.2f : ((laneMode == 1) ? 5.2f : 3.0f);
                float scale = meshSize.z > 0 ? (targetSpan / meshSize.z) : 0.092f;

                foreach (var col in treeVisual.GetComponentsInChildren<Collider>()) Destroy(col);

                if (slideMat != null)
                {
                    foreach (var r in treeVisual.GetComponentsInChildren<Renderer>())
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = slideMat;
                        r.sharedMaterials = mats;
                    }
                }

                // Exact horizontal orientation across the road:
                // Quaternion.Euler(-5f, 90f, 35f) lays the trunk flat across X from curb to curb
                // Root base sits anchored on the left roadside curb at X = centerX - (targetSpan * 0.5f)
                // Across the entire roadway (all 3 lanes), the lower edge of the trunk provides a clean, open gap at Y ~ 1.20m - 1.35m
                // Providing a clean, visible gap for sliding, while blocking upright running (up to Y ~ 2.8m - 5.1m)
                treeVisual.transform.localScale = Vector3.one * scale;
                treeVisual.transform.localRotation = Quaternion.Euler(-5f, 90f, 35f);

                float startX = centerX - (targetSpan * 0.5f);
                treeVisual.transform.localPosition = new Vector3(startX, 2.50f, 0f);

                // Anchored trunk base on roadside curb connecting ground (Y=0) up to the fallen trunk
                GameObject rootAnchor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rootAnchor.name = "Trunk_RootAnchor";
                rootAnchor.transform.SetParent(archParent.transform, false);
                rootAnchor.transform.localPosition = new Vector3(startX, 1.25f, 0f);
                rootAnchor.transform.localRotation = Quaternion.Euler(0, 0, -6f);
                rootAnchor.transform.localScale = new Vector3(1.3f, 1.35f, 1.3f);
                Destroy(rootAnchor.GetComponent<Collider>());
                if (slideMat != null) rootAnchor.GetComponent<MeshRenderer>().sharedMaterial = slideMat;
            }
            else
            {
                GameObject crossTrunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                crossTrunk.name = "FallbackBrokenTrunk";
                crossTrunk.transform.SetParent(archParent.transform, false);
                crossTrunk.transform.localPosition = new Vector3(centerX, 1.85f, 0);
                crossTrunk.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                crossTrunk.transform.localScale = new Vector3(0.70f, spanWidth * 0.5f, 0.70f);
                Destroy(crossTrunk.GetComponent<Collider>());
                if (slideMat != null) crossTrunk.GetComponent<MeshRenderer>().sharedMaterial = slideMat;
            }

            // 2. SlideArch BoxCollider:
            // Center Y = 1.95m, Height = 1.45m (bounds from Y = 1.225m to 2.675m)
            // Ground clearance: 0 to 1.225m is completely OPEN for sliding!
            // Upright running: head hits at ~1.75m - 2.0m!
            BoxCollider tbc = archParent.AddComponent<BoxCollider>();
            tbc.center = new Vector3(centerX, 1.95f, 0);
            tbc.size = new Vector3(spanWidth, 1.45f, 1.2f);

            var tobs = archParent.AddComponent<Obstacle>();
            tobs.SetObstacleType(ObstacleType.SlideArch);
            SafeSetTag(archParent, "Obstacle");

            return archParent;
        }

        // Backward compatibility overloads
        private GameObject SpawnTreeBranchJumpObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1) => SpawnSkullJumpObstacle(parent, localPos, laneMode, targetLane);
        private GameObject SpawnTreeBranchJumpObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1) => SpawnSkullJumpObstacle(parent, localPos, out blockedMask, laneMode, targetLane);
        private GameObject SpawnTreeBranchSlideObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1) => SpawnBrokenTreeSlideObstacle(parent, localPos, laneMode, targetLane);
        private GameObject SpawnTreeBranchSlideObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1) => SpawnBrokenTreeSlideObstacle(parent, localPos, out blockedMask, laneMode, targetLane);

        private void SpawnRoadsideBrazier(Transform parent, Vector3 localPos)
        {
            EnsureMaterials();
            Ensure3DModels();

            GameObject brazier = new GameObject("RoadsideTorchBrazier");
            brazier.transform.SetParent(parent, false);
            brazier.transform.localPosition = localPos;

            if (torchBrazierPrefab != null)
            {
                GameObject torchVisual = Instantiate(torchBrazierPrefab, brazier.transform);
                torchVisual.name = "TorchBrazier_Visual";
                torchVisual.transform.localPosition = Vector3.zero;
                torchVisual.transform.localRotation = Quaternion.identity;
                torchVisual.transform.localScale = Vector3.one * 0.35f;

                foreach (var col in torchVisual.GetComponentsInChildren<Collider>())
                    Destroy(col);

                foreach (var r in torchVisual.GetComponentsInChildren<Renderer>())
                {
                    string rName = r.gameObject.name.ToLower();
                    if (rName.Contains("flame"))
                    {
                        if (torchFlameMatCache != null) r.sharedMaterial = torchFlameMatCache;
                    }
                    else if (rName.Contains("torch"))
                    {
                        if (torchMatCache != null) r.sharedMaterial = torchMatCache;
                    }
                    else
                    {
                        Material[] mats = r.sharedMaterials;
                        if (mats != null && mats.Length >= 2)
                        {
                            if (torchMatCache != null) mats[0] = torchMatCache;
                            if (torchFlameMatCache != null) mats[1] = torchFlameMatCache;
                            r.sharedMaterials = mats;
                        }
                        else if (torchMatCache != null)
                        {
                            r.sharedMaterial = torchMatCache;
                        }
                    }
                }
            }
            else
            {
                // Fallback procedural stone pillar & bowl
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Brazier_Pillar";
                pillar.transform.SetParent(brazier.transform, false);
                pillar.transform.localPosition = new Vector3(0, 0.35f, 0);
                pillar.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                Destroy(pillar.GetComponent<Collider>());
                if (curbMatCache != null) pillar.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bowl.name = "Brazier_Bowl";
                bowl.transform.SetParent(brazier.transform, false);
                bowl.transform.localPosition = new Vector3(0, 0.72f, 0);
                bowl.transform.localScale = new Vector3(0.50f, 0.20f, 0.50f);
                Destroy(bowl.GetComponent<Collider>());
                if (curbMatCache != null) bowl.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                GameObject fireCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fireCore.name = "FireCore";
                fireCore.transform.SetParent(brazier.transform, false);
                fireCore.transform.localPosition = new Vector3(0, 0.85f, 0);
                fireCore.transform.localScale = new Vector3(0.24f, 0.32f, 0.24f);
                Destroy(fireCore.GetComponent<Collider>());
                Material fireMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.55f, 0.05f));
                if (fireMat != null)
                {
                    fireMat.EnableKeyword("_EMISSION");
                    fireMat.SetColor("_EmissionColor", new Color(1.0f, 0.50f, 0.05f) * 2.5f);
                    fireCore.GetComponent<MeshRenderer>().sharedMaterial = fireMat;
                }
            }

            // Warm radiant fiery amber point light positioned at flame elevation
            GameObject lightObj = new GameObject("TorchLight");
            lightObj.transform.SetParent(brazier.transform, false);
            lightObj.transform.localPosition = new Vector3(0, 1.25f, 0.05f);

            Light torchLight = lightObj.AddComponent<Light>();
            torchLight.type = LightType.Point;
            torchLight.color = new Color(1.0f, 0.65f, 0.15f);
            torchLight.range = 8.5f;
            torchLight.intensity = 2.8f;
            torchLight.shadows = LightShadows.None;
        }

        private TrackChunk CreateProceduralChunk(ChunkType type)
        {
            EnsureMaterials();
            Ensure3DModels();

            GameObject chunkObj = new GameObject($"Chunk_{type}");
            chunkObj.transform.SetParent(transform);
            TrackChunk chunk = chunkObj.AddComponent<TrackChunk>();
            chunk.SetChunkType(type);

            // 1. Floor mesh & Authentic 3D Sidewalk Mesh
            if (type != ChunkType.Gap)
            {
                // Main Track Floor (7.6m wide, 10.3m long - ample room for 3 full lanes at -2.0, 0, +2.0)
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(chunkObj.transform, false);
                floor.transform.localPosition = new Vector3(0, -0.1f, 5.0f);
                floor.transform.localScale = new Vector3(7.6f, 0.2f, 10.3f);
                SafeSetTag(floor, "Ground");
                floor.GetComponent<MeshRenderer>().sharedMaterial = trackMatCache;

                bool isLeftJunc = (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionDouble);
                bool isRightJunc = (type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble);

                // Left Stone Border Curb (Only up to entrance on left-turn junctions so path is completely open)
                if (!isLeftJunc)
                {
                    GameObject leftCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftCurb.name = "LeftCurb";
                    leftCurb.transform.SetParent(chunkObj.transform, false);
                    leftCurb.transform.localPosition = new Vector3(-3.85f, 0.25f, 5.0f);
                    leftCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    leftCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                    // Left outer jungle ground (lush tropical undergrowth)
                    GameObject leftGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftGround.name = "LeftJungleGround";
                    leftGround.transform.SetParent(chunkObj.transform, false);
                    leftGround.transform.localPosition = new Vector3(-8.85f, -0.1f, 5.0f);
                    leftGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    SafeSetTag(leftGround, "Ground");
                    leftGround.GetComponent<MeshRenderer>().sharedMaterial = jungleGroundMatCache != null ? jungleGroundMatCache : curbMatCache;

                    // Roadside torch brazier
                    if (Random.value < 0.55f)
                    {
                        SpawnRoadsideBrazier(chunkObj.transform, new Vector3(-3.85f, 0.50f, 5.0f));
                    }
                }
                else
                {
                    // Entrance curb leading up to junction (Z: 0 to 2.85m)
                    GameObject leftCurbIn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftCurbIn.name = "LeftCurbEntrance";
                    leftCurbIn.transform.SetParent(chunkObj.transform, false);
                    leftCurbIn.transform.localPosition = new Vector3(-3.85f, 0.25f, 1.425f);
                    leftCurbIn.transform.localScale = new Vector3(0.7f, 0.5f, 2.85f);
                    leftCurbIn.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                }

                // Right Stone Border Curb (Only up to entrance on right-turn junctions so path is completely open)
                if (!isRightJunc)
                {
                    GameObject rightCurb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightCurb.name = "RightCurb";
                    rightCurb.transform.SetParent(chunkObj.transform, false);
                    rightCurb.transform.localPosition = new Vector3(3.85f, 0.25f, 5.0f);
                    rightCurb.transform.localScale = new Vector3(0.7f, 0.5f, 10.3f);
                    rightCurb.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                    // Right outer jungle ground (lush tropical undergrowth)
                    GameObject rightGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightGround.name = "RightJungleGround";
                    rightGround.transform.SetParent(chunkObj.transform, false);
                    rightGround.transform.localPosition = new Vector3(8.85f, -0.1f, 5.0f);
                    rightGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    SafeSetTag(rightGround, "Ground");
                    rightGround.GetComponent<MeshRenderer>().sharedMaterial = jungleGroundMatCache != null ? jungleGroundMatCache : curbMatCache;

                    // Roadside torch brazier
                    if (Random.value < 0.55f)
                    {
                        SpawnRoadsideBrazier(chunkObj.transform, new Vector3(3.85f, 0.50f, 5.0f));
                    }
                }
                else
                {
                    // Entrance curb leading up to junction (Z: 0 to 2.85m)
                    GameObject rightCurbIn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightCurbIn.name = "RightCurbEntrance";
                    rightCurbIn.transform.SetParent(chunkObj.transform, false);
                    rightCurbIn.transform.localPosition = new Vector3(3.85f, 0.25f, 1.425f);
                    rightCurbIn.transform.localScale = new Vector3(0.7f, 0.5f, 2.85f);
                    rightCurbIn.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                }
            }

            // 2. Specialized Obstacles & Junction Corridors
            if (type == ChunkType.LowObstacle)
            {
                // Authentic 3D Ancient Skull Jump Hurdle across randomized lanes (3.2m - 4.8m)
                float spawnZ = Random.Range(3.2f, 4.8f);
                SpawnSkullJumpObstacle(chunkObj.transform, new Vector3(0, 0, spawnZ), out int blockedMask);

                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                // Spawn hearts based on which lanes are blocked vs open
                for (int l = 0; l < 3; l++)
                {
                    bool isBlocked = (blockedMask & (1 << l)) != 0;
                    if (isBlocked)
                    {
                        // Arcing trajectory guiding player to jump over the skull obstacle in this lane
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.85f, spawnZ - 2.0f));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 1.65f, spawnZ)); // Peak jump heart!
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.85f, spawnZ + 2.0f));
                    }
                    else
                    {
                        // Clear ground lane: normal ground hearts
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.85f, spawnZ - 1.5f));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.85f, spawnZ + 1.5f));
                    }
                }

                // Decreased rare 5% chance to spawn a random 3D Power-Up
                if (Random.value < 0.05f)
                {
                    int puLane = Random.Range(0, 3);
                    bool puBlocked = (blockedMask & (1 << puLane)) != 0;
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[puLane], puBlocked ? 1.85f : 1.0f, spawnZ));
                }

                // Clear runway after jump hurdle to allow clean landing without secondary obstacle collisions
            }
            else if (type == ChunkType.SlideArch)
            {
                // Authentic 3D Broken Tree Overhead Slide Trunk spanning full road width across all 3 lanes (3.4f - 5.0f)
                float spawnZ = Random.Range(3.4f, 5.0f);
                SpawnBrokenTreeSlideObstacle(chunkObj.transform, new Vector3(0, 0, spawnZ), out _, 2);

                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                for (int l = 0; l < 3; l++)
                {
                    // Low hearts under the broken tree guiding player to slide across all 3 lanes
                    SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ - 1.8f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ)); // Underneath broken tree!
                    SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ + 1.8f));
                }

                // Decreased rare 5% chance to spawn a random 3D Power-Up
                if (Random.value < 0.05f)
                {
                    int puLane = Random.Range(0, 3);
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[puLane], 1.0f, spawnZ + 2.8f));
                }

                // Clear runway after slide arch to allow player to recover from crouch slide safely
            }
            else if (type == ChunkType.LaneBlocker)
            {
                // Minimized single-lane obstacle challenge:
                // Only 1 obstacle spawned at Z = 5.0m in 1 single lane, leaving 2 WIDE OPEN LANES!
                float[] laneCoords = { -2.0f, 0.0f, 2.0f };
                int blockedLane = Random.Range(0, 3);

                // 65% chance rock boulder/gravestone, 35% single-lane jump hurdle
                if (Random.value < 0.35f)
                {
                    SpawnSkullJumpObstacle(chunkObj.transform, new Vector3(0, 0, 5.0f), out _, 0, blockedLane);
                }
                else
                {
                    SpawnLaneBlockerVariant(chunkObj.transform, laneCoords[blockedLane], 5.0f);
                }

                // Place rewarding hearts through both open escape lanes for clear guidance
                for (int l = 0; l < 3; l++)
                {
                    if (l != blockedLane)
                    {
                        SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[l], 0.85f, 2.0f));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[l], 0.85f, 5.0f));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[l], 0.85f, 8.0f));
                    }
                }

                if (Random.value < 0.08f)
                {
                    int openLane = (blockedLane + 1) % 3;
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(laneCoords[openLane], 1.0f, 5.0f));
                }
            }
            else if (type == ChunkType.HeartRun)
            {
                // Dynamic randomized trails in any of the 3 lanes (-2.0m, 0.0m, +2.0m) - 100% HAZARD-FREE REWARD SPRINT!
                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                int primaryLane = Random.Range(0, 3);
                int secondaryLane = (primaryLane + Random.Range(1, 3)) % 3;

                // Long trail of 10 hearts in primary random lane
                for (int c = 0; c < 10; c++)
                {
                    float z = 0.5f + c * 1.0f;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[primaryLane], 0.85f, z));
                }

                // Secondary random lane trail of 7 hearts
                for (int c = 0; c < 7; c++)
                {
                    float z = 1.0f + c * 1.25f;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[secondaryLane], 0.85f, z));
                }

                // Decreased rare 7% chance for a random power-up
                if (Random.value < 0.07f)
                {
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[primaryLane], 1.0f, 5.0f));
                }

                // Roadside tombstones (pure decor, zero collision)
                SpawnRoadsideTombstones(chunkObj.transform);
            }
            else if (type == ChunkType.Straight)
            {
                float[] lanes = { -2.0f, 0.0f, 2.0f };

                // Clean straight runway: 0% obstacle chance, pure running freedom!
                int blockedLaneIdx = -1;

                // Choose 1 or 2 random clear lanes for hearts
                int heartLane1 = Random.Range(0, 3);
                if (heartLane1 == blockedLaneIdx) heartLane1 = (heartLane1 + 1) % 3;

                for (int c = 0; c < 6; c++)
                {
                    float z = 1.0f + c * 1.5f;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(lanes[heartLane1], 0.85f, z));
                }

                // 50% chance to also have hearts in a second clear lane
                if (Random.value < 0.50f)
                {
                    int heartLane2 = 3 - blockedLaneIdx - heartLane1;
                    if (heartLane2 >= 0 && heartLane2 < 3 && heartLane2 != blockedLaneIdx && heartLane2 != heartLane1)
                    {
                        for (int c = 0; c < 5; c++)
                        {
                            float z = 1.5f + c * 1.6f;
                            SpawnCoinHeart(chunkObj.transform, new Vector3(lanes[heartLane2], 0.85f, z));
                        }
                    }
                }

                // 7% chance to spawn a random 3D Power-Up
                if (Random.value < 0.07f)
                {
                    int puLaneIdx = Random.Range(0, 3);
                    if (puLaneIdx == blockedLaneIdx) puLaneIdx = (puLaneIdx + 1) % 3;
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(lanes[puLaneIdx], 1.0f, 5.0f));
                }
                else if (Random.value < 0.08f)
                {
                    // 8% chance to spawn an ancient Mystery Artifact Chest!
                    int chestLane = Random.Range(0, 3);
                    if (chestLane == blockedLaneIdx) chestLane = (chestLane + 1) % 3;
                    SpawnMysteryChest(chunkObj.transform, new Vector3(lanes[chestLane], 0.35f, 5.0f));
                }

                // Roadside tombstones (pure decor, zero collision)
                SpawnRoadsideTombstones(chunkObj.transform);
            }
            else if (type == ChunkType.CoinRun)
            {
                // Dynamic randomized trails in any of the 3 lanes (-2.0m, 0.0m, +2.0m)
                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                int pattern = Random.Range(0, 3);

                if (pattern == 0)
                {
                    // Full 3-Lane Spread: Hearts across all 3 lanes (Left, Center, Right)
                    for (int c = 0; c < 5; c++)
                    {
                        float z = 1.0f + c * 1.8f;
                        SpawnCoinHeart(chunkObj.transform, new Vector3(-2.0f, 0.85f, z));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(0.0f, 0.85f, z));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(2.0f, 0.85f, z));
                    }
                }
                else if (pattern == 1)
                {
                    // 2 Random Lanes filled with long heart runs
                    int lane1 = Random.Range(0, 3);
                    int lane2 = (lane1 + Random.Range(1, 3)) % 3;
                    for (int c = 0; c < 7; c++)
                    {
                        float z = 1.0f + c * 1.25f;
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[lane1], 0.85f, z));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[lane2], 0.85f, z));
                    }
                }
                else
                {
                    // S-Curve weaving trail across lanes: Left -> Center -> Right
                    for (int c = 0; c < 8; c++)
                    {
                        float z = 0.8f + c * 1.15f;
                        float laneX = (c < 3) ? -2.0f : ((c < 6) ? 0.0f : 2.0f);
                        if (Random.value < 0.5f) laneX = -laneX; // Mirror randomly
                        SpawnCoinHeart(chunkObj.transform, new Vector3(laneX, 0.85f, z));
                    }
                }

                // 6% chance to spawn a random 3D Power-Up or Mystery Chest
                if (Random.value < 0.06f)
                {
                    int puLane = Random.Range(0, 3);
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[puLane], 1.0f, 5.0f));
                }
                else if (Random.value < 0.07f)
                {
                    int chestLane = Random.Range(0, 3);
                    SpawnMysteryChest(chunkObj.transform, new Vector3(allLanes[chestLane], 0.35f, 5.0f));
                }

                // Roadside tombstones (pure decor, zero collision)
                SpawnRoadsideTombstones(chunkObj.transform);
            }
            else if (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble)
            {
                // 90-Degree T-Junction:
                // Forward dead-end wall
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "DeadEndWall";
                wall.transform.SetParent(chunkObj.transform, false);
                wall.transform.localPosition = new Vector3(0, 1.8f, 9.15f);
                wall.transform.localScale = new Vector3(7.6f, 3.6f, 0.7f);
                wall.GetComponent<MeshRenderer>().sharedMaterial = obstacleMatCache;
                var obs = wall.AddComponent<Obstacle>();
                obs.SetObstacleType(ObstacleType.DeadEndWall);

                // Junction Turn Zone
                var triggerObj = new GameObject("JunctionTrigger");
                triggerObj.transform.SetParent(chunkObj.transform, false);
                triggerObj.transform.localPosition = new Vector3(0, 0, 5.0f);
                var junc = triggerObj.AddComponent<JunctionTrigger>();

                bool canLeft = (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionDouble);
                bool canRight = (type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble);
                junc.Configure(canLeft, canRight);

                // Add glowing 3D turn arrows on the dead-end wall so player clearly sees available turn directions
                GameObject signBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                signBoard.name = "TurnSignBoard";
                signBoard.transform.SetParent(chunkObj.transform, false);
                signBoard.transform.localPosition = new Vector3(0, 2.0f, 8.75f);
                signBoard.transform.localScale = new Vector3(3.2f, 0.9f, 0.1f);
                Destroy(signBoard.GetComponent<Collider>());
                Material signMat = MaterialHelper.CreateSafeMaterial(new Color(0.1f, 0.1f, 0.12f));
                if (signMat != null) signBoard.GetComponent<MeshRenderer>().sharedMaterial = signMat;

                Material arrowMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.85f, 0.1f));
                if (arrowMat != null)
                {
                    arrowMat.EnableKeyword("_EMISSION");
                    arrowMat.SetColor("_EmissionColor", new Color(1.0f, 0.85f, 0.1f) * 0.85f);
                }

                if (canLeft)
                {
                    GameObject leftArrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftArrow.name = "LeftTurnArrow";
                    leftArrow.transform.SetParent(signBoard.transform, false);
                    leftArrow.transform.localPosition = new Vector3(canRight ? -0.28f : 0.0f, 0, -0.6f);
                    leftArrow.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    leftArrow.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                    Destroy(leftArrow.GetComponent<Collider>());
                    if (arrowMat != null) leftArrow.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
                }

                if (canRight)
                {
                    GameObject rightArrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightArrow.name = "RightTurnArrow";
                    rightArrow.transform.SetParent(signBoard.transform, false);
                    rightArrow.transform.localPosition = new Vector3(canLeft ? 0.28f : 0.0f, 0, -0.6f);
                    rightArrow.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    rightArrow.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                    Destroy(rightArrow.GetComponent<Collider>());
                    if (arrowMat != null) rightArrow.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
                }

                // Branch Corridors extending out 90 degrees (strictly outside track |X| >= 3.85m)
                if (canLeft)
                {
                    GameObject leftFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftFloor.name = "LeftBranchFloor";
                    leftFloor.transform.SetParent(chunkObj.transform, false);
                    leftFloor.transform.localPosition = new Vector3(-7.925f, -0.1f, 5.0f);
                    leftFloor.transform.localScale = new Vector3(8.15f, 0.2f, 7.6f);
                    SafeSetTag(leftFloor, "Ground");
                    leftFloor.GetComponent<MeshRenderer>().sharedMaterial = trackMatCache;

                    // Left branch curbs (strictly outside left curb entrance)
                    GameObject bCurb1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bCurb1.name = "LeftBranchCurbTop";
                    bCurb1.transform.SetParent(chunkObj.transform, false);
                    bCurb1.transform.localPosition = new Vector3(-7.925f, 0.25f, 8.8f);
                    bCurb1.transform.localScale = new Vector3(8.15f, 0.5f, 0.7f);
                    bCurb1.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                    GameObject bCurb2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bCurb2.name = "LeftBranchCurbBottom";
                    bCurb2.transform.SetParent(chunkObj.transform, false);
                    bCurb2.transform.localPosition = new Vector3(-7.925f, 0.25f, 1.2f);
                    bCurb2.transform.localScale = new Vector3(8.15f, 0.5f, 0.7f);
                    bCurb2.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                }
                else
                {
                    // Barrier wall on non-turning left side
                    GameObject leftBarrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftBarrier.name = "LeftBarrier";
                    leftBarrier.transform.SetParent(chunkObj.transform, false);
                    leftBarrier.transform.localPosition = new Vector3(-3.85f, 1.5f, 5.0f);
                    leftBarrier.transform.localScale = new Vector3(0.7f, 3.0f, 10.3f);
                    leftBarrier.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                    var obsL = leftBarrier.AddComponent<Obstacle>();
                    obsL.SetObstacleType(ObstacleType.DeadEndWall);
                }

                if (canRight)
                {
                    GameObject rightFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightFloor.name = "RightBranchFloor";
                    rightFloor.transform.SetParent(chunkObj.transform, false);
                    rightFloor.transform.localPosition = new Vector3(7.925f, -0.1f, 5.0f);
                    rightFloor.transform.localScale = new Vector3(8.15f, 0.2f, 7.6f);
                    SafeSetTag(rightFloor, "Ground");
                    rightFloor.GetComponent<MeshRenderer>().sharedMaterial = trackMatCache;

                    // Right branch curbs (strictly outside right curb entrance)
                    GameObject bCurb1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bCurb1.name = "RightBranchCurbTop";
                    bCurb1.transform.SetParent(chunkObj.transform, false);
                    bCurb1.transform.localPosition = new Vector3(7.925f, 0.25f, 8.8f);
                    bCurb1.transform.localScale = new Vector3(8.15f, 0.5f, 0.7f);
                    bCurb1.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;

                    GameObject bCurb2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bCurb2.name = "RightBranchCurbBottom";
                    bCurb2.transform.SetParent(chunkObj.transform, false);
                    bCurb2.transform.localPosition = new Vector3(7.925f, 0.25f, 1.2f);
                    bCurb2.transform.localScale = new Vector3(8.15f, 0.5f, 0.7f);
                    bCurb2.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                }
                else
                {
                    // Barrier wall on non-turning right side
                    GameObject rightBarrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightBarrier.name = "RightBarrier";
                    rightBarrier.transform.SetParent(chunkObj.transform, false);
                    rightBarrier.transform.localPosition = new Vector3(3.85f, 1.5f, 5.0f);
                    rightBarrier.transform.localScale = new Vector3(0.7f, 3.0f, 10.3f);
                    rightBarrier.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
                    var obsR = rightBarrier.AddComponent<Obstacle>();
                    obsR.SetObstacleType(ObstacleType.DeadEndWall);
                }
            }


            // 3. Lush 3D Roadside Trees on BOTH sides outside the lane (optimized 3 trees per line for smooth 60 FPS on mobile)
            if (monsteraTreePrefab != null || pineTreePrefab != null)
            {
                bool isLeftBranch = (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionDouble);
                bool isRightBranch = (type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble);

                // Left side trees (staggered at 1.8m, 5.0m, 8.2m outside left curb)
                if (!isLeftBranch)
                {
                    float[] zOffsetsL = { 1.8f, 5.0f, 8.2f };
                    for (int i = 0; i < zOffsetsL.Length; i++)
                    {
                        float zPos = zOffsetsL[i];
                        float xPos = -5.8f - (i % 2) * 0.45f;
                        GameObject prefab = (i % 2 == 0) ?
                            (monsteraTreePrefab != null ? monsteraTreePrefab : pineTreePrefab) :
                            (pineTreePrefab != null ? pineTreePrefab : monsteraTreePrefab);

                        if (prefab != null)
                        {
                            GameObject treeL = Instantiate(prefab, chunkObj.transform);
                            treeL.name = $"JungleTree_Left_{i + 1}";
                            treeL.transform.localPosition = new Vector3(xPos, 0.0f, zPos);
                            treeL.transform.localRotation = Quaternion.Euler(0, (i * 115f) % 360f, 0);
                            float sc = (prefab == monsteraTreePrefab ? 1.35f : 1.15f) * Random.Range(0.95f, 1.15f);
                            treeL.transform.localScale = Vector3.one * sc;
                            foreach (var col in treeL.GetComponentsInChildren<Collider>()) Destroy(col);
                            foreach (var r in treeL.GetComponentsInChildren<Renderer>())
                            {
                                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            }
                            ApplyTreeMaterials(treeL, prefab == monsteraTreePrefab);
                        }
                    }
                }

                // Right side trees (staggered at 2.2m, 5.4m, 8.6m outside right curb)
                if (!isRightBranch)
                {
                    float[] zOffsetsR = { 2.2f, 5.4f, 8.6f };
                    for (int i = 0; i < zOffsetsR.Length; i++)
                    {
                        float zPos = zOffsetsR[i];
                        float xPos = 5.8f + (i % 2) * 0.45f;
                        GameObject prefab = (i % 2 == 0) ?
                            (pineTreePrefab != null ? pineTreePrefab : monsteraTreePrefab) :
                            (monsteraTreePrefab != null ? monsteraTreePrefab : pineTreePrefab);

                        if (prefab != null)
                        {
                            GameObject treeR = Instantiate(prefab, chunkObj.transform);
                            treeR.name = $"JungleTree_Right_{i + 1}";
                            treeR.transform.localPosition = new Vector3(xPos, 0.0f, zPos);
                            treeR.transform.localRotation = Quaternion.Euler(0, (i * 135f + 45f) % 360f, 0);
                            float sc = (prefab == monsteraTreePrefab ? 1.35f : 1.15f) * Random.Range(0.95f, 1.15f);
                            treeR.transform.localScale = Vector3.one * sc;
                            foreach (var col in treeR.GetComponentsInChildren<Collider>()) Destroy(col);
                            foreach (var r in treeR.GetComponentsInChildren<Renderer>())
                            {
                                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            }
                            ApplyTreeMaterials(treeR, prefab == monsteraTreePrefab);
                        }
                    }
                }
            }

            return chunk;
        }

        private void ApplyTreeMaterials(GameObject treeObj, bool isMonstera)
        {
            if (isMonstera)
            {
                if (monsteraMatCache == null) return;
                foreach (var r in treeObj.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = monsteraMatCache;
                    r.sharedMaterials = mats;
                }
            }
            else
            {
                if (canopyBarkMatCache == null && canopyLeavesMatCache == null) return;
                foreach (var r in treeObj.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++)
                    {
                        string matName = (r.sharedMaterials[m] != null) ? r.sharedMaterials[m].name.ToLower() : "";
                        if (matName.Contains("leaf") || matName.Contains("leaves") || m > 0)
                        {
                            mats[m] = canopyLeavesMatCache != null ? canopyLeavesMatCache : canopyBarkMatCache;
                        }
                        else
                        {
                            mats[m] = canopyBarkMatCache != null ? canopyBarkMatCache : canopyLeavesMatCache;
                        }
                    }
                    r.sharedMaterials = mats;
                }
            }
        }

        private static void SafeSetTag(GameObject obj, string tagName)
        {
            try
            {
                obj.tag = tagName;
            }
            catch
            {
                // Tag not registered yet in TagManager, ignored gracefully
            }
        }

        private void ApplyRoadVisual(TrackChunk chunk, float chunkDistance)
        {
            if (chunk == null || chunk.Type == ChunkType.Gap)
            {
                Transform r1 = chunk != null ? chunk.transform.Find("Road3D_Tier1") : null;
                Transform r2 = chunk != null ? chunk.transform.Find("Road3D_Tier2") : null;
                if (r1 != null) r1.gameObject.SetActive(false);
                if (r2 != null) r2.gameObject.SetActive(false);
                return;
            }

            Ensure3DModels();
            EnsureMaterials();

            Transform t1 = chunk.transform.Find("Road3D_Tier1");
            Transform t2 = chunk.transform.Find("Road3D_Tier2");

            if (chunkDistance < 1000f)
            {
                // Tier 0: Default road surface
                if (t1 != null) t1.gameObject.SetActive(false);
                if (t2 != null) t2.gameObject.SetActive(false);
            }
            else if (chunkDistance < 2000f)
            {
                // Tier 1: 3D Rocky Path (1000m - 2000m)
                if (t2 != null) t2.gameObject.SetActive(false);

                if (t1 == null && rockyPathPrefab != null)
                {
                    GameObject rObj = Instantiate(rockyPathPrefab, chunk.transform);
                    rObj.name = "Road3D_Tier1";
                    rObj.transform.localPosition = new Vector3(0, 0.02f, 5.0f);
                    rObj.transform.localRotation = Quaternion.identity;
                    rObj.transform.localScale = new Vector3(3.816f, 1.0f, 7.024f);

                    foreach (var col in rObj.GetComponentsInChildren<Collider>()) Destroy(col);

                    if (rockyPathMatCache != null)
                    {
                        foreach (var r in rObj.GetComponentsInChildren<Renderer>())
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int m = 0; m < mats.Length; m++) mats[m] = rockyPathMatCache;
                            r.sharedMaterials = mats;
                        }
                    }
                    t1 = rObj.transform;
                }

                if (t1 != null) t1.gameObject.SetActive(true);
            }
            else
            {
                // Tier 2: 3D Low-Poly Volcanic Road (2000m+)
                if (t1 != null) t1.gameObject.SetActive(false);

                if (t2 == null && lowPolyRoadPrefab != null)
                {
                    GameObject rObj = Instantiate(lowPolyRoadPrefab, chunk.transform);
                    rObj.name = "Road3D_Tier2";
                    rObj.transform.localPosition = new Vector3(0, 0.02f, 5.0f);
                    rObj.transform.localRotation = Quaternion.identity;
                    rObj.transform.localScale = new Vector3(2.533f, 1.0f, 3.336f);

                    foreach (var col in rObj.GetComponentsInChildren<Collider>()) Destroy(col);

                    if (lowPolyRoadMatCache != null)
                    {
                        foreach (var r in rObj.GetComponentsInChildren<Renderer>())
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int m = 0; m < mats.Length; m++) mats[m] = lowPolyRoadMatCache;
                            r.sharedMaterials = mats;
                        }
                    }
                    t2 = rObj.transform;
                }

                if (t2 != null) t2.gameObject.SetActive(true);
            }
        }

        private void CheckAndSpawnStoneGate(TrackChunk chunk, float chunkDistance)
        {
            if (chunk == null || chunk.IsJunction || chunk.Type == ChunkType.Gap) return;

            // Only spawn on non-obstacle running chunks for clean visibility
            bool isSafeChunk = (chunk.Type == ChunkType.Straight || chunk.Type == ChunkType.CoinRun || chunk.Type == ChunkType.HeartRun);

            // 1. Guaranteed Milestone Gate at 1000m
            if (!gateSpawnedAt1000 && chunkDistance >= 990f && chunkDistance <= 1040f && isSafeChunk)
            {
                gateSpawnedAt1000 = true;
                SpawnStoneGate(chunk, 1.0f);
                nextStoneGateDistance = chunkDistance + Random.Range(130.0f, 160.0f);
                return;
            }

            // 2. Guaranteed Milestone Gate at 2000m
            if (!gateSpawnedAt2000 && chunkDistance >= 1990f && chunkDistance <= 2040f && isSafeChunk)
            {
                gateSpawnedAt2000 = true;
                SpawnStoneGate(chunk, 1.0f);
                nextStoneGateDistance = chunkDistance + Random.Range(130.0f, 160.0f);
                return;
            }

            // 3. Periodic Gate every ~120m-160m on straight chunks
            if (chunkDistance >= nextStoneGateDistance && isSafeChunk)
            {
                SpawnStoneGate(chunk, 5.0f);
                nextStoneGateDistance = chunkDistance + Random.Range(130.0f, 170.0f);
            }
        }

        private void SpawnStoneGate(TrackChunk chunk, float localZ)
        {
            Ensure3DModels();
            EnsureMaterials();

            if (stoneGatePrefab == null) return;

            GameObject gateObj = Instantiate(stoneGatePrefab, chunk.transform);
            gateObj.name = "AncientStoneGate";
            gateObj.transform.localPosition = new Vector3(0, 0.0f, localZ);
            gateObj.transform.localRotation = Quaternion.identity;
            gateObj.transform.localScale = Vector3.one * 11.0f;

            // Destroy all colliders so central archway is completely open (zero collision)
            foreach (var col in gateObj.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            if (stoneGateMatCache != null)
            {
                foreach (var r in gateObj.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = stoneGateMatCache;
                    r.sharedMaterials = mats;
                }
            }
        }
        #endregion
    }
}

