using System;
using System.Collections.Generic;
using UnityEngine;
using Runner.UI;

namespace Runner.Core
{
    public enum MissionType
    {
        JumpObstacles,      // Leap over X hurdles
        SlideObstacles,     // Slide under X arches
        CollectCoins,       // Collect X coins/hearts
        DistanceMeters,     // Run X total meters
        SingleRunScore,     // Score X points in a single run
        UsePowerUps,        // Activate X power-ups
        OpenMysteryChests   // Open X mystery chests
    }

    [Serializable]
    public class MissionData
    {
        public string Id;
        public string Title;
        public string Description;
        public MissionType Type;
        public int TargetAmount;
        public int CurrentProgress;
        public int RewardCoins;
        public bool IsClaimed;
        public bool IsDaily;

        public bool IsComplete => CurrentProgress >= TargetAmount;
        public float ProgressNormalized => Mathf.Clamp01((float)CurrentProgress / Mathf.Max(1, TargetAmount));
    }

    /// <summary>
    /// Centralized Mission and Achievement Manager:
    /// - Generates 3 rotating daily missions each day (persisted by date).
    /// - Tracks permanent lifetime achievements with coin bounties.
    /// - Live in-game toast notifications when objectives are achieved.
    /// - Reward claiming with persistence in PlayerPrefs.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        private static MissionManager _instance;
        public static MissionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<MissionManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("MissionManager");
                        _instance = go.AddComponent<MissionManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private const string LAST_DAILY_DATE_KEY = "Runner_LastDailyDate";
        private const string MISSION_PROGRESS_PREFIX = "Runner_MissionProg_";
        private const string MISSION_CLAIMED_PREFIX = "Runner_MissionClaimed_";

        private List<MissionData> _dailyMissions = new List<MissionData>();
        private List<MissionData> _lifetimeAchievements = new List<MissionData>();
        private bool isInitialized = false;

        public List<MissionData> DailyMissions
        {
            get
            {
                EnsureInitialized();
                return _dailyMissions;
            }
        }

        public List<MissionData> LifetimeAchievements
        {
            get
            {
                EnsureInitialized();
                return _lifetimeAchievements;
            }
        }

