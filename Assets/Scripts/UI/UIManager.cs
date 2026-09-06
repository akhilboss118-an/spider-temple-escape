using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Runner.Core;
using Runner.Pickups;

namespace Runner.UI
{
    public enum MenuModal
    {
        None,
        HeroSuits,
        Upgrades,
        Settings,
        Leaderboard,
        QuitConfirm
    }

    /// <summary>
    /// Comprehensive UI Manager for Spider Temple Escape:
    /// 1. Main Menu (Stitch design): Top HUD pills (Hearts, Hero Level/XP, Best Score),
    ///    cinematic Jungle Escape title, radiant golden START RUN CTA, 4 glass action cards
    ///    (Hero Suits, Upgrades, Settings, Quit), sound toggle, trophy leaderboard, and glowing ticker.
    /// 2. Interactive Sub-Modals: Hero Suits selection, Relic Upgrades shop (with Heart Capacity), Game Settings, Global Stats, Quit Confirm.
    /// 3. In-Game HUD: Top-Left Pause (⏸), Top-Center Circular Power Dial with radial countdown ring,
    ///    Top-Right Distance/Score pill & Hearts pool with bloom pulse.
    /// 4. Pause System: Freezes gameplay, 3-second unscaled resume countdown (3... 2... 1... GO!), Settings, Restart, Quit to Menu.
    /// 5. Ending Death Screen (Stitch design): Beast background art, crimson defeat vignette, dynamic cause pill,
    ///    3D crimson carved title, gold corner brackets summary card, Try Again & Main Menu buttons.
    /// 6. Dynamic Toast Notification overlay for feedback.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private DeathType lastDeathType;
        private int lastFinalScore;
        private bool isGameOver = false;
        private float gameOverDuration = 0f;

        // Main Menu State & Modals
        private MenuModal activeModal = MenuModal.None;
        private Texture2D menuBgTexture;
        private Texture2D deathBgTexture;
        private Texture2D whiteTexture;

        // In-Game Pause & Countdown State
        private bool isMidGameSettingsOpen = false;
        private float resumeCountdownTimer = 0f;
        private float heartBloomTimer = 0f;

        // Real Heart Collection Meter & Special Effects State
        private float animatedHeartFill = 1.0f;
        private float heartCollectFloatTimer = 0f;
        private int lastRecordedCoins = -1;
        private int lastRecordedLives = -1;

        // Toast Feedback System
        private string toastIcon = "";
        private string toastMessage = "";
        private float toastTimer = 0f;

        // Loading Screen System (Starting Boot & Death -> Main Menu)
        private bool isLoading = true; // Starts TRUE for initial boot loading screen!
        private float loadingTimer = 0f;
        private float loadingDuration = 2.2f;
        private float loadingProgress = 0f;
        private float loadingFadeAlpha = 1f;
        private bool isLoadingFadingOut = false;
        private string loadingHeader = "SPIDER TEMPLE ESCAPE";
        private string loadingSubHeader = "ENDLESS 3D JUNGLE RUNNER";
        private string loadingStatusText = "INITIALIZING EXPEDITION...";
        private Action onLoadingFinished = null;
        private int currentTipIndex = 0;
        private float tipChangeTimer = 0f;

        private static readonly string[] LoadingTips = new string[]
        {
            "💡 Swipe UP to leap over fallen ancient tree logs",
            "💡 Swipe DOWN to slide under overhead tree branch arches",
            "💡 Swipe LEFT or RIGHT to switch lanes and take sharp 90° corners",
            "💡 Grab Monster Energy drinks for invulnerable speed bursts",
            "💡 Collect glowing 💖 Hearts to bank relics and earn extra lives",
            "💡 Activate Shields to absorb fatal obstacle collisions"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Purge any legacy or stray Canvas GameObjects to guarantee 100% unblocked 3D camera rendering
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                Destroy(c.gameObject);
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                GameManager.Instance.OnGameOver += HandleGameOver;
                GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                GameManager.Instance.OnLivesChanged += HandleLivesChanged;
                HandleGameStateChanged(GameManager.Instance.CurrentState);
                lastRecordedCoins = GameManager.Instance.CoinsCollected;
                lastRecordedLives = GameManager.Instance.CurrentLives;
            }
            EnsureTextures();

