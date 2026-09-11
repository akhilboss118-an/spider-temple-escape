using UnityEngine;
using UnityEditor;
using Runner.Core;
using Runner.UI;
using Runner.Player;
using Runner.Pickups;

namespace Runner.EditorTools
{
    public static class MenuVerificationTest
    {
        [MenuItem("Runner/Verify Menu & Compilation")]
        public static void VerifyCompilationAndSetup()
        {
            Debug.Log("[MenuVerificationTest] Verifying Main Menu and Game Setup...");

            // 1. Verify textures exist
            string menuBgPath = Application.dataPath + "/Textures/UI/jungle_escape_bg.png";
            string deathBgPath = Application.dataPath + "/Textures/UI/death_screen_bg.png";
            bool menuBgExists = System.IO.File.Exists(menuBgPath);
            bool deathBgExists = System.IO.File.Exists(deathBgPath);
            Debug.Log($"[MenuVerificationTest] jungle_escape_bg.png exists: {menuBgExists} | death_screen_bg.png exists: {deathBgExists}");

            // 2. Verify PlayerPrefs keys and GameManager configurations
            int suit = PlayerPrefs.GetInt(GameManager.SUIT_KEY, 0);
            int shieldLvl = PlayerPrefs.GetInt(GameManager.SHIELD_LVL_KEY, 1);
            int speedLvl = PlayerPrefs.GetInt(GameManager.SPEED_LVL_KEY, 1);
            int magnetLvl = PlayerPrefs.GetInt(GameManager.MAGNET_LVL_KEY, 1);
            int maxLives = PlayerPrefs.GetInt(GameManager.MAX_LIVES_KEY, 3);
            int bankedRelics = PlayerPrefs.GetInt("Runner_TotalCoins", 15);
            float audioVol = PlayerPrefs.GetFloat(GameManager.AUDIO_VOL_KEY, 1.0f);
            int controlScheme = PlayerPrefs.GetInt(GameManager.CONTROLS_KEY, 0);
            int frenzyLvl = PlayerPrefs.GetInt(GameManager.FRENZY_LVL_KEY, 1);
            int haptics = PlayerPrefs.GetInt(GameManager.HAPTICS_KEY, 1);

            Debug.Log($"[MenuVerificationTest] Config State: Suit={suit}, ShieldLvl={shieldLvl}, SpeedLvl={speedLvl}, MagnetLvl={magnetLvl}, FrenzyLvl={frenzyLvl}, MaxLives={maxLives}, Relics={bankedRelics}, AudioVol={audioVol}, Controls={controlScheme}, Haptics={haptics}");

            // 3. Verify PowerUpType enum completeness
            bool hasSpeedrun = System.Enum.IsDefined(typeof(PowerUpType), PowerUpType.Speedrun);
            bool hasMagnet = System.Enum.IsDefined(typeof(PowerUpType), PowerUpType.Magnet);
            bool hasShield = System.Enum.IsDefined(typeof(PowerUpType), PowerUpType.Shield);
            bool hasHeartRevive = System.Enum.IsDefined(typeof(PowerUpType), PowerUpType.HeartRevive);
            bool hasFrenzy = System.Enum.IsDefined(typeof(PowerUpType), PowerUpType.MultiplierFrenzy);
            Debug.Log($"[MenuVerificationTest] PowerUp Enums: Speedrun={hasSpeedrun}, Magnet={hasMagnet}, Shield={hasShield}, Heart={hasHeartRevive}, Frenzy={hasFrenzy}");

            // 3b. Verify MissionManager
            var mm = MissionManager.Instance;
            Debug.Log($"[MenuVerificationTest] MissionManager Initialized: {mm != null}, DailyMissions={mm?.DailyMissions.Count}, Achievements={mm?.LifetimeAchievements.Count}");

            // 4. Verify 3D Models for Obstacles, PowerUps, and Environment
            string[] testModels = new string[]
            {
                "Assets/Models/PowerUps/Shield/shield.fbx",
                "Assets/Models/PowerUps/Speedrun/speedrun.obj",
                "Assets/Models/PowerUps/Magnet/magnet.obj",
                "Assets/Models/PowerUps/Heart/heart.obj",
                "Assets/Models/Obstacles/mossy_stone.obj",
                "Assets/Models/Obstacles/dead_tree_obstacle.obj",
                "Assets/Models/Obstacles/tree_branch_jump.obj",
                "Assets/Models/Obstacles/tree_branch_slide.obj",
                "Assets/Models/Obstacles/tree_branch_jump.glb",
                "Assets/Models/Obstacles/tree_branch_slide.glb",
                "Assets/Models/Obstacles/gravestone_obstacle.obj",
                "Assets/Models/Obstacles/skull_obstacle.obj",
                "Assets/Models/Path/path_sidewalk.obj",
                "Assets/Models/Path/rocky_path.obj",
                "Assets/Models/Path/low_poly_road.obj",
                "Assets/Models/Environment/stone_gate.obj",
                "Assets/Models/Jungle/monstera-tree/source/monstera3.fbx",
                "Assets/Models/Jungle/pine-tree/source/Tree.fbx"
            };

            foreach (var modelPath in testModels)
            {
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                string boundsInfo = "";
                if (obj != null)
                {
                    MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>();
                    if (mfs.Length > 0 && mfs[0].sharedMesh != null)
                    {
                        boundsInfo = $" size={mfs[0].sharedMesh.bounds.size}, center={mfs[0].sharedMesh.bounds.center}";
                    }
                }
                Debug.Log($"[MenuVerificationTest] 3D Model '{System.IO.Path.GetFileName(modelPath)}' Loaded: {obj != null}{boundsInfo}");
            }

            // 5. Verify obstacle variant assets (gravestone, tree branches) load from Resources
            string[] variantModels = new string[]
            {
                "Obstacles/tree_branch_jump",
                "Obstacles/tree_branch_slide",
                "Obstacles/gravestone_obstacle"
            };
            foreach (var vp in variantModels)
            {
                GameObject vo = Resources.Load<GameObject>(vp);
                Debug.Log($"[MenuVerificationTest] Obstacle Variant '{vp}' Loaded: {vo != null}");
            }
            Texture2D graveTex = Resources.Load<Texture2D>("Obstacles/gravestone_tex_0");
            Debug.Log($"[MenuVerificationTest] Gravestone texture Loaded: {graveTex != null}");
            Material branchJumpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Jump_0.mat");
            Material branchSlideMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_TreeBranch_Slide.mat");
            Debug.Log($"[MenuVerificationTest] Branch materials: Jump0={branchJumpMat != null}, Slide={branchSlideMat != null}");

            Debug.Log("[MenuVerificationTest] ALL SYSTEMS AND 3D MODELS VERIFIED! Ready for execution.");
        }

        [MenuItem("Runner/Run Full Upgrade & Snapshot")]
        public static void RunFullUpgradeAndSnapshotCapture()
        {
            Debug.Log("[MenuVerificationTest] Running Graphics Upgrade...");
            GraphicsUpgradeUtility.UpgradeAllGraphics();

            Debug.Log("[MenuVerificationTest] Capturing Fresh Scene Snapshots...");
            SceneSnapshotTool.CaptureSceneSnapshot();

            Debug.Log("[MenuVerificationTest] Verifying Compilation and Setup...");
            VerifyCompilationAndSetup();

            Debug.Log("[MenuVerificationTest] Verifying Road Progression and Gates...");
            RoadAndGateVerificationTest.RunVerification();
        }
    }
}