        public event Action OnMissionsUpdated;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            InitializeLifetimeAchievements();
            CheckAndGenerateDailyMissions();
            LoadAllProgress();
        }

        private void InitializeLifetimeAchievements()
        {
            _lifetimeAchievements = new List<MissionData>
            {
                new MissionData
                {
                    Id = "ach_first_step",
                    Title = "First Expedition",
                    Description = "Survive a run for at least 500 meters",
                    Type = MissionType.DistanceMeters,
                    TargetAmount = 500,
                    RewardCoins = 20,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_coin_hoarder",
                    Title = "Relic Hoarder",
                    Description = "Gather a lifetime total of 250 relics",
                    Type = MissionType.CollectCoins,
                    TargetAmount = 250,
                    RewardCoins = 35,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_acrobat",
                    Title = "Jungle Acrobat",
                    Description = "Perform 40 jumps and slides across all runs",
                    Type = MissionType.JumpObstacles,
                    TargetAmount = 40,
                    RewardCoins = 30,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_power_surge",
                    Title = "Mystic Avatar",
                    Description = "Activate 25 power-ups during expeditions",
                    Type = MissionType.UsePowerUps,
                    TargetAmount = 25,
                    RewardCoins = 40,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_chest_hunter",
                    Title = "Crypt Raider",
                    Description = "Crack open 5 ancient Mystery Artifact Chests",
                    Type = MissionType.OpenMysteryChests,
                    TargetAmount = 5,
                    RewardCoins = 50,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_high_roller",
                    Title = "Temple Master",
                    Description = "Reach 50,000 score in a single expedition",
                    Type = MissionType.SingleRunScore,
                    TargetAmount = 50000,
                    RewardCoins = 60,
                    IsDaily = false
                },
                new MissionData
                {
                    Id = "ach_caverns",
                    Title = "Volcanic Trailblazer",
                    Description = "Reach 2,000 meters into the Volcanic Caverns",
                    Type = MissionType.DistanceMeters,
                    TargetAmount = 2000,
                    RewardCoins = 75,
                    IsDaily = false
                }
            };
        }

        private void CheckAndGenerateDailyMissions()
        {
            string todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string savedDate = PlayerPrefs.GetString(LAST_DAILY_DATE_KEY, "");

            int dayHash = todayStr.GetHashCode();
            var rand = new System.Random(dayHash);

            _dailyMissions = new List<MissionData>();

            // Candidate pool of daily missions
            var templates = new List<(string id, string title, string desc, MissionType type, int target, int reward)>
            {
                ("daily_jump", "High Leaper", "Leap over {0} obstacles", MissionType.JumpObstacles, 10, 15),
                ("daily_slide", "Low Glider", "Slide under {0} fallen branches", MissionType.SlideObstacles, 8, 15),
                ("daily_coins", "Relic Seeker", "Collect {0} relics in expeditions", MissionType.CollectCoins, 50, 20),
                ("daily_distance", "Long Trek", "Sprint a total of {0} meters", MissionType.DistanceMeters, 1200, 25),
                ("daily_power", "Power Rush", "Activate {0} power-ups", MissionType.UsePowerUps, 3, 20),
                ("daily_chests", "Chest Finder", "Open {0} Mystery Artifact Chests", MissionType.OpenMysteryChests, 2, 25),
                ("daily_score", "Score Chaser", "Score {0} pts in a single run", MissionType.SingleRunScore, 20000, 30)
            };

            // Pick 3 unique missions based on day hash
            var indices = new List<int>();
            for (int i = 0; i < templates.Count; i++) indices.Add(i);
            
            // Fisher-Yates shuffle with fixed day seed
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int k = rand.Next(i + 1);
                int tmp = indices[i];
                indices[i] = indices[k];
                indices[k] = tmp;
            }

            for (int i = 0; i < 3; i++)
            {
                var t = templates[indices[i]];
                string formattedDesc = string.Format(t.desc, t.target);
                _dailyMissions.Add(new MissionData
                {
                    Id = $"{t.id}_{todayStr}",
                    Title = t.title,
                    Description = formattedDesc,
                    Type = t.type,
                    TargetAmount = t.target,
                    RewardCoins = t.reward,
                    IsDaily = true
                });
            }

            if (savedDate != todayStr)
            {
                // New day! Clear previous day progress
                PlayerPrefs.SetString(LAST_DAILY_DATE_KEY, todayStr);
                PlayerPrefs.Save();
            }
        }

        private void LoadAllProgress()
        {
            foreach (var m in DailyMissions)
            {
                m.CurrentProgress = PlayerPrefs.GetInt(MISSION_PROGRESS_PREFIX + m.Id, 0);
                m.IsClaimed = PlayerPrefs.GetInt(MISSION_CLAIMED_PREFIX + m.Id, 0) == 1;
            }

            foreach (var a in LifetimeAchievements)
            {
                a.CurrentProgress = PlayerPrefs.GetInt(MISSION_PROGRESS_PREFIX + a.Id, 0);
                a.IsClaimed = PlayerPrefs.GetInt(MISSION_CLAIMED_PREFIX + a.Id, 0) == 1;
            }
        }

        public void SaveMissionProgress(MissionData m)
        {
            PlayerPrefs.SetInt(MISSION_PROGRESS_PREFIX + m.Id, m.CurrentProgress);
            PlayerPrefs.SetInt(MISSION_CLAIMED_PREFIX + m.Id, m.IsClaimed ? 1 : 0);
            PlayerPrefs.Save();
        }

        #region Progress Event Reporters
        public void ReportJump()
        {
            IncrementProgress(MissionType.JumpObstacles, 1);
        }

        public void ReportSlide()
        {
            IncrementProgress(MissionType.SlideObstacles, 1);
        }

        public void ReportCoinsCollected(int amount = 1)
        {
            IncrementProgress(MissionType.CollectCoins, amount);
        }

        public void ReportDistance(float meters)
        {
            IncrementProgress(MissionType.DistanceMeters, Mathf.FloorToInt(meters));
        }

        public void ReportPowerUpUsed()
        {
            IncrementProgress(MissionType.UsePowerUps, 1);
        }

        public void ReportChestOpened()
        {
            IncrementProgress(MissionType.OpenMysteryChests, 1);
        }

        public void ReportRunFinished(int score, float distance)
        {
            SetMaxProgress(MissionType.SingleRunScore, score);
            SetMaxProgress(MissionType.DistanceMeters, Mathf.FloorToInt(distance));
        }

        private void IncrementProgress(MissionType type, int amount)
        {
            CheckMissionList(_dailyMissions, type, amount, isIncrement: true);
            CheckMissionList(_lifetimeAchievements, type, amount, isIncrement: true);
            OnMissionsUpdated?.Invoke();
        }

        private void SetMaxProgress(MissionType type, int value)
        {
            CheckMissionList(_dailyMissions, type, value, isIncrement: false);
            CheckMissionList(_lifetimeAchievements, type, value, isIncrement: false);
            OnMissionsUpdated?.Invoke();
        }

        private void CheckMissionList(List<MissionData> list, MissionType type, int val, bool isIncrement)
        {
            foreach (var m in list)
            {
                if (m.Type != type || m.IsClaimed) continue;

                bool wasComplete = m.IsComplete;
                if (isIncrement)
                {
                    m.CurrentProgress += val;
                }
                else
                {
                    m.CurrentProgress = Mathf.Max(m.CurrentProgress, val);
                }

                SaveMissionProgress(m);

                if (!wasComplete && m.IsComplete)
                {
                    // Trigger live celebration toast in-game!
                    string badge = m.IsDaily ? "📜 DAILY MISSION" : "🏆 ACHIEVEMENT";
                    UIManager.Instance?.ShowToast($"{badge} UNLOCKED: {m.Title}!", "✨");
                    Runner.Audio.AudioManager.Instance?.PlayMissionComplete();
                }
            }
        }
        #endregion

        public bool ClaimReward(MissionData mission)
        {
            if (!mission.IsComplete || mission.IsClaimed) return false;

            mission.IsClaimed = true;
            SaveMissionProgress(mission);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(mission.RewardCoins);
            }

            UIManager.Instance?.ShowToast($"CLAIMED: +{mission.RewardCoins} RELICS!", "💎");
            Runner.Audio.AudioManager.Instance?.PlayMissionComplete();
            OnMissionsUpdated?.Invoke();
            return true;
        }

        public int GetUnclaimedRewardsCount()
        {
            int count = 0;
            foreach (var m in DailyMissions) if (m.IsComplete && !m.IsClaimed) count++;
            foreach (var a in LifetimeAchievements) if (a.IsComplete && !a.IsClaimed) count++;
            return count;
        }
    }
}
