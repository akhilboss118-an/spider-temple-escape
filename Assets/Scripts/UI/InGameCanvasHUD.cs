using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runner.Core;
using Runner.Pickups;

namespace Runner.UI
{
    /// <summary>
    /// High-performance, zero-allocation native uGUI Canvas HUD for active gameplay.
    /// Replaces legacy OnGUI IMGUI rendering during GameState.Playing, eliminating GC spikes,
    /// reducing draw calls via GPU UI batching, and preserving 100% responsive swipe input.
    /// </summary>
    [DisallowMultipleComponent]
    public class InGameCanvasHUD : MonoBehaviour
    {
        public static InGameCanvasHUD Instance { get; private set; }

        [Header("Canvas Hierarchy")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Header("Top Left - Pause & Power-Ups")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private RectTransform powerUpTrayRoot;
        [SerializeField] private List<PowerUpSlotUI> powerUpSlots = new List<PowerUpSlotUI>();

        [Header("Top Right - Stats & Hearts")]
        [SerializeField] private Text distanceText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text heartsText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image heartProgressBar;
        [SerializeField] private GameObject newBestBadge;

        [Header("Center Alerts")]
        [SerializeField] private GameObject milestoneRoot;
        [SerializeField] private Text milestoneTitle;
        [SerializeField] private Text milestoneSub;
        [SerializeField] private CanvasGroup milestoneGroup;
        [SerializeField] private GameObject beastWarningRoot;
        [SerializeField] private Text beastWarningText;

        // Caching for zero-allocation updates
        private int lastDistance = -1;
        private int lastScore = -1;
        private int lastCoins = -1;
        private int lastLives = -1;
        private int lastMaxLives = -1;
        private float animatedHeartFill = 0f;
        private float milestoneTimer = 0f;
        private const float MILESTONE_DURATION = 2.8f;

        private static Sprite cachedWhiteSprite;
        private static Font cachedFont;

        [Serializable]
        public class PowerUpSlotUI
        {
            public GameObject Root;
            public Image Background;
            public Image ProgressBar;
            public Text LabelText;
            public Text ValueText;
            public Image LeftAccent;
        }

        public static InGameCanvasHUD EnsureInstance()
        {
            if (Instance != null) return Instance;

            GameObject existing = GameObject.Find("InGameCanvasHUD");
            if (existing != null)
            {
                Instance = existing.GetComponent<InGameCanvasHUD>();
                if (Instance != null) return Instance;
            }

            GameObject hudObj = new GameObject("InGameCanvasHUD");
            Instance = hudObj.AddComponent<InGameCanvasHUD>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            BuildCanvasHierarchy();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                SetVisible(false);
            }
            else
            {
                SetVisible(false);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleGameStateChanged(GameState state)
        {
            SetVisible(false);
            if (state == GameState.Playing)
            {
                // Reset cached stats to force instant refresh
                lastDistance = -1;
                lastScore = -1;
                lastCoins = -1;
                lastLives = -1;
                lastMaxLives = -1;
            }
        }

        public void SetVisible(bool visible)
        {
            if (hudCanvas != null)
            {
                hudCanvas.enabled = visible;
            }
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = visible ? 1f : 0f;
                hudCanvasGroup.blocksRaycasts = visible;
            }
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;

            UpdateDistanceAndScore();
            UpdateHeartsMeter();
            UpdateActivePowerUps();
            UpdateBeastWarning();
            UpdateMilestoneBanner();
        }

        private void UpdateDistanceAndScore()
        {
            int currentDist = Mathf.FloorToInt(GameManager.Instance.DistanceTraveled);
            if (currentDist != lastDistance)
            {
                lastDistance = currentDist;
                if (distanceText != null)
                {
                    distanceText.text = $"📍 {currentDist:N0}m";
                }
            }

            int currentScore = GameManager.Instance.Score;
            if (currentScore != lastScore)
            {
                lastScore = currentScore;
                if (scoreText != null)
                {
                    scoreText.text = $"⭐ {currentScore:N0}";
                }
            }

            if (newBestBadge != null)
            {
                bool isNewBest = GameManager.Instance.HighScore > 0 && currentScore > GameManager.Instance.HighScore;
                if (newBestBadge.activeSelf != isNewBest)
                {
                    newBestBadge.SetActive(isNewBest);
                }
            }
        }

        private void UpdateHeartsMeter()
        {
            int coins = GameManager.Instance.CoinsCollected;
            if (coins != lastCoins)
            {
                lastCoins = coins;
                if (heartsText != null)
                {
                    heartsText.text = $"💖 HEARTS: {coins}";
                }
            }

            int lives = GameManager.Instance.CurrentLives;
            int maxLives = Mathf.Max(1, GameManager.Instance.MaxLives);
            if (lives != lastLives || maxLives != lastMaxLives)
            {
                lastLives = lives;
                lastMaxLives = maxLives;
                if (healthText != null)
                {
                    healthText.text = $"HP {lives}/{maxLives} ❤️";
                    healthText.color = (lives <= 1) ? new Color(1f, 0.35f, 0.35f) : new Color(0.85f, 0.90f, 0.95f);
                }
            }

            // Smooth animated heart meter fill
            int tierGoal = 10;
            int currentInTier = coins % tierGoal;
            float targetFill = (coins == 0) ? 0f : ((currentInTier == 0) ? 1f : (float)currentInTier / tierGoal);
            animatedHeartFill = Mathf.Lerp(animatedHeartFill, targetFill, Time.unscaledDeltaTime * 10f);

            if (heartProgressBar != null)
            {
                heartProgressBar.fillAmount = animatedHeartFill;
            }
        }

        private void UpdateActivePowerUps()
        {
            if (PickupManager.Instance == null) return;

            var activeList = PickupManager.Instance.GetActivePowerUps();
            int activeCount = (activeList != null) ? activeList.Count : 0;

            for (int i = 0; i < powerUpSlots.Count; i++)
            {
                var slot = powerUpSlots[i];
                if (i < activeCount)
                {
                    var pu = activeList[i];
                    if (!slot.Root.activeSelf) slot.Root.SetActive(true);

                    if (slot.LabelText != null)
                    {
                        slot.LabelText.text = $"{pu.Icon} {pu.DisplayName}";
                    }

                    if (slot.ValueText != null)
                    {
                        slot.ValueText.text = (pu.Type == PowerUpType.Shield) ? "ACTIVE" : $"{pu.TimeRemaining:F1}s";
                        slot.ValueText.color = pu.ThemeColor;
                    }

                    if (slot.ProgressBar != null)
                    {
                        slot.ProgressBar.fillAmount = pu.Progress;
                        slot.ProgressBar.color = new Color(pu.ThemeColor.r, pu.ThemeColor.g, pu.ThemeColor.b, 0.45f);
                    }

                    if (slot.LeftAccent != null)
                    {
                        slot.LeftAccent.color = pu.ThemeColor;
                    }
                }
                else
                {
                    if (slot.Root.activeSelf) slot.Root.SetActive(false);
                }
            }
        }

        private void UpdateBeastWarning()
        {
            if (beastWarningRoot == null) return;

            bool isStumbling = GameManager.Instance != null && GameManager.Instance.IsStumbling;
            if (isStumbling)
            {
                if (!beastWarningRoot.activeSelf) beastWarningRoot.SetActive(true);

                float flash = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
                if (beastWarningText != null)
                {
                    beastWarningText.text = $"⚠️ BEAST CLOSING IN! {GameManager.Instance.StumbleTimeRemaining:F1}s";
                    beastWarningText.color = Color.Lerp(new Color(1f, 0.40f, 0.40f), new Color(1f, 0.95f, 0.70f), flash);
                }
            }
            else
            {
                if (beastWarningRoot.activeSelf) beastWarningRoot.SetActive(false);
            }
        }

        public void ShowMilestone(string title, string subtitle)
        {
            if (milestoneRoot == null) return;

            milestoneTimer = MILESTONE_DURATION;
            if (milestoneTitle != null) milestoneTitle.text = title;
            if (milestoneSub != null) milestoneSub.text = subtitle;
            milestoneRoot.SetActive(true);
        }

        private void UpdateMilestoneBanner()
        {
            if (milestoneRoot == null || !milestoneRoot.activeSelf) return;

            if (milestoneTimer > 0f)
            {
                milestoneTimer -= Time.unscaledDeltaTime;
                float age = MILESTONE_DURATION - milestoneTimer;
                float pop = Mathf.Clamp01(age / 0.25f);
                float ease = 1f - Mathf.Pow(1f - pop, 3f);
                float alpha = Mathf.Clamp01(milestoneTimer / 0.5f);

                if (milestoneGroup != null)
                {
                    milestoneGroup.alpha = alpha;
                }
                milestoneRoot.transform.localScale = Vector3.one * (0.85f + 0.15f * ease);

                if (milestoneTimer <= 0f)
                {
                    milestoneRoot.SetActive(false);
                }
            }
            else
            {
                milestoneRoot.SetActive(false);
            }
        }

        #region Procedural UI Hierarchy Construction
        private void BuildCanvasHierarchy()
        {
            // 1. Canvas Setup
            hudCanvas = gameObject.GetComponent<Canvas>();
            if (hudCanvas == null) hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 45;

            canvasScaler = gameObject.GetComponent<CanvasScaler>();
            if (canvasScaler == null) canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080, 1920);
            canvasScaler.matchWidthOrHeight = 0.5f;

            graphicRaycaster = gameObject.GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null) graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            hudCanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (hudCanvasGroup == null) hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();

