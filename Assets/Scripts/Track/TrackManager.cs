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
        [SerializeField] private int activeChunkCount = 28;

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

            // Chunk 0 (0 - 10m): Safe straight runway with initial guide hearts
            SpawnChunkOfType(ChunkType.Straight);

            // Chunk 1 (10 - 20m): 3D Tree Branch JUMP Hurdle (LowObstacle) with arcing hearts
            SpawnChunkOfType(ChunkType.LowObstacle);

            // Chunk 2 (20 - 30m): 3D Tree Branch SLIDE Overhead Arch (SlideArch) with low slide hearts
            SpawnChunkOfType(ChunkType.SlideArch);

            // Chunk 3 (30 - 40m): 3D Tree Branch JUMP Hurdle across randomized lanes
            SpawnChunkOfType(ChunkType.LowObstacle);

            // Chunk 4 (40 - 50m): 3D Tree Branch SLIDE Overhead Arch with Power-Up
            SpawnChunkOfType(ChunkType.SlideArch);

            // Chunk 5 (50 - 60m): 10-Heart Coin/Heart Run!
            SpawnChunkOfType(ChunkType.HeartRun);

            // Chunk 6 (60 - 70m): Lane Blocker / Boulder challenge
            SpawnChunkOfType(ChunkType.LaneBlocker);

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
        /// Strict Generator Constraint Validator:
        /// 1. Temple Run 90-degree Junctions every 8-12 chunks of straight running
        /// 2. Tree Branch Jump hurdles and Slide arches are the dominant obstacle types (~64% of obstacles)
        /// 3. Jump Obstacle Spacing >= 5m * (v / 8)
        /// </summary>
        private ChunkType SelectNextValidChunkType()
        {
            float speed = GameManager.Instance != null ? GameManager.Instance.CurrentSpeed : 8.0f;
            float minJumpSpacing = 5.0f * (speed / 8.0f);

            // 1. Temple Run 90-degree Turn Junctions:
            // Spawn a junction only after 8 or more chunks of obstacle running
            if (chunksSinceLastJunction >= 8)
            {
                int juncRoll = Random.Range(0, 100);
                if (juncRoll < 35) return ChunkType.TJunctionLeft;
                else if (juncRoll < 70) return ChunkType.TJunctionRight;
                else return ChunkType.TJunctionDouble;
            }

            // 2. Obstacles: Dominant Tree Branch Jump hurdles & Tree Branch Slide arches!
            int roll = Random.Range(0, 100);
            ChunkType candidate;

            if (roll < 4) candidate = ChunkType.Straight;
            else if (roll < 12) candidate = ChunkType.CoinRun;
            else if (roll < 20) candidate = ChunkType.HeartRun;    // 8% chance for 10-heart trail!
            else if (roll < 36) candidate = ChunkType.LaneBlocker; // 16% chance for boulders
            else if (roll < 68) candidate = ChunkType.LowObstacle; // 32% chance for Tree Branch JUMP hurdle!
            else candidate = ChunkType.SlideArch;                 // 32% chance for Tree Branch SLIDE arch!

            // Spacing check: avoid two jump hurdles or two slide arches back-to-back
            if (candidate == ChunkType.LowObstacle && distanceSinceLastJump < minJumpSpacing)
            {
                return ChunkType.SlideArch;
            }
            if (candidate == ChunkType.SlideArch && distanceSinceLastSlide < minJumpSpacing)
            {
                return ChunkType.LaneBlocker;
            }

            return candidate;
        }

        private void SpawnChunkOfType(ChunkType type)
        {
            TrackChunk chunk = GetOrCreateChunk(type);

            chunk.transform.position = nextSpawnPosition;
            chunk.transform.rotation = currentTrackRotation;
            chunk.gameObject.SetActive(true);
            chunk.ResetChunk();

            if (Runner.Effects.BiomeManager.Instance != null)
            {
                var curBiome = Runner.Effects.BiomeManager.Instance.CurrentBiome;
                var floorMat = Runner.Effects.BiomeManager.Instance.GetFloorMaterialForBiome(curBiome);
                var curbMat = Runner.Effects.BiomeManager.Instance.GetCurbMaterialForBiome(curBiome);
                chunk.ApplyBiome(floorMat, curbMat);
            }

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
        private Material monsteraMatCache;
        private Material canopyBarkMatCache;
        private Material canopyLeavesMatCache;

        private GameObject pathSidewalkPrefab;
        private GameObject mossyStonePrefab;
        private GameObject deadTreePrefab;
        private GameObject gravestonePrefab;
        private GameObject skullPrefab;
        private GameObject monsteraTreePrefab;
        private GameObject pineTreePrefab;
        private GameObject treeBranchJumpPrefab;
        private GameObject treeBranchSlidePrefab;

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
                    trackMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.35f, 0.38f, 0.34f), trackTex);
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
                    deadTreeMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
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
                    mossyStoneMatCache = MaterialHelper.CreateSafeMaterial(Color.white, diff);
                }
            }

            if (skullMatCache == null)
            {
                Texture2D skullDiff = Resources.Load<Texture2D>("Obstacles/skull_tex_0");
                skullMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.9f, 0.88f, 0.82f), skullDiff);
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

            if (curbMatCache == null)
            {
                Texture2D curbTex = Resources.Load<Texture2D>("Textures/Tex_Curb");
                #if UNITY_EDITOR
                if (curbTex == null)
                    curbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Curb.png");
                #endif

                curbMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.25f, 0.27f, 0.23f), curbTex);
                if (curbMatCache != null && curbTex != null)
                {
                    curbMatCache.mainTextureScale = new Vector2(1.0f, 4.0f);
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
                Texture2D coinTex = Resources.Load<Texture2D>("Textures/Tex_Coin");
                #if UNITY_EDITOR
                if (coinTex == null)
                    coinTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Coin.png");
                #endif

                coinMatCache = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.88f, 0.20f), coinTex);
            }

            if (monsteraMatCache == null)
            {
                Texture2D diff = Resources.Load<Texture2D>("Environment/ALBEDO-monstera");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/ALBEDO-monstera.png");
                #endif
                Texture2D norm = Resources.Load<Texture2D>("Environment/SNormal-monstera3");
                #if UNITY_EDITOR
                if (norm == null)
                    norm = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/monstera-tree/textures/SNormal-monstera3.png");
                #endif
                monsteraMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.95f, 0.85f), diff);
                if (monsteraMatCache != null && norm != null && monsteraMatCache.HasProperty("_BumpMap"))
                {
                    monsteraMatCache.SetTexture("_BumpMap", norm);
                    monsteraMatCache.EnableKeyword("_NORMALMAP");
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
                Texture2D diff = Resources.Load<Texture2D>("Environment/Leavs_basecolor_.tga");
                #if UNITY_EDITOR
                if (diff == null)
                    diff = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Jungle/pine-tree/textures/Leavs_basecolor_.tga.png");
                #endif
                canopyLeavesMatCache = MaterialHelper.CreateSafeMaterial(new Color(0.85f, 0.95f, 0.85f), diff);
            }
        }

        private void SpawnRandomPowerUp(Transform parent, Vector3 localPos)
        {
            GameObject puObj = new GameObject("PowerUp_Item");
            puObj.transform.SetParent(parent, false);
            puObj.transform.localPosition = localPos;

            var puItem = puObj.AddComponent<PowerUpItem>();
            // Equal 33.3% distribution across ALL 3 power-ups: Speedrun (Monster Energy), Shield, Magnet
            int rollType = Random.Range(0, 3);
            PowerUpType pType = rollType switch
            {
                0 => PowerUpType.Speedrun, // Monster energy speedboost
                1 => PowerUpType.Shield,   // Invulnerability shield
                _ => PowerUpType.Magnet    // 3D Pink Heart magnet
            };
            puItem.Initialize(pType);
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
                rockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rockObj.name = $"MossyRockPrimitive_{obstacleType}";
                rockObj.transform.SetParent(parent, false);
                rockObj.GetComponent<MeshRenderer>().sharedMaterial = mossyStoneMatCache;
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

            // Add accurate BoxCollider configured for runner obstacle detection
            BoxCollider bc = rockObj.AddComponent<BoxCollider>();
            bc.center = meshCenter;
            bc.size = new Vector3(meshSize.x * 0.88f, meshSize.y * 0.92f, meshSize.z * 0.85f);

            var obs = rockObj.AddComponent<Obstacle>();
            obs.SetObstacleType(obstacleType);
            SafeSetTag(rockObj, "Obstacle");

            return rockObj;
        }

        private GameObject SpawnTreeBranchJumpObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1)
        {
            return SpawnTreeBranchJumpObstacle(parent, localPos, out _, laneMode, targetLane);
        }

        private GameObject SpawnTreeBranchJumpObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1)
        {
            Ensure3DModels();
            EnsureMaterials();

            GameObject rootsParent = new GameObject("Obstacle_TreeBranch_Jump");
            rootsParent.transform.SetParent(parent, false);
            rootsParent.transform.localPosition = localPos;

            // Determine lane configuration:
            // laneMode: 0 = Single Lane (45%), 1 = Dual Lanes (40%), 2 = All 3 Lanes (15%)
            if (laneMode < 0)
            {
                int r = Random.Range(0, 100);
                if (r < 45) laneMode = 0;
                else if (r < 85) laneMode = 1;
                else laneMode = 2;
            }

            float[] laneXCoords = { -2.0f, 0.0f, 2.0f };
            float centerX = 0f;
            float spanWidth = 7.2f;

            List<float> logPositionsX = new List<float>();

            if (laneMode == 0)
            {
                // Single Lane (0 = Left, 1 = Center, 2 = Right)
                if (targetLane < 0 || targetLane > 2) targetLane = Random.Range(0, 3);
                blockedMask = 1 << targetLane;
                centerX = laneXCoords[targetLane];
                spanWidth = 1.90f;
                logPositionsX.Add(centerX);
            }
            else if (laneMode == 1)
            {
                // Dual Lanes (0 = Left + Center, 1 = Center + Right)
                if (targetLane < 0 || targetLane > 1) targetLane = Random.Range(0, 2);
                if (targetLane == 0)
                {
                    blockedMask = (1 << 0) | (1 << 1); // Left + Center
                    centerX = -1.0f;
                    logPositionsX.Add(-2.0f);
                    logPositionsX.Add(0.0f);
                }
                else
                {
                    blockedMask = (1 << 1) | (1 << 2); // Center + Right
                    centerX = 1.0f;
                    logPositionsX.Add(0.0f);
                    logPositionsX.Add(2.0f);
                }
                spanWidth = 3.90f;
            }
            else
            {
                // All 3 Lanes
                blockedMask = (1 << 0) | (1 << 1) | (1 << 2);
                centerX = 0f;
                spanWidth = 7.20f;
                logPositionsX.Add(-2.0f);
                logPositionsX.Add(0.0f);
                logPositionsX.Add(2.0f);
            }

            // 1. Primary 3D Tree Branch Model for jumping (tree_branch.glb / .obj)
            if (treeBranchJumpPrefab != null)
            {
                for (int i = 0; i < logPositionsX.Count; i++)
                {
                    float logX = logPositionsX[i];
                    GameObject branchVisual = Instantiate(treeBranchJumpPrefab, rootsParent.transform);
                    branchVisual.name = $"TreeBranch_JumpMesh_{i}";
                    // The log is 1.92m along Z; rotating 90 deg around Y spans across the lane
                    float rotY = 90f + Random.Range(-6f, 6f);
                    branchVisual.transform.localPosition = new Vector3(logX, 0.02f, Random.Range(-0.08f, 0.08f));
                    branchVisual.transform.localRotation = Quaternion.Euler(0, rotY, 0);
                    branchVisual.transform.localScale = new Vector3(1.05f, 0.95f, 1.05f);

                    foreach (var col in branchVisual.GetComponentsInChildren<Collider>()) Destroy(col);

                    // Apply multi-submesh photogrammetry bark textures:
                    foreach (var r in branchVisual.GetComponentsInChildren<Renderer>())
                    {
                        string rName = r.gameObject.name.ToLower();
                        Material chosenMat = treeBranchJumpMat0;
                        if (rName.Contains("1") || rName.Contains("scan_1")) chosenMat = treeBranchJumpMat1;
                        else if (rName.Contains("2") || rName.Contains("scan_2")) chosenMat = treeBranchJumpMat2;
                        else chosenMat = treeBranchJumpMat0;

                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = chosenMat;
                        r.sharedMaterials = mats;
                    }
                }
            }
            else
            {
                // Fallback primitive log only if 3D model asset is missing
                GameObject fallbackLog = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallbackLog.name = "FallbackLog";
                fallbackLog.transform.SetParent(rootsParent.transform, false);
                fallbackLog.transform.localPosition = new Vector3(centerX, 0.22f, 0);
                fallbackLog.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                fallbackLog.transform.localScale = new Vector3(0.44f, spanWidth * 0.5f, 0.44f);
                Destroy(fallbackLog.GetComponent<Collider>());
                if (treeBranchJumpMat0 != null) fallbackLog.GetComponent<MeshRenderer>().sharedMaterial = treeBranchJumpMat0;
            }

            // 2. Accurate LowLog BoxCollider for Jump clearance (knee height 0.46m matching blocked lane width)
            BoxCollider bc = rootsParent.AddComponent<BoxCollider>();
            bc.center = new Vector3(centerX, 0.23f, 0);
            bc.size = new Vector3(spanWidth, 0.46f, 1.0f);

            var obs = rootsParent.AddComponent<Obstacle>();
            obs.SetObstacleType(ObstacleType.LowLog);
            SafeSetTag(rootsParent, "Obstacle");

            return rootsParent;
        }

        private GameObject SpawnTreeBranchSlideObstacle(Transform parent, Vector3 localPos, int laneMode = -1, int targetLane = -1)
        {
            return SpawnTreeBranchSlideObstacle(parent, localPos, out _, laneMode, targetLane);
        }

        private GameObject SpawnTreeBranchSlideObstacle(Transform parent, Vector3 localPos, out int blockedMask, int laneMode = -1, int targetLane = -1)
        {
            Ensure3DModels();
            EnsureMaterials();

            GameObject archParent = new GameObject("Obstacle_TreeBranch_Slide");
            archParent.transform.SetParent(parent, false);
            archParent.transform.localPosition = localPos;

            Material slideMat = treeBranchSlideMatCache != null ? treeBranchSlideMatCache : (deadTreeMatCache != null ? deadTreeMatCache : mossyStoneMatCache);

            // Determine lane configuration:
            // laneMode: 0 = Single Lane (45%), 1 = Dual Lanes (40%), 2 = All 3 Lanes (15%)
            if (laneMode < 0)
            {
                int r = Random.Range(0, 100);
                if (r < 45) laneMode = 0;
                else if (r < 85) laneMode = 1;
                else laneMode = 2;
            }

            float[] laneXCoords = { -2.0f, 0.0f, 2.0f };
            float centerX = 0f;
            float spanWidth = 7.2f;

            if (laneMode == 0)
            {
                // Single Lane (0 = Left, 1 = Center, 2 = Right)
                if (targetLane < 0 || targetLane > 2) targetLane = Random.Range(0, 3);
                blockedMask = 1 << targetLane;
                centerX = laneXCoords[targetLane];
                spanWidth = 1.90f;
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
                spanWidth = 3.90f;
            }
            else
            {
                // All 3 Lanes
                blockedMask = (1 << 0) | (1 << 1) | (1 << 2);
                centerX = 0f;
                spanWidth = 7.20f;
            }

            // 1. Primary 3D Tree Branch Model for sliding (tree_branch (1).glb / .obj)
            if (treeBranchSlidePrefab != null)
            {
                GameObject branchVisual = Instantiate(treeBranchSlidePrefab, archParent.transform);
                branchVisual.name = "TreeBranch_SlideMesh";

                // Scale and position the curved tree branch arch over the blocked span:
                // Original mesh: spanX = 60.14m, center = (-11.90, 5.82, -0.94)
                float scale = (laneMode == 2) ? 0.123f : ((laneMode == 1) ? 0.080f : 0.050f);
                branchVisual.transform.localScale = new Vector3(scale, scale, scale);
                // Position so the bottom of the arch clearance is Y = 1.15m:
                branchVisual.transform.localPosition = new Vector3(centerX + 11.90f * scale, 1.15f, 0);
                branchVisual.transform.localRotation = Quaternion.identity;

                foreach (var col in branchVisual.GetComponentsInChildren<Collider>()) Destroy(col);
                if (slideMat != null)
                {
                    foreach (var r in branchVisual.GetComponentsInChildren<Renderer>())
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = slideMat;
                        r.sharedMaterials = mats;
                    }
                }
            }
            else
            {
                // Fallback primitive overhead crossbar only if 3D model asset is missing
                GameObject crossBranch = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                crossBranch.name = "FallbackCrossBranch";
                crossBranch.transform.SetParent(archParent.transform, false);
                crossBranch.transform.localPosition = new Vector3(centerX, 1.55f, 0);
                crossBranch.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                crossBranch.transform.localScale = new Vector3(0.45f, spanWidth * 0.5f, 0.45f);
                Destroy(crossBranch.GetComponent<Collider>());
                if (slideMat != null) crossBranch.GetComponent<MeshRenderer>().sharedMaterial = slideMat;
            }

            // 2. SlideArch BoxCollider:
            // Center Y = 1.62m, Height = 0.92m (bounds from Y = 1.16m to 2.08m)
            // Ground clearance: 0 to 1.16m is completely OPEN for sliding!
            // Upright running: head hits at ~1.75m!
            BoxCollider bc = archParent.AddComponent<BoxCollider>();
            bc.center = new Vector3(centerX, 1.62f, 0);
            bc.size = new Vector3(spanWidth, 0.92f, 1.0f);

            var obs = archParent.AddComponent<Obstacle>();
            obs.SetObstacleType(ObstacleType.SlideArch);
            SafeSetTag(archParent, "Obstacle");

            return archParent;
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

                    // Left outer jungle ground
                    GameObject leftGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftGround.name = "LeftJungleGround";
                    leftGround.transform.SetParent(chunkObj.transform, false);
                    leftGround.transform.localPosition = new Vector3(-8.85f, -0.1f, 5.0f);
                    leftGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    SafeSetTag(leftGround, "Ground");
                    leftGround.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
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

                    // Right outer jungle ground
                    GameObject rightGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightGround.name = "RightJungleGround";
                    rightGround.transform.SetParent(chunkObj.transform, false);
                    rightGround.transform.localPosition = new Vector3(8.85f, -0.1f, 5.0f);
                    rightGround.transform.localScale = new Vector3(10.0f, 0.2f, 10.3f);
                    SafeSetTag(rightGround, "Ground");
                    rightGround.GetComponent<MeshRenderer>().sharedMaterial = curbMatCache;
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
                // Authentic 3D Tree Branch Roots Hurdle across randomized lanes and randomized Z spawn (3.2m - 4.8m)
                float spawnZ = Random.Range(3.2f, 4.8f);
                SpawnTreeBranchJumpObstacle(chunkObj.transform, new Vector3(0, 0, spawnZ), out int blockedMask);

                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                // Spawn hearts based on which lanes are blocked vs open
                for (int l = 0; l < 3; l++)
                {
                    bool isBlocked = (blockedMask & (1 << l)) != 0;
                    if (isBlocked)
                    {
                        // Arcing trajectory guiding player to jump over the tree branch hurdle in this lane
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
                // Authentic 3D Tree Branch Overhead Arch across randomized lanes and randomized Z spawn (3.4f - 5.0f)
                float spawnZ = Random.Range(3.4f, 5.0f);
                SpawnTreeBranchSlideObstacle(chunkObj.transform, new Vector3(0, 0, spawnZ), out int blockedMask);

                float[] allLanes = { -2.0f, 0.0f, 2.0f };
                for (int l = 0; l < 3; l++)
                {
                    bool isBlocked = (blockedMask & (1 << l)) != 0;
                    if (isBlocked)
                    {
                        // Low hearts under the branch guiding player to slide
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ - 1.8f));
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ)); // Underneath branch!
                        SpawnCoinHeart(chunkObj.transform, new Vector3(allLanes[l], 0.35f, spawnZ + 1.8f));
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
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[puLane], 1.0f, spawnZ + 2.8f));
                }

                // Clear runway after slide arch to allow player to recover from crouch slide safely
            }
            else if (type == ChunkType.LaneBlocker)
            {
                // Rich multi-lane formations mixing Boulders and single/dual-lane Tree Branch obstacles
                float[] laneCoords = { -2.0f, 0.0f, 2.0f };
                int pattern = Random.Range(0, 5);

                if (pattern == 0)
                {
                    // Slalom / Weave: 2 obstacles at different Z depths (Z ~3.0m and Z ~7.0m) in different lanes
                    int laneA = Random.Range(0, 3);
                    int laneB = (laneA + Random.Range(1, 3)) % 3;

                    // 50% chance obstacle A is a single-lane Tree Branch hurdle, else rock boulder
                    if (Random.value < 0.50f)
                        SpawnTreeBranchJumpObstacle(chunkObj.transform, new Vector3(0, 0, 3.2f), out _, 0, laneA);
                    else
                        SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[laneA], 0, 3.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[laneB], 0, 7.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    // Hearts in the open lane for dodging:
                    int freeLane = 3 - laneA - laneB;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 3.0f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 5.0f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 7.0f));

                    if (Random.value < 0.05f)
                    {
                        SpawnRandomPowerUp(chunkObj.transform, new Vector3(laneCoords[freeLane], 1.0f, 5.0f));
                    }
                }
                else if (pattern == 1)
                {
                    // Dual Blocker: 2 lanes blocked at Z = 5.0m, leaving 1 escape lane
                    int openLaneIdx = Random.Range(0, 3);
                    for (int i = 0; i < 3; i++)
                    {
                        if (i != openLaneIdx)
                        {
                            SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[i], 0, 5.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));
                        }
                    }

                    // Hearts leading through the open escape lane!
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLaneIdx], 0.85f, 2.0f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLaneIdx], 0.85f, 5.0f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLaneIdx], 0.85f, 8.0f));

                    if (Random.value < 0.05f)
                    {
                        SpawnRandomPowerUp(chunkObj.transform, new Vector3(laneCoords[openLaneIdx], 1.0f, 6.5f));
                    }
                }
                else if (pattern == 2)
                {
                    // Staggered: 1 lane blocked at Z = 4.0m and 1 lane blocked at Z = 8.0m
                    int blockedA = Random.Range(0, 3);
                    SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[blockedA], 0, 4.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    int blockedB = (blockedA + 1) % 3;
                    // 50% chance obstacle B is an overhead slide branch across blockedB!
                    if (Random.value < 0.50f)
                        SpawnTreeBranchSlideObstacle(chunkObj.transform, new Vector3(0, 0, 7.8f), out _, 0, blockedB);
                    else
                        SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[blockedB], 0, 8.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    int openLane = (blockedA != 1 && blockedB != 1) ? 1 : ((blockedA != 0 && blockedB != 0) ? 0 : 2);
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLane], 0.85f, 2.5f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLane], 0.85f, 5.5f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[openLane], 0.85f, 8.5f));

                    if (Random.value < 0.05f)
                    {
                        SpawnRandomPowerUp(chunkObj.transform, new Vector3(laneCoords[openLane], 1.0f, 6.0f));
                    }
                }
                else if (pattern == 3)
                {
                    // Combo Pattern 3: Single-lane Tree Branch Jump at Z ~3.5m + Rock Boulder in another lane at Z ~7.2m
                    int branchLane = Random.Range(0, 3);
                    int rockLane = (branchLane + Random.Range(1, 3)) % 3;
                    float branchZ = Random.Range(3.2f, 4.0f);

                    SpawnTreeBranchJumpObstacle(chunkObj.transform, new Vector3(0, 0, branchZ), out _, 0, branchLane);
                    SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[rockLane], 0, 7.2f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    // Arcing heart over jump lane, ground heart in open lane
                    int freeLane = 3 - branchLane - rockLane;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[branchLane], 1.65f, branchZ));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 3.5f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 7.0f));
                }
                else
                {
                    // Combo Pattern 4: Single-lane Overhead Tree Branch Slide at Z ~3.8m + Rock Boulder at Z ~7.5m
                    int slideLane = Random.Range(0, 3);
                    int rockLane = (slideLane + Random.Range(1, 3)) % 3;
                    float slideZ = Random.Range(3.4f, 4.2f);

                    SpawnTreeBranchSlideObstacle(chunkObj.transform, new Vector3(0, 0, slideZ), out _, 0, slideLane);
                    SpawnRockObstacle(chunkObj.transform, new Vector3(laneCoords[rockLane], 0, 7.5f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));

                    int freeLane = 3 - slideLane - rockLane;
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[slideLane], 0.35f, slideZ));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 3.8f));
                    SpawnCoinHeart(chunkObj.transform, new Vector3(laneCoords[freeLane], 0.85f, 7.5f));
                }
            }
            else if (type == ChunkType.HeartRun)
            {
                // Dynamic randomized trails in any of the 3 lanes (-2.0m, 0.0m, +2.0m)
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

                // Center rock obstacle to dodge
                int rockLane = 3 - primaryLane - secondaryLane;
                if (rockLane >= 0 && rockLane < 3)
                {
                    float rotY = Random.Range(0, 360f);
                    SpawnRockObstacle(chunkObj.transform, new Vector3(allLanes[rockLane], 0, 5.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, rotY);
                }

                // Decreased rare 7% chance for a random power-up
                if (Random.value < 0.07f)
                {
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[primaryLane], 1.0f, 5.0f));
                }
            }
            else if (type == ChunkType.Straight)
            {
                float[] lanes = { -2.0f, 0.0f, 2.0f };

                // 40% chance of a lone big stone boulder to dodge in a lane
                int blockedLaneIdx = -1;
                if (Random.value < 0.40f)
                {
                    blockedLaneIdx = Random.Range(0, 3);
                    SpawnRockObstacle(chunkObj.transform, new Vector3(lanes[blockedLaneIdx], 0, 5.0f), new Vector3(0.65f, 0.55f, 0.65f), ObstacleType.LaneBlocker, Random.Range(0, 360f));
                }

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

                // Decreased rare 6% chance to spawn a random 3D Power-Up
                if (Random.value < 0.06f)
                {
                    int puLaneIdx = Random.Range(0, 3);
                    if (puLaneIdx == blockedLaneIdx) puLaneIdx = (puLaneIdx + 1) % 3;
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(lanes[puLaneIdx], 1.0f, 5.0f));
                }
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

                // Decreased rare 6% chance to spawn a random 3D Power-Up
                if (Random.value < 0.06f)
                {
                    int puLane = Random.Range(0, 3);
                    SpawnRandomPowerUp(chunkObj.transform, new Vector3(allLanes[puLane], 1.0f, 5.0f));
                }
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


            // 3. Lush 3D Roadside Trees on BOTH sides outside the lane (dense jungle corridor with 8 trees per line)
            if (monsteraTreePrefab != null || pineTreePrefab != null)
            {
                bool isLeftBranch = (type == ChunkType.TJunctionLeft || type == ChunkType.TJunctionDouble);
                bool isRightBranch = (type == ChunkType.TJunctionRight || type == ChunkType.TJunctionDouble);

                // Left side trees (outside left curb at X = -5.8m to -6.8m) - Exactly 8 trees per line
                if (!isLeftBranch)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float zPos = 0.6f + i * 1.25f; // spans 0.6m to 9.35m across 10m chunk
                        float xPos = -5.8f - (i % 3) * 0.45f;
                        GameObject prefab = (i % 2 == 0) ?
                            (monsteraTreePrefab != null ? monsteraTreePrefab : pineTreePrefab) :
                            (pineTreePrefab != null ? pineTreePrefab : monsteraTreePrefab);

                        if (prefab != null)
                        {
                            GameObject treeL = Instantiate(prefab, chunkObj.transform);
                            treeL.name = $"JungleTree_Left_{i + 1}";
                            treeL.transform.localPosition = new Vector3(xPos, 0.0f, zPos);
                            treeL.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                            float sc = (prefab == monsteraTreePrefab ? 1.35f : 1.15f) * Random.Range(0.92f, 1.15f);
                            treeL.transform.localScale = Vector3.one * sc;
                            foreach (var col in treeL.GetComponentsInChildren<Collider>()) Destroy(col);
                            ApplyTreeMaterials(treeL, prefab == monsteraTreePrefab);
                        }
                    }
                }

                // Right side trees (outside right curb at X = +5.8m to +6.8m) - Exactly 8 trees per line
                if (!isRightBranch)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float zPos = 0.6f + i * 1.25f; // spans 0.6m to 9.35m across 10m chunk
                        float xPos = 5.8f + (i % 3) * 0.45f;
                        GameObject prefab = (i % 2 == 0) ?
                            (pineTreePrefab != null ? pineTreePrefab : monsteraTreePrefab) :
                            (monsteraTreePrefab != null ? monsteraTreePrefab : pineTreePrefab);

                        if (prefab != null)
                        {
                            GameObject treeR = Instantiate(prefab, chunkObj.transform);
                            treeR.name = $"JungleTree_Right_{i + 1}";
                            treeR.transform.localPosition = new Vector3(xPos, 0.0f, zPos);
                            treeR.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                            float sc = (prefab == monsteraTreePrefab ? 1.35f : 1.15f) * Random.Range(0.92f, 1.15f);
                            treeR.transform.localScale = Vector3.one * sc;
                            foreach (var col in treeR.GetComponentsInChildren<Collider>()) Destroy(col);
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
        #endregion
    }
}