            // Initial Game Launch Loading Screen
            StartLoading(2.2f, "INITIALIZING EXPEDITION...", null, "SPIDER TEMPLE ESCAPE", "ENDLESS 3D JUNGLE RUNNER");
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameOver -= HandleGameOver;
                GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                GameManager.Instance.OnLivesChanged -= HandleLivesChanged;
            }
        }

        private void HandleCoinsChanged(int newCoins)
        {
            if (lastRecordedCoins >= 0 && newCoins > lastRecordedCoins)
            {
                heartBloomTimer = 0.9f;
                heartCollectFloatTimer = 1.2f;
            }
            lastRecordedCoins = newCoins;
        }

        private void HandleLivesChanged(int current, int max)
        {
            if (lastRecordedLives >= 0 && current > lastRecordedLives)
            {
                heartBloomTimer = 1.0f;
                heartCollectFloatTimer = 1.4f;
            }
            lastRecordedLives = current;
        }

        public void OnStartRunClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGame();
            }
        }

        public void OnTryAgainClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }

        public void OnReturnToMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                StartLoading(1.6f, "RETURNING TO CAMP...", () =>
                {
                    GameManager.Instance.ReturnToMenu();
                }, "RETURNING TO BASE CAMP", "SAVING EXPEDITION DATA");
            }
        }

        public void OnPauseClicked()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.Playing)
                    GameManager.Instance.PauseGame();
                else if (GameManager.Instance.CurrentState == GameState.Paused)
                    GameManager.Instance.ResumeGame();
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                isGameOver = false;
                gameOverDuration = 0f;
            }
        }

        private void EnsureTextures()
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            if (menuBgTexture == null)
            {
#if UNITY_EDITOR
                menuBgTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/jungle_escape_bg.png");
                if (menuBgTexture == null)
                {
                    menuBgTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/jungle_escape_bg.png");
                }
#endif
                if (menuBgTexture == null)
                {
                    menuBgTexture = Resources.Load<Texture2D>("Textures/jungle_escape_bg");
                }
            }

            if (deathBgTexture == null)
            {
#if UNITY_EDITOR
                deathBgTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/death_screen_bg.png");
                if (deathBgTexture == null)
                {
                    deathBgTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/death_screen_bg.png");
                }
#endif
                if (deathBgTexture == null)
                {
                    deathBgTexture = Resources.Load<Texture2D>("Textures/death_screen_bg");
                }
            }
        }

        public void ShowToast(string icon, string message, float duration = 1.8f)
        {
            toastIcon = icon;
            toastMessage = message;
            toastTimer = duration;
        }

        public void TriggerHeartBloom(float duration = 0.8f)
        {
            heartBloomTimer = duration;
        }

        #region Unified Pointer Input (Device Simulator, Mobile Touch, Desktop Mouse)
        private Vector2 pointerGUIPosition = new Vector2(-1000, -1000);
        private bool pointerPressedThisFrame = false;
        private bool pointerReleasedThisFrame = false;
        private bool isPointerHeld = false;
        private int activePressControlId = -1;

        // Reflection cache for New Input System Pointer
        private static bool inputSystemChecked = false;
        private static System.Type pointerClass = null;
        private static System.Reflection.PropertyInfo pointerCurrentProp = null;
        private static System.Reflection.PropertyInfo pointerPositionProp = null;
        private static System.Reflection.PropertyInfo pointerPressProp = null;
        private static System.Reflection.PropertyInfo isPressedProp = null;
        private static System.Reflection.PropertyInfo wasPressedThisFrameProp = null;
        private static System.Reflection.PropertyInfo wasReleasedThisFrameProp = null;
        private static System.Reflection.MethodInfo readValueMethod = null;

        private void InitInputSystemPointer()
        {
            if (inputSystemChecked) return;
            inputSystemChecked = true;

            try
            {
#pragma warning disable UAC0005
                var loadedAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
#pragma warning restore UAC0005
                foreach (var asm in loadedAssemblies)
                {
                    if (asm.GetName().Name == "Unity.InputSystem")
                    {
                        pointerClass = asm.GetType("UnityEngine.InputSystem.Pointer");
                        if (pointerClass != null)
                        {
                            pointerCurrentProp = pointerClass.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            pointerPositionProp = pointerClass.GetProperty("position");
                            pointerPressProp = pointerClass.GetProperty("press");

                            var buttonControl = asm.GetType("UnityEngine.InputSystem.Controls.ButtonControl");
                            if (buttonControl != null)
                            {
                                isPressedProp = buttonControl.GetProperty("isPressed");
                                wasPressedThisFrameProp = buttonControl.GetProperty("wasPressedThisFrame");
                                wasReleasedThisFrameProp = buttonControl.GetProperty("wasReleasedThisFrame");
                            }

                            var vector2Control = asm.GetType("UnityEngine.InputSystem.Controls.Vector2Control");
                            if (vector2Control != null)
                            {
                                readValueMethod = vector2Control.GetMethod("ReadValue");
                            }
                        }
                        break;
                    }
                }
            }
            catch (System.Exception) { }
        }

        private void UpdatePointerInput()
        {
            pointerPressedThisFrame = false;
            pointerReleasedThisFrame = false;

            bool readSuccess = false;

            // 1. Try Touch / Mouse input (Device Simulator feeds touch/mouse directly in full screen resolution)
            try
            {
                if (Input.touchCount > 0)
                {
                    Touch t = Input.GetTouch(0);
                    pointerGUIPosition = new Vector2(t.position.x, Screen.height - t.position.y);
                    if (t.phase == TouchPhase.Began) pointerPressedThisFrame = true;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) pointerReleasedThisFrame = true;
                    isPointerHeld = (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled);
                    readSuccess = true;
                }
                else if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0) || Input.GetMouseButton(0))
                {
                    Vector3 m = Input.mousePosition;
                    pointerGUIPosition = new Vector2(m.x, Screen.height - m.y);
                    pointerPressedThisFrame = Input.GetMouseButtonDown(0);
                    pointerReleasedThisFrame = Input.GetMouseButtonUp(0);
                    isPointerHeld = Input.GetMouseButton(0);
                    readSuccess = true;
                }
                else
                {
                    Vector3 m = Input.mousePosition;
                    pointerGUIPosition = new Vector2(m.x, Screen.height - m.y);
                    readSuccess = true;
                }
            }
            catch (System.InvalidOperationException)
            {
                // New Input System is active exclusively
                readSuccess = false;
            }

            // 2. Fallback to New Input System Pointer if legacy is disabled
            if (!readSuccess)
            {
                InitInputSystemPointer();
                if (pointerCurrentProp != null && pointerPositionProp != null)
                {
                    try
                    {
                        object ptr = pointerCurrentProp.GetValue(null);
                        if (ptr != null)
                        {
                            object posControl = pointerPositionProp.GetValue(ptr);
                            if (posControl != null && readValueMethod != null)
                            {
                                Vector2 rawPos = (Vector2)readValueMethod.Invoke(posControl, null);
                                pointerGUIPosition = new Vector2(rawPos.x, Screen.height - rawPos.y);
                            }

                            if (pointerPressProp != null)
                            {
                                object pressControl = pointerPressProp.GetValue(ptr);
                                if (pressControl != null)
                                {
                                    if (wasPressedThisFrameProp != null)
                                        pointerPressedThisFrame = (bool)wasPressedThisFrameProp.GetValue(pressControl);
                                    if (wasReleasedThisFrameProp != null)
                                        pointerReleasedThisFrame = (bool)wasReleasedThisFrameProp.GetValue(pressControl);
                                    if (isPressedProp != null)
                                        isPointerHeld = (bool)isPressedProp.GetValue(pressControl);
                                }
                            }
                        }
                    }
                    catch (System.Exception) { }
                }
            }
        }

        private bool IsCardClicked(int controlId, Rect rect)
        {
            if (isLoading && loadingFadeAlpha > 0.15f) return false;

            // 1. Screen-space Pointer (Device Simulator, Mobile Touch, and Game View)
            if (rect.Contains(pointerGUIPosition))
            {
                if (pointerPressedThisFrame)
                {
                    activePressControlId = controlId;
                }
                else if (pointerReleasedThisFrame && (activePressControlId == controlId || activePressControlId == -1))
                {
                    activePressControlId = -1;
                    pointerReleasedThisFrame = false; // consume event
                    return true;
                }
            }

            // 2. Event.current in OnGUI (Desktop Editor Game View)
            Event evt = Event.current;
            if (evt != null)
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    if (rect.Contains(evt.mousePosition))
                    {
                        activePressControlId = controlId;
                    }
                }
                else if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    if (activePressControlId == controlId)
                    {
                        activePressControlId = -1;
                        if (rect.Contains(evt.mousePosition))
                        {
                            return true;
                        }
                    }
                }
            }

            // 3. Fallback: Native IMGUI GUI.Button
            GUI.color = Color.clear;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                return true;
            }

            return false;
        }

        private void DrawCard(Rect rect, Color baseColor)
        {
            bool hovered = rect.Contains(pointerGUIPosition) || (Event.current != null && rect.Contains(Event.current.mousePosition));
            GUI.color = hovered ? (baseColor + new Color(0.10f, 0.10f, 0.10f, 0.12f)) : baseColor;
            GUI.DrawTexture(rect, whiteTexture);
            if (hovered)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(rect, whiteTexture);
            }
        }
        #endregion

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), whiteTexture);
        }

        private void DrawCornerBrackets(Rect rect, float size, float thickness, Color color)
        {
            GUI.color = color;
            // Top-Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, size, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, size), whiteTexture);
            // Top-Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - size, rect.y, size, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, size), whiteTexture);
            // Bottom-Left
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, size, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - size, thickness, size), whiteTexture);
            // Bottom-Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - size, rect.y + rect.height - thickness, size, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y + rect.height - size, thickness, size), whiteTexture);
        }

        private void HandleGameOver(DeathType deathType, int finalScore)
        {
            isGameOver = true;
            gameOverDuration = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            lastDeathType = deathType;
            lastFinalScore = finalScore;
        }

        private void GetDeathDetails(DeathType deathType, out string tag, out string title, out string subtitle)
        {
            switch (deathType)
            {
                case DeathType.CaughtByMonster:
                    tag = "AMBUSH AT BRIDGE";
                    title = "CAUGHT BY BEAST!";
                    subtitle = "The ancient guardian overtook you in the shadows";
                    break;
                case DeathType.HeadOnCollision:
                    tag = "HIGH HURDLE COLLISION";
                    title = "CRASHED HEAD-ON!";
                    subtitle = "The ancient stone ruins halted your escape";
                    break;
                case DeathType.FallIntoVoid:
                    tag = "FELL INTO THE ABYSS";
                    title = "FALLEN TO THE DEPTHS!";
                    subtitle = "Swallowed by the bottomless jungle chasm";
                    break;
                case DeathType.MissedTurn:
                    tag = "MISSED JUNGLE CORNER";
                    title = "MISSED THE CORNER!";
                    subtitle = "The temple pathway slipped beneath your feet";
                    break;
                default:
                    tag = "EXPEDITION FAILED";
                    title = "GAME OVER";
                    subtitle = "The jungle claims another lost explorer";
                    break;
            }
        }

        private string GetDeathReasonMessage(DeathType deathType)
        {
            GetDeathDetails(deathType, out _, out string title, out _);
            return title;
        }

        private void Update()
        {
            UpdatePointerInput();

            if (isLoading)
            {
                UpdateLoading();
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
            }

            if (heartBloomTimer > 0f)
            {
                heartBloomTimer -= Time.unscaledDeltaTime;
            }

            if (heartCollectFloatTimer > 0f)
            {
                heartCollectFloatTimer -= Time.unscaledDeltaTime;
            }

            if (isGameOver)
            {
                gameOverDuration += Time.unscaledDeltaTime;
                if (gameOverDuration > 0.35f && Time.timeScale > 0f)
                {
                    Time.timeScale = 0f;
                }
            }

            if (resumeCountdownTimer > 0f)
            {
                resumeCountdownTimer -= Time.unscaledDeltaTime;
                if (resumeCountdownTimer <= 0f)
                {
                    resumeCountdownTimer = 0f;
                    GameManager.Instance?.ResumeGame();
                }
            }
        }

        private void OnGUI()
        {
            EnsureTextures();

            if (GameManager.Instance == null) return;

            // Block keyboard navigation if loading screen is actively showing
            if (isLoading && loadingFadeAlpha > 0.2f)
            {
                if (Event.current != null && Event.current.isKey)
                {
                    Event.current.Use();
                }
            }
            else
            {
                // Universal key navigation (works under both Old and New Input Systems without exceptions)
                Event e = Event.current;
                if (e != null && e.isKey && e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        if (GameManager.Instance.CurrentState == GameState.Menu)
                        {
                            if (activeModal == MenuModal.None)
                            {
                                ShowToast("🏃", "Starting Expedition...");
                                GameManager.Instance.StartGame();
                                e.Use();
                            }
                            else
                            {
                                activeModal = MenuModal.None;
                                e.Use();
                            }
                        }
                        else if (GameManager.Instance.CurrentState == GameState.GameOver && gameOverDuration > 0.25f)
                        {
                            GameManager.Instance.StartGame();
                            e.Use();
                        }
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        if (activeModal != MenuModal.None)
                        {
                            activeModal = MenuModal.None;
                            e.Use();
                        }
                        else if (GameManager.Instance.CurrentState == GameState.Playing)
                        {
                            isMidGameSettingsOpen = false;
                            GameManager.Instance.PauseGame();
                            e.Use();
                        }
                        else if (GameManager.Instance.CurrentState == GameState.Paused)
                        {
                            if (isMidGameSettingsOpen)
                            {
                                isMidGameSettingsOpen = false;
                            }
                            else if (resumeCountdownTimer <= 0f)
                            {
                                resumeCountdownTimer = 3.6f;
                            }
                            e.Use();
                        }
                        else if (GameManager.Instance.CurrentState == GameState.GameOver)
                        {
                            isGameOver = false;
                            Time.timeScale = 1.0f;
                            StartLoading(1.6f, "RETURNING TO CAMP...", () =>
                            {
                                GameManager.Instance?.ReturnToMenu();
                            }, "RETURNING TO BASE CAMP", "SAVING EXPEDITION DATA");
                            e.Use();
                        }
                    }
                }
            }

            switch (GameManager.Instance.CurrentState)
            {
                case GameState.Menu:
                    RenderMainMenu();
                    break;
                case GameState.GameOver:
                    RenderGameOverModal();
                    break;
                case GameState.Playing:
                    RenderInGameHUD();
                    break;
                case GameState.Paused:
                    RenderInGameHUD();
                    float pauseScale = Mathf.Clamp(Screen.width / 420.0f, 1.0f, 2.8f);
                    if (resumeCountdownTimer > 0f)
                    {
                        RenderResumeCountdown(pauseScale);
                    }
                    else if (isMidGameSettingsOpen)
                    {
                        RenderSettingsModal(pauseScale, true);
                    }
                    else
                    {
                        RenderPauseModal(pauseScale);
                    }
                    break;
            }

            RenderToast();

            if (isLoading)
            {
                RenderLoadingScreen();
            }
        }

        #region Main Menu (Stitch Design)
        private void RenderMainMenu()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float uiScale = Mathf.Clamp(Screen.width / 390.0f, 0.80f, 2.4f);

            // 1. Fullscreen Background Art
            if (menuBgTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), menuBgTexture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.color = new Color(0.04f, 0.08f, 0.07f, 1f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
            }

            // If a sub-modal is open, render modal in foreground and return immediately
            if (activeModal != MenuModal.None)
            {
                RenderSubModal(uiScale);
                return;
            }

            // 2. Safe Area calculation for mobile cutouts
            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeTop = Mathf.Max(14f * uiScale, topOffset + (8f * uiScale));

            // 3. Top Status Pills
            float pillH = 38f * uiScale;
            float pillW = (Screen.width - (36f * uiScale)) / 3.0f;
            float x1 = 12f * uiScale;
            float x2 = x1 + pillW + (6f * uiScale);
            float x3 = x2 + pillW + (6f * uiScale);

            // Pill 1: Hearts
            Rect pill1Rect = new Rect(x1, safeTop, pillW, pillH);
            DrawCard(pill1Rect, new Color(0.06f, 0.09f, 0.08f, 0.82f));
            DrawBorder(pill1Rect, new Color(1.0f, 0.30f, 0.65f, 0.50f), 1.2f);
            GUI.color = new Color(1.0f, 0.40f, 0.70f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(x1, safeTop + (3f * uiScale), pillW, 14f * uiScale), "💖 HEARTS");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.Label(new Rect(x1, safeTop + (16f * uiScale), pillW, 20f * uiScale), $"{GameManager.Instance.TotalBankedCoins} / 5");

            // Pill 2: Level & XP
            Rect pill2Rect = new Rect(x2, safeTop, pillW, pillH);
            DrawCard(pill2Rect, new Color(0.06f, 0.09f, 0.08f, 0.82f));
            DrawBorder(pill2Rect, new Color(0.20f, 0.90f, 0.80f, 0.50f), 1.2f);
            GUI.color = new Color(0.25f, 0.95f, 0.85f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(x2, safeTop + (3f * uiScale), pillW, 16f * uiScale), "LVL 32 • HERO");
            // Mini XP Progress Bar
            float barW = pillW * 0.70f;
            float barH = 4f * uiScale;
            float barX = x2 + (pillW - barW) * 0.5f;
            float barY = safeTop + (24f * uiScale);
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTexture);
            GUI.color = new Color(0.15f, 0.95f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(barX, barY, barW * 0.72f, barH), whiteTexture);

            // Pill 3: Best Distance
            Rect pill3Rect = new Rect(x3, safeTop, pillW, pillH);
            DrawCard(pill3Rect, new Color(0.06f, 0.09f, 0.08f, 0.82f));
            DrawBorder(pill3Rect, new Color(0.35f, 0.95f, 0.55f, 0.50f), 1.2f);
            GUI.color = new Color(0.40f, 0.95f, 0.60f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(x3, safeTop + (3f * uiScale), pillW, 14f * uiScale), "BEST DISTANCE");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.Label(new Rect(x3, safeTop + (16f * uiScale), pillW, 20f * uiScale), $"{GameManager.Instance.HighScore:N0}m ⭐");

            // 4. Branding Area (Title)
            float titleY = safeTop + pillH + (14f * uiScale);

            // Subtitle pill tag
            float tagW = 210f * uiScale;
            float tagH = 22f * uiScale;
            float tagX = (Screen.width - tagW) * 0.5f;
            Rect subtitleRect = new Rect(tagX, titleY, tagW, tagH);
            DrawCard(subtitleRect, new Color(0.05f, 0.07f, 0.06f, 0.82f));
            DrawBorder(subtitleRect, new Color(1.0f, 0.82f, 0.32f, 0.60f), 1.2f);
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(subtitleRect, "• ANCIENT TEMPLE RUNNER •");

            // Grand Title: JUNGLE ESCAPE with soft floating animation & crisp drop shadow
            float floatOffset = Mathf.Sin(Time.unscaledTime * 2.5f) * 3f;
            float heroTitleY = titleY + tagH + (4f * uiScale) + floatOffset;
            int titleFontSize = Mathf.RoundToInt(28 * uiScale);

            // Shadow layer
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.90f);
            GUI.skin.label.fontSize = titleFontSize;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(2f, heroTitleY + 2f, Screen.width, 36f * uiScale), "JUNGLE ESCAPE");

            // Front radiant layer
            GUI.color = new Color(1.0f, 0.90f, 0.45f);
            GUI.Label(new Rect(0, heroTitleY, Screen.width, 36f * uiScale), "JUNGLE ESCAPE");

            // Gold Filigree Divider
            float divW = 160f * uiScale;
            float divY = heroTitleY + (38f * uiScale);
            float divX = (Screen.width - divW) * 0.5f;
            GUI.color = new Color(1.0f, 0.78f, 0.20f, 0.85f);
            GUI.DrawTexture(new Rect(divX, divY + (4f * uiScale), divW * 0.42f, 1.5f), whiteTexture);
            GUI.DrawTexture(new Rect(divX + (divW * 0.58f), divY + (4f * uiScale), divW * 0.42f, 1.5f), whiteTexture);
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
            GUI.Label(new Rect(divX + (divW * 0.42f), divY - (4f * uiScale), divW * 0.16f, 16f * uiScale), "◆");

            // 5. Main Controls Section (Positioned at bottom of screen)
            float maxActionW = Mathf.Min(Screen.width - (32f * uiScale), 360f * uiScale);
            float actionX = (Screen.width - maxActionW) * 0.5f;
            float actionStackH = 265f * uiScale;
            float minActionY = divY + (32f * uiScale);
            float desiredActionY = Screen.height - actionStackH - (18f * uiScale);
            float startActionY = Mathf.Max(minActionY, desiredActionY);

            // If screen height is compact, adjust uiScale so buttons never overflow the bottom
            if (startActionY + actionStackH > Screen.height - (10f * uiScale) && Screen.height > 300f)
            {
                startActionY = Mathf.Max(divY + (16f * uiScale), Screen.height - actionStackH - (10f * uiScale));
            }

            // Primary Golden CTA Button (START RUN) - Radiant, glowing, crystal clear
            float ctaH = 54f * uiScale;
            Rect ctaRect = new Rect(actionX, startActionY, maxActionW, ctaH);

            // Golden Gradient Button Background
            DrawCard(ctaRect, new Color(1.0f, 0.75f, 0.08f, 0.96f));
            // Highlight line at top
            GUI.color = new Color(1.0f, 0.95f, 0.65f, 0.85f);
            GUI.DrawTexture(new Rect(actionX + 2, startActionY + 2, maxActionW - 4, 2f), whiteTexture);
            // Shadow bevel at bottom
            GUI.color = new Color(0.72f, 0.42f, 0.02f, 0.85f);
            GUI.DrawTexture(new Rect(actionX + 2, startActionY + ctaH - 3, maxActionW - 4, 3f), whiteTexture);
            // Luminous golden border
            DrawBorder(ctaRect, new Color(1.0f, 0.92f, 0.40f, 1.0f), 2f * uiScale);

            // CTA Content: [▶] START RUN (Survive The Temple Beast) [>]
            // Deep charcoal black text for 100% contrast and legibility
            GUI.color = new Color(0.08f, 0.04f, 0.01f, 1.0f);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(18 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (18f * uiScale), startActionY + (7f * uiScale), maxActionW - (60f * uiScale), 24f * uiScale), "▶  START RUN");

            GUI.color = new Color(0.24f, 0.12f, 0.02f, 0.90f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (18f * uiScale), startActionY + (31f * uiScale), maxActionW - (60f * uiScale), 16f * uiScale), "SURVIVE THE TEMPLE BEAST");

            GUI.color = new Color(0.08f, 0.04f, 0.01f, 1.0f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(22 * uiScale);
            GUI.Label(new Rect(actionX, startActionY, maxActionW - (18f * uiScale), ctaH), "›");

            if (IsCardClicked(101, ctaRect))
            {
                ShowToast("🏃", "Starting Expedition...");
                GameManager.Instance.StartGame();
                return;
            }

            // 6. Secondary Action Cards Stack
            float cardH = 38f * uiScale;
            float cardGap = 5f * uiScale;
            float curCardY = startActionY + ctaH + (8f * uiScale);

            // Card 1: HERO SUITS
            Rect suitRect = new Rect(actionX, curCardY, maxActionW, cardH);
            DrawCard(suitRect, new Color(0.06f, 0.09f, 0.11f, 0.85f));
            DrawBorder(suitRect, new Color(0.20f, 0.85f, 0.95f, 0.70f), 1.2f);
            // Left edge indicator stripe
            GUI.color = new Color(0.20f, 0.85f, 0.95f, 0.90f);
            GUI.DrawTexture(new Rect(actionX, curCardY, 3f * uiScale, cardH), whiteTexture);

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (14f * uiScale), curCardY, maxActionW * 0.6f, cardH), "🕷️  HERO SUITS");
            GUI.color = new Color(0.25f, 0.95f, 1.0f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.Label(new Rect(actionX, curCardY, maxActionW - (14f * uiScale), cardH), "3 Unlocked ›");
            if (IsCardClicked(102, suitRect))
            {
                activeModal = MenuModal.HeroSuits;
            }

            // Card 2: UPGRADES
            curCardY += cardH + cardGap;
            Rect upgRect = new Rect(actionX, curCardY, maxActionW, cardH);
            DrawCard(upgRect, new Color(0.06f, 0.09f, 0.11f, 0.85f));
            DrawBorder(upgRect, new Color(1.0f, 0.80f, 0.25f, 0.70f), 1.2f);
            GUI.color = new Color(1.0f, 0.80f, 0.25f, 0.90f);
            GUI.DrawTexture(new Rect(actionX, curCardY, 3f * uiScale, cardH), whiteTexture);

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (14f * uiScale), curCardY, maxActionW * 0.6f, cardH), "⚡  UPGRADES");
            GUI.color = new Color(1.0f, 0.88f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.Label(new Rect(actionX, curCardY, maxActionW - (14f * uiScale), cardH), "Boosts & Relics ›");
            if (IsCardClicked(103, upgRect))
            {
                activeModal = MenuModal.Upgrades;
            }

            // Card 3: SETTINGS
            curCardY += cardH + cardGap;
            Rect setRect = new Rect(actionX, curCardY, maxActionW, cardH);
            DrawCard(setRect, new Color(0.06f, 0.09f, 0.11f, 0.85f));
            DrawBorder(setRect, new Color(0.60f, 0.75f, 0.90f, 0.65f), 1.2f);
            GUI.color = new Color(0.60f, 0.75f, 0.90f, 0.90f);
            GUI.DrawTexture(new Rect(actionX, curCardY, 3f * uiScale, cardH), whiteTexture);

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (14f * uiScale), curCardY, maxActionW * 0.6f, cardH), "⚙️  SETTINGS");
            GUI.color = new Color(0.80f, 0.88f, 0.95f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.Label(new Rect(actionX, curCardY, maxActionW - (14f * uiScale), cardH), "Options ›");
            if (IsCardClicked(104, setRect))
            {
                activeModal = MenuModal.Settings;
            }

            // Card 4: QUIT
            curCardY += cardH + cardGap;
            Rect quitRect = new Rect(actionX, curCardY, maxActionW, cardH);
            DrawCard(quitRect, new Color(0.10f, 0.06f, 0.07f, 0.85f));
            DrawBorder(quitRect, new Color(0.95f, 0.35f, 0.35f, 0.70f), 1.2f);
            GUI.color = new Color(0.95f, 0.35f, 0.35f, 0.90f);
            GUI.DrawTexture(new Rect(actionX, curCardY, 3f * uiScale, cardH), whiteTexture);

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(actionX + (14f * uiScale), curCardY, maxActionW * 0.6f, cardH), "🚪  QUIT");
            GUI.color = new Color(1.0f, 0.55f, 0.55f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.Label(new Rect(actionX, curCardY, maxActionW - (14f * uiScale), cardH), "Exit Game ›");
            if (IsCardClicked(105, quitRect))
            {
                activeModal = MenuModal.QuitConfirm;
            }

            // 7. Bottom Bar (Audio Toggle & Trophy Leaderboard)
            curCardY += cardH + (6f * uiScale);
            float soundW = 120f * uiScale;
            float soundH = 26f * uiScale;
            Rect soundRect = new Rect(actionX, curCardY, soundW, soundH);

            DrawCard(soundRect, new Color(0.08f, 0.12f, 0.10f, 0.90f));
            GUI.color = GameManager.Instance.IsAudioEnabled ? new Color(0.2f, 0.95f, 0.55f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.Label(soundRect, GameManager.Instance.IsAudioEnabled ? "🔊 SOUND: ON" : "🔈 SOUND: OFF");
            if (IsCardClicked(106, soundRect))
            {
                GameManager.Instance.ToggleAudio();
                ShowToast(GameManager.Instance.IsAudioEnabled ? "🔊" : "🔈", GameManager.Instance.IsAudioEnabled ? "Jungle Audio: ON" : "Jungle Audio: MUTED");
            }

            // Trophy Button
            float trophySize = 26f * uiScale;
            Rect trophyRect = new Rect(actionX + maxActionW - trophySize, curCardY, trophySize, trophySize);
            DrawCard(trophyRect, new Color(0.08f, 0.10f, 0.08f, 0.90f));
            GUI.color = new Color(1.0f, 0.85f, 0.25f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
            GUI.Label(trophyRect, "🏆");
            if (IsCardClicked(107, trophyRect))
            {
                activeModal = MenuModal.Leaderboard;
            }

            // Glowing Ticker
            float tickerY = curCardY + soundH + (3f * uiScale);
            float tickerAlpha = 0.60f + Mathf.PingPong(Time.unscaledTime * 0.9f, 0.40f);
            GUI.color = new Color(0.35f, 0.95f, 0.65f, tickerAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.Label(new Rect(0, tickerY, Screen.width, 16f * uiScale), "⚡ TAP START TO OUTRUN THE MONSTER ⚡");
        }
        #endregion

        #region Sub-Modals (Hero Suits, Upgrades, Settings, Leaderboard)
        private void RenderSubModal(float uiScale)
        {
            // Dim Backdrop
            GUI.color = new Color(0, 0, 0, 0.86f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            float modalW = Mathf.Min(Screen.width - (30f * uiScale), 360f * uiScale);
            float modalH = Mathf.Min(Screen.height - (40f * uiScale), 440f * uiScale);
            float modalX = (Screen.width - modalW) * 0.5f;
            float modalY = (Screen.height - modalH) * 0.5f;

            // Modal Box
            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            GUI.color = new Color(0.06f, 0.09f, 0.08f, 0.96f);
            GUI.DrawTexture(modalRect, whiteTexture);
            DrawBorder(modalRect, new Color(1.0f, 0.82f, 0.32f, 0.60f), 1.5f * uiScale);

            // Top Header & Close Button
            float headerH = 40f * uiScale;
            GUI.color = new Color(1.0f, 0.82f, 0.32f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;

            string title = "";
            switch (activeModal)
            {
                case MenuModal.HeroSuits: title = "🕷️ HERO SUITS"; break;
                case MenuModal.Upgrades: title = "⚡ RELIC UPGRADES"; break;
                case MenuModal.Settings: title = "⚙️ GAME SETTINGS"; break;
                case MenuModal.Leaderboard: title = "🏆 GLOBAL STATS"; break;
                case MenuModal.QuitConfirm: title = "🚪 QUIT EXPEDITION"; break;
            }
            GUI.Label(new Rect(modalX + (16f * uiScale), modalY + (8f * uiScale), modalW - (60f * uiScale), headerH), title);

            // Close [X] Button
            float closeSize = 28f * uiScale;
            Rect closeRect = new Rect(modalX + modalW - closeSize - (10f * uiScale), modalY + (8f * uiScale), closeSize, closeSize);
            DrawCard(closeRect, new Color(0.35f, 0.12f, 0.12f, 0.95f));
            GUI.color = new Color(1f, 0.6f, 0.6f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
            GUI.Label(closeRect, "✕");
            if (IsCardClicked(201, closeRect))
            {
                activeModal = MenuModal.None;
                return;
            }

            // Divider
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(modalX + 10, modalY + headerH, modalW - 20, 1), whiteTexture);

            // Modal Body
            float bodyY = modalY + headerH + (12f * uiScale);
            switch (activeModal)
            {
                case MenuModal.HeroSuits:
                    RenderHeroSuitsBody(modalX, bodyY, modalW, uiScale);
                    break;
                case MenuModal.Upgrades:
                    RenderUpgradesBody(modalX, bodyY, modalW, uiScale);
                    break;
                case MenuModal.Settings:
                    RenderSettingsBody(modalX, bodyY, modalW, uiScale);
                    break;
                case MenuModal.Leaderboard:
                    RenderLeaderboardBody(modalX, bodyY, modalW, uiScale);
                    break;
                case MenuModal.QuitConfirm:
                    RenderQuitConfirmBody(modalX, bodyY, modalW, uiScale);
                    break;
            }

            // Bottom Done / Close Button (hidden for QuitConfirm which has own actions)
            if (activeModal != MenuModal.QuitConfirm)
            {
                float doneH = 38f * uiScale;
                float doneY = modalY + modalH - doneH - (12f * uiScale);
                Rect doneRect = new Rect(modalX + (16f * uiScale), doneY, modalW - (32f * uiScale), doneH);
                GUI.color = new Color(0.20f, 0.85f, 0.45f);
                GUI.skin.button.fontSize = Mathf.RoundToInt(13 * uiScale);
                GUI.skin.button.fontStyle = FontStyle.Bold;
                if (IsCardClicked(202, doneRect) || GUI.Button(doneRect, "RETURN TO MENU"))
                {
                    activeModal = MenuModal.None;
                }
            }
        }

        private void RenderHeroSuitsBody(float x, float y, float w, float scale)
        {
            string[] suitNames = { "Classic Red/Blue", "Stealth Black", "Relic Armor" };
            string[] suitDesc = { "Iconic Red & Blue athletic suit", "Carbon weave with stealth glow", "Ancient gilded shrine armor with amber runes" };

            float cardH = 72f * scale;
            float cardW = w - (32f * scale);
            float startX = x + (16f * scale);

            for (int i = 0; i < 3; i++)
            {
                float cardY = y + (i * (cardH + (8f * scale)));
                Rect r = new Rect(startX, cardY, cardW, cardH);

                bool isSelected = GameManager.Instance.SelectedSuitIndex == i;

                GUI.color = isSelected ? new Color(0.12f, 0.28f, 0.22f, 0.95f) : new Color(0.10f, 0.13f, 0.12f, 0.85f);
                GUI.Box(r, "");

                // Suit Title
                GUI.color = isSelected ? new Color(0.25f, 0.95f, 0.65f) : Color.white;
                GUI.skin.label.alignment = TextAnchor.UpperLeft;
                GUI.skin.label.fontSize = Mathf.RoundToInt(13 * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(startX + (12f * scale), cardY + (8f * scale), cardW - (100f * scale), 20f * scale), suitNames[i]);

                // Suit Description
                GUI.color = new Color(0.7f, 0.75f, 0.75f);
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(startX + (12f * scale), cardY + (30f * scale), cardW - (100f * scale), 32f * scale), suitDesc[i]);

                // Equip / Select Button
                float btnW = 80f * scale;
                float btnH = 32f * scale;
                Rect btnRect = new Rect(startX + cardW - btnW - (10f * scale), cardY + (20f * scale), btnW, btnH);

                if (isSelected)
                {
                    GUI.color = new Color(0.20f, 0.85f, 0.45f);
                    GUI.Box(btnRect, "");
                    GUI.color = Color.white;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(10 * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, "EQUIPPED");
                }
                else
                {
                    GUI.color = new Color(0.15f, 0.85f, 0.95f);
                    GUI.skin.button.fontSize = Mathf.RoundToInt(10 * scale);
                    GUI.skin.button.fontStyle = FontStyle.Bold;
                    if (IsCardClicked(300 + i, btnRect) || GUI.Button(btnRect, "SELECT"))
                    {
                        GameManager.Instance.SelectSuit(i);
                        ShowToast("🕷️", $"{suitNames[i]} Equipped!");
                    }
                }
            }
        }

        private void RenderUpgradesBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float cardW = w - (32f * scale);

            // Banked Hearts Header
            GUI.color = new Color(1.0f, 0.35f, 0.70f);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX, y, cardW, 24f * scale), $"💖 Available: {GameManager.Instance.TotalBankedCoins} Hearts");

            float curY = y + (26f * scale);
            float cardH = 64f * scale;
            float gap = 6f * scale;

            // 1. Shield Relic
            RenderUpgradeCard(startX, curY, cardW, cardH, scale,
                "🛡️ SHIELD RELIC",
                $"Absorbs 1 collision. Lvl {GameManager.Instance.ShieldLevel}/5",
                "Lvl 2+ starts every run with Shield ready!",
                PowerUpType.Shield, GameManager.Instance.ShieldLevel);

            // 2. Super Boost Speedrun
            curY += cardH + gap;
            RenderUpgradeCard(startX, curY, cardW, cardH, scale,
                "⚡ SUPER BOOST",
                $"3x speed & barrier smash. Lvl {GameManager.Instance.SpeedLevel}/5",
                $"+{(GameManager.Instance.SpeedLevel - 1) * 2}s duration boost (total: {6 + (GameManager.Instance.SpeedLevel - 1) * 2}s)",
                PowerUpType.Speedrun, GameManager.Instance.SpeedLevel);

            // 3. Magnet Relic
            curY += cardH + gap;
            RenderUpgradeCard(startX, curY, cardW, cardH, scale,
                "🧲 RELIC MAGNET",
                $"Draws hearts across lanes. Lvl {GameManager.Instance.MagnetLevel}/5",
                $"Range: {8 + (GameManager.Instance.MagnetLevel - 1) * 3}m across all 3 tracks",
                PowerUpType.Magnet, GameManager.Instance.MagnetLevel);

            // 4. Heart Capacity
            curY += cardH + gap;
            RenderHeartCapacityCard(startX, curY, cardW, cardH, scale);
        }

        private void RenderUpgradeCard(float x, float y, float w, float h, float scale, string name, string levelStr, string desc, PowerUpType type, int currentLvl)
        {
            Rect r = new Rect(x, y, w, h);
            DrawCard(r, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            // Title & Level
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (5f * scale), w - (105f * scale), 18f * scale), name);

            // Level & Desc
            GUI.color = new Color(0.85f, 0.90f, 0.88f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (23f * scale), w - (105f * scale), 15f * scale), levelStr);

            GUI.color = new Color(0.60f, 0.70f, 0.65f);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(x + (10f * scale), y + (38f * scale), w - (105f * scale), 24f * scale), desc);

            // Upgrade Button
            float btnW = 95f * scale;
            float btnH = 32f * scale;
            Rect btnRect = new Rect(x + w - btnW - (8f * scale), y + (16f * scale), btnW, btnH);

            if (currentLvl >= 5)
            {
                GUI.color = new Color(0.35f, 0.40f, 0.38f);
                GUI.Box(btnRect, "");
                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.Label(btnRect, "MAX LEVEL");
            }
            else
            {
                GUI.color = new Color(0.15f, 0.85f, 0.40f);
                GUI.skin.button.fontSize = Mathf.RoundToInt(10 * scale);
                GUI.skin.button.fontStyle = FontStyle.Bold;
                if (IsCardClicked(400 + (int)type, btnRect) || GUI.Button(btnRect, "UPGRADE (5❤️)"))
                {
                    bool ok = GameManager.Instance.UpgradePowerup(type, 5);
                    if (ok)
                    {
                        ShowToast("✨", $"{name} Upgraded to Lvl {GameManager.Instance.GetPowerupLevel(type)}!");
                    }
                    else
                    {
                        ShowToast("❌", "Collect 5 Hearts to Upgrade!");
                    }
                }
            }
        }

        private void RenderHeartCapacityCard(float x, float y, float w, float h, float scale)
        {
            Rect r = new Rect(x, y, w, h);
            DrawCard(r, new Color(0.14f, 0.10f, 0.12f, 0.90f));

            GUI.color = new Color(1.0f, 0.45f, 0.75f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (5f * scale), w - (105f * scale), 18f * scale), "💖 HEART CAPACITY");

            GUI.color = new Color(0.85f, 0.90f, 0.88f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (23f * scale), w - (105f * scale), 15f * scale), $"Life Pool: {GameManager.Instance.MaxLives}/5 Hearts");

            GUI.color = new Color(0.70f, 0.65f, 0.70f);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(x + (10f * scale), y + (38f * scale), w - (105f * scale), 24f * scale), "Survive extra obstacle collisions and traps");

            float btnW = 95f * scale;
            float btnH = 32f * scale;
            Rect btnRect = new Rect(x + w - btnW - (8f * scale), y + (16f * scale), btnW, btnH);

            if (GameManager.Instance.MaxLives >= 5)
            {
                GUI.color = new Color(0.35f, 0.40f, 0.38f);
                GUI.Box(btnRect, "");
                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.Label(btnRect, "MAX HEARTS");
            }
            else
            {
                GUI.color = new Color(1.0f, 0.35f, 0.70f);
                GUI.skin.button.fontSize = Mathf.RoundToInt(10 * scale);
                GUI.skin.button.fontStyle = FontStyle.Bold;
                if (IsCardClicked(450, btnRect) || GUI.Button(btnRect, "UPGRADE (8❤️)"))
                {
                    bool ok = GameManager.Instance.UpgradeHeartCapacity(8);
                    if (ok)
                    {
                        ShowToast("💖", $"Heart Capacity Upgraded to {GameManager.Instance.MaxLives} Hearts!");
                    }
                    else
                    {
                        ShowToast("❌", "Collect 8 Hearts to Upgrade!");
                    }
                }
            }
        }

        private void RenderSettingsBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float contentW = w - (32f * scale);
            float rowH = 36f * scale;

            // 1. Audio Volume & Toggle
            Rect soundRow = new Rect(startX, y, contentW, rowH);
            DrawCard(soundRow, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX + (8f * scale), y, contentW * 0.32f, rowH), "AUDIO VOL");

            // Volume Slider
            float sliderW = contentW * 0.38f;
            float sliderX = startX + (contentW * 0.32f);
            float sliderY = y + (10f * scale);
            float curVol = GameManager.Instance.AudioVolume;
            float newVol = GUI.HorizontalSlider(new Rect(sliderX, sliderY, sliderW, 16f * scale), curVol, 0f, 1f);
            if (Mathf.Abs(newVol - curVol) > 0.02f)
            {
                GameManager.Instance.SetAudioVolume(newVol);
            }

            // Audio Mute/Enable Button
            float btnW = 74f * scale;
            float btnH = 26f * scale;
            Rect soundBtn = new Rect(startX + contentW - btnW - (6f * scale), y + (5f * scale), btnW, btnH);
            GUI.color = GameManager.Instance.IsAudioEnabled ? new Color(0.2f, 0.85f, 0.45f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.skin.button.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.skin.button.fontStyle = FontStyle.Bold;
            if (IsCardClicked(501, soundBtn) || GUI.Button(soundBtn, GameManager.Instance.IsAudioEnabled ? "ENABLED" : "MUTED"))
            {
                GameManager.Instance.ToggleAudio();
                ShowToast(GameManager.Instance.IsAudioEnabled ? "🔊" : "🔈", GameManager.Instance.IsAudioEnabled ? "Audio: ON" : "Audio: MUTED");
            }

            // 2. Control Scheme: Swipe vs Tilt
            float curY = y + rowH + (6f * scale);
            Rect ctrlRow = new Rect(startX, curY, contentW, rowH);
            DrawCard(ctrlRow, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(startX + (8f * scale), curY, contentW * 0.45f, rowH), "CONTROLS");

            float ctrlBtnW = 125f * scale;
            Rect ctrlBtn = new Rect(startX + contentW - ctrlBtnW - (6f * scale), curY + (5f * scale), ctrlBtnW, btnH);
            GUI.color = new Color(0.20f, 0.85f, 0.95f);
            string ctrlName = GameManager.Instance.ControlScheme == 0 ? "👉 SWIPE CONTROLS" : "📱 TILT ACCEL";
            if (IsCardClicked(503, ctrlBtn) || GUI.Button(ctrlBtn, ctrlName))
            {
                int nextScheme = 1 - GameManager.Instance.ControlScheme;
                GameManager.Instance.SetControlScheme(nextScheme);
                ShowToast("🎮", nextScheme == 0 ? "Finger Swipe Controls" : "Tilt Accelerometer Active");
            }

            // 3. Graphics Quality Toggle
            curY += rowH + (6f * scale);
            Rect qualRow = new Rect(startX, curY, contentW, rowH);
            DrawCard(qualRow, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            GUI.color = Color.white;
            GUI.Label(new Rect(startX + (8f * scale), curY, contentW * 0.45f, rowH), "GRAPHICS");

            Rect qualBtn = new Rect(startX + contentW - btnW - (6f * scale), curY + (5f * scale), btnW, btnH);
            GUI.color = new Color(1.0f, 0.85f, 0.25f);
            string qualName = QualitySettings.GetQualityLevel() == 0 ? "LOW" : (QualitySettings.GetQualityLevel() == 1 ? "MEDIUM" : "ULTRA");
            if (IsCardClicked(502, qualBtn) || GUI.Button(qualBtn, qualName))
            {
                int next = (QualitySettings.GetQualityLevel() + 1) % 3;
                QualitySettings.SetQualityLevel(next, true);
                string newQualName = QualitySettings.GetQualityLevel() == 0 ? "LOW" : (QualitySettings.GetQualityLevel() == 1 ? "MEDIUM" : "ULTRA");
                ShowToast("⚙️", $"Graphics Quality: {newQualName}");
            }

            // 4. Haptic Feedback Toggle
            curY += rowH + (6f * scale);
            Rect hapRow = new Rect(startX, curY, contentW, rowH);
            DrawCard(hapRow, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            GUI.color = Color.white;
            GUI.Label(new Rect(startX + (8f * scale), curY, contentW * 0.45f, rowH), "HAPTIC FEEDBACK");

            Rect hapBtn = new Rect(startX + contentW - btnW - (6f * scale), curY + (5f * scale), btnW, btnH);
            GUI.color = GameManager.Instance.IsHapticsEnabled ? new Color(0.20f, 0.85f, 0.45f) : new Color(0.6f, 0.6f, 0.6f);
            if (IsCardClicked(504, hapBtn) || GUI.Button(hapBtn, GameManager.Instance.IsHapticsEnabled ? "ON" : "OFF"))
            {
                GameManager.Instance.SetHapticsEnabled(!GameManager.Instance.IsHapticsEnabled);
                if (GameManager.Instance.IsHapticsEnabled) GameManager.Instance.TriggerHaptic();
                ShowToast("📳", GameManager.Instance.IsHapticsEnabled ? "Haptic Vibration: ON" : "Haptic Vibration: OFF");
            }

            // 5. Controls Help Guide
            curY += rowH + (10f * scale);
            Rect ctrlBox = new Rect(startX, curY, contentW, 110f * scale);
            DrawCard(ctrlBox, new Color(0.08f, 0.11f, 0.10f, 0.95f));

            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.Label(new Rect(startX, curY + (6f * scale), contentW, 18f * scale), "🏃 HOW TO PLAY");

            GUI.color = new Color(0.85f, 0.90f, 0.88f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            float padX = startX + (12f * scale);
            GUI.Label(new Rect(padX, curY + (26f * scale), contentW - (24f * scale), 16f * scale), "•  A / D or ◄ / ► : Switch Lanes");
            GUI.Label(new Rect(padX, curY + (44f * scale), contentW - (24f * scale), 16f * scale), "•  W or ▲ or Space : Jump Hurdles");
            GUI.Label(new Rect(padX, curY + (62f * scale), contentW - (24f * scale), 16f * scale), "•  S or ▼ : Slide Under Obstacles");
            GUI.Label(new Rect(padX, curY + (80f * scale), contentW - (24f * scale), 26f * scale), "•  Mobile: Swipe or Tilt to Dodge & Turn");
        }

        private void RenderQuitConfirmBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float contentW = w - (32f * scale);

            Rect card = new Rect(startX, y + (20f * scale), contentW, 160f * scale);
            DrawCard(card, new Color(0.12f, 0.08f, 0.08f, 0.95f));
            DrawBorder(card, new Color(0.95f, 0.40f, 0.40f, 0.50f), 1.5f);

            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(15 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX, y + (36f * scale), contentW, 26f * scale), "ABANDON EXPEDITION?");

            GUI.color = new Color(0.85f, 0.88f, 0.85f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * scale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(startX + (10f * scale), y + (68f * scale), contentW - (20f * scale), 36f * scale), "Are you sure you want to exit the Spider Temple Escape app?");

            float btnW = (contentW - (12f * scale)) * 0.5f;
            float btnH = 38f * scale;
            float btnY = y + (120f * scale);

            Rect exitRect = new Rect(startX, btnY, btnW, btnH);
            DrawCard(exitRect, new Color(0.75f, 0.18f, 0.18f, 0.95f));
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(exitRect, "EXIT GAME");
            if (IsCardClicked(951, exitRect))
            {
                GameManager.Instance.QuitGame();
            }

            Rect cancelRect = new Rect(startX + btnW + (12f * scale), btnY, btnW, btnH);
            DrawCard(cancelRect, new Color(0.18f, 0.24f, 0.22f, 0.95f));
            GUI.color = new Color(0.35f, 0.95f, 0.65f);
            GUI.Label(cancelRect, "STAY");
            if (IsCardClicked(952, cancelRect))
            {
                activeModal = MenuModal.None;
            }
        }

        private void RenderSettingsModal(float uiScale, bool isMidGame = false)
        {
            GUI.color = new Color(0, 0, 0, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            float modalW = Mathf.Min(Screen.width - (30f * uiScale), 360f * uiScale);
            float modalH = Mathf.Min(Screen.height - (40f * uiScale), 440f * uiScale);
            float modalX = (Screen.width - modalW) * 0.5f;
            float modalY = (Screen.height - modalH) * 0.5f;

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            DrawCard(modalRect, new Color(0.08f, 0.12f, 0.10f, 0.98f));
            DrawBorder(modalRect, new Color(1.0f, 0.82f, 0.32f, 0.60f), 2f);
            DrawCornerBrackets(modalRect, 18f * uiScale, 2f * uiScale, new Color(1.0f, 0.85f, 0.35f));

            float headerH = 40f * uiScale;
            GUI.color = new Color(1.0f, 0.82f, 0.32f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(modalX + (16f * uiScale), modalY + (8f * uiScale), modalW - (60f * uiScale), headerH), "⚙️ GAME SETTINGS");

            // Close button
            float closeSize = 28f * uiScale;
            Rect closeRect = new Rect(modalX + modalW - closeSize - (10f * uiScale), modalY + (8f * uiScale), closeSize, closeSize);
            DrawCard(closeRect, new Color(0.35f, 0.12f, 0.12f, 0.95f));
            GUI.color = new Color(1f, 0.6f, 0.6f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
            GUI.Label(closeRect, "✕");
            if (IsCardClicked(201, closeRect))
            {
                if (isMidGame) isMidGameSettingsOpen = false;
                else activeModal = MenuModal.None;
                return;
            }

            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(modalX + 10, modalY + headerH, modalW - 20, 1), whiteTexture);

            float bodyY = modalY + headerH + (12f * uiScale);
            RenderSettingsBody(modalX, bodyY, modalW, uiScale);

            // Bottom Return Button
            float doneH = 38f * uiScale;
            float doneY = modalY + modalH - doneH - (12f * uiScale);
            Rect doneRect = new Rect(modalX + (16f * uiScale), doneY, modalW - (32f * uiScale), doneH);
            GUI.color = new Color(0.20f, 0.85f, 0.45f);
            GUI.skin.button.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.skin.button.fontStyle = FontStyle.Bold;
            string backText = isMidGame ? "RETURN TO PAUSE" : "RETURN TO MENU";
            if (IsCardClicked(202, doneRect) || GUI.Button(doneRect, backText))
            {
                if (isMidGame) isMidGameSettingsOpen = false;
                else activeModal = MenuModal.None;
            }
        }

        private void RenderLeaderboardBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float contentW = w - (32f * scale);

            // Stats Card
            Rect statsBox = new Rect(startX, y, contentW, 220f * scale);
            DrawCard(statsBox, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            float lineY = y + (12f * scale);
            float lineH = 34f * scale;

            // Stat 1: Best Distance
            GUI.color = new Color(0.35f, 0.90f, 0.55f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX, lineY, contentW, 16f * scale), "BEST RUN DISTANCE");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(18 * scale);
            GUI.Label(new Rect(startX, lineY + (16f * scale), contentW, 24f * scale), $"{GameManager.Instance.HighScore:N0} METERS");

            // Stat 2: Total Hearts
            lineY += lineH + (16f * scale);
            GUI.color = new Color(1.0f, 0.35f, 0.70f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.Label(new Rect(startX, lineY, contentW, 16f * scale), "TOTAL HEARTS BANKED");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(18 * scale);
            GUI.Label(new Rect(startX, lineY + (16f * scale), contentW, 24f * scale), $"{GameManager.Instance.TotalBankedCoins} HEARTS");

            // Stat 3: Player Rank
            lineY += lineH + (16f * scale);
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.Label(new Rect(startX, lineY, contentW, 16f * scale), "EXPEDITION RANK");
            GUI.color = new Color(0.25f, 0.95f, 0.85f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * scale);
            GUI.Label(new Rect(startX, lineY + (16f * scale), contentW, 24f * scale), "LEVEL 32 • HERO RUNNER");

            // Stat 4: World Standing
            lineY += lineH + (16f * scale);
            GUI.color = new Color(0.7f, 0.75f, 0.75f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * scale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(startX, lineY, contentW, 20f * scale), "🏆 Global Ranking: Top 1% Temple Survivors");
        }
        #endregion

        #region In-Game HUD
        private void RenderInGameHUD()
        {
            if (GameManager.Instance == null) return;

            float uiScale = Mathf.Clamp(Screen.width / 420.0f, 1.0f, 2.8f);

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeLeft = Mathf.Max(16f * uiScale, safe.x + 10f * uiScale);
            float safeTop = Mathf.Max(16f * uiScale, topOffset + 10f * uiScale);

            // 1. Top-Left: Pause Button (⏸)
            float pauseSize = 42f * uiScale;
            Rect pauseRect = new Rect(safeLeft, safeTop, pauseSize, pauseSize);
            DrawCard(pauseRect, new Color(0.08f, 0.12f, 0.10f, 0.88f));
            DrawBorder(pauseRect, new Color(1.0f, 0.82f, 0.32f, 0.40f), 1.5f);

            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(18 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(pauseRect, "⏸");
            if (IsCardClicked(901, pauseRect))
            {
                isMidGameSettingsOpen = false;
                GameManager.Instance.PauseGame();
            }

            // 2. Active Power-Ups Tray (Renders directly below pause button, completely avoiding top-bar overlap)
            RenderActivePowerUpsTray(safeLeft, safeTop + pauseSize + (8f * uiScale), uiScale);

            // 3. Top-Right: Distance & Score Dual Pill + Hearts Pool Pill
            float pillW = 180f * uiScale;
            float pillX = Screen.width - safeLeft - pillW;

            // Pill 1: Distance & Score
            float distH = 34f * uiScale;
            Rect distRect = new Rect(pillX, safeTop, pillW, distH);
            DrawCard(distRect, new Color(0.06f, 0.10f, 0.08f, 0.88f));
            DrawBorder(distRect, new Color(0.30f, 0.85f, 0.55f, 0.35f), 1f);

            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = new Color(0.35f, 0.95f, 0.65f);
            GUI.Label(new Rect(pillX + (8f * uiScale), safeTop, pillW * 0.55f, distH), $"📍 {GameManager.Instance.DistanceTraveled:N0}m");

            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.color = new Color(1.0f, 0.85f, 0.25f);
            GUI.Label(new Rect(pillX, safeTop, pillW - (8f * uiScale), distH), $"⭐ {GameManager.Instance.Score:N0}");

            // Pill 2: Real Heart Collection Meter (Segmented gauge, heartbeat pulse, shimmer sheen, and floating +1 popup)
            float heartY = safeTop + distH + (6f * uiScale);
            RenderHeartMeter(pillX, heartY, pillW, uiScale);
        }

        private void RenderHeartMeter(float x, float y, float width, float uiScale)
        {
            if (GameManager.Instance == null) return;

            int coins = GameManager.Instance.CoinsCollected;
            int curLives = GameManager.Instance.CurrentLives;
            int maxLives = Mathf.Max(1, GameManager.Instance.MaxLives);

            // Heart Collection Progress: fills towards every 10-heart collection streak
            int tierGoal = 10;
            int currentInTier = coins % tierGoal;
            float targetFill = (coins == 0) ? 0f : ((currentInTier == 0) ? 1f : (float)currentInTier / tierGoal);

            // Smooth animated fill
            animatedHeartFill = Mathf.Lerp(animatedHeartFill, targetFill, Time.unscaledDeltaTime * 10f);

            float meterH = 46f * uiScale;
            Rect meterRect = new Rect(x, y, width, meterH);

            // 1. Obsidian Glass Card Container
            float bloomAlpha = Mathf.Clamp01(heartBloomTimer / 0.9f);
            DrawCard(meterRect, new Color(0.08f, 0.04f, 0.06f, 0.88f));

            // Dynamic border: Low-health danger strobe, collection bloom, or neon rose
            Color borderColor;
            if (curLives <= 2)
            {
                float dangerPulse = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
                borderColor = Color.Lerp(new Color(1f, 0.20f, 0.30f, 0.95f), new Color(1f, 0.65f, 0.15f, 1f), dangerPulse);
            }
            else if (bloomAlpha > 0f)
            {
                borderColor = Color.Lerp(new Color(1f, 0.35f, 0.75f, 0.65f), new Color(1f, 0.85f, 1f, 1f), bloomAlpha);
            }
            else
            {
                borderColor = new Color(1f, 0.30f, 0.65f, 0.55f);
            }
            DrawBorder(meterRect, borderColor, (curLives <= 2 || bloomAlpha > 0f) ? 1.8f : 1.2f);

            // 2. Header Row: Beating Heart Icon + Hearts Collected (Coin System) & HP
            // Realistic Lub-Dub Heartbeat Pulse
            float beat = Mathf.Sin(Time.unscaledTime * 5.0f);
            float pulseScale = 1.0f;
            if (beat > 0.6f) pulseScale += (beat - 0.6f) * 0.35f;
            if (bloomAlpha > 0f) pulseScale += bloomAlpha * 0.25f;

            float padX = 8f * uiScale;
            float topY = y + (3f * uiScale);

            // Left: Beating Heart Label with live Coins/Hearts Collected
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale * pulseScale);
            GUI.color = (bloomAlpha > 0f) ? new Color(1f, 0.85f, 0.95f) : new Color(1f, 0.40f, 0.75f);
            GUI.Label(new Rect(x + padX, topY, width * 0.65f, 18f * uiScale), $"💖 HEARTS: {coins}");

            // Right: Health Pool
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.color = (curLives <= 2) ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.90f, 0.95f);
            GUI.Label(new Rect(x, topY, width - padX, 18f * uiScale), $"HP {curLives}/{maxLives} ❤️");

            // 3. Segmented Glowing Fill Gauge for Heart Collection
            float gaugeX = x + padX;
            float gaugeY = y + (23f * uiScale);
            float gaugeW = width - (padX * 2f);
            float gaugeH = 14f * uiScale;

            // Gauge Backplate
            GUI.color = new Color(0.03f, 0.02f, 0.04f, 0.92f);
            GUI.DrawTexture(new Rect(gaugeX, gaugeY, gaugeW, gaugeH), whiteTexture);
            DrawBorder(new Rect(gaugeX, gaugeY, gaugeW, gaugeH), new Color(1f, 1f, 1f, 0.15f), 1f);

            // Filled Bar
            float fillW = gaugeW * animatedHeartFill;
            if (fillW > 0.5f)
            {
                // Radiant Gradient Fill: Hot Pink to Coral
                Color fillColor = Color.Lerp(new Color(1f, 0.15f, 0.55f, 0.95f), new Color(1f, 0.65f, 0.85f, 0.95f), bloomAlpha);
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(gaugeX + 1, gaugeY + 1, fillW - 2, gaugeH - 2), whiteTexture);

                // Top Sheen Line
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(gaugeX + 1, gaugeY + 1, fillW - 2, 2f), whiteTexture);

                // Sweeping Shimmer Beam Effect
                float sweepPhase = Mathf.Repeat(Time.unscaledTime * 0.75f, 1.3f);
                if (sweepPhase <= 1.0f)
                {
                    float sweepX = gaugeX + (sweepPhase * fillW);
                    float sweepW = Mathf.Min(18f * uiScale, gaugeX + fillW - sweepX);
                    if (sweepW > 0)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.45f);
                        GUI.DrawTexture(new Rect(sweepX, gaugeY + 1, sweepW, gaugeH - 2), whiteTexture);
                    }
                }
            }

            // 10-Segment Dividers (Pips)
            int segments = tierGoal;
            float segW = gaugeW / segments;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            for (int i = 1; i < segments; i++)
            {
                float tickX = gaugeX + (i * segW);
                GUI.DrawTexture(new Rect(tickX, gaugeY, 1.5f, gaugeH), whiteTexture);
            }

            // 4. Floating +1 💖 Pickup Popup
            if (heartCollectFloatTimer > 0f)
            {
                float floatRatio = heartCollectFloatTimer / 1.2f;
                float floatY = y - (16f * uiScale) - ((1f - floatRatio) * 18f * uiScale);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;

                // Soft shadow
                GUI.color = new Color(0f, 0f, 0f, floatRatio * 0.8f);
                GUI.Label(new Rect(x + 1, floatY + 1, width, 18f * uiScale), "+1 💖 HEART!");

                // Front glow
                GUI.color = new Color(1f, 0.45f, 0.85f, floatRatio);
                GUI.Label(new Rect(x, floatY, width, 18f * uiScale), "+1 💖 HEART!");
            }
        }

        private void RenderActivePowerUpsTray(float x, float y, float uiScale)
        {
            if (PickupManager.Instance == null) return;

            var activeList = PickupManager.Instance.GetActivePowerUps();
            if (activeList == null || activeList.Count == 0)
                return;

            float curY = y;
            float pillW = 125f * uiScale;
            float pillH = 26f * uiScale;
            float gap = 5f * uiScale;

            foreach (var pu in activeList)
            {
                Color themeColor;
                string icon;
                string name;

                switch (pu.Type)
                {
                    case PowerUpType.Speedrun:
                        themeColor = new Color(0.15f, 0.95f, 1.0f);
                        icon = "⚡";
                        name = "BOOST";
                        break;
                    case PowerUpType.Shield:
                        themeColor = new Color(0.20f, 0.95f, 0.75f);
                        icon = "🛡️";
                        name = "SHIELD";
                        break;
                    default:
                        themeColor = new Color(1.0f, 0.85f, 0.20f);
                        icon = "🧲";
                        name = "MAGNET";
                        break;
                }

                Rect pillRect = new Rect(x, curY, pillW, pillH);
                DrawCard(pillRect, new Color(0.06f, 0.09f, 0.12f, 0.88f));

                // Progress fill bar inside card
                float fillW = pillW * Mathf.Clamp01(pu.Progress);
                if (fillW > 0.5f)
                {
                    GUI.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.35f);
                    GUI.DrawTexture(new Rect(x, curY, fillW, pillH), whiteTexture);
                }

                // Left border accent
                GUI.color = themeColor;
                GUI.DrawTexture(new Rect(x, curY, 3.5f * uiScale, pillH), whiteTexture);

                // Icon + Name
                GUI.skin.label.alignment = TextAnchor.MiddleLeft;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.color = Color.white;
                GUI.Label(new Rect(x + (7f * uiScale), curY, pillW * 0.65f, pillH), $"{icon} {name}");

                // Remaining time or Hits
                GUI.skin.label.alignment = TextAnchor.MiddleRight;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * uiScale);
                GUI.color = themeColor;
                string timeStr = pu.Type == PowerUpType.Shield ? "ACTIVE" : $"{pu.TimeRemaining:F1}s";
                GUI.Label(new Rect(x, curY, pillW - (6f * uiScale), pillH), timeStr);

                curY += pillH + gap;
            }
        }
        #endregion

        #region In-Game Pause System
        private void RenderPauseModal(float uiScale)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Dark backdrop overlay
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            float modalW = Mathf.Min(Screen.width - (36f * uiScale), 320f * uiScale);
            float modalH = Mathf.Min(Screen.height - (60f * uiScale), 380f * uiScale);
            float modalX = (Screen.width - modalW) * 0.5f;
            float modalY = (Screen.height - modalH) * 0.5f;

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            DrawCard(modalRect, new Color(0.08f, 0.11f, 0.10f, 0.98f));
            DrawBorder(modalRect, new Color(1.0f, 0.82f, 0.32f, 0.60f), 2f);
            DrawCornerBrackets(modalRect, 20f * uiScale, 2.5f * uiScale, new Color(1.0f, 0.85f, 0.35f));

            // Header Title
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(17 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(modalX, modalY + (18f * uiScale), modalW, 26f * uiScale), "⏸ GAME PAUSED");

            GUI.color = new Color(0.65f, 0.75f, 0.70f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Italic;
            GUI.Label(new Rect(modalX, modalY + (44f * uiScale), modalW, 18f * uiScale), "EXPEDITION SUSPENDED");

            float btnW = modalW - (44f * uiScale);
            float btnH = 46f * uiScale;
            float btnX = modalX + (22f * uiScale);
            float curY = modalY + (74f * uiScale);
            float gap = 12f * uiScale;

            // 1. RESUME (3-second countdown)
            Rect resumeRect = new Rect(btnX, curY, btnW, btnH);
            DrawCard(resumeRect, new Color(0.95f, 0.75f, 0.15f, 0.95f));
            DrawBorder(resumeRect, new Color(1.0f, 0.95f, 0.60f), 1.5f);
            GUI.color = new Color(0.10f, 0.08f, 0.02f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(resumeRect, "▶  RESUME EXPEDITION");
            if (IsCardClicked(801, resumeRect))
            {
                resumeCountdownTimer = 3.6f;
            }

            // 2. SETTINGS
            curY += btnH + gap;
            Rect settingsRect = new Rect(btnX, curY, btnW, btnH);
            DrawCard(settingsRect, new Color(0.12f, 0.18f, 0.16f, 0.90f));
            DrawBorder(settingsRect, new Color(0.25f, 0.85f, 0.65f, 0.50f), 1.2f);
            GUI.color = Color.white;
            GUI.Label(settingsRect, "⚙️  SETTINGS");
            if (IsCardClicked(802, settingsRect))
            {
                isMidGameSettingsOpen = true;
            }

            // 3. RESTART (Fresh 0m)
            curY += btnH + gap;
            Rect restartRect = new Rect(btnX, curY, btnW, btnH);
            DrawCard(restartRect, new Color(0.12f, 0.18f, 0.16f, 0.90f));
            DrawBorder(restartRect, new Color(0.25f, 0.75f, 0.95f, 0.50f), 1.2f);
            GUI.color = Color.white;
            GUI.Label(restartRect, "🔄  RESTART (0m)");
            if (IsCardClicked(803, restartRect))
            {
                GameManager.Instance.RestartGame();
            }

            // 4. QUIT TO MENU (Saves relics)
            curY += btnH + gap;
            Rect menuRect = new Rect(btnX, curY, btnW, btnH);
            DrawCard(menuRect, new Color(0.20f, 0.10f, 0.10f, 0.90f));
            DrawBorder(menuRect, new Color(0.95f, 0.40f, 0.40f, 0.50f), 1.2f);
            GUI.color = new Color(1.0f, 0.65f, 0.65f);
            GUI.Label(menuRect, "🏠  QUIT TO MENU");
            if (IsCardClicked(804, menuRect))
            {
                Time.timeScale = 1.0f;
                StartLoading(1.6f, "RETURNING TO CAMP...", () =>
                {
                    GameManager.Instance?.ReturnToMenu();
                }, "RETURNING TO BASE CAMP", "SAVING EXPEDITION DATA");
            }
        }

        private void RenderResumeCountdown(float uiScale)
        {
            float t = resumeCountdownTimer;
            string text = "GO!";
            Color col = new Color(0.20f, 1.0f, 0.50f);
            if (t > 2.7f)
            {
                text = "3";
                col = new Color(1.0f, 0.85f, 0.20f);
            }
            else if (t > 1.8f)
            {
                text = "2";
                col = new Color(1.0f, 0.60f, 0.20f);
            }
            else if (t > 0.9f)
            {
                text = "1";
                col = new Color(0.30f, 0.85f, 1.0f);
            }
            else
            {
                text = "GO!";
                col = new Color(0.20f, 1.0f, 0.50f);
            }

            float frac = t - Mathf.Floor(t);
            float pulse = 1.0f + Mathf.Sin(frac * Mathf.PI) * 0.35f;

            float size = 160f * uiScale * pulse;
            Rect r = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);

            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.DrawTexture(new Rect(r.x - 20, r.y - 20, r.width + 40, r.height + 40), whiteTexture);

            GUI.color = col;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(48 * uiScale * pulse);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(r, text);
        }
        #endregion

        #region Game Over Modal (High-Contrast & Responsive Layout)
        private void RenderGameOverModal()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float scaleW = Screen.width / 390.0f;
            float scaleH = Screen.height / 780.0f;
            float uiScale = Mathf.Clamp(Mathf.Min(scaleW, scaleH), 0.75f, 2.2f);

            // 1. Atmospheric Dark Backdrop with Death Background Art
            if (deathBgTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), deathBgTexture, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0.04f, 0.02f, 0.03f, 0.72f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
            }
            else
            {
                GUI.color = new Color(0.04f, 0.03f, 0.05f, 0.92f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
            }

            // Top subtle danger vignette
            GUI.color = new Color(0.35f, 0.04f, 0.07f, 0.30f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 140f * uiScale), whiteTexture);

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeLeft = Mathf.Max(16f * uiScale, safe.x + 8f * uiScale);
            float safeTop = Mathf.Max(12f * uiScale, topOffset + 8f * uiScale);

            // 2. Top Header Status Bar
            float livesW = 120f * uiScale;
            float livesH = 28f * uiScale;
            Rect livesRect = new Rect(safeLeft, safeTop, livesW, livesH);
            DrawCard(livesRect, new Color(0.12f, 0.04f, 0.06f, 0.90f));
            GUI.color = new Color(1.0f, 0.40f, 0.55f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(livesRect, $"💖 LIVES: {GameManager.Instance.CurrentLives}/{GameManager.Instance.MaxLives}");

            float zoneW = 125f * uiScale;
            float zoneH = 28f * uiScale;
            Rect zoneRect = new Rect(Screen.width - safeLeft - zoneW, safeTop, zoneW, zoneH);
            DrawCard(zoneRect, new Color(0.10f, 0.08f, 0.02f, 0.90f));
            GUI.color = new Color(1.0f, 0.88f, 0.35f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * uiScale);
            GUI.Label(zoneRect, "SPIDER SHRINE");

            // 3. Hero Defeat Banner: Cause Badge & Bold Title
            GetDeathDetails(lastDeathType, out string causeTag, out string deathTitle, out string deathSubtitle);

            float centerAreaY = safeTop + livesH + (10f * uiScale);

            // Defeat Icon
            float skullSize = 42f * uiScale;
            Rect skullRect = new Rect((Screen.width - skullSize) * 0.5f, centerAreaY, skullSize, skullSize);
            DrawCard(skullRect, new Color(0.35f, 0.08f, 0.10f, 0.92f));
            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(22 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(skullRect, "💀");

            // Cause Tag Pill
            float tagW = 160f * uiScale;
            float tagH = 22f * uiScale;
            float tagY = centerAreaY + skullSize + (6f * uiScale);
            Rect tagRect = new Rect((Screen.width - tagW) * 0.5f, tagY, tagW, tagH);
            DrawCard(tagRect, new Color(0.22f, 0.05f, 0.06f, 0.92f));
            GUI.color = new Color(1.0f, 0.72f, 0.72f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.Label(tagRect, causeTag);

            // Title
            float titleY = tagY + tagH + (4f * uiScale);
            float titleH = 34f * uiScale;
            int titleFont = Mathf.RoundToInt(24 * uiScale);

            GUI.color = new Color(0.02f, 0.01f, 0.01f, 0.95f);
            GUI.skin.label.fontSize = titleFont;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(2f, titleY + 2f, Screen.width, titleH), deathTitle);

            GUI.color = new Color(1.0f, 0.95f, 0.95f);
            GUI.Label(new Rect(0, titleY, Screen.width, titleH), deathTitle);

            // Subtitle
            float subY = titleY + titleH;
            GUI.color = new Color(1.0f, 0.85f, 0.55f, 0.95f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Italic;
            GUI.Label(new Rect(0, subY, Screen.width, 18f * uiScale), deathSubtitle);

            // 4. Run Summary Card (High Contrast, Crisp Text, Normal Flat Surface)
            float cardW = Mathf.Min(Screen.width - (32f * uiScale), 350f * uiScale);
            float cardH = 160f * uiScale;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = subY + (10f * uiScale);

            Rect summaryRect = new Rect(cardX, cardY, cardW, cardH);
            DrawCard(summaryRect, new Color(0.07f, 0.09f, 0.14f, 0.96f));

            // Record Badge Pill
            bool isNewRecord = lastFinalScore >= GameManager.Instance.HighScore && lastFinalScore > 0;
            float recW = 140f * uiScale;
            float recH = 20f * uiScale;
            Rect recRect = new Rect(cardX + (cardW - recW) * 0.5f, cardY + (8f * uiScale), recW, recH);
            DrawCard(recRect, new Color(0.24f, 0.18f, 0.05f, 0.92f));
            GUI.color = new Color(1.0f, 0.92f, 0.45f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(recRect, isNewRecord ? "★ NEW BEST RECORD! ★" : "★ RUN SUMMARY ★");

            // Metric 1: Distance Reached (Golden High Contrast)
            float metricY = cardY + (32f * uiScale);
            GUI.color = new Color(0.85f, 0.90f, 0.95f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(cardX, metricY, cardW, 14f * uiScale), "DISTANCE TRAVELED");

            GUI.color = new Color(1.0f, 0.88f, 0.20f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(26 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(cardX, metricY + (14f * uiScale), cardW, 32f * uiScale), $"{GameManager.Instance.DistanceTraveled:N0} M");

            // Horizontal Divider
            float divY = metricY + (48f * uiScale);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(cardX + 16, divY, cardW - 32, 1), whiteTexture);

            // Secondary Metrics Grid: Hearts Gathered & Expedition Score
            float gridY = divY + (8f * uiScale);
            float colW = (cardW - (28f * uiScale)) * 0.5f;

            // Left Col: Hearts Gathered (Neon Pink High Contrast)
            Rect relicsBox = new Rect(cardX + (10f * uiScale), gridY, colW, 48f * uiScale);
            DrawCard(relicsBox, new Color(0.04f, 0.06f, 0.09f, 0.90f));

            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.color = new Color(0.85f, 0.90f, 0.95f);
            GUI.Label(new Rect(relicsBox.x, relicsBox.y + (4f * uiScale), colW, 14f * uiScale), "HEARTS GATHERED");

            GUI.skin.label.fontSize = Mathf.RoundToInt(17 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = new Color(1.0f, 0.35f, 0.70f);
            GUI.Label(new Rect(relicsBox.x, relicsBox.y + (20f * uiScale), colW, 22f * uiScale), $"💖 {GameManager.Instance.CoinsCollected:N0}");

            // Right Col: Expedition Score (Vibrant Cyan High Contrast)
            Rect scoreBox = new Rect(cardX + cardW - colW - (10f * uiScale), gridY, colW, 48f * uiScale);
            DrawCard(scoreBox, new Color(0.04f, 0.06f, 0.09f, 0.90f));

            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.color = new Color(0.85f, 0.90f, 0.95f);
            GUI.Label(new Rect(scoreBox.x, scoreBox.y + (4f * uiScale), colW, 14f * uiScale), "EXPEDITION SCORE");

            GUI.skin.label.fontSize = Mathf.RoundToInt(17 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = new Color(0.20f, 0.92f, 1.0f);
            GUI.Label(new Rect(scoreBox.x, scoreBox.y + (20f * uiScale), colW, 22f * uiScale), $"⭐ {lastFinalScore:N0}");

            // 5. Action Buttons (Normal Solid Clean Buttons)
            float footerY = cardY + cardH + (12f * uiScale);
            float btnW = cardW;
            float btnH = 46f * uiScale;
            float btnX = cardX;

            // 1. PLAY AGAIN (Solid Emerald)
            Rect playAgainRect = new Rect(btnX, footerY, btnW, btnH);
            DrawCard(playAgainRect, new Color(0.12f, 0.76f, 0.35f, 0.98f));
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(new Rect(btnX + 4, footerY + 2, btnW - 8, 2f), whiteTexture);

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(15 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(playAgainRect, "▶   PLAY AGAIN");

            if ((IsCardClicked(601, playAgainRect) || GUI.Button(playAgainRect, GUIContent.none, GUIStyle.none)) && gameOverDuration > 0.20f)
            {
                isGameOver = false;
                Time.timeScale = 1.0f;
                GameManager.Instance?.RestartGame();
            }

            // 2. MAIN MENU (Solid Slate Blue-Gray)
            float menuY = footerY + btnH + (8f * uiScale);
            float menuH = 40f * uiScale;
            Rect mainMenuRect = new Rect(btnX, menuY, btnW, menuH);
            DrawCard(mainMenuRect, new Color(0.16f, 0.20f, 0.28f, 0.96f));

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(mainMenuRect, "🏠   MAIN MENU");

            if ((IsCardClicked(602, mainMenuRect) || GUI.Button(mainMenuRect, GUIContent.none, GUIStyle.none)) && gameOverDuration > 0.20f)
            {
                isGameOver = false;
                Time.timeScale = 1.0f;
                StartLoading(1.6f, "RETURNING TO CAMP...", () =>
                {
                    GameManager.Instance?.ReturnToMenu();
                }, "RETURNING TO BASE CAMP", "SAVING EXPEDITION DATA");
            }
        }
        #endregion

        #region Toast Notification
        private void RenderToast()
        {
            if (toastTimer <= 0f) return;

            float uiScale = Mathf.Clamp(Screen.width / 390.0f, 0.80f, 2.2f);
            float toastW = Mathf.Min(Screen.width - (40f * uiScale), 280f * uiScale);
            float toastH = 36f * uiScale;
            float toastX = (Screen.width - toastW) * 0.5f;
            float toastY = 60f * uiScale;

            float alpha = Mathf.Clamp01(toastTimer / 0.35f);
            Rect toastRect = new Rect(toastX, toastY, toastW, toastH);
            GUI.color = new Color(0.04f, 0.08f, 0.07f, 0.92f * alpha);
            GUI.DrawTexture(toastRect, whiteTexture);
            DrawBorder(toastRect, new Color(0.20f, 0.95f, 0.85f, 0.60f * alpha), 1.2f);

            GUI.color = new Color(0.20f, 0.95f, 0.85f, alpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(toastX, toastY, toastW, toastH), $"{toastIcon}  {toastMessage}");
        }
        #endregion

        #region Loading Screen System
        public void StartLoading(float duration, string statusText, Action onFinished = null, string header = "SPIDER TEMPLE ESCAPE", string subHeader = "ENDLESS 3D JUNGLE RUNNER")
        {
            isLoading = true;
            isLoadingFadingOut = false;
            loadingTimer = 0f;
            loadingDuration = Mathf.Max(0.6f, duration);
            loadingProgress = 0f;
            loadingFadeAlpha = 1f;
            loadingStatusText = statusText;
            loadingHeader = header;
            loadingSubHeader = subHeader;
            onLoadingFinished = onFinished;
            tipChangeTimer = 0f;
            currentTipIndex = UnityEngine.Random.Range(0, LoadingTips.Length);
        }

        private void UpdateLoading()
        {
            if (!isLoading) return;

            if (!isLoadingFadingOut)
            {
                loadingTimer += Time.unscaledDeltaTime;
                float rawT = Mathf.Clamp01(loadingTimer / loadingDuration);
                // Realistic smooth ease loading curve
                loadingProgress = Mathf.Sin(rawT * Mathf.PI * 0.5f);

                tipChangeTimer += Time.unscaledDeltaTime;
                if (tipChangeTimer > 2.2f)
                {
                    tipChangeTimer = 0f;
                    currentTipIndex = (currentTipIndex + 1) % LoadingTips.Length;
                }

                if (loadingTimer >= loadingDuration)
                {
                    loadingProgress = 1f;
                    isLoadingFadingOut = true;
                    try
                    {
                        onLoadingFinished?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    onLoadingFinished = null;
                }
            }
            else
            {
                // Smooth fade out into destination scene
                loadingFadeAlpha -= Time.unscaledDeltaTime * 2.8f;
                if (loadingFadeAlpha <= 0f)
                {
                    loadingFadeAlpha = 0f;
                    isLoading = false;
                    isLoadingFadingOut = false;
                }
            }
        }

        private void RenderLoadingScreen()
        {
            if (loadingFadeAlpha <= 0f) return;

            float uiScale = Mathf.Clamp(Screen.width / 390.0f, 0.80f, 2.4f);

            // 1. Clean, Pitch-Black Obsidian Background (No background art)
            GUI.color = new Color(0.04f, 0.05f, 0.06f, loadingFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            // Absorb pointer clicks on the full screen
            GUI.color = Color.clear;
            GUI.Button(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);

            // 2. Safe Area calculations
            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeTop = Mathf.Max(42f * uiScale, topOffset + (30f * uiScale));

            // 3. Header & Sub-header Branding
            GUI.color = new Color(0.30f, 0.95f, 0.80f, 0.90f * loadingFadeAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(0, safeTop, Screen.width, 20f * uiScale), loadingSubHeader);

            GUI.color = new Color(1.0f, 0.92f, 0.72f, loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(22 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(0, safeTop + (20f * uiScale), Screen.width, 36f * uiScale), loadingHeader);

            // Golden Accent Divider
            float divW = Mathf.Min(Screen.width * 0.72f, 260f * uiScale);
            float divX = (Screen.width - divW) * 0.5f;
            float divY = safeTop + (60f * uiScale);
            GUI.color = new Color(0.95f, 0.78f, 0.25f, 0.75f * loadingFadeAlpha);
            GUI.DrawTexture(new Rect(divX, divY, divW, 2f * uiScale), whiteTexture);

            // 4. Clean Centered Progress Bar & Status (No middle icon)
            float barW = Mathf.Min(Screen.width - (48f * uiScale), 330f * uiScale);
            float barH = 16f * uiScale;
            float barX = (Screen.width - barW) * 0.5f;
            float barY = Screen.height * 0.48f;

            // Animated status text with pulsating dots
            int dotCount = ((int)(Time.unscaledTime * 3f)) % 4;
            string animatedStatus = loadingStatusText.TrimEnd('.') + new string('.', dotCount);

            GUI.color = new Color(0.35f, 0.95f, 0.85f, 0.95f * loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(barX + 2, barY - (26f * uiScale), barW * 0.72f, 22f * uiScale), animatedStatus);

            // Percentage on the right
            int pct = Mathf.RoundToInt(Mathf.Clamp01(loadingProgress) * 100f);
            GUI.color = new Color(1.0f, 0.85f, 0.25f, loadingFadeAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(barX + (barW * 0.72f), barY - (26f * uiScale), barW * 0.28f, 22f * uiScale), $"{pct}%");

            // Progress Bar Outer Track
            Rect barOuterRect = new Rect(barX, barY, barW, barH);
            DrawCard(barOuterRect, new Color(0.08f, 0.12f, 0.14f, 0.95f * loadingFadeAlpha));
            DrawBorder(barOuterRect, new Color(0.25f, 0.85f, 0.70f, 0.60f * loadingFadeAlpha), 1.2f);

            // Progress Bar Inner Fill
            float innerPad = 2.5f * uiScale;
            float maxFillW = barW - (innerPad * 2f);
            float currentFillW = maxFillW * Mathf.Clamp01(loadingProgress);
            if (currentFillW > 0f)
            {
                Rect fillRect = new Rect(barX + innerPad, barY + innerPad, currentFillW, barH - (innerPad * 2f));
                GUI.color = new Color(0.18f, 0.95f, 0.68f, 0.98f * loadingFadeAlpha);
                GUI.DrawTexture(fillRect, whiteTexture);

                // Top glossy shine highlight
                GUI.color = new Color(1f, 1f, 1f, 0.35f * loadingFadeAlpha);
                GUI.DrawTexture(new Rect(fillRect.x, fillRect.y, fillRect.width, fillRect.height * 0.50f), whiteTexture);
            }

            // 5. Polished Tip Card at the Bottom (Enlarged, word-wrapped, spacious, no clipping!)
            float tipW = Mathf.Min(Screen.width - (36f * uiScale), 350f * uiScale);
            float tipH = 58f * uiScale;
            float tipX = (Screen.width - tipW) * 0.5f;
            float tipY = Screen.height - tipH - Mathf.Max(32f * uiScale, (Screen.safeArea.y > 0 ? Screen.safeArea.y : 22f * uiScale));

            Rect tipRect = new Rect(tipX, tipY, tipW, tipH);
            DrawCard(tipRect, new Color(0.06f, 0.10f, 0.09f, 0.92f * loadingFadeAlpha));
            DrawBorder(tipRect, new Color(0.95f, 0.78f, 0.25f, 0.55f * loadingFadeAlpha), 1.2f);
            DrawCornerBrackets(tipRect, 8f * uiScale, 1.5f, new Color(0.95f, 0.78f, 0.25f, 0.85f * loadingFadeAlpha));

            string currentTip = (currentTipIndex >= 0 && currentTipIndex < LoadingTips.Length)
                ? LoadingTips[currentTipIndex]
                : "💡 Avoid obstacles to survive!";

            bool prevWrap = GUI.skin.label.wordWrap;
            TextAnchor prevAlign = GUI.skin.label.alignment;
            int prevSize = GUI.skin.label.fontSize;
            FontStyle prevStyle = GUI.skin.label.fontStyle;

            GUI.skin.label.wordWrap = true;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(12 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;

            // Margin inside tip box
            Rect textRect = new Rect(tipX + (12f * uiScale), tipY + (4f * uiScale), tipW - (24f * uiScale), tipH - (8f * uiScale));
            GUI.color = new Color(1f, 0.95f, 0.85f, 0.95f * loadingFadeAlpha);
            GUI.Label(textRect, currentTip);

            // Restore GUI skin properties
            GUI.skin.label.wordWrap = prevWrap;
            GUI.skin.label.alignment = prevAlign;
            GUI.skin.label.fontSize = prevSize;
            GUI.skin.label.fontStyle = prevStyle;
        }
        #endregion
    }
}