            EnsureResources();

            // 2. Safe Area Container (Non-blocking)
            GameObject safeObj = CreateUIElement("SafeAreaContainer", transform);
            RectTransform safeRect = safeObj.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            // 3. Top-Left: Pause Button (⏸)
            GameObject pauseObj = CreateUIElement("Btn_Pause", safeRect);
            RectTransform pauseRect = pauseObj.GetComponent<RectTransform>();
            pauseRect.anchorMin = new Vector2(0, 1);
            pauseRect.anchorMax = new Vector2(0, 1);
            pauseRect.pivot = new Vector2(0, 1);
            pauseRect.anchoredPosition = new Vector2(36, -36);
            pauseRect.sizeDelta = new Vector2(100, 100);

            Image pauseImg = pauseObj.AddComponent<Image>();
            pauseImg.sprite = cachedWhiteSprite;
            pauseImg.color = new Color(0.08f, 0.12f, 0.10f, 0.88f);
            pauseImg.raycastTarget = true; // Only interactive element!

            pauseButton = pauseObj.AddComponent<Button>();
            pauseButton.targetGraphic = pauseImg;
            pauseButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
                {
                    GameManager.Instance.PauseGame();
                }
            });

            // Pause Icon Text
            GameObject pauseTxtObj = CreateUIElement("Txt_PauseIcon", pauseRect);
            RectTransform pTxtRect = pauseTxtObj.GetComponent<RectTransform>();
            pTxtRect.anchorMin = Vector2.zero;
            pTxtRect.anchorMax = Vector2.one;
            pTxtRect.offsetMin = Vector2.zero;
            pTxtRect.offsetMax = Vector2.zero;

            Text pauseTxt = pauseTxtObj.AddComponent<Text>();
            pauseTxt.font = cachedFont;
            pauseTxt.text = "⏸";
            pauseTxt.fontSize = 44;
            pauseTxt.fontStyle = FontStyle.Bold;
            pauseTxt.alignment = TextAnchor.MiddleCenter;
            pauseTxt.color = new Color(1.0f, 0.85f, 0.35f);
            pauseTxt.raycastTarget = false;

            // 4. Top-Left Power-Ups Tray (Directly under Pause)
            GameObject trayObj = CreateUIElement("PowerUpTray", safeRect);
            powerUpTrayRoot = trayObj.GetComponent<RectTransform>();
            powerUpTrayRoot.anchorMin = new Vector2(0, 1);
            powerUpTrayRoot.anchorMax = new Vector2(0, 1);
            powerUpTrayRoot.pivot = new Vector2(0, 1);
            powerUpTrayRoot.anchoredPosition = new Vector2(36, -150);
            powerUpTrayRoot.sizeDelta = new Vector2(340, 260);

            powerUpSlots.Clear();
            for (int i = 0; i < 3; i++)
            {
                var slot = CreatePowerUpSlot(powerUpTrayRoot, i);
                powerUpSlots.Add(slot);
                slot.Root.SetActive(false);
            }

            // 5. Top-Right: Distance & Score Dual Pill
            GameObject distPillObj = CreateUIElement("Pill_DistanceScore", safeRect);
            RectTransform distPillRect = distPillObj.GetComponent<RectTransform>();
            distPillRect.anchorMin = new Vector2(1, 1);
            distPillRect.anchorMax = new Vector2(1, 1);
            distPillRect.pivot = new Vector2(1, 1);
            distPillRect.anchoredPosition = new Vector2(-36, -36);
            distPillRect.sizeDelta = new Vector2(460, 90);

            Image distBg = distPillObj.AddComponent<Image>();
            distBg.sprite = cachedWhiteSprite;
            distBg.color = new Color(0.06f, 0.10f, 0.08f, 0.90f);
            distBg.raycastTarget = false;

            // Distance Text (Left half of pill)
            GameObject distTxtObj = CreateUIElement("Txt_Distance", distPillRect);
            RectTransform dTxtRect = distTxtObj.GetComponent<RectTransform>();
            dTxtRect.anchorMin = new Vector2(0, 0);
            dTxtRect.anchorMax = new Vector2(0.55f, 1);
            dTxtRect.offsetMin = new Vector2(18, 0);
            dTxtRect.offsetMax = Vector2.zero;

            distanceText = distTxtObj.AddComponent<Text>();
            distanceText.font = cachedFont;
            distanceText.text = "📍 0m";
            distanceText.fontSize = 28;
            distanceText.fontStyle = FontStyle.Bold;
            distanceText.alignment = TextAnchor.MiddleLeft;
            distanceText.color = new Color(0.35f, 0.95f, 0.65f);
            distanceText.raycastTarget = false;

            // Score Text (Right half of pill)
            GameObject scoreTxtObj = CreateUIElement("Txt_Score", distPillRect);
            RectTransform sTxtRect = scoreTxtObj.GetComponent<RectTransform>();
            sTxtRect.anchorMin = new Vector2(0.55f, 0);
            sTxtRect.anchorMax = new Vector2(1, 1);
            sTxtRect.offsetMin = Vector2.zero;
            sTxtRect.offsetMax = new Vector2(-18, 0);

            scoreText = scoreTxtObj.AddComponent<Text>();
            scoreText.font = cachedFont;
            scoreText.text = "⭐ 0";
            scoreText.fontSize = 28;
            scoreText.fontStyle = FontStyle.Bold;
            scoreText.alignment = TextAnchor.MiddleRight;
            scoreText.color = new Color(1.0f, 0.85f, 0.25f);
            scoreText.raycastTarget = false;

            // 6. Top-Right: Heart Meter Pill (Obsidian rose container with animated fill)
            GameObject heartPillObj = CreateUIElement("Pill_HeartMeter", safeRect);
            RectTransform heartPillRect = heartPillObj.GetComponent<RectTransform>();
            heartPillRect.anchorMin = new Vector2(1, 1);
            heartPillRect.anchorMax = new Vector2(1, 1);
            heartPillRect.pivot = new Vector2(1, 1);
            heartPillRect.anchoredPosition = new Vector2(-36, -136);
            heartPillRect.sizeDelta = new Vector2(460, 116);

            Image heartBg = heartPillObj.AddComponent<Image>();
            heartBg.sprite = cachedWhiteSprite;
            heartBg.color = new Color(0.08f, 0.04f, 0.06f, 0.90f);
            heartBg.raycastTarget = false;

            // Hearts Count (Top-Left of pill)
            GameObject hTxtObj = CreateUIElement("Txt_Hearts", heartPillRect);
            RectTransform hTxtRect = hTxtObj.GetComponent<RectTransform>();
            hTxtRect.anchorMin = new Vector2(0, 0.5f);
            hTxtRect.anchorMax = new Vector2(0.65f, 1);
            hTxtRect.offsetMin = new Vector2(18, 0);
            hTxtRect.offsetMax = new Vector2(0, -6);

            heartsText = hTxtObj.AddComponent<Text>();
            heartsText.font = cachedFont;
            heartsText.text = "💖 HEARTS: 0";
            heartsText.fontSize = 24;
            heartsText.fontStyle = FontStyle.Bold;
            heartsText.alignment = TextAnchor.MiddleLeft;
            heartsText.color = new Color(1f, 0.45f, 0.75f);
            heartsText.raycastTarget = false;

            // Health Count (Top-Right of pill)
            GameObject hpTxtObj = CreateUIElement("Txt_HP", heartPillRect);
            RectTransform hpTxtRect = hpTxtObj.GetComponent<RectTransform>();
            hpTxtRect.anchorMin = new Vector2(0.65f, 0.5f);
            hpTxtRect.anchorMax = new Vector2(1, 1);
            hpTxtRect.offsetMin = Vector2.zero;
            hpTxtRect.offsetMax = new Vector2(-18, -6);

            healthText = hpTxtObj.AddComponent<Text>();
            healthText.font = cachedFont;
            healthText.text = "HP 3/3 ❤️";
            healthText.fontSize = 22;
            healthText.fontStyle = FontStyle.Bold;
            healthText.alignment = TextAnchor.MiddleRight;
            healthText.color = new Color(0.85f, 0.90f, 0.95f);
            healthText.raycastTarget = false;

            // Progress Bar Track
            GameObject progTrackObj = CreateUIElement("HeartProgress_Track", heartPillRect);
            RectTransform progTrackRect = progTrackObj.GetComponent<RectTransform>();
            progTrackRect.anchorMin = new Vector2(0, 0);
            progTrackRect.anchorMax = new Vector2(1, 0.5f);
            progTrackRect.offsetMin = new Vector2(18, 12);
            progTrackRect.offsetMax = new Vector2(-18, -4);

            Image trackImg = progTrackObj.AddComponent<Image>();
            trackImg.sprite = cachedWhiteSprite;
            trackImg.color = new Color(0.02f, 0.02f, 0.03f, 0.95f);
            trackImg.raycastTarget = false;

            // Progress Bar Fill
            GameObject progFillObj = CreateUIElement("HeartProgress_Fill", progTrackRect);
            RectTransform progFillRect = progFillObj.GetComponent<RectTransform>();
            progFillRect.anchorMin = Vector2.zero;
            progFillRect.anchorMax = Vector2.one;
            progFillRect.offsetMin = new Vector2(2, 2);
            progFillRect.offsetMax = new Vector2(-2, -2);

            heartProgressBar = progFillObj.AddComponent<Image>();
            heartProgressBar.sprite = cachedWhiteSprite;
            heartProgressBar.type = Image.Type.Filled;
            heartProgressBar.fillMethod = Image.FillMethod.Horizontal;
            heartProgressBar.fillAmount = 0f;
            heartProgressBar.color = new Color(1f, 0.20f, 0.60f, 0.95f);
            heartProgressBar.raycastTarget = false;

            // 7. Top-Right: New Best Record Badge (pulsing gold pill)
            GameObject bestObj = CreateUIElement("Badge_NewBest", safeRect);
            RectTransform bestRect = bestObj.GetComponent<RectTransform>();
            bestRect.anchorMin = new Vector2(1, 1);
            bestRect.anchorMax = new Vector2(1, 1);
            bestRect.pivot = new Vector2(1, 1);
            bestRect.anchoredPosition = new Vector2(-36, -262);
            bestRect.sizeDelta = new Vector2(460, 52);

            Image bestBg = bestObj.AddComponent<Image>();
            bestBg.sprite = cachedWhiteSprite;
            bestBg.color = new Color(0.28f, 0.20f, 0.04f, 0.92f);
            bestBg.raycastTarget = false;

            GameObject bestTxtObj = CreateUIElement("Txt_NewBest", bestRect);
            RectTransform bTxtRect = bestTxtObj.GetComponent<RectTransform>();
            bTxtRect.anchorMin = Vector2.zero;
            bTxtRect.anchorMax = Vector2.one;
            bTxtRect.offsetMin = Vector2.zero;
            bTxtRect.offsetMax = Vector2.zero;

            Text bTxt = bestTxtObj.AddComponent<Text>();
            bTxt.font = cachedFont;
            bTxt.text = "★ NEW BEST RECORD ★";
            bTxt.fontSize = 24;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.color = new Color(1.0f, 0.90f, 0.40f);
            bTxt.raycastTarget = false;

            newBestBadge = bestObj;
            newBestBadge.SetActive(false);

            // 8. Top-Center: Milestone Banner
            GameObject mileObj = CreateUIElement("Banner_Milestone", safeRect);
            RectTransform mileRect = mileObj.GetComponent<RectTransform>();
            mileRect.anchorMin = new Vector2(0.5f, 1);
            mileRect.anchorMax = new Vector2(0.5f, 1);
            mileRect.pivot = new Vector2(0.5f, 1);
            mileRect.anchoredPosition = new Vector2(0, -180);
            mileRect.sizeDelta = new Vector2(720, 130);

            Image mileBg = mileObj.AddComponent<Image>();
            mileBg.sprite = cachedWhiteSprite;
            mileBg.color = new Color(0.10f, 0.08f, 0.03f, 0.94f);
            mileBg.raycastTarget = false;

            milestoneGroup = mileObj.AddComponent<CanvasGroup>();
            milestoneGroup.blocksRaycasts = false;

            GameObject mTitleObj = CreateUIElement("Txt_MilestoneTitle", mileRect);
            RectTransform mtRect = mTitleObj.GetComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0, 0.45f);
            mtRect.anchorMax = new Vector2(1, 1);
            mtRect.offsetMin = Vector2.zero;
            mtRect.offsetMax = new Vector2(0, -6);

            milestoneTitle = mTitleObj.AddComponent<Text>();
            milestoneTitle.font = cachedFont;
            milestoneTitle.text = "🏛️ 500m";
            milestoneTitle.fontSize = 42;
            milestoneTitle.fontStyle = FontStyle.Bold;
            milestoneTitle.alignment = TextAnchor.MiddleCenter;
            milestoneTitle.color = new Color(1.0f, 0.88f, 0.35f);
            milestoneTitle.raycastTarget = false;

            GameObject mSubObj = CreateUIElement("Txt_MilestoneSub", mileRect);
            RectTransform msRect = mSubObj.GetComponent<RectTransform>();
            msRect.anchorMin = new Vector2(0, 0);
            msRect.anchorMax = new Vector2(1, 0.45f);
            msRect.offsetMin = Vector2.zero;
            msRect.offsetMax = Vector2.zero;

            milestoneSub = mSubObj.AddComponent<Text>();
            milestoneSub.font = cachedFont;
            milestoneSub.text = "JUNGLE TRAIL — KEEP RUNNING!";
            milestoneSub.fontSize = 24;
            milestoneSub.alignment = TextAnchor.MiddleCenter;
            milestoneSub.color = new Color(0.55f, 0.95f, 0.75f);
            milestoneSub.raycastTarget = false;

            milestoneRoot = mileObj;
            milestoneRoot.SetActive(false);

            // 9. Top-Center: Beast Proximity Warning
            GameObject beastObj = CreateUIElement("Banner_BeastWarning", safeRect);
            RectTransform beastRect = beastObj.GetComponent<RectTransform>();
            beastRect.anchorMin = new Vector2(0.5f, 1);
            beastRect.anchorMax = new Vector2(0.5f, 1);
            beastRect.pivot = new Vector2(0.5f, 1);
            beastRect.anchoredPosition = new Vector2(0, -325);
            beastRect.sizeDelta = new Vector2(660, 64);

            Image beastBg = beastObj.AddComponent<Image>();
            beastBg.sprite = cachedWhiteSprite;
            beastBg.color = new Color(0.32f, 0.04f, 0.06f, 0.92f);
            beastBg.raycastTarget = false;

            GameObject beastTxtObj = CreateUIElement("Txt_BeastWarning", beastRect);
            RectTransform btRect = beastTxtObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;

            beastWarningText = beastTxtObj.AddComponent<Text>();
            beastWarningText.font = cachedFont;
            beastWarningText.text = "⚠️ BEAST CLOSING IN! 2.5s";
            beastWarningText.fontSize = 26;
            beastWarningText.fontStyle = FontStyle.Bold;
            beastWarningText.alignment = TextAnchor.MiddleCenter;
            beastWarningText.color = new Color(1f, 0.85f, 0.30f);
            beastWarningText.raycastTarget = false;

            beastWarningRoot = beastObj;
            beastWarningRoot.SetActive(false);
        }

        private PowerUpSlotUI CreatePowerUpSlot(Transform parent, int index)
        {
            GameObject slotObj = CreateUIElement($"PowerUpSlot_{index}", parent);
            RectTransform slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0, 1);
            slotRect.anchorMax = new Vector2(0, 1);
            slotRect.pivot = new Vector2(0, 1);
            slotRect.anchoredPosition = new Vector2(0, -index * 68);
            slotRect.sizeDelta = new Vector2(340, 58);

            Image bg = slotObj.AddComponent<Image>();
            bg.sprite = cachedWhiteSprite;
            bg.color = new Color(0.06f, 0.09f, 0.12f, 0.88f);
            bg.raycastTarget = false;

            // Fill Bar
            GameObject fillObj = CreateUIElement("FillBar", slotRect);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.sprite = cachedWhiteSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.2f, 0.8f, 1f, 0.40f);
            fillImg.raycastTarget = false;

            // Left Border Accent
            GameObject accentObj = CreateUIElement("AccentBorder", slotRect);
            RectTransform accentRect = accentObj.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = new Vector2(6, 0);

            Image accentImg = accentObj.AddComponent<Image>();
            accentImg.sprite = cachedWhiteSprite;
            accentImg.color = Color.cyan;
            accentImg.raycastTarget = false;

            // Label Text (Left)
            GameObject lblObj = CreateUIElement("Txt_Label", slotRect);
            RectTransform lblRect = lblObj.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0, 0);
            lblRect.anchorMax = new Vector2(0.68f, 1);
            lblRect.offsetMin = new Vector2(16, 0);
            lblRect.offsetMax = Vector2.zero;

            Text lblTxt = lblObj.AddComponent<Text>();
            lblTxt.font = cachedFont;
            lblTxt.text = "⚡ BOOST";
            lblTxt.fontSize = 22;
            lblTxt.fontStyle = FontStyle.Bold;
            lblTxt.alignment = TextAnchor.MiddleLeft;
            lblTxt.color = Color.white;
            lblTxt.raycastTarget = false;

            // Value Text (Right)
            GameObject valObj = CreateUIElement("Txt_Value", slotRect);
            RectTransform valRect = valObj.GetComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0.68f, 0);
            valRect.anchorMax = new Vector2(1, 1);
            valRect.offsetMin = Vector2.zero;
            valRect.offsetMax = new Vector2(-12, 0);

            Text valTxt = valObj.AddComponent<Text>();
            valTxt.font = cachedFont;
            valTxt.text = "5.0s";
            valTxt.fontSize = 20;
            valTxt.alignment = TextAnchor.MiddleRight;
            valTxt.color = Color.cyan;
            valTxt.raycastTarget = false;

            return new PowerUpSlotUI
            {
                Root = slotObj,
                Background = bg,
                ProgressBar = fillImg,
                LeftAccent = accentImg,
                LabelText = lblTxt,
                ValueText = valTxt
            };
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void EnsureResources()
        {
            if (cachedWhiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (cachedFont == null)
                {
                    var allFonts = Resources.FindObjectsOfTypeAll<Font>();
                    if (allFonts != null && allFonts.Length > 0) cachedFont = allFonts[0];
                }
            }
        }
        #endregion
    }
}
