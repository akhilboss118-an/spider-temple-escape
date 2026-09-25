using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Runner.Core;
using Runner.Pickups;
using Runner.Player;

namespace Runner.UI
{
    public enum MenuModal
    {
        None,
        HeroSuits,
        Upgrades,
        Missions,
        Settings,
        Leaderboard,
        QuitConfirm,
        MapSelect
    }

    /// <summary>
    /// Photo Mode state for free camera screenshots during gameplay.
    /// </summary>
    public enum PhotoModeState
    {
        Off,
        Active
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
        private float deathScreenEntranceTimer = 0f;

        // Main Menu State & Modals
        private MenuModal activeModal = MenuModal.None;
        private Texture2D menuBgTexture;
        private Texture2D deathBgTexture;
        private Texture2D relicTexture;
        private Texture2D suitPreviewTexture;
        private Texture2D loadingBackgroundTexture;
        private Texture2D appLogoTexture;
        private Texture2D whiteTexture;

        // AAA Cinematic Overlay Textures
        private Texture2D vignetteTexture;       // Radial dark vignette for HUD
        private Texture2D vignetteRedTexture;  // Red danger vignette for death
        private Texture2D speedLinesTexture;   // Speed radial blur hint
        private float vignetteTimer = 0f;
        private float speedLineAlpha = 0f;

        // Distance Milestone Celebration Banners (tied to road-tier progression)
        private int lastMilestoneIndex = -1;
        private float milestoneBannerTimer = 0f;
        private string milestoneBannerText = "";
        private string milestoneBannerSub = "";

        private static readonly float[] MilestoneThresholds = new float[]
        {
            100f, 250f, 500f, 750f, 1000f, 1500f, 2000f, 3000f, 4000f, 5000f, 7500f, 10000f
        };

        // In-Game Pause & Countdown State
        private bool isMidGameSettingsOpen = false;
        private float resumeCountdownTimer = 0f;
        private float heartBloomTimer = 0f;

        // Real Heart Collection Meter & Special Effects State
        private float heartBarFill = 0f;
        private float heartTierBurstTimer = 0f;
        private float heartCollectFloatTimer = 0f;
        private int lastRecordedCoins = -1;
        private int lastRecordedLives = -1;

        // Toast Feedback System
        private string toastIcon = "";
        private string toastMessage = "";
        private float toastTimer = 0f;
        private int mainMenuCharOffset = 0;
        private float menuEntranceTimer = 0f;

        // Loading Screen System (Starting Boot & Death -> Main Menu)
        private bool isLoading = true; // Starts TRUE for initial boot loading screen!
        private float loadingTimer = 0f;
        private float loadingDuration = 2.8f;
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
            "🕷️ SPIDER REFLEX // Swipe UP to leap over fallen jungle timber & mossy boulders",
            "🕷️ LOW PROFILE // Swipe DOWN to slide beneath stone temple arches and spike grates",
            "🕷️ ACROBATIC CORNERING // Swipe LEFT or RIGHT before 90° corners to anchor web-lines",
            "⚡ MONSTER BURST // Grab energy cans for invulnerable supersonic barrier speed",
            "💖 SACRED RELICS // Gather glowing Hearts to bank artifacts and survive fatal strikes",
            "🛡️ SHIELD PROTOCOL // Shields absorb one catastrophic obstacle crash without losing momentum",
            "🕷️ ZOMBIE THREAT // The ancient guardian is relentless - keep running!",
            "🕸️ WEB-SLING RECOVERY // Double-check lane indicators before entering foggy temple corridors"
        };

        // First-Time Tutorial Overlay
        private const string TUTORIAL_SHOWN_KEY = "SpiderRunner_TutorialShown";
        private bool showTutorial = false;
        private float tutorialTimer = 0f;
        private int tutorialStep = 0;
        private float tutorialFadeAlpha = 0f;

        // Photo Mode
        private PhotoModeState photoModeState = PhotoModeState.Off;
        private float photoModeSensitivity = 3.0f;
        private float photoModeZoom = 1.0f;
        private float photoModeOrbitAngle = 0f;
        private float photoModeOrbitHeight = 0f;
        private Vector3 photoModeOrbitTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Purge any legacy or stray Canvas GameObjects (preserving InGameCanvasHUD)
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.name.Contains("InGameCanvasHUD")) continue;
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
            InGameCanvasHUD.EnsureInstance();
            Runner.Characters.CharacterManager.EnsureInstance();

            // Keep the boot sequence visible in the Editor too, so the startup experience
            // can be tested before making a device build.
            StartLoading(2.8f, "INITIALIZING EXPEDITION...", null, "SPIDER TEMPLE ESCAPE", "ENDLESS 3D JUNGLE RUNNER");
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
                if (lastRecordedCoins % 10 != 0 && newCoins % 10 == 0)
                {
                    heartTierBurstTimer = 1.1f;
                }
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
                lastMilestoneIndex = -1;
                milestoneBannerTimer = 0f;

                // Show tutorial on first run
                if (!showTutorial && PlayerPrefs.GetInt(TUTORIAL_SHOWN_KEY, 0) == 0)
                {
                    showTutorial = true;
                    tutorialTimer = 0f;
                    tutorialStep = 0;
                    tutorialFadeAlpha = 0f;
                }
            }
            if (state == GameState.Menu)
            {
                menuEntranceTimer = 0f;
            }
            if (InGameCanvasHUD.Instance != null)
            {
                // Let the canvas HUD decide for itself based on state + HUD style
                InGameCanvasHUD.Instance.RefreshVisibility();
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

            if (relicTexture == null)
            {
#if UNITY_EDITOR
                relicTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/golden_spider_relic.png");
                if (relicTexture == null)
                {
                    relicTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/golden_spider_relic.png");
                }
#endif
                if (relicTexture == null)
                {
                    relicTexture = Resources.Load<Texture2D>("Textures/golden_spider_relic");
                }
            }

            if (suitPreviewTexture == null)
            {
#if UNITY_EDITOR
                suitPreviewTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/hero_suit_preview.png");
                if (suitPreviewTexture == null)
                {
                    suitPreviewTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/hero_suit_preview.png");
                }
#endif
                if (suitPreviewTexture == null)
                {
                    suitPreviewTexture = Resources.Load<Texture2D>("Textures/hero_suit_preview");
                }
            }

            if (loadingBackgroundTexture == null)
            {
#if UNITY_EDITOR
                loadingBackgroundTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/jungle_escape_bg.png");
#endif
                if (loadingBackgroundTexture == null)
                    loadingBackgroundTexture = Resources.Load<Texture2D>("Textures/jungle_escape_bg");
            }

            if (appLogoTexture == null)
            {
#if UNITY_EDITOR
                appLogoTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/UI/app_logo.png");
#endif
                if (appLogoTexture == null)
                    appLogoTexture = Resources.Load<Texture2D>("Textures/app_logo");
            }

            // Create AAA cinematic vignette textures
            CreateVignetteTextures();
        }

        private void CreateVignetteTextures()
        {
            if (vignetteTexture == null)
            {
                vignetteTexture = CreateRadialVignette(64, 0f, 0.55f, new Color(0, 0, 0, 1f));
            }
            if (vignetteRedTexture == null)
            {
                vignetteRedTexture = CreateRadialVignette(64, 0f, 0.75f, new Color(0.3f, 0.02f, 0.04f, 1f));
            }
            if (speedLinesTexture == null)
            {
                speedLinesTexture = CreateRadialVignette(64, 0f, 0.6f, new Color(1f, 1f, 1f, 1f));
            }
        }

        /// <summary>
        /// Creates a radial vignette texture.
        /// centerAlpha: alpha at center (usually 0 = fully transparent center)
        /// edgeAlpha: alpha at edges (high = dark/bright edges)
        /// power: falloff curve (higher = sharper falloff)
        /// color: RGBA color of the vignette
        /// </summary>
        private Texture2D CreateRadialVignette(int resolution, float centerAlpha, float edgeAlpha, Color color)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[resolution * resolution];

            float center = resolution * 0.5f;
            float maxDist = center;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    dist = Mathf.Clamp01(dist);
                    float t = Mathf.Pow(dist, 0.7f);
                    float alpha = Mathf.Lerp(centerAlpha, edgeAlpha, t);
                    pixels[y * resolution + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
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
                    Runner.Audio.AudioManager.Instance?.PlayUIClick();
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
            Color prevColor = GUI.color;
            GUI.color = Color.clear;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.color = prevColor;
            if (clicked)
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

        #region GUI State Safety & Text Helpers
        // ─────────────────────────────────────────────────────────────────────────────
        // GUI.skin.label is a SINGLE SHARED OBJECT mutated by every label call.
        // Any missed reset causes alignment / font-size / style LEAKAGE into the next
        // draw call. Always pair a style-changing label with ResetGUIStyle() after.
        // ─────────────────────────────────────────────────────────────────────────────
        private void ResetGUIStyle()
        {
            if (GUI.skin == null || GUI.skin.label == null) return;
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = 0;        // 0 = inherit from skin
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.skin.label.wordWrap = false;
            GUI.skin.label.font = null;         // skin default
        }

        // Wraps GUI.Label with overflow protection — text is truncated with "…" if it
        // would exceed the rect width. Also auto-resets style afterward.
        private void SafeLabel(Rect rect, string text, int maxFontSize, bool wordWrap = false)
        {
            if (string.IsNullOrEmpty(text)) { ResetGUIStyle(); return; }

            // Truncate if needed (avoids ugly overflow)
            string display = wordWrap ? text : TruncateText(text, rect.width, maxFontSize);
            GUI.Label(rect, display);
            ResetGUIStyle();
        }

        private string TruncateText(string text, float maxWidth, int fontSize)
        {
            // Approximate char width at given font size (average for proportional fonts)
            float charWidth = fontSize * 0.55f;
            int maxChars = Mathf.FloorToInt(maxWidth / charWidth);
            if (maxChars <= 3) return "...";
            if (text.Length <= maxChars) return text;
            return text.Substring(0, Mathf.Max(0, maxChars - 2)) + "…";
        }

        // Convenience overload that also applies common style then resets
        private void StyledLabel(Rect rect, string text, TextAnchor align, int fontSize,
                                FontStyle style, Color color, bool wrap = false)
        {
            GUI.skin.label.alignment = align;
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.label.fontStyle = style;
            GUI.skin.label.wordWrap = wrap;
            GUI.color = color;
            SafeLabel(rect, text, fontSize, wrap);
            ResetGUIStyle();
        }

        // Draw a button card with press-feedback (scales down slightly when held)
        private bool DrawPressButton(Rect rect, Color baseColor, Color textColor,
                                    string label, int fontSize, float uiScale)
        {
            bool hovered = rect.Contains(pointerGUIPosition);
            bool pressed = hovered && isPointerHeld;

            // Scale-down on press
            Rect drawRect = rect;
            if (pressed)
            {
                float shrink = 4f * uiScale;
                drawRect = new Rect(rect.x + shrink, rect.y + shrink,
                                    rect.width - shrink * 2, rect.height - shrink * 2);
            }

            Color cardColor = pressed
                ? new Color(baseColor.r * 0.75f, baseColor.g * 0.75f, baseColor.b * 0.75f, baseColor.a)
                : (hovered
                    ? new Color(
                        Mathf.Min(1f, baseColor.r + 0.08f),
                        Mathf.Min(1f, baseColor.g + 0.08f),
                        Mathf.Min(1f, baseColor.b + 0.08f),
                        baseColor.a)
                    : baseColor);

            DrawCard(drawRect, cardColor);

            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.wordWrap = false;
            GUI.color = textColor;
            GUI.Label(drawRect, label);
            ResetGUIStyle();

            return IsCardClicked(rect.GetHashCode(), rect);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Draws a glass-card style button with icon + label + press feedback in one call.
        // Returns true if clicked. Keeps card, icon, and label visually clean (no double-draw).
        // ─────────────────────────────────────────────────────────────────────────────
        private bool DrawGlassCardButton(Rect rect, Color bgColor, Color borderColor,
                                       string icon, string label, Color iconColor, float uiScale)
        {
            bool hovered = rect.Contains(pointerGUIPosition);
            bool pressed = hovered && isPointerHeld;

            // Scale-down on press
            Rect drawRect = rect;
            if (pressed)
            {
                float shrink = 3f * uiScale;
                drawRect = new Rect(rect.x + shrink, rect.y + shrink,
                                   rect.width - shrink * 2, rect.height - shrink * 2);
            }

            Color cardBg = pressed
                ? new Color(bgColor.r * 0.75f, bgColor.g * 0.75f, bgColor.b * 0.75f, bgColor.a)
                : bgColor;

            DrawGlassCard(drawRect, cardBg, borderColor);

            float tileW = rect.width;

            // Icon
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(15 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = iconColor;
            GUI.Label(new Rect(drawRect.x, drawRect.y + (4f * uiScale), tileW, 20f * uiScale), icon);
            ResetGUIStyle();

            // Label
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.color = Color.white;
            GUI.Label(new Rect(drawRect.x, drawRect.y + (26f * uiScale), tileW, 16f * uiScale), label);
            ResetGUIStyle();

            return IsCardClicked(rect.GetHashCode(), rect);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Responsive uiScale that properly covers ultra-wide and foldable screens.
        // Uses matchWidthOrHeight=0.5 (average) with a wider clamp than before.
        // ─────────────────────────────────────────────────────────────────────────────
        private float GetResponsiveScale()
        {
            float baseW = 390f;
            float baseH = 844f;  // ~9:20 ratio for modern tall phones
            float scaleW = Screen.width / baseW;
            float scaleH = Screen.height / baseH;
            float avg = (scaleW + scaleH) * 0.5f;
            return Mathf.Clamp(avg, 0.7f, 2.8f);
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

        private void DrawGlassPill(Rect rect, Color baseColor, Color borderColor)
        {
            DrawCard(rect, baseColor);
            DrawBorder(rect, borderColor, 1.2f);
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, 1.2f), whiteTexture);
        }

        private void DrawGlassCard(Rect rect, Color baseColor, Color borderColor, float borderThick = 1.2f)
        {
            DrawCard(rect, baseColor);
            DrawBorder(rect, borderColor, borderThick);
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, 1.5f), whiteTexture);
        }

        private void HandleGameOver(DeathType deathType, int finalScore)
        {
            isGameOver = true;
            gameOverDuration = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            lastDeathType = deathType;
            lastFinalScore = finalScore;
            deathScreenEntranceTimer = 0f;
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

            // AAA: Animate vignette pulse when hit / low health
            if (vignetteTimer > 0f)
            {
                vignetteTimer -= Time.unscaledDeltaTime;
            }

            // AAA: Speed line effect based on current speed
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                float speed = GameManager.Instance.CurrentSpeed;
                float speedT = Mathf.InverseLerp(14f, 20f, speed);
                speedLineAlpha = Mathf.Lerp(speedLineAlpha, speedT * 0.4f, Time.deltaTime * 4f);
            }
            else
            {
                speedLineAlpha = Mathf.Lerp(speedLineAlpha, 0f, Time.deltaTime * 3f);
            }

            // AAA: Distance milestone celebration banners
            CheckDistanceMilestones();
            if (milestoneBannerTimer > 0f)
            {
                milestoneBannerTimer -= Time.unscaledDeltaTime;
            }

            if (isLoading)
            {
                UpdateLoading();
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Menu)
            {
                menuEntranceTimer += Time.unscaledDeltaTime;
            }

            if (heartBloomTimer > 0f)
            {
                heartBloomTimer -= Time.unscaledDeltaTime;
            }

            if (heartTierBurstTimer > 0f)
            {
                heartTierBurstTimer -= Time.unscaledDeltaTime;
            }

            if (heartCollectFloatTimer > 0f)
            {
                heartCollectFloatTimer -= Time.unscaledDeltaTime;
            }

            if (isGameOver)
            {
                gameOverDuration += Time.unscaledDeltaTime;
                deathScreenEntranceTimer += Time.unscaledDeltaTime;
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

            // Tutorial overlay timer
            if (showTutorial)
            {
                tutorialTimer += Time.unscaledDeltaTime;
                float stepDuration = 3.0f;
                float totalDuration = stepDuration * 3f;

                // Fade in/out
                if (tutorialTimer < 0.4f)
                    tutorialFadeAlpha = tutorialTimer / 0.4f;
                else if (tutorialTimer > totalDuration - 0.5f)
                    tutorialFadeAlpha = Mathf.Max(0f, (totalDuration - tutorialTimer) / 0.5f);
                else
                    tutorialFadeAlpha = 1f;

                // Step progression
                int newStep = Mathf.FloorToInt(tutorialTimer / stepDuration);
                if (newStep != tutorialStep && newStep < 3)
                    tutorialStep = newStep;

                // End tutorial
                if (tutorialTimer >= totalDuration)
                {
                    showTutorial = false;
                    PlayerPrefs.SetInt(TUTORIAL_SHOWN_KEY, 1);
                    PlayerPrefs.Save();
                }
            }

            // Photo Mode camera control
            if (photoModeState == PhotoModeState.Active)
            {
                UpdatePhotoModeCamera();
            }

            // Universal key detection from Update for restarting on game over
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.GameOver && gameOverDuration > 0.8f)
                {
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                        Input.GetKeyDown(KeyCode.R))
                    {
                        GameManager.Instance.RestartGame();
                    }
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
                            if (activeModal != MenuModal.None)
                            {
                                activeModal = MenuModal.None;
                                e.Use();
                            }
                        }
                    else if (GameManager.Instance.CurrentState == GameState.GameOver && gameOverDuration > 0.8f)
                    {
                        GameManager.Instance.RestartGame();
                        e.Use();
                    }
                }
                else if (e.keyCode == KeyCode.R && GameManager.Instance.CurrentState == GameState.GameOver && gameOverDuration > 0.8f)
                    {
                        GameManager.Instance.RestartGame();
                        e.Use();
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
                    RenderTutorialOverlay();
                    break;
                case GameState.Paused:
                    RenderInGameHUD();
                    RenderTutorialOverlay();
                    if (photoModeState == PhotoModeState.Active)
                    {
                        RenderPhotoModeUI();
                    }
                    else if (resumeCountdownTimer > 0f)
                    {
                        RenderResumeCountdown(GetResponsiveScale());
                    }
                    else if (isMidGameSettingsOpen)
                    {
                        RenderSettingsModal(GetResponsiveScale(), true);
                    }
                    else
                    {
                        RenderPauseModal(GetResponsiveScale());
                    }
                    break;
            }

            RenderToast();

            if (isLoading)
            {
                RenderLoadingScreen();
            }
        }

        #region Main Menu (Clean Modern Design)
        private void RenderMainMenu()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float uiScale = GetResponsiveScale();

            // Menu entrance animation (0.4s ease-out for bottom elements)
            float menuEntranceProgress = Mathf.Clamp01(menuEntranceTimer / 0.4f);
            float menuEntranceEase = 1f - Mathf.Pow(1f - menuEntranceProgress, 3f);

            // 1. Cinematic Soft Vignette Backdrop (reveals 3D character runner & ancient runway)
            DrawCinematicVignette();
            GUI.color = new Color(0.02f, 0.04f, 0.03f, 0.28f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            // 1.5 Parallax Jungle Layers (depth illusion for 3D scene backdrop)
            DrawParallaxJungleLayers();

            // If a sub-modal is open, render modal in foreground and return immediately
            if (activeModal != MenuModal.None)
            {
                RenderSubModal(uiScale);
                return;
            }

            // 2. Safe Area calculation for modern mobile cutouts / notches
            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeTop = Mathf.Max(12f * uiScale, topOffset + (6f * uiScale));
            float safeSide = Mathf.Max(14f * uiScale, safe.x + (8f * uiScale));

            // 3. Top Floating Status Bar - Exactly TWO pills: Hearts & Distance Record (Zero Coins)
            float pillH = 34f * uiScale;
            float pillW = 126f * uiScale;

            // Left Pill: Sacred Hearts Banked
            Rect heartsPill = new Rect(safeSide, safeTop, pillW, pillH);
            DrawGlassPill(heartsPill, new Color(0.05f, 0.08f, 0.07f, 0.88f), new Color(1.0f, 0.35f, 0.65f, 0.70f));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11f * uiScale);
            GUI.color = new Color(1.0f, 0.45f, 0.75f);
            GUI.Label(heartsPill, $"💖  {GameManager.Instance.TotalBankedHearts} HEARTS");

            // Right Pill: Best High Score Distance
            Rect recordPill = new Rect(Screen.width - safeSide - pillW, safeTop, pillW, pillH);
            DrawGlassPill(recordPill, new Color(0.05f, 0.08f, 0.07f, 0.88f), new Color(1.0f, 0.82f, 0.25f, 0.70f));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11f * uiScale);
            GUI.color = new Color(1.0f, 0.88f, 0.30f);
            GUI.Label(recordPill, $"👑  {GameManager.Instance.BestDistance:N0}m");

            // Quick Top Utilities (Audio & Leaderboard) beside Right Pill
            float utilSize = 34f * uiScale;
            float utilY = safeTop;

            // Trophy / Leaderboard Icon Button
            Rect trophyRect = new Rect(Screen.width - safeSide - pillW - utilSize - (6f * uiScale), utilY, utilSize, utilSize);
            DrawGlassCard(trophyRect, new Color(0.06f, 0.09f, 0.10f, 0.85f), new Color(1.0f, 0.80f, 0.20f, 0.50f));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(15 * uiScale);
            GUI.color = new Color(1.0f, 0.85f, 0.25f);
            GUI.Label(trophyRect, "🏆");
            if (IsCardClicked(107, trophyRect))
            {
                activeModal = MenuModal.Leaderboard;
            }

            // Audio Toggle Icon Button
            Rect audioRect = new Rect(Screen.width - safeSide - pillW - (utilSize * 2f) - (12f * uiScale), utilY, utilSize, utilSize);
            DrawGlassCard(audioRect, new Color(0.06f, 0.09f, 0.10f, 0.85f), GameManager.Instance.IsAudioEnabled ? new Color(0.20f, 0.90f, 0.65f, 0.50f) : new Color(0.6f, 0.6f, 0.6f, 0.35f));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
            GUI.color = GameManager.Instance.IsAudioEnabled ? new Color(0.25f, 0.95f, 0.65f) : new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(audioRect, GameManager.Instance.IsAudioEnabled ? "🔊" : "🔈");
            if (IsCardClicked(106, audioRect))
            {
                GameManager.Instance.ToggleAudio();
                ShowToast(GameManager.Instance.IsAudioEnabled ? "🔊" : "🔈", GameManager.Instance.IsAudioEnabled ? "Audio Enabled" : "Audio Muted");
            }

            // 4. Hero Title Branding (Placed high to maximize character view)
            float titleY = safeTop + pillH + (16f * uiScale);

            if (relicTexture != null)
            {
                float relicSize = 34f * uiScale;
                Rect menuRelicRect = new Rect((Screen.width - relicSize) * 0.5f, titleY - (16f * uiScale), relicSize, relicSize);
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                GUI.DrawTexture(menuRelicRect, relicTexture, ScaleMode.ScaleToFit);
                titleY += (22f * uiScale);
            }

            // Subtitle Tag
            float tagW = 190f * uiScale;
            float tagH = 20f * uiScale;
            float tagX = (Screen.width - tagW) * 0.5f;
            Rect subRect = new Rect(tagX, titleY, tagW, tagH);
            DrawGlassPill(subRect, new Color(0.04f, 0.06f, 0.07f, 0.82f), new Color(1.0f, 0.82f, 0.30f, 0.45f));
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(subRect, "• 3D ANCIENT EXPEDITION •");

            // Grand Floating Title
            float animT = Time.unscaledTime;
            float floatOffset = Mathf.Sin(animT * 2.5f) * 2.5f;
            float mainTitleY = titleY + tagH + (4f * uiScale) + floatOffset;
            int titleFontSize = Mathf.RoundToInt(26 * uiScale);

            // Animated breathing scale
            float breathe = 1.0f + Mathf.Sin(animT * 1.8f) * 0.018f;
            int animatedFontSize = Mathf.RoundToInt(titleFontSize * breathe);

            // Glow pulse intensity
            float glowPulse = (Mathf.Sin(animT * 2.0f) + 1f) * 0.5f;

            // Shadow with animated offset
            float shadowOffset = 2f + glowPulse * 1f;
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.90f);
            GUI.skin.label.fontSize = animatedFontSize;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(2f, mainTitleY + shadowOffset, Screen.width, 34f * uiScale), "SPIDER TEMPLE ESCAPE");
            ResetGUIStyle();

            // Radiant Gold with glow
            float goldR = 1.0f;
            float goldG = 0.88f + glowPulse * 0.08f;
            float goldB = 0.40f + glowPulse * 0.15f;
            GUI.color = new Color(goldR, goldG, goldB);
            GUI.skin.label.fontSize = animatedFontSize;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(0, mainTitleY, Screen.width, 34f * uiScale), "SPIDER TEMPLE ESCAPE");
            ResetGUIStyle();

            // Thin Golden Filigree Line with animated shimmer
            float divW = 140f * uiScale;
            float divY = mainTitleY + (36f * uiScale);
            float divX = (Screen.width - divW) * 0.5f;
            float shimmer = Mathf.Sin(animT * 3.5f) * 0.15f;
            GUI.color = new Color(1.0f, 0.78f + shimmer, 0.20f, 0.75f);
            GUI.DrawTexture(new Rect(divX, divY + (3f * uiScale), divW * 0.42f, 1f), whiteTexture);
            GUI.DrawTexture(new Rect(divX + (divW * 0.58f), divY + (3f * uiScale), divW * 0.42f, 1f), whiteTexture);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(divX + (divW * 0.42f), divY - (4f * uiScale), divW * 0.16f, 14f * uiScale), "◆");
            ResetGUIStyle();

            // 5. Center 3D Play Space Tap Area & Pulsing Hint
            float dockW = Mathf.Min(Screen.width - (safeSide * 2f), 390f * uiScale);
            float dockX = (Screen.width - dockW) * 0.5f;
            float dockBottom = Mathf.Max(14f * uiScale, safe.y + 4f * uiScale);

            float charSectionH = 70f * uiScale;
            float mapBannerH = 26f * uiScale;
            float ctaH = 48f * uiScale;
            float dockTileH = 46f * uiScale;
            float dockGap = 5f * uiScale;
            float totalBottomH = charSectionH + dockGap + mapBannerH + dockGap + ctaH + dockGap + dockTileH;
            float dockY = Screen.height - dockBottom - totalBottomH;

            // Apply slide-up entrance animation to bottom dock
            float dockSlideOffset = (1f - menuEntranceEase) * 60f;
            dockY += dockSlideOffset;

            // 6. Character Selection Section (Horizontal Cards matching approved AAA mockup)
            RenderMainMenuCharacterSection(dockX, dockY, dockW, charSectionH, uiScale);

            // 6.5 Interactive Map Selection Banner
            float mapY = dockY + charSectionH + dockGap;
            Rect mapRect = new Rect(dockX, mapY, dockW, mapBannerH);

            string mapIcon = "🌿";
            string mapName = "JUNGLE CANOPY";
            string mapSub = "ENTER ANCIENT TEMPLE RUINS";
            Color mapAccent = new Color(0.25f, 0.95f, 0.55f);

            DrawGlassCard(mapRect, new Color(0.06f, 0.09f, 0.11f, 0.92f), mapAccent * 0.7f);
            GUI.color = mapAccent;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(dockX + (10f * uiScale), mapY, dockW * 0.75f, mapBannerH), $"{mapIcon}  MAP: {mapName}");

            GUI.color = new Color(1f, 1f, 1f, 0.70f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.Label(new Rect(dockX, mapY, dockW - (10f * uiScale), mapBannerH), "SELECT MAP ›");

            if (activeModal == MenuModal.None && IsCardClicked(105, mapRect))
            {
                activeModal = MenuModal.MapSelect;
            }

            // 7. Primary PLAY EXPEDITION Action Button
            float ctaY = mapY + mapBannerH + dockGap;
            Rect ctaRect = new Rect(dockX, ctaY, dockW, ctaH);
            DrawCard(ctaRect, new Color(1.0f, 0.74f, 0.08f, 0.96f));
            // Bevel highlight line
            GUI.color = new Color(1.0f, 0.96f, 0.70f, 0.85f);
            GUI.DrawTexture(new Rect(dockX + 2, ctaY + 2, dockW - 4, 2f), whiteTexture);
            // Shadow bevel
            GUI.color = new Color(0.68f, 0.38f, 0.02f, 0.85f);
            GUI.DrawTexture(new Rect(dockX + 2, ctaY + ctaH - 3, dockW - 4, 3f), whiteTexture);
            DrawBorder(ctaRect, new Color(1.0f, 0.92f, 0.40f, 1f), 1.8f * uiScale);

            // PLAY EXPEDITION Label
            GUI.color = new Color(0.08f, 0.04f, 0.01f, 1.0f);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(dockX + (16f * uiScale), ctaY + (5f * uiScale), dockW * 0.7f, 22f * uiScale), "▶  PLAY EXPEDITION");

            GUI.color = new Color(0.24f, 0.12f, 0.02f, 0.90f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(dockX + (16f * uiScale), ctaY + (26f * uiScale), dockW * 0.7f, 16f * uiScale), mapSub);

            GUI.color = new Color(0.08f, 0.04f, 0.01f, 1.0f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(20 * uiScale);
            GUI.Label(new Rect(dockX, ctaY, dockW - (16f * uiScale), ctaH), "›");

            if (activeModal == MenuModal.None && IsCardClicked(101, ctaRect))
            {
                ShowToast("🏃", "Starting Expedition...");
                GameManager.Instance.StartGame();
                return;
            }

            // 8. Symmetrical 4-Tile Horizontal Icon Dock directly underneath
            float tileY = ctaY + ctaH + dockGap;
            float tileGap = 6f * uiScale;
            float tileW = (dockW - (tileGap * 3f)) / 4.0f;

            // Tile 1: HERO ROSTER / CHARACTERS
            Rect suitTile = new Rect(dockX, tileY, tileW, dockTileH);
            if (DrawGlassCardButton(suitTile, new Color(0.06f, 0.09f, 0.11f, 0.88f),
                new Color(0.20f, 0.85f, 0.95f, 0.65f), "🦸", "HEROES",
                new Color(0.25f, 0.95f, 1.0f), uiScale))
            {
                activeModal = MenuModal.HeroSuits;
            }

            // Tile 2: UPGRADES / BOOSTS
            Rect upgTile = new Rect(dockX + tileW + tileGap, tileY, tileW, dockTileH);
            if (DrawGlassCardButton(upgTile, new Color(0.06f, 0.09f, 0.11f, 0.88f),
                new Color(1.0f, 0.80f, 0.25f, 0.65f), "⚡", "BOOSTS",
                new Color(1.0f, 0.85f, 0.25f), uiScale))
            {
                activeModal = MenuModal.Upgrades;
            }

            // Tile 3: MISSIONS / QUESTS
            Rect misTile = new Rect(dockX + (tileW + tileGap) * 2f, tileY, tileW, dockTileH);
            if (DrawGlassCardButton(misTile, new Color(0.06f, 0.09f, 0.11f, 0.88f),
                new Color(0.25f, 0.95f, 0.55f, 0.65f), "📜", "QUESTS",
                new Color(0.35f, 0.95f, 0.65f), uiScale))
            {
                activeModal = MenuModal.Missions;
            }

            // Badge if unclaimed missions (drawn after so it sits on top)
            int unclaimedCount = MissionManager.Instance != null ? MissionManager.Instance.GetUnclaimedRewardsCount() : 0;
            if (unclaimedCount > 0)
            {
                float badgeSize = 10f * uiScale;
                Rect badgeRect = new Rect(misTile.x + tileW - badgeSize - (3f * uiScale), misTile.y + (3f * uiScale), badgeSize, badgeSize);
                GUI.color = new Color(1.0f, 0.85f, 0.20f);
                GUI.DrawTexture(badgeRect, whiteTexture);
            }

            // Tile 4: SETTINGS / OPTIONS
            Rect setTile = new Rect(dockX + (tileW + tileGap) * 3f, tileY, tileW, dockTileH);
            if (DrawGlassCardButton(setTile, new Color(0.06f, 0.09f, 0.11f, 0.88f),
                new Color(0.65f, 0.78f, 0.92f, 0.65f), "⚙️", "OPTIONS",
                new Color(0.75f, 0.88f, 0.98f), uiScale))
            {
                activeModal = MenuModal.Settings;
            }
        }

        private void RenderMainMenuCharacterSection(float x, float y, float w, float h, float uiScale)
        {
            var cm = Runner.Characters.CharacterManager.Instance;
            int totalCount = (cm != null) ? cm.CharacterCount : 4;
            int visibleCount = Mathf.Min(4, totalCount);

            bool showArrows = totalCount > 4;
            float arrowW = showArrows ? 24f * uiScale : 0f;
            float gap = 5f * uiScale;
            float cardsAreaW = w - (arrowW * 2f);
            float cardW = (cardsAreaW - (gap * (visibleCount - 1))) / visibleCount;

            // Card animation time
            float animTime = Time.unscaledTime;

            // Clamp offset
            mainMenuCharOffset = Mathf.Clamp(mainMenuCharOffset, 0, Mathf.Max(0, totalCount - visibleCount));

            // Left Arrow
            if (showArrows)
            {
                Rect leftArrowRect = new Rect(x, y, arrowW - (4f * uiScale), h);
                bool canScrollLeft = mainMenuCharOffset > 0;
                DrawCard(leftArrowRect, canScrollLeft ? new Color(0.10f, 0.14f, 0.18f, 0.85f) : new Color(0.04f, 0.05f, 0.07f, 0.40f));
                DrawBorder(leftArrowRect, canScrollLeft ? new Color(1.0f, 0.85f, 0.30f, 0.70f) : new Color(0.3f, 0.3f, 0.3f, 0.25f), 1f);
                GUI.color = canScrollLeft ? new Color(1.0f, 0.90f, 0.40f) : new Color(0.4f, 0.4f, 0.4f, 0.5f);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(leftArrowRect, "‹");
                ResetGUIStyle();

                if (canScrollLeft && IsCardClicked(240, leftArrowRect))
                {
                    mainMenuCharOffset--;
                }
            }

            // Cards Area
            float startCardsX = x + arrowW;
            for (int v = 0; v < visibleCount; v++)
            {
                int i = mainMenuCharOffset + v;
                if (i >= totalCount) break;

                var slot = (cm != null) ? cm.GetCharacter(i) : null;
                float cardX = startCardsX + v * (cardW + gap);
                Rect cardRect = new Rect(cardX, y, cardW, h);

                bool isSelected = (cm != null && cm.SelectedCharacterIndex == i);
                bool isUnlocked = (cm != null && cm.IsCharacterUnlocked(i));

                // Card Obsidian Backdrop
                Color cardBg = isSelected ? new Color(0.12f, 0.20f, 0.16f, 0.95f) :
                               isUnlocked ? new Color(0.06f, 0.09f, 0.12f, 0.88f) :
                                            new Color(0.04f, 0.05f, 0.07f, 0.82f);
                DrawCard(cardRect, cardBg);

                // Animated selected card effects
                if (isSelected)
                {
                    // Pulsing glow border
                    float pulse = (Mathf.Sin(animTime * 3.0f) + 1f) * 0.5f;
                    Color glowCol = new Color(1.0f, 0.85f, 0.30f, 0.70f + pulse * 0.30f);
                    DrawBorder(cardRect, glowCol, 1.8f + pulse * 0.6f);

                    // Subtle vertical bounce on icon
                    float bounce = Mathf.Sin(animTime * 2.5f) * 2.0f * uiScale;
                    string iconSel = (i == 0) ? "🕷️" : (isUnlocked ? "🦸" : "🔒");
                    GUI.color = new Color(1.0f, 0.88f, 0.35f, 1.0f);
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(15 * uiScale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(new Rect(cardX, y + (4f * uiScale) + bounce, cardW, 20f * uiScale), iconSel);
                    ResetGUIStyle();
                }
                else
                {
                    // Ornate Border (non-animated)
                    Color borderCol = isUnlocked ? new Color(0.25f, 0.75f, 0.90f, 0.50f) :
                                                 new Color(0.40f, 0.40f, 0.48f, 0.35f);
                    DrawBorder(cardRect, borderCol, 1.0f);

                    string icon = (i == 0) ? "🕷️" : (isUnlocked ? "🦸" : "🔒");
                    GUI.color = isUnlocked ? Color.white : new Color(0.65f, 0.65f, 0.70f);
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(15 * uiScale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(new Rect(cardX, y + (4f * uiScale), cardW, 20f * uiScale), icon);
                    ResetGUIStyle();
                }

                // Top highlight line for equipped hero
                if (isSelected)
                {
                    float highlightPulse = (Mathf.Sin(animTime * 4.0f) + 1f) * 0.5f;
                    GUI.color = new Color(1.0f, 0.88f, 0.40f, 0.75f + highlightPulse * 0.25f);
                    GUI.DrawTexture(new Rect(cardX + 2, y + 2, cardW - 4, 1.5f), whiteTexture);
                }

                // Character Name
                string nameText = (i == 0) ? "SPIDER" : (slot != null ? slot.characterName.ToUpperInvariant() : $"HERO {i + 1}");
                if (nameText.Length > 8) nameText = nameText.Substring(0, 8); // compact fit
                GUI.color = isSelected ? Color.white : (isUnlocked ? new Color(0.85f, 0.90f, 0.95f) : new Color(0.60f, 0.60f, 0.65f));
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(cardX, y + (25f * uiScale), cardW, 15f * uiScale), nameText);
                ResetGUIStyle();

                // Bottom Status Pill (EQUIPPED / SELECT / X Hearts)
                float badgeH = 18f * uiScale;
                Rect badgeRect = new Rect(cardX + (4f * uiScale), y + h - badgeH - (4f * uiScale), cardW - (8f * uiScale), badgeH);

                if (isSelected)
                {
                    DrawCard(badgeRect, new Color(0.12f, 0.45f, 0.25f, 0.95f));
                    GUI.color = new Color(0.35f, 0.98f, 0.65f);
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(8f * uiScale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(badgeRect, "EQUIPPED");
                    ResetGUIStyle();
                }
                else if (isUnlocked)
                {
                    DrawCard(badgeRect, new Color(0.12f, 0.24f, 0.35f, 0.90f));
                    GUI.color = Color.white;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(8f * uiScale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(badgeRect, "SELECT");
                    ResetGUIStyle();
                }
                else
                {
                    int cost = slot != null ? slot.heartUnlockCost : 25;
                    DrawCard(badgeRect, new Color(0.25f, 0.12f, 0.05f, 0.90f));
                    DrawBorder(badgeRect, new Color(1.0f, 0.75f, 0.20f, 0.45f), 0.8f);
                    GUI.color = new Color(1.0f, 0.80f, 0.30f);
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(8f * uiScale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(badgeRect, $"{cost} 💖");
                    ResetGUIStyle();
                }

                // Click Handling
                if (IsCardClicked(250 + i, cardRect))
                {
                    if (cm != null)
                    {
                        cm.SelectCharacter(i);
                        GameManager.Instance.SelectSuit(i);
                        ShowToast((i == 0) ? "🕷️" : "🦸", $"{nameText} Equipped!");
                    }
                }
            }

            // Right Arrow
            if (showArrows)
            {
                Rect rightArrowRect = new Rect(x + w - arrowW + (4f * uiScale), y, arrowW - (4f * uiScale), h);
                bool canScrollRight = mainMenuCharOffset < totalCount - visibleCount;
                DrawCard(rightArrowRect, canScrollRight ? new Color(0.10f, 0.14f, 0.18f, 0.85f) : new Color(0.04f, 0.05f, 0.07f, 0.40f));
                DrawBorder(rightArrowRect, canScrollRight ? new Color(1.0f, 0.85f, 0.30f, 0.70f) : new Color(0.3f, 0.3f, 0.3f, 0.25f), 1f);
                GUI.color = canScrollRight ? new Color(1.0f, 0.90f, 0.40f) : new Color(0.4f, 0.4f, 0.4f, 0.5f);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(14 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(rightArrowRect, "›");
                ResetGUIStyle();

                if (canScrollRight && IsCardClicked(241, rightArrowRect))
                {
                    mainMenuCharOffset++;
                }
            }
        }
        #endregion

        #region Sub-Modals (Hero Suits, Upgrades, Settings, Leaderboard)
        private void RenderSubModal(float uiScale)
        {
            // Dim Backdrop
            GUI.color = new Color(0, 0, 0, 0.86f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            float modalW = Mathf.Min(Screen.width - (30f * uiScale), 360f * uiScale);
            float modalH = Mathf.Min(Screen.height - (30f * uiScale), 465f * uiScale);
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
                case MenuModal.HeroSuits: title = "⚡ CHARACTER ROSTER ⚡"; break;
                case MenuModal.Upgrades: title = "⚡ RELIC UPGRADES"; break;
                case MenuModal.Missions: title = "📜 EXPEDITION MISSIONS"; break;
                case MenuModal.Settings: title = "⚙️ GAME SETTINGS"; break;
                case MenuModal.Leaderboard: title = "🏆 STATS & LEADERBOARD"; break;
                case MenuModal.QuitConfirm: title = "🚪 QUIT EXPEDITION"; break;
                case MenuModal.MapSelect: title = "🗺️ SELECT EXPEDITION MAP"; break;
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
                case MenuModal.Missions:
                    RenderMissionsBody(modalX, bodyY, modalW, uiScale);
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
                case MenuModal.MapSelect:
                    RenderMapSelectBody(modalX, bodyY, modalW, uiScale);
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

        private void RenderMapSelectBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float cardW = w - (32f * scale);
            float cardH = 62f * scale;
            float gap = 6f * scale;

            var currentMode = Runner.Effects.BiomeManager.CurrentMapMode;
            int mapCount = System.Enum.GetNames(typeof(Runner.Effects.SelectedMapMode)).Length;

            for (int modeIndex = 0; modeIndex < mapCount; modeIndex++)
            {
                var mode = (Runner.Effects.SelectedMapMode)modeIndex;
                float cardY = y + (modeIndex * (cardH + gap));
                Rect r = new Rect(startX, cardY, cardW, cardH);

                Color accent;
                string icon, title, tag, desc;
                switch (mode)
                {
                    default:
                        accent = new Color(0.25f, 0.95f, 0.55f);
                        icon = "🌿";
                        title = "JUNGLE CANOPY";
                        tag = "• CLASSIC TEMPLE •";
                        desc = "Ancient overgrown Aztec ruins, lush foliage, golden sun & falling leaves.";
                        break;
                }

                bool isSelected = currentMode == mode;
                DrawCard(r, isSelected ? new Color(0.10f, 0.22f, 0.18f, 0.96f) : new Color(0.08f, 0.11f, 0.12f, 0.92f));
                DrawBorder(r, isSelected ? accent : new Color(accent.r, accent.g, accent.b, 0.35f),
                    (isSelected ? 2.2f : 1.2f) * scale);

                // Icon
                GUI.color = accent;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(18f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(startX + (4f * scale), cardY + (8f * scale), 34f * scale, 34f * scale), icon);

                // Title & Tag
                float textX = startX + (40f * scale);
                float textW = cardW - (135f * scale);
                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.UpperLeft;
                GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(textX, cardY + (4f * scale), textW, 15f * scale), title);

                GUI.color = accent;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8f * scale);
                GUI.Label(new Rect(textX, cardY + (18f * scale), textW, 13f * scale), tag);

                GUI.color = new Color(0.70f, 0.75f, 0.80f);
                GUI.skin.label.fontSize = Mathf.RoundToInt(7.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(textX, cardY + (31f * scale), textW, 28f * scale), desc);

                // State button (SELECT / ✓ ACTIVE)
                float btnW = 74f * scale;
                float btnH = 26f * scale;
                float btnX = startX + cardW - btnW - (8f * scale);
                float btnY = cardY + ((cardH - btnH) * 0.5f);
                Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

                if (isSelected)
                {
                    DrawGlassPill(btnRect, new Color(0.04f, 0.16f, 0.12f, 0.92f), accent);
                    GUI.color = accent;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, "✓ ACTIVE");
                }
                else
                {
                    DrawGlassPill(btnRect, new Color(0.10f, 0.14f, 0.18f, 0.92f), new Color(accent.r, accent.g, accent.b, 0.75f));
                    GUI.color = Color.white;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, "SELECT");
                    if (IsCardClicked(500 + modeIndex, btnRect) || IsCardClicked(510 + modeIndex, r))
                    {
                        Runner.Effects.BiomeManager.Instance?.SetSelectedMap(mode);
                        ShowToast(icon, $"{title} Selected!", 2.0f);
                    }
                }
            }
        }

        private void RenderHeroSuitsBody(float x, float y, float w, float scale)
        {
            var cm = Runner.Characters.CharacterManager.Instance;
            int count = (cm != null) ? cm.CharacterCount : 4;

            float cardH = 68f * scale;
            float cardW = w - (32f * scale);
            float startX = x + (16f * scale);
            float animTime = Time.unscaledTime;

            for (int i = 0; i < count; i++)
            {
                var slot = (cm != null) ? cm.GetCharacter(i) : null;
                float cardY = y + (i * (cardH + (6f * scale)));
                Rect r = new Rect(startX, cardY, cardW, cardH);

                bool isSelected = (cm != null && cm.SelectedCharacterIndex == i);
                bool isUnlocked = (cm != null && cm.IsCharacterUnlocked(i));

                Color cardBg = isSelected ? new Color(0.10f, 0.28f, 0.22f, 0.95f) :
                               isUnlocked ? new Color(0.08f, 0.12f, 0.14f, 0.88f) :
                                            new Color(0.06f, 0.07f, 0.09f, 0.78f);
                DrawCard(r, cardBg);

                if (isSelected)
                {
                    float pulse = (Mathf.Sin(animTime * 3.0f) + 1f) * 0.5f;
                    Color borderPulse = new Color(0.20f, 0.95f, 0.65f, 0.75f + pulse * 0.25f);
                    DrawBorder(r, borderPulse, 1.2f + pulse * 0.5f);
                }
                else
                {
                    Color borderCol = isUnlocked ? new Color(0.25f, 0.75f, 0.90f, 0.50f) :
                                                 new Color(0.40f, 0.40f, 0.45f, 0.35f);
                    DrawBorder(r, borderCol, 1.2f);
                }

                string charName = (slot != null) ? slot.characterName : (i == 0 ? "Spider-Man" : $"Hero Slot {i + 1}");
                string charTitle = (slot != null) ? slot.characterTitle : (i == 0 ? "Temple Runner" : "Ready for Model");
                string charDesc = (slot != null) ? slot.description : "Assign custom 3D model in CharacterManager.";

                // Title & Subtitle
                GUI.color = isSelected ? new Color(0.30f, 0.98f, 0.70f) : (isUnlocked ? Color.white : new Color(0.70f, 0.70f, 0.75f));
                GUI.skin.label.alignment = TextAnchor.UpperLeft;
                GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(startX + (10f * scale), cardY + (4f * scale), cardW - (115f * scale), 16f * scale), $"{charName}  •  {charTitle}");

                // Attribute Ratings
                float spd = slot != null ? slot.speedRating : 1.0f;
                float shd = slot != null ? slot.shieldRating : 1.0f;
                float agi = slot != null ? slot.agilityRating : 1.0f;
                string statsStr = $"⚡ Spd: {spd:F1}x  🛡️ Shd: {shd:F1}x  🧲 Agi: {agi:F1}x";
                GUI.color = isSelected ? new Color(1.0f, 0.88f, 0.35f) : (isUnlocked ? new Color(0.35f, 0.90f, 1.0f) : new Color(0.85f, 0.60f, 0.30f));
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(startX + (10f * scale), cardY + (20f * scale), cardW - (115f * scale), 15f * scale), statsStr);

                // Description
                GUI.color = new Color(0.60f, 0.68f, 0.65f);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.skin.label.fontSize = Mathf.RoundToInt(7.5f * scale);
                GUI.Label(new Rect(startX + (10f * scale), cardY + (36f * scale), cardW - (115f * scale), 28f * scale), charDesc);

                // Action Button (EQUIPPED / SELECT / UNLOCK)
                float btnW = 95f * scale;
                float btnH = 34f * scale;
                Rect btnRect = new Rect(startX + cardW - btnW - (8f * scale), cardY + (17f * scale), btnW, btnH);

                if (isSelected)
                {
                    DrawCard(btnRect, new Color(0.18f, 0.75f, 0.40f, 0.95f));
                    GUI.color = Color.white;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, "EQUIPPED");
                }
                else if (isUnlocked)
                {
                    DrawCard(btnRect, new Color(0.15f, 0.55f, 0.85f, 0.90f));
                    GUI.color = Color.white;
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, "SELECT");
                    if (IsCardClicked(300 + i, btnRect))
                    {
                        cm?.SelectCharacter(i);
                        GameManager.Instance.SelectSuit(i);
                        ShowToast((i == 0) ? "🕷️" : "🦸", $"{charName} Equipped!");
                    }
                }
                else
                {
                    int unlockCost = (cm != null) ? cm.GetCharacterUnlockCost(i) : 0;
                    bool canAfford = GameManager.Instance != null && GameManager.Instance.TotalBankedHearts >= unlockCost;
                    DrawCard(btnRect, canAfford ? new Color(0.55f, 0.40f, 0.12f, 0.95f) : new Color(0.26f, 0.22f, 0.14f, 0.92f));
                    GUI.color = canAfford ? new Color(1f, 0.92f, 0.55f) : new Color(0.72f, 0.66f, 0.55f);
                    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                    GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                    GUI.skin.label.fontStyle = FontStyle.Bold;
                    GUI.Label(btnRect, $"🔒 {unlockCost} ❤");
                    if (IsCardClicked(300 + i, btnRect))
                    {
                        if (cm != null && cm.UnlockCharacter(i))
                        {
                            GameManager.Instance?.SelectSuit(i);
                            ShowToast("🔓", $"{charName} Unlocked!");
                        }
                        else
                        {
                            int held = (GameManager.Instance != null) ? GameManager.Instance.TotalBankedHearts : 0;
                            ShowToast("💔", $"Need {unlockCost} Hearts — you have {held}");
                        }
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
            GUI.Label(new Rect(startX, y, cardW, 24f * scale), $"💖 Available: {GameManager.Instance.TotalBankedHearts} Hearts");

            float curY = y + (26f * scale);
            float cardH = 50f * scale;
            float gap = 4f * scale;

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
                $"+{(GameManager.Instance.SpeedLevel - 1) * 2}s boost (total: {6 + (GameManager.Instance.SpeedLevel - 1) * 2}s)",
                PowerUpType.Speedrun, GameManager.Instance.SpeedLevel);

            // 3. Magnet Relic
            curY += cardH + gap;
            RenderUpgradeCard(startX, curY, cardW, cardH, scale,
                "🧲 RELIC MAGNET",
                $"Draws hearts across lanes. Lvl {GameManager.Instance.MagnetLevel}/5",
                $"Range: {8 + (GameManager.Instance.MagnetLevel - 1) * 3}m pull radius",
                PowerUpType.Magnet, GameManager.Instance.MagnetLevel);

            // 4. Multiplier Frenzy Totem
            curY += cardH + gap;
            RenderUpgradeCard(startX, curY, cardW, cardH, scale,
                "🔥 FRENZY TOTEM",
                $"3x Score Multiplier. Lvl {GameManager.Instance.FrenzyLevel}/5",
                $"+{(GameManager.Instance.FrenzyLevel - 1) * 2}s frenzy (total: {8 + (GameManager.Instance.FrenzyLevel - 1) * 2}s)",
                PowerUpType.MultiplierFrenzy, GameManager.Instance.FrenzyLevel);

            // 5. Heart Capacity
            curY += cardH + gap;
            RenderHeartCapacityCard(startX, curY, cardW, cardH, scale);
        }

        private int missionsTab = 0; // 0 = Daily Quests, 1 = Lifetime Achievements

        private void RenderMissionsBody(float x, float y, float w, float scale)
        {
            float startX = x + (16f * scale);
            float bodyW = w - (32f * scale);

            // Tab Buttons: [ 📜 DAILY MISSIONS ]  [ 🏆 ACHIEVEMENTS ]
            float tabH = 30f * scale;
            float tabW = (bodyW - (6f * scale)) * 0.5f;

            // Tab 0: Daily
            Rect tab0Rect = new Rect(startX, y, tabW, tabH);
            bool isTab0 = (missionsTab == 0);
            DrawCard(tab0Rect, isTab0 ? new Color(0.12f, 0.24f, 0.18f, 0.95f) : new Color(0.08f, 0.10f, 0.10f, 0.70f));
            if (isTab0) DrawBorder(tab0Rect, new Color(0.30f, 0.95f, 0.60f, 0.90f), 1.2f);
            GUI.color = isTab0 ? new Color(0.35f, 0.95f, 0.65f) : new Color(0.6f, 0.7f, 0.65f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(tab0Rect, "📜 DAILY MISSIONS");
            if (IsCardClicked(210, tab0Rect)) missionsTab = 0;

            // Tab 1: Achievements
            Rect tab1Rect = new Rect(startX + tabW + (6f * scale), y, tabW, tabH);
            bool isTab1 = (missionsTab == 1);
            DrawCard(tab1Rect, isTab1 ? new Color(0.24f, 0.18f, 0.08f, 0.95f) : new Color(0.08f, 0.10f, 0.10f, 0.70f));
            if (isTab1) DrawBorder(tab1Rect, new Color(1.0f, 0.85f, 0.30f, 0.90f), 1.2f);
            GUI.color = isTab1 ? new Color(1.0f, 0.85f, 0.35f) : new Color(0.7f, 0.65f, 0.6f);
            GUI.Label(tab1Rect, "🏆 ACHIEVEMENTS");
            if (IsCardClicked(211, tab1Rect)) missionsTab = 1;

            float curY = y + tabH + (8f * scale);
            float cardH = 58f * scale;
            float gap = 5f * scale;

            var list = (missionsTab == 0)
                ? MissionManager.Instance?.DailyMissions
                : MissionManager.Instance?.LifetimeAchievements;

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (curY + cardH > y + (360f * scale)) break; // fit within modal bounds
                    var m = list[i];
                    RenderMissionCard(startX, curY, bodyW, cardH, scale, m, 500 + (missionsTab * 50) + i);
                    curY += cardH + gap;
                }
            }
        }

        private void RenderMissionCard(float x, float y, float w, float h, float scale, MissionData mission, int clickId)
        {
            Rect r = new Rect(x, y, w, h);
            Color bg = mission.IsClaimed 
                ? new Color(0.07f, 0.09f, 0.08f, 0.65f)
                : (mission.IsComplete ? new Color(0.10f, 0.20f, 0.14f, 0.95f) : new Color(0.08f, 0.11f, 0.10f, 0.90f));
            DrawCard(r, bg);

            Color borderCol = mission.IsClaimed
                ? new Color(0.35f, 0.45f, 0.40f, 0.40f)
                : (mission.IsComplete ? new Color(0.30f, 0.95f, 0.60f, 0.85f) : new Color(1.0f, 0.82f, 0.32f, 0.40f));
            DrawBorder(r, borderCol, 1.0f);

            // Title
            GUI.color = mission.IsComplete && !mission.IsClaimed ? new Color(0.35f, 0.95f, 0.65f) : new Color(1.0f, 0.88f, 0.40f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (5f * scale), w - (95f * scale), 18f * scale), mission.Title);

            // Description
            GUI.color = new Color(0.75f, 0.85f, 0.80f);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.skin.label.fontSize = Mathf.RoundToInt(8f * scale);
            GUI.Label(new Rect(x + (10f * scale), y + (22f * scale), w - (95f * scale), 16f * scale), mission.Description);

            // Progress Bar
            float barW = w - (105f * scale);
            float barH = 6f * scale;
            float barX = x + (10f * scale);
            float barY = y + (40f * scale);

            // Bar background
            GUI.color = new Color(0.12f, 0.16f, 0.14f, 0.90f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTexture);

            // Bar fill
            float fillRatio = mission.ProgressNormalized;
            GUI.color = mission.IsComplete ? new Color(0.25f, 0.95f, 0.55f) : new Color(1.0f, 0.78f, 0.20f);
            GUI.DrawTexture(new Rect(barX, barY, barW * fillRatio, barH), whiteTexture);

            // Progress text
            GUI.color = new Color(0.9f, 0.95f, 0.9f);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(7.5f * scale);
            string progText = mission.IsComplete ? "COMPLETE" : $"{mission.CurrentProgress}/{mission.TargetAmount}";
            GUI.Label(new Rect(barX, barY - (9f * scale), barW, 9f * scale), progText);

            // Right Button / Status Badge
            float btnW = 84f * scale;
            float btnH = 32f * scale;
            Rect btnRect = new Rect(x + w - btnW - (8f * scale), y + (13f * scale), btnW, btnH);

            if (mission.IsClaimed)
            {
                DrawCard(btnRect, new Color(0.12f, 0.16f, 0.14f, 0.70f));
                GUI.color = new Color(0.55f, 0.65f, 0.60f);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.Label(btnRect, "✓ CLAIMED");
            }
            else if (mission.IsComplete)
            {
                // Pulsing Green Radiant Claim CTA!
                float pulse = 0.85f + Mathf.PingPong(Time.unscaledTime * 2.0f, 0.15f);
                DrawCard(btnRect, new Color(0.15f * pulse, 0.65f * pulse, 0.25f * pulse, 0.95f));
                DrawBorder(btnRect, new Color(0.40f, 1.0f, 0.60f, 0.95f), 1.2f);
                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(btnRect, $"CLAIM +{mission.RewardCoins}💖");
                if (IsCardClicked(clickId, btnRect))
                {
                    MissionManager.Instance?.ClaimReward(mission);
                }
            }
            else
            {
                DrawCard(btnRect, new Color(0.14f, 0.12f, 0.08f, 0.80f));
                GUI.color = new Color(1.0f, 0.82f, 0.35f);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(btnRect, $"+{mission.RewardCoins} 💖");
            }
        }

        private void RenderUpgradeCard(float x, float y, float w, float h, float scale, string name, string levelStr, string desc, PowerUpType type, int currentLvl)
        {
            Rect r = new Rect(x, y, w, h);
            DrawCard(r, new Color(0.10f, 0.14f, 0.12f, 0.90f));
            DrawBorder(r, new Color(1.0f, 0.82f, 0.32f, 0.35f), 1.0f);

            // Title
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x + (10f * scale), y + (5f * scale), w - (110f * scale), 18f * scale), name);

            // Level Pips Progress Bar (5 pips)
            float pipW = 12f * scale;
            float pipH = 5f * scale;
            float pipGap = 3f * scale;
            float pipStartX = x + (10f * scale);
            float pipY = y + (23f * scale);
            for (int p = 0; p < 5; p++)
            {
                Rect pipRect = new Rect(pipStartX + p * (pipW + pipGap), pipY, pipW, pipH);
                Color pipCol = (p < currentLvl) ? new Color(0.25f, 0.95f, 0.55f, 1f) : new Color(0.25f, 0.30f, 0.28f, 0.85f);
                GUI.color = pipCol;
                GUI.DrawTexture(pipRect, whiteTexture);
            }

            // Desc
            GUI.color = new Color(0.65f, 0.75f, 0.70f);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * scale);
            GUI.Label(new Rect(x + (10f * scale), y + (34f * scale), w - (110f * scale), 26f * scale), desc);

            // Upgrade Button with tiered cost: 5, 8, 11, 14
            int cost = 5 + (currentLvl - 1) * 3;
            float btnW = 95f * scale;
            float btnH = 32f * scale;
            Rect btnRect = new Rect(x + w - btnW - (8f * scale), y + (16f * scale), btnW, btnH);

            if (currentLvl >= 5)
            {
                DrawCard(btnRect, new Color(0.25f, 0.30f, 0.28f, 0.80f));
                GUI.color = new Color(0.8f, 0.85f, 0.8f);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(btnRect, "MAX LEVEL");
            }
            else
            {
                DrawCard(btnRect, new Color(0.15f, 0.70f, 0.35f, 0.95f));
                DrawBorder(btnRect, new Color(0.4f, 1.0f, 0.6f, 0.80f), 1.0f);
                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(btnRect, $"UPGRADE ({cost}❤️)");

                if (IsCardClicked(400 + (int)type, btnRect))
                {
                    bool ok = GameManager.Instance.UpgradePowerup(type, cost);
                    if (ok)
                    {
                        ShowToast("✨", $"{name} Upgraded to Lvl {GameManager.Instance.GetPowerupLevel(type)}!");
                    }
                    else
                    {
                        ShowToast("❌", $"Need {cost} Hearts to Upgrade!");
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

            // 5. HUD Style: Canvas (uGUI) vs Classic (IMGUI)
            curY += rowH + (6f * scale);
            Rect hudRow = new Rect(startX, curY, contentW, rowH);
            DrawCard(hudRow, new Color(0.10f, 0.14f, 0.12f, 0.90f));

            GUI.color = Color.white;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(startX + (8f * scale), curY, contentW * 0.45f, rowH), "IN-GAME HUD");

            Rect hudBtn = new Rect(startX + contentW - btnW - (6f * scale), curY + (5f * scale), btnW, btnH);
            GUI.color = GameManager.Instance.UseCanvasHud ? new Color(0.20f, 0.85f, 0.75f) : new Color(0.6f, 0.6f, 0.6f);
            string hudName = GameManager.Instance.UseCanvasHud ? "CANVAS" : "CLASSIC";
            if (IsCardClicked(505, hudBtn) || GUI.Button(hudBtn, hudName))
            {
                GameManager.Instance.SetHudStyle(!GameManager.Instance.UseCanvasHud);
                InGameCanvasHUD.Instance?.RefreshVisibility();
                ShowToast("🖥️", GameManager.Instance.UseCanvasHud
                    ? "HUD Style: Canvas (native)"
                    : "HUD Style: Classic (legacy)");
            }

            // 6. Controls Help Guide
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
            var gm = GameManager.Instance;
            if (gm == null) return;

            float startX = x + (16f * scale);
            float contentW = w - (32f * scale);
            float lineY = y;

            // 1. Level card with XP progress
            Rect levelBox = new Rect(startX, lineY, contentW, 74f * scale);
            DrawCard(levelBox, new Color(0.10f, 0.14f, 0.12f, 0.92f));
            DrawBorder(levelBox, new Color(0.30f, 0.90f, 0.75f, 0.55f), 1.4f * scale);

            GUI.color = new Color(0.35f, 0.95f, 0.80f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX + (10f * scale), lineY + (6f * scale), contentW * 0.55f, 15f * scale),
                $"LEVEL {gm.PlayerLevel}");

            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(startX + contentW * 0.40f, lineY + (6f * scale), contentW * 0.60f - (10f * scale), 15f * scale),
                gm.RankTitle);

            GUI.color = new Color(0.85f, 0.90f, 0.92f);
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(startX + (10f * scale), lineY + (23f * scale), contentW - (20f * scale), 14f * scale),
                $"{gm.XPIntoLevel:N0} / {gm.XPForNextLevel:N0} XP  •  {gm.TotalXP:N0} total XP");

            // XP progress bar
            float barX = startX + (10f * scale);
            float barW = contentW - (20f * scale);
            float barY = lineY + (42f * scale);
            Rect barBg = new Rect(barX, barY, barW, 14f * scale);
            DrawCard(barBg, new Color(0.05f, 0.08f, 0.07f, 0.95f));
            float fill = (gm.XPForNextLevel > 0) ? Mathf.Clamp01((float)gm.XPIntoLevel / gm.XPForNextLevel) : 0f;
            Rect barFill = new Rect(barX + (2f * scale), barY + (2f * scale), (barW - (4f * scale)) * fill, 10f * scale);
            GUI.color = new Color(0.30f, 0.92f, 0.70f);
            GUI.DrawTexture(barFill, whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(barBg, $"{Mathf.RoundToInt(fill * 100f)}%");

            // 2. Key stats row
            lineY += 74f * scale + (8f * scale);
            float statH = 44f * scale;
            float statW = (contentW - (6f * scale)) * 0.5f;

            Rect stat1 = new Rect(startX, lineY, statW, statH);
            DrawCard(stat1, new Color(0.10f, 0.14f, 0.12f, 0.90f));
            GUI.color = new Color(0.35f, 0.90f, 0.55f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(stat1.x, lineY + (5f * scale), statW, 14f * scale), "BEST DISTANCE");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * scale);
            GUI.Label(new Rect(stat1.x, lineY + (21f * scale), statW, 20f * scale), $"{gm.BestDistance:N0} m");

            Rect stat2 = new Rect(startX + statW + (6f * scale), lineY, statW, statH);
            DrawCard(stat2, new Color(0.10f, 0.14f, 0.12f, 0.90f));
            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(stat2.x, lineY + (5f * scale), statW, 14f * scale), "HIGH SCORE");
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(14 * scale);
            GUI.Label(new Rect(stat2.x, lineY + (21f * scale), statW, 20f * scale), $"{gm.HighScore:N0}");

            // 3. Local leaderboard
            lineY += statH + (8f * scale);
            GUI.color = new Color(1.0f, 0.35f, 0.70f);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * scale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(startX, lineY, contentW, 16f * scale),
                $"💖 {gm.TotalBankedHearts:N0} Hearts Banked");
            lineY += 16f * scale + (4f * scale);

            GUI.color = new Color(0.75f, 0.80f, 0.82f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
            GUI.Label(new Rect(startX, lineY, contentW, 14f * scale), "TOP RUNS (THIS DEVICE)");
            lineY += 15f * scale;

            var board = gm.Leaderboard;
            if (board == null || board.Count == 0)
            {
                GUI.color = new Color(0.55f, 0.60f, 0.62f);
                GUI.skin.label.fontSize = Mathf.RoundToInt(9 * scale);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(startX, lineY, contentW, 30f * scale),
                    "No runs recorded yet — hit the temple and set your first record!");
                return;
            }

            for (int i = 0; i < board.Count; i++)
            {
                var entry = board[i];
                float rowY = lineY + (i * 20f * scale);
                Rect row = new Rect(startX, rowY, contentW, 18f * scale);
                DrawCard(row, (i == 0)
                    ? new Color(0.16f, 0.14f, 0.06f, 0.92f)
                    : new Color(0.08f, 0.11f, 0.12f, 0.85f));

                GUI.color = (i == 0) ? new Color(1.0f, 0.85f, 0.30f) : new Color(0.70f, 0.76f, 0.78f);
                GUI.skin.label.alignment = TextAnchor.MiddleLeft;
                GUI.skin.label.fontSize = Mathf.RoundToInt(8.5f * scale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(row.x + (6f * scale), rowY, 40f * scale, 18f * scale), $"#{i + 1}");

                GUI.color = Color.white;
                GUI.skin.label.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(row.x + (44f * scale), rowY, contentW * 0.5f, 18f * scale),
                    $"{entry.score:N0} pts");

                GUI.color = new Color(0.65f, 0.85f, 0.95f);
                GUI.skin.label.alignment = TextAnchor.MiddleRight;
                GUI.skin.label.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(row.x + contentW * 0.5f, rowY, contentW * 0.5f - (6f * scale), 18f * scale),
                    $"{entry.distance:N0} m  •  Lv{entry.level}");
            }
        }
        #endregion

        #region In-Game HUD
        private void RenderInGameHUD()
        {
            if (GameManager.Instance == null) return;

            // Native uGUI canvas HUD takes over in-game rendering when enabled
            if (GameManager.Instance.UseCanvasHud) return;

            float uiScale = GetResponsiveScale();

            // AAA: Draw cinematic vignette overlay for depth and immersion
            DrawCinematicVignette();

            // AAA: Draw speed radial effect when running fast
            DrawSpeedOverlay();

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeLeft = Mathf.Max(14f * uiScale, safe.x + 8f * uiScale);
            float safeTop = Mathf.Max(14f * uiScale, topOffset + 8f * uiScale);

            // 1. Top-Left: Pause Button (⏸)
            float pauseSize = 40f * uiScale;
            Rect pauseRect = new Rect(safeLeft, safeTop, pauseSize, pauseSize);
            DrawCard(pauseRect, new Color(0.08f, 0.12f, 0.10f, 0.88f));
            DrawBorder(pauseRect, new Color(1.0f, 0.82f, 0.32f, 0.40f), 1.5f);

            GUI.color = new Color(1.0f, 0.85f, 0.35f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(18 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(pauseRect, "⏸");
            ResetGUIStyle();
            if (IsCardClicked(901, pauseRect))
            {
                isMidGameSettingsOpen = false;
                GameManager.Instance.PauseGame();
            }

            // 2. Active Power-Ups Tray (Renders directly below pause button, completely avoiding top-bar overlap)
            RenderActivePowerUpsTray(safeLeft, safeTop + pauseSize + (8f * uiScale), uiScale);

            // 3. Top-Right: Distance & Score Dual Pill + Hearts Pool Pill (bounded to avoid overlapping left items)
            float pillW = Mathf.Min(180f * uiScale, Screen.width * 0.46f);
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

            // Pill 2: Heart Collection Meter (text header + animated gradient bar, heartbeat pulse, floating +1 popup)
            float heartY = safeTop + distH + (6f * uiScale);
            RenderHeartMeter(pillX, heartY, pillW, uiScale);

            // Live record badge, milestone celebrations & beast-proximity warning
            RenderNewBestBadge(pillX, heartY + (46f * uiScale) + (5f * uiScale), pillW, uiScale);
            RenderMilestoneBanner(uiScale);
            RenderBeastWarning(uiScale);
        }

        private void RenderHeartMeter(float x, float y, float width, float uiScale)
        {
            if (GameManager.Instance == null) return;

            int coins = GameManager.Instance.CoinsCollected;
            int curLives = GameManager.Instance.CurrentLives;
            int maxLives = Mathf.Max(1, GameManager.Instance.MaxLives);

            float meterH = 46f * uiScale;
            Rect meterRect = new Rect(x, y, width, meterH);

            // 1. Obsidian Glass Card Container
            float bloomAlpha = Mathf.Clamp01(heartBloomTimer / 0.9f);
            float burstAlpha = Mathf.Clamp01(heartTierBurstTimer / 1.1f);
            float flashAlpha = Mathf.Max(bloomAlpha, burstAlpha);
            DrawCard(meterRect, new Color(0.08f, 0.04f, 0.06f, 0.88f));

            // Dynamic border: Low-health danger strobe, collection bloom, or neon rose
            Color borderColor;
            if (curLives <= 2)
            {
                float dangerPulse = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
                borderColor = Color.Lerp(new Color(1f, 0.20f, 0.30f, 0.95f), new Color(1f, 0.65f, 0.15f, 1f), dangerPulse);
            }
            else if (flashAlpha > 0f)
            {
                borderColor = Color.Lerp(new Color(1f, 0.35f, 0.75f, 0.65f), new Color(1f, 0.85f, 1f, 1f), flashAlpha);
            }
            else
            {
                borderColor = new Color(1f, 0.30f, 0.65f, 0.55f);
            }
            DrawBorder(meterRect, borderColor, (curLives <= 2 || flashAlpha > 0f) ? 1.8f : 1.2f);

            // 2. Header Row: Beating Heart Icon + Hearts Collected (Coin System) & HP
            // Realistic Lub-Dub Heartbeat Pulse
            float beat = HeartBeatPulse();
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

            // 3. Heart Collection Bar: rounded gradient fill, sheen sweep, edge bloom, 10 steps
            int tierGoal = 10;
            int inTier = coins % tierGoal;
            int filled = (coins > 0 && inTier == 0) ? tierGoal : inTier;
            heartBarFill = Mathf.Lerp(heartBarFill, filled / (float)tierGoal, Time.unscaledDeltaTime * 9f);

            float counterW = 34f * uiScale;
            float gaugeX = x + padX;
            float gaugeY = y + (26f * uiScale);
            float gaugeW = width - (padX * 2f) - counterW;
            float gaugeH = 14f * uiScale;

            // Track
            GUI.color = new Color(0.03f, 0.02f, 0.04f, 0.94f);
            GUI.DrawTexture(new Rect(gaugeX, gaugeY, gaugeW, gaugeH), HeartMeterVisuals.Bar.texture);

            float fillW = gaugeW * heartBarFill;
            if (fillW > 1.5f)
            {
                Rect fillRect = new Rect(gaugeX + 1.5f, gaugeY + 1.5f, fillW - 3f, gaugeH - 3f);

                // Fill: sprite is squeezed into the live width so the leading edge stays rounded
                if (burstAlpha > 0f)
                {
                    GUI.color = Color.Lerp(Color.white, new Color(1f, 0.86f, 0.35f), Mathf.Abs(Mathf.Sin(burstAlpha * Mathf.PI * 3f)));
                }
                else
                {
                    GUI.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.97f), flashAlpha * 0.7f);
                }
                GUI.DrawTexture(fillRect, HeartMeterVisuals.Fill.texture);

                // Sheen sweep, clipped to the filled portion
                float sweep = Mathf.Repeat(Time.unscaledTime * 0.62f, 1.9f);
                if (sweep <= 1f)
                {
                    float bandW = gaugeH * 2.6f;
                    GUI.BeginGroup(fillRect);
                    GUI.color = new Color(1f, 1f, 1f, 0.85f - burstAlpha * 0.35f);
                    GUI.DrawTexture(new Rect((sweep * fillRect.width) - (bandW * 0.5f), -2f, bandW, fillRect.height + 4f), HeartMeterVisuals.Sheen.texture);
                    GUI.EndGroup();
                }

                // Leading edge bloom
                float glowW = gaugeH * 1.7f;
                GUI.color = new Color(1f, 0.78f, 0.92f, Mathf.Clamp01(0.55f + beat * 0.35f + flashAlpha * 0.30f));
                GUI.DrawTexture(new Rect(fillRect.xMax - (glowW * 0.5f), gaugeY - (3f * uiScale), glowW, gaugeH + (6f * uiScale)), HeartMeterVisuals.Glow.texture);
            }

            // 10 collection steps
            float stepW = gaugeW / tierGoal;
            GUI.color = Color.Lerp(new Color(0f, 0f, 0f, 0.62f), new Color(1f, 0.86f, 0.35f, 0.9f), burstAlpha);
            for (int i = 1; i < tierGoal; i++)
            {
                GUI.DrawTexture(new Rect(gaugeX + (i * stepW) - 1f, gaugeY + 3f, 2f, gaugeH - 6f), whiteTexture);
            }

            // Tier counter (n/10)
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10f * uiScale);
            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.80f, 0.95f), new Color(1f, 0.86f, 0.35f, 1f), burstAlpha);
            GUI.Label(new Rect(x + width - padX - counterW, gaugeY - 1f, counterW, gaugeH + 2f), $"{filled}/{tierGoal}");

            // 4. Floating +1 💖 Pickup Popup
            if (heartCollectFloatTimer > 0f)
            {
                float floatRatio = heartCollectFloatTimer / 1.2f;
                float floatY = y + (26f * uiScale) - ((1f - floatRatio) * 16f * uiScale);
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

        /// <summary>Two quick spikes per cycle, mimicking a lub-dub heartbeat (0..1).</summary>
        private static float HeartBeatPulse()
        {
            float phase = Mathf.Repeat(Time.unscaledTime, 1.15f);
            float lub = Mathf.Clamp01(1f - phase / 0.13f);
            float dub = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.24f) / 0.11f);
            return Mathf.Max(lub, dub * 0.72f);
        }

        private void RenderActivePowerUpsTray(float x, float y, float uiScale)
        {
            if (PickupManager.Instance == null) return;

            var activeList = PickupManager.Instance.GetActivePowerUps();
            if (activeList == null || activeList.Count == 0)
                return;

            float curY = y;
            float pillW = Mathf.Min(125f * uiScale, Screen.width * 0.36f);
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
            ResetGUIStyle();

            GUI.color = new Color(0.65f, 0.75f, 0.70f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Italic;
            GUI.Label(new Rect(modalX, modalY + (44f * uiScale), modalW, 18f * uiScale), "EXPEDITION SUSPENDED");
            ResetGUIStyle();

            float btnW = modalW - (44f * uiScale);
            float btnH = 46f * uiScale;
            float btnX = modalX + (22f * uiScale);
            float curY = modalY + (74f * uiScale);
            float gap = 12f * uiScale;

            // 1. RESUME — reduced from 3.6s to 1.5s for snappier UX
            Rect resumeRect = new Rect(btnX, curY, btnW, btnH);
            if (DrawPressButton(resumeRect, new Color(0.95f, 0.75f, 0.15f, 0.95f),
                                new Color(0.10f, 0.08f, 0.02f), "▶  RESUME EXPEDITION",
                                Mathf.RoundToInt(13 * uiScale), uiScale))
            {
                resumeCountdownTimer = 1.5f;
            }

            // 2. SETTINGS
            curY += btnH + gap;
            Rect settingsRect = new Rect(btnX, curY, btnW, btnH);
            if (DrawPressButton(settingsRect, new Color(0.12f, 0.18f, 0.16f, 0.90f),
                                Color.white, "⚙️  SETTINGS", Mathf.RoundToInt(13 * uiScale), uiScale))
            {
                isMidGameSettingsOpen = true;
            }

            // 2.5 PHOTO MODE
            curY += btnH + gap;
            Rect photoRect = new Rect(btnX, curY, btnW, btnH);
            if (DrawPressButton(photoRect, new Color(0.10f, 0.14f, 0.20f, 0.90f),
                                new Color(0.40f, 0.85f, 1.0f), "📸  PHOTO MODE",
                                Mathf.RoundToInt(13 * uiScale), uiScale))
            {
                EnterPhotoMode();
            }

            // 3. RESTART (Fresh 0m)
            curY += btnH + gap;
            Rect restartRect = new Rect(btnX, curY, btnW, btnH);
            if (DrawPressButton(restartRect, new Color(0.12f, 0.18f, 0.16f, 0.90f),
                                Color.white, "🔄  RESTART (0m)", Mathf.RoundToInt(13 * uiScale), uiScale))
            {
                GameManager.Instance.RestartGame();
            }

            // 4. QUIT TO MENU (Saves relics)
            curY += btnH + gap;
            Rect menuRect = new Rect(btnX, curY, btnW, btnH);
            if (DrawPressButton(menuRect, new Color(0.20f, 0.10f, 0.10f, 0.90f),
                                new Color(1.0f, 0.65f, 0.65f), "🏠  QUIT TO MENU",
                                Mathf.RoundToInt(13 * uiScale), uiScale))
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

        #region Photo Mode
        public void EnterPhotoMode()
        {
            if (Runner.CameraControl.RunnerCameraController.Instance == null) return;
            photoModeState = PhotoModeState.Active;

            // Lock orbit target to current player position
            var player = FindFirstObjectByType<Runner.Player.PlayerController>();
            photoModeOrbitTarget = player != null ? player.transform.position : Runner.CameraControl.RunnerCameraController.Instance.transform.position;

            // Calculate initial orbit angle from camera to target
            Vector3 dir = Runner.CameraControl.RunnerCameraController.Instance.transform.position - photoModeOrbitTarget;
            photoModeOrbitAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            photoModeOrbitHeight = dir.y;
            photoModeZoom = Mathf.Max(dir.magnitude, 3.0f);

            Time.timeScale = 0f;
            ShowToast("📸", "Photo Mode — Drag to orbit, pinch to zoom", 2.0f);
        }

        public void ExitPhotoMode()
        {
            photoModeState = PhotoModeState.Off;
            Time.timeScale = 1.0f;
            if (Runner.CameraControl.RunnerCameraController.Instance != null)
            {
                Runner.CameraControl.RunnerCameraController.Instance.SnapToPlayer();
            }
        }

        private void UpdatePhotoModeCamera()
        {
            if (Runner.CameraControl.RunnerCameraController.Instance == null) return;

            Camera cam = Runner.CameraControl.RunnerCameraController.Instance.GetComponent<Camera>();
            if (cam == null) return;

            // Keep target updated with player position if available
            var player = FindFirstObjectByType<Runner.Player.PlayerController>();
            if (player != null)
                photoModeOrbitTarget = player.transform.position;

            // Drag to orbit around target
            if (Input.GetMouseButton(0))
            {
                float h = Input.GetAxis("Mouse X") * photoModeSensitivity;
                float v = Input.GetAxis("Mouse Y") * photoModeSensitivity;
                photoModeOrbitAngle += h;
                photoModeOrbitHeight += v * 0.5f;
                photoModeOrbitHeight = Mathf.Clamp(photoModeOrbitHeight, -2f, 8f);
            }

            // Touch single-finger orbit
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                Touch t = Input.GetTouch(0);
                float h = (t.deltaPosition.x / Screen.width) * photoModeSensitivity * 40f;
                float v = (t.deltaPosition.y / Screen.height) * photoModeSensitivity * 20f;
                photoModeOrbitAngle += h;
                photoModeOrbitHeight += v;
                photoModeOrbitHeight = Mathf.Clamp(photoModeOrbitHeight, -2f, 8f);
            }

            // Scroll / pinch to zoom distance
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                photoModeZoom = Mathf.Clamp(photoModeZoom - scroll * 5f, 3.0f, 15f);

            if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                float prevDist = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
                float currDist = (t0.position - t1.position).magnitude;
                float diff = (currDist - prevDist) * 0.02f;
                photoModeZoom = Mathf.Clamp(photoModeZoom - diff, 3.0f, 15f);
            }

            // Calculate orbit position around target
            float rad = photoModeOrbitAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * photoModeZoom, photoModeOrbitHeight, Mathf.Cos(rad) * photoModeZoom);
            Vector3 camPos = photoModeOrbitTarget + offset;

            cam.transform.position = camPos;
            cam.transform.LookAt(photoModeOrbitTarget);
            cam.fieldOfView = Mathf.Lerp(60f, 25f, (photoModeZoom - 3f) / 12f);

            // Escape / back to exit
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                ExitPhotoMode();
        }

        private void RenderPhotoModeUI()
        {
            if (photoModeState != PhotoModeState.Active) return;

            float uiScale = Mathf.Clamp(Screen.width / 420.0f, 0.85f, 2.4f);
            Rect safe = Screen.safeArea;

            // Top bar with controls
            float barH = 50f * uiScale;
            float barY = safe.y + 8f * uiScale;
            float barW = Mathf.Min(Screen.width * 0.9f, 380f * uiScale);
            float barX = (Screen.width - barW) * 0.5f;

            GUI.color = new Color(0.02f, 0.03f, 0.02f, 0.85f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTexture);
            GUI.color = new Color(0.20f, 0.90f, 0.55f, 0.60f);
            GUI.DrawTexture(new Rect(barX, barY, barW, 1.5f), whiteTexture);
            GUI.DrawTexture(new Rect(barX, barY + barH - 1.5f, barW, 1.5f), whiteTexture);

            GUI.color = new Color(1f, 0.90f, 0.40f);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(barX, barY, barW, barH), "📸  PHOTO MODE");

            // Bottom controls
            float btnH = 40f * uiScale;
            float btnY = Screen.height - safe.y - btnH - (16f * uiScale);
            float btnW = 120f * uiScale;
            float gap = 12f * uiScale;
            float totalBtnsW = btnW * 2 + gap;
            float btnStartX = (Screen.width - totalBtnsW) * 0.5f;

            // Capture button
            Rect captureRect = new Rect(btnStartX, btnY, btnW, btnH);
            DrawCard(captureRect, new Color(0.15f, 0.70f, 0.35f, 0.95f));
            GUI.color = Color.white;
            GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
            GUI.Label(captureRect, "📷 CAPTURE");
            if (IsCardClicked(920, captureRect))
            {
                StartCoroutine(CaptureScreenshotCoroutine());
            }

            // Exit button
            Rect exitRect = new Rect(btnStartX + btnW + gap, btnY, btnW, btnH);
            DrawCard(exitRect, new Color(0.65f, 0.18f, 0.18f, 0.95f));
            GUI.color = Color.white;
            GUI.Label(exitRect, "✕ EXIT");
            if (IsCardClicked(921, exitRect))
            {
                ExitPhotoMode();
            }

            // Hint text
            GUI.color = new Color(0.6f, 0.65f, 0.62f, 0.6f);
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(0, btnY - (16f * uiScale), Screen.width, 14f * uiScale),
                "Drag to orbit • Pinch/scroll to zoom • ESC to exit");
        }

        private System.Collections.IEnumerator CaptureScreenshotCoroutine()
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Destroy(tex);

            string filename = $"SpiderTemple_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string cachePath = System.IO.Path.Combine(Application.temporaryCachePath, filename);
            System.IO.File.WriteAllBytes(cachePath, bytes);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Save to gallery via MediaStore + show share chooser
            SaveAndShareScreenshotAndroid(cachePath, filename, bytes);
#else
            GUIUtility.systemCopyBuffer = cachePath;
            ShowToast("📸", $"Screenshot saved: {filename}", 2.5f);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void SaveAndShareScreenshotAndroid(string cachePath, string filename, byte[] pngBytes)
        {
            try
            {
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Save to external Pictures directory (no special permission needed for app's own writes on modern Android)
                var envClass = new AndroidJavaClass("android.os.Environment");
                var picturesDir = envClass.CallStatic<AndroidJavaObject>("getExternalPublicDirectory", envClass.GetStatic<string>("DIRECTORY_PICTURES"));
                var folder = new AndroidJavaObject("java.io.File", picturesDir, "SpiderTempleEscape");
                folder.Call<bool>("mkdirs");

                var file = new AndroidJavaObject("java.io.File", folder, filename);
                var fos = new AndroidJavaObject("java.io.FileOutputStream", file);
                var javaBytes = new AndroidJavaObject("java.io.ByteArrayOutputStream");
                var bos = javaBytes;
                bos.Call("write", pngBytes);
                fos.Call("write", bos.Call<byte[]>("toByteArray"));
                fos.Call("flush");
                fos.Call("close");
                bos.Call("close");

                string absolutePath = file.Call<string>("getAbsolutePath");

                // Scan into MediaStore so it appears in gallery
                var scannerClass = new AndroidJavaClass("android.media.MediaScannerConnection");
                scannerClass.CallStatic("scanFile", currentActivity, new AndroidJavaObject("java.lang.String[]", absolutePath), null, null);

                // Share via intent
                var uriClass = new AndroidJavaClass("android.net.Uri");
                var fileUri = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + absolutePath);

                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "image/png");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), GenerateShareText());
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), fileUri);
                    intent.Call<AndroidJavaObject>("addFlags", 0x00000003);
                    var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share or Save Screenshot");
                    currentActivity.Call("startActivity", chooser);
                }

                ShowToast("📸", "Saved to gallery + ready to share!", 2.5f);
            }
            catch (System.Exception e)
            {
                Debug.Log($"[PhotoMode] Save/share failed: {e.Message}");
                ShowToast("📸", "Screenshot captured", 2.5f);
            }
        }
#endif

        private string GenerateShareText()
        {
            float distance = GameManager.Instance != null ? GameManager.Instance.DistanceTraveled : 0f;
            int score = lastFinalScore;
            int coins = GameManager.Instance != null ? GameManager.Instance.CoinsCollected : 0;
            string charName = "Spider-Man";
            if (Characters.CharacterManager.Instance != null)
            {
                var slot = Characters.CharacterManager.Instance.GetCharacter(Characters.CharacterManager.Instance.SelectedCharacterIndex);
                if (slot != null && !string.IsNullOrEmpty(slot.characterName))
                    charName = slot.characterName;
            }
            return $"SPIDER TEMPLE ESCAPE\n{distance:N0}m | {score:N0}pts | {coins:N0} hearts\nHero: {charName}\nCan you beat my run?";
        }
        #endregion

        private void RenderTutorialOverlay()
        {
            if (!showTutorial || tutorialFadeAlpha <= 0f) return;

            float uiScale = Mathf.Clamp(Screen.width / 420.0f, 0.85f, 2.4f);
            float w = Screen.width;
            float h = Screen.height;
            float animT = Time.unscaledTime;

            // Dim background
            GUI.color = new Color(0f, 0f, 0f, 0.55f * tutorialFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, w, h), whiteTexture);

            // Tutorial card
            float cardW = Mathf.Min(w * 0.85f, 360f * uiScale);
            float cardH = 200f * uiScale;
            float cardX = (w - cardW) * 0.5f;
            float cardY = (h - cardH) * 0.5f;

            GUI.color = new Color(0.04f, 0.07f, 0.05f, 0.95f * tutorialFadeAlpha);
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), whiteTexture);
            GUI.color = new Color(0.20f, 0.90f, 0.55f, 0.70f * tutorialFadeAlpha);
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, 2f), whiteTexture);
            GUI.DrawTexture(new Rect(cardX, cardY + cardH - 2f, cardW, 2f), whiteTexture);
            GUI.DrawTexture(new Rect(cardX, cardY, 2f, cardH), whiteTexture);
            GUI.DrawTexture(new Rect(cardX + cardW - 2f, cardY, 2f, cardH), whiteTexture);

            // Title
            GUI.color = new Color(1f, 0.90f, 0.40f, tutorialFadeAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(cardX, cardY + (12f * uiScale), cardW, 24f * uiScale), "🕷️ HOW TO SURVIVE");

            // Step content
            string[][] steps = new string[][] {
                new string[] { "⬆️  SWIPE UP", "Leap over fallen logs,\nstone barriers & spikes" },
                new string[] { "⬇️  SWIPE DOWN", "Slide under temple arches,\nwooden beams & grates" },
                new string[] { "⬅️ ➡️  SWIPE LEFT/RIGHT", "Dodge between 3 lanes\nto avoid obstacles" }
            };

            if (tutorialStep < steps.Length)
            {
                float bounce = Mathf.Sin(animT * 3.0f) * 3f * uiScale;
                GUI.color = new Color(0.25f, 0.95f, 0.65f, tutorialFadeAlpha);
                GUI.skin.label.fontSize = Mathf.RoundToInt(20 * uiScale);
                GUI.Label(new Rect(cardX, cardY + (50f * uiScale) + bounce, cardW, 30f * uiScale), steps[tutorialStep][0]);

                GUI.color = new Color(0.85f, 0.92f, 0.88f, 0.85f * tutorialFadeAlpha);
                GUI.skin.label.fontSize = Mathf.RoundToInt(11 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Normal;
                bool prevWrap = GUI.skin.label.wordWrap;
                GUI.skin.label.wordWrap = true;
                GUI.Label(new Rect(cardX + (20f * uiScale), cardY + (95f * uiScale), cardW - (40f * uiScale), 50f * uiScale), steps[tutorialStep][1]);
                GUI.skin.label.wordWrap = prevWrap;
            }

            // Step indicators
            float dotSize = 8f * uiScale;
            float dotGap = 16f * uiScale;
            float dotsW = dotSize * 3 + dotGap * 2;
            float dotsX = (w - dotsW) * 0.5f;
            float dotsY = cardY + cardH - (28f * uiScale);
            for (int i = 0; i < 3; i++)
            {
                float dx = dotsX + i * (dotSize + dotGap);
                GUI.color = (i == tutorialStep)
                    ? new Color(0.25f, 0.95f, 0.65f, tutorialFadeAlpha)
                    : new Color(0.4f, 0.4f, 0.4f, 0.5f * tutorialFadeAlpha);
                GUI.DrawTexture(new Rect(dx, dotsY, dotSize, dotSize), whiteTexture);
            }

            // Tap to skip hint
            GUI.color = new Color(0.6f, 0.65f, 0.62f, 0.6f * tutorialFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(8 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(cardX, cardY + cardH - (12f * uiScale), cardW, 14f * uiScale), "TAP ANYWHERE TO CONTINUE");

            // Tap to dismiss
            if (tutorialFadeAlpha > 0.9f && Event.current.type == EventType.MouseDown)
            {
                showTutorial = false;
                PlayerPrefs.SetInt(TUTORIAL_SHOWN_KEY, 1);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region Game Over Modal (Clean Modern UI/UX)
        private void RenderGameOverModal()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float scaleW = Screen.width / 390.0f;
            float scaleH = Screen.height / 780.0f;
            float uiScale = Mathf.Clamp(Mathf.Min(scaleW, scaleH), 0.75f, 2.2f);

            // Entrance animation: smooth scale-in and fade-in over 0.5s
            float entranceProgress = Mathf.Clamp01(deathScreenEntranceTimer / 0.5f);
            float entranceEase = 1f - Mathf.Pow(1f - entranceProgress, 3f); // ease-out cubic
            float entranceAlpha = Mathf.Clamp01(deathScreenEntranceTimer / 0.35f);
            float entranceScale = 0.85f + 0.15f * entranceEase;

            // 1. Atmospheric Defeat Backdrop with Dark Vignette
            if (deathBgTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, entranceAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), deathBgTexture, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0.04f, 0.02f, 0.03f, 0.78f * entranceAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
            }
            else
            {
                GUI.color = new Color(0.04f, 0.02f, 0.03f, 0.94f * entranceAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
            }

            // Red danger vignette glow around screen edges
            if (vignetteRedTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.75f * entranceAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteRedTexture, ScaleMode.StretchToFill);
            }

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeTop = Mathf.Max(14f * uiScale, topOffset + (8f * uiScale));

            // 2. Defeat Context Header
            GetDeathDetails(lastDeathType, out string causeTag, out string deathTitle, out string deathSubtitle);

            float headerY = safeTop + (6f * uiScale);

            if (relicTexture != null)
            {
                float relicH = 32f * uiScale;
                Rect topRelicRect = new Rect((Screen.width - relicH) * 0.5f, headerY, relicH, relicH);
                GUI.color = new Color(1.0f, 0.80f, 0.80f, 0.95f * entranceAlpha);
                GUI.DrawTexture(topRelicRect, relicTexture, ScaleMode.ScaleToFit);
                headerY += (34f * uiScale);
            }

            // Refined Defeat Pill Badge
            float tagW = 200f * uiScale;
            float tagH = 22f * uiScale;
            Rect tagRect = new Rect((Screen.width - tagW) * 0.5f, headerY, tagW, tagH);
            DrawGlassPill(tagRect, new Color(0.24f, 0.05f, 0.08f, 0.92f * entranceAlpha), new Color(0.95f, 0.30f, 0.35f, 0.75f * entranceAlpha));
            GUI.color = new Color(1.0f, 0.75f, 0.75f, entranceAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(tagRect, $"✦ {causeTag} ✦");

            // Bold Death Title (with scale-in animation)
            float titleY = headerY + tagH + (6f * uiScale);
            float titleH = 34f * uiScale;
            int titleFont = Mathf.RoundToInt(25 * uiScale * entranceScale);

            // Drop shadow
            GUI.color = new Color(0.02f, 0.01f, 0.01f, 0.95f * entranceAlpha);
            GUI.skin.label.fontSize = titleFont;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(2f, titleY + 2f, Screen.width, titleH), deathTitle);

            // Front radiant layer
            GUI.color = new Color(1.0f, 0.92f, 0.92f, entranceAlpha);
            GUI.Label(new Rect(0, titleY, Screen.width, titleH), deathTitle);

            // Subtitle
            float subY = titleY + titleH;
            GUI.color = new Color(1.0f, 0.82f, 0.60f, 0.90f * entranceAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Italic;
            GUI.Label(new Rect(0, subY, Screen.width, 18f * uiScale), deathSubtitle);

            // 3. Unified Glass Scorecard (slides up from below)
            float cardW = Mathf.Min(Screen.width - (36f * uiScale), 350f * uiScale);
            float cardH = 168f * uiScale;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardSlideOffset = (1f - entranceEase) * 40f;
            float cardY = subY + (12f * uiScale) + cardSlideOffset;

            Rect summaryRect = new Rect(cardX, cardY, cardW, cardH);
            DrawGlassCard(summaryRect, new Color(0.06f, 0.08f, 0.12f, 0.95f * entranceAlpha), new Color(1f, 1f, 1f, 0.15f * entranceAlpha), 1.2f);
            DrawCornerBrackets(summaryRect, 10f * uiScale, 1.5f, new Color(1.0f, 0.82f, 0.30f, 0.70f * entranceAlpha));

            // Record Badge Pill
            bool isNewRecord = lastFinalScore >= GameManager.Instance.HighScore && lastFinalScore > 0;
            float recW = 150f * uiScale;
            float recH = 22f * uiScale;
            Rect recRect = new Rect(cardX + (cardW - recW) * 0.5f, cardY + (10f * uiScale), recW, recH);
            if (isNewRecord)
            {
                DrawGlassPill(recRect, new Color(0.28f, 0.20f, 0.04f, 0.95f * entranceAlpha), new Color(1.0f, 0.88f, 0.30f, 0.90f * entranceAlpha));
                GUI.color = new Color(1.0f, 0.92f, 0.40f, entranceAlpha);
            }
            else
            {
                DrawGlassPill(recRect, new Color(0.10f, 0.14f, 0.18f, 0.90f * entranceAlpha), new Color(1f, 1f, 1f, 0.20f * entranceAlpha));
                GUI.color = new Color(0.80f, 0.88f, 0.95f, entranceAlpha);
            }
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(recRect, isNewRecord ? "★ NEW BEST RECORD! ★" : "★ EXPEDITION SUMMARY ★");
            ResetGUIStyle();

            // Hero Metric: Distance Traveled
            float metricY = cardY + (38f * uiScale);
            GUI.color = new Color(0.80f, 0.85f, 0.92f, entranceAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(cardX, metricY, cardW, 14f * uiScale), "DISTANCE SURVIVED");
            ResetGUIStyle();

            // Distance — use overflow-protected SafeLabel (prevents large numbers bleeding off screen)
            GUI.color = new Color(1.0f, 0.88f, 0.25f, entranceAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(26 * uiScale); // Slightly reduced from 28 to prevent overflow
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            string distLabel = $"{GameManager.Instance.DistanceTraveled:N0} M";
            SafeLabel(new Rect(cardX, metricY + (14f * uiScale), cardW, 34f * uiScale), distLabel, Mathf.RoundToInt(26 * uiScale));

            // Divider Line
            float divY = metricY + (52f * uiScale);
            GUI.color = new Color(1f, 1f, 1f, 0.10f * entranceAlpha);
            GUI.DrawTexture(new Rect(cardX + 18, divY, cardW - 36, 1), whiteTexture);

            // 2-Column Secondary Metrics Grid
            float gridY = divY + (10f * uiScale);
            float colW = (cardW - (28f * uiScale)) * 0.5f;

            // Left Col: Relics Saved
            Rect relicsBox = new Rect(cardX + (10f * uiScale), gridY, colW, 48f * uiScale);
            DrawGlassCard(relicsBox, new Color(0.04f, 0.06f, 0.08f, 0.85f * entranceAlpha), new Color(1.0f, 0.35f, 0.65f, 0.40f * entranceAlpha));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.color = new Color(0.85f, 0.90f, 0.95f, entranceAlpha);
            GUI.Label(new Rect(relicsBox.x, relicsBox.y + (4f * uiScale), colW, 14f * uiScale), "HEARTS COLLECTED");
            ResetGUIStyle();
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = new Color(1.0f, 0.40f, 0.72f, entranceAlpha);
            GUI.Label(new Rect(relicsBox.x, relicsBox.y + (20f * uiScale), colW, 22f * uiScale), $"💖 {GameManager.Instance.CoinsCollected:N0}");
            ResetGUIStyle();

            // Right Col: Expedition Score
            Rect scoreBox = new Rect(cardX + cardW - colW - (10f * uiScale), gridY, colW, 48f * uiScale);
            DrawGlassCard(scoreBox, new Color(0.04f, 0.06f, 0.08f, 0.85f * entranceAlpha), new Color(0.20f, 0.85f, 0.95f, 0.40f * entranceAlpha));
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUI.color = new Color(0.85f, 0.90f, 0.95f, entranceAlpha);
            GUI.Label(new Rect(scoreBox.x, scoreBox.y + (4f * uiScale), colW, 14f * uiScale), "FINAL SCORE");
            ResetGUIStyle();
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.color = new Color(0.25f, 0.92f, 1.0f, entranceAlpha);
            GUI.Label(new Rect(scoreBox.x, scoreBox.y + (20f * uiScale), colW, 22f * uiScale), $"⭐ {lastFinalScore:N0}");
            ResetGUIStyle();

            // 4. Action Buttons — staggered reveal so they cascade in gracefully
            float safeButtonDelay = 0.8f; // Prevent accidental clicks
            bool buttonsReady = gameOverDuration > safeButtonDelay && entranceProgress > 0.9f;

            // Stagger: each button reveals 180ms after the previous
            const float STAGGER_DELAY = 0.18f;
            float btn1Alpha = buttonsReady ? Mathf.Clamp01((gameOverDuration - safeButtonDelay) / 0.3f) : 0f;
            float btn2Alpha = buttonsReady ? Mathf.Clamp01((gameOverDuration - safeButtonDelay - STAGGER_DELAY) / 0.3f) : 0f;
            float btn3Alpha = buttonsReady ? Mathf.Clamp01((gameOverDuration - safeButtonDelay - STAGGER_DELAY * 2f) / 0.3f) : 0f;
            float hintAlpha = buttonsReady ? Mathf.Clamp01((gameOverDuration - safeButtonDelay - STAGGER_DELAY * 3f) / 0.3f) : 0f;

            float footerY = cardY + cardH + (14f * uiScale);
            float btnW = cardW;
            float btnH = 48f * uiScale;
            float btnX = cardX;

            // 1. PLAY AGAIN (Prominent Emerald Radiant Primary CTA)
            Rect playAgainRect = new Rect(btnX, footerY, btnW, btnH);
            DrawCard(playAgainRect, new Color(0.12f, 0.78f, 0.38f, 0.98f * btn1Alpha));
            GUI.color = new Color(1f, 1f, 1f, 0.30f * btn1Alpha);
            GUI.DrawTexture(new Rect(btnX + 4, footerY + 2, btnW - 8, 2f), whiteTexture);
            GUI.color = new Color(0.06f, 0.40f, 0.18f, 0.80f * btn1Alpha);
            GUI.DrawTexture(new Rect(btnX + 4, footerY + btnH - 3, btnW - 8, 3f), whiteTexture);
            DrawBorder(playAgainRect, new Color(0.40f, 0.95f, 0.60f, btn1Alpha), 1.5f * uiScale);

            GUI.color = new Color(1f, 1f, 1f, btn1Alpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(16 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(playAgainRect, "▶   PLAY AGAIN");
            ResetGUIStyle();

            if (btn1Alpha > 0.5f && (IsCardClicked(601, playAgainRect) || GUI.Button(playAgainRect, GUIContent.none, GUIStyle.none)))
            {
                isGameOver = false;
                Time.timeScale = 1.0f;
                GameManager.Instance?.RestartGame();
            }

            // 2. MAIN MENU (Sleek Slate Ghost Button) — staggered in
            float menuY = footerY + btnH + (8f * uiScale);
            float menuH = 42f * uiScale;
            Rect mainMenuRect = new Rect(btnX, menuY, btnW, menuH);
            DrawGlassCard(mainMenuRect, new Color(0.12f, 0.16f, 0.22f, 0.90f * btn2Alpha), new Color(1f, 1f, 1f, 0.20f * btn2Alpha), 1.2f);

            GUI.color = new Color(0.85f, 0.90f, 0.95f, btn2Alpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(mainMenuRect, "🏠   RETURN TO CAMP");
            ResetGUIStyle();

            if (btn2Alpha > 0.5f && (IsCardClicked(602, mainMenuRect) || GUI.Button(mainMenuRect, GUIContent.none, GUIStyle.none)))
            {
                isGameOver = false;
                Time.timeScale = 1.0f;
                StartLoading(1.6f, "RETURNING TO CAMP...", () =>
                {
                    GameManager.Instance?.ReturnToMenu();
                }, "RETURNING TO BASE CAMP", "SAVING EXPEDITION DATA");
            }

            // 3. SHARE RUN STATS (Golden accent button) — staggered in
            float shareY = menuY + menuH + (8f * uiScale);
            float shareH = 42f * uiScale;
            Rect shareRect = new Rect(btnX, shareY, btnW, shareH);
            DrawGlassCard(shareRect, new Color(0.18f, 0.14f, 0.04f, 0.92f * btn3Alpha), new Color(1.0f, 0.82f, 0.30f, 0.45f * btn3Alpha), 1.2f);

            GUI.color = new Color(1.0f, 0.88f, 0.35f, btn3Alpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(13.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(shareRect, "📤   SHARE RUN STATS");
            ResetGUIStyle();

            if (btn3Alpha > 0.5f && (IsCardClicked(603, shareRect) || GUI.Button(shareRect, GUIContent.none, GUIStyle.none)))
            {
                ShareRunStats();
            }

            // Subtle keyboard navigation hint — only show on desktop/editor (not mobile)
            float hintY = shareY + shareH + (8f * uiScale);
            bool isMobile = Application.platform == RuntimePlatform.Android ||
                            Application.platform == RuntimePlatform.IPhonePlayer;
            if (!isMobile)
            {
                GUI.color = new Color(0.70f, 0.75f, 0.80f, 0.65f * hintAlpha);
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(9f * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Italic;
                GUI.Label(new Rect(0, hintY, Screen.width, 16f * uiScale), "Press SPACE or R to quick restart");
                ResetGUIStyle();
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

        #region Milestone Banners & Live Badges
        private static string GetZoneNameForDistance(float distance)
        {
            if (distance < 1000f) return "JUNGLE TRAIL";
            if (distance < 2000f) return "DEEP CANOPY";
            if (distance < 3500f) return "ANCIENT RUINS";
            if (distance < 5000f) return "TEMPLE DEPTHS";
            return "BOSS TERRITORY";
        }

        private void CheckDistanceMilestones()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            float dist = GameManager.Instance.DistanceTraveled;
            int nextIdx = lastMilestoneIndex + 1;
            if (nextIdx < 0) nextIdx = 0;

            while (nextIdx < MilestoneThresholds.Length && dist >= MilestoneThresholds[nextIdx])
            {
                lastMilestoneIndex = nextIdx;
                float hit = MilestoneThresholds[nextIdx];
                milestoneBannerText = $"🏛️ {hit:N0}m";
                milestoneBannerSub = $"{GetZoneNameForDistance(hit)} — KEEP RUNNING!";
                milestoneBannerTimer = 2.8f;
                InGameCanvasHUD.Instance?.ShowMilestone(milestoneBannerText, milestoneBannerSub);
                nextIdx++;
            }
        }

        private void RenderMilestoneBanner(float uiScale)
        {
            if (milestoneBannerTimer <= 0f) return;

            // Scale-in pop for the first 0.3s, fade out over the last 0.6s
            float age = 2.8f - milestoneBannerTimer;
            float pop = Mathf.Clamp01(age / 0.3f);
            float ease = 1f - Mathf.Pow(1f - pop, 3f);
            float alpha = Mathf.Clamp01(milestoneBannerTimer / 0.6f);

            float w = Mathf.Min(300f * uiScale, Screen.width - (40f * uiScale)) * (0.7f + 0.3f * ease);
            float h = 54f * uiScale;
            float x = (Screen.width - w) * 0.5f;

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float y = Mathf.Max(14f * uiScale, topOffset + 8f * uiScale) + (58f * uiScale);

            Rect bannerRect = new Rect(x, y, w, h);
            DrawCard(bannerRect, new Color(0.10f, 0.08f, 0.03f, 0.92f * alpha));
            DrawBorder(bannerRect, new Color(1.0f, 0.82f, 0.30f, 0.95f * alpha), 1.8f);
            DrawCornerBrackets(bannerRect, 10f * uiScale, 1.5f, new Color(1.0f, 0.88f, 0.40f, 0.90f * alpha));

            GUI.color = new Color(1.0f, 0.88f, 0.35f, alpha);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(17 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x, y + (4f * uiScale), w, 24f * uiScale), milestoneBannerText);

            GUI.color = new Color(0.55f, 0.95f, 0.75f, alpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.Label(new Rect(x, y + (28f * uiScale), w, 18f * uiScale), milestoneBannerSub);
        }

        private void RenderBeastWarning(float uiScale)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsStumbling) return;

            float flash = (Mathf.Sin(Time.unscaledTime * 10f) + 1f) * 0.5f;
            float w = Mathf.Min(260f * uiScale, Screen.width - (60f * uiScale));
            float h = 26f * uiScale;
            float x = (Screen.width - w) * 0.5f;

            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float y = Mathf.Max(14f * uiScale, topOffset + 8f * uiScale) + (118f * uiScale);

            Rect warnRect = new Rect(x, y, w, h);
            DrawCard(warnRect, new Color(0.30f, 0.04f, 0.06f, 0.90f));
            DrawBorder(warnRect, Color.Lerp(new Color(1f, 0.25f, 0.30f), new Color(1f, 0.75f, 0.20f), flash), 1.8f);

            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.55f), new Color(1f, 0.95f, 0.80f), flash);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(warnRect, $"⚠️ BEAST CLOSING IN! {GameManager.Instance.StumbleTimeRemaining:F1}s");
        }

        private void RenderNewBestBadge(float pillX, float badgeY, float pillW, float uiScale)
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.HighScore <= 0) return;
            if (GameManager.Instance.Score <= GameManager.Instance.HighScore) return;

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            float h = 20f * uiScale;
            Rect badgeRect = new Rect(pillX, badgeY, pillW, h);
            DrawCard(badgeRect, new Color(0.28f, 0.20f, 0.04f, 0.92f));
            DrawBorder(badgeRect, new Color(1.0f, 0.88f, 0.30f, pulse), 1.4f);

            GUI.color = new Color(1.0f, 0.90f, 0.40f, pulse);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(badgeRect, "★ NEW BEST ★");
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
                if (pointerPressedThisFrame || (Event.current != null && Event.current.isKey))
                {
                    loadingTimer = loadingDuration;
                }

                loadingTimer += Time.unscaledDeltaTime;
                float rawT = Mathf.Clamp01(loadingTimer / loadingDuration);
                // Realistic smooth ease loading curve (smoothstep / sinusoidal)
                loadingProgress = rawT * rawT * (3f - 2f * rawT);

                tipChangeTimer += Time.unscaledDeltaTime;
                if (tipChangeTimer > 2.0f)
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
                loadingFadeAlpha -= Time.unscaledDeltaTime * 2.5f;
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

            float uiScale = GetResponsiveScale();

            // 1. Full Screen Pointer Trap (absorbs clicks to prevent background UI triggers)
            GUI.color = Color.clear;
            GUI.Button(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);

            // 2. Temple corridor backdrop, darkened so the UI remains readable.
            GUI.color = new Color(0.34f, 0.43f, 0.40f, 0.78f * loadingFadeAlpha);
            if (loadingBackgroundTexture != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), loadingBackgroundTexture, ScaleMode.ScaleAndCrop);
            else
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            GUI.color = new Color(0.01f, 0.025f, 0.03f, 0.70f * loadingFadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);

            // 3. Ambient Atmospheric Glow (Warm Gold & Deep Emerald Core Aura)
            if (vignetteTexture != null)
            {
                float auraSize = Mathf.Max(Screen.width, Screen.height) * 1.15f;
                float auraX = (Screen.width - auraSize) * 0.5f;
                float auraY = (Screen.height - auraSize) * 0.5f;

                // Deep emerald center aura
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f);
                GUI.color = new Color(0.10f, 0.45f, 0.32f, 0.22f * pulse * loadingFadeAlpha);
                GUI.DrawTexture(new Rect(auraX, auraY, auraSize, auraSize), vignetteTexture, ScaleMode.StretchToFill);

                // Warm golden core accent
                float coreSize = auraSize * 0.65f;
                GUI.color = new Color(0.95f, 0.70f, 0.15f, 0.06f * pulse * loadingFadeAlpha);
                GUI.DrawTexture(new Rect((Screen.width - coreSize) * 0.5f, (Screen.height - coreSize) * 0.5f, coreSize, coreSize), vignetteTexture, ScaleMode.StretchToFill);
            }

            // 4. Procedural Floating Ambient Embers
            for (int i = 0; i < 16; i++)
            {
                float seed = i * 61.17f;
                float speedX = 14f + (i % 4) * 6f;
                float speedY = 30f + (i % 5) * 8f;
                float px = (seed * 37.3f + Time.unscaledTime * speedX + Mathf.Sin(Time.unscaledTime * 1.5f + seed) * 35f) % Screen.width;
                if (px < 0) px += Screen.width;
                float py = Screen.height - ((seed * 53.1f + Time.unscaledTime * speedY) % (Screen.height + 40f));

                float moteAlpha = (0.20f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f + seed)) * loadingFadeAlpha;
                float moteSize = (2f + (i % 3) * 1.5f) * uiScale;

                GUI.color = (i % 2 == 0)
                    ? new Color(0.20f, 0.95f, 0.72f, moteAlpha)   // Emerald
                    : new Color(1.0f, 0.82f, 0.25f, moteAlpha);   // Gold
                GUI.DrawTexture(new Rect(px, py, moteSize, moteSize), whiteTexture);
            }

            // Safe Area calculations
            Rect safe = Screen.safeArea;
            float topOffset = Screen.height > safe.height ? (Screen.height - (safe.y + safe.height)) : 0f;
            float safeBottom = Mathf.Max(16f * uiScale, safe.y);

            // Save previous font / alignment state
            TextAnchor prevAlign = GUI.skin.label.alignment;
            int prevSize = GUI.skin.label.fontSize;
            FontStyle prevStyle = GUI.skin.label.fontStyle;

            // 5. Centered game emblem with a restrained glow.
            float crestSize = Mathf.Min(128f * uiScale, Screen.width * 0.34f);
            float crestX = (Screen.width - crestSize) * 0.5f;
            float crestY = Screen.height * 0.28f;

            Rect crestRect = new Rect(crestX, crestY, crestSize, crestSize);
            DrawCornerBrackets(crestRect, 14f * uiScale, 1.5f, new Color(0.95f, 0.78f, 0.25f, 0.85f * loadingFadeAlpha));
            DrawGlassCard(crestRect, new Color(0.02f, 0.05f, 0.06f, 0.74f * loadingFadeAlpha), new Color(0.20f, 0.90f, 0.75f, 0.50f * loadingFadeAlpha), 1.2f);

            // Glowing Spider Icon with breathing pulse
            float spiderPulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3.5f);
            GUI.color = new Color(1.0f, 0.88f, 0.35f, spiderPulse * loadingFadeAlpha);
            if (appLogoTexture != null)
            {
                Rect innerLogo = new Rect(crestRect.x + 5, crestRect.y + 5, crestRect.width - 10, crestRect.height - 10);
                GUI.DrawTexture(innerLogo, appLogoTexture, ScaleMode.ScaleToFit);
            }
            else if (relicTexture != null)
            {
                Rect innerRelic = new Rect(crestRect.x + 8, crestRect.y + 8, crestRect.width - 16, crestRect.height - 16);
                GUI.DrawTexture(innerRelic, relicTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                GUI.skin.label.fontSize = Mathf.RoundToInt(26 * uiScale);
                GUI.skin.label.fontStyle = FontStyle.Bold;
                GUI.Label(crestRect, "🕷");
            }

            // Expanding Ping Ring
            float pingProg = (Time.unscaledTime * 0.75f) % 1.0f;
            float pingSize = crestSize + (pingProg * 40f * uiScale);
            float pingAlpha = (1f - pingProg) * 0.35f * loadingFadeAlpha;
            Rect pingRect = new Rect((Screen.width - pingSize) * 0.5f, (crestY + crestSize * 0.5f) - (pingSize * 0.5f), pingSize, pingSize);
            DrawBorder(pingRect, new Color(0.25f, 0.95f, 0.80f, pingAlpha), 1f);

            // 6. Cinematic Clean Title Treatment
            float titleY = crestY + crestSize + (18f * uiScale);

            // Subtitle Tag
            GUI.color = new Color(0.30f, 0.95f, 0.85f, 0.85f * loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, titleY, Screen.width, 18f * uiScale), "—  A N C I E N T   T E M P L E   E X P E D I T I O N  —");
            ResetGUIStyle();

            // Title Shadow
            GUI.color = new Color(0.02f, 0.05f, 0.04f, 0.90f * loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(24 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(2f, titleY + (20f * uiScale) + 2f, Screen.width, 36f * uiScale), loadingHeader);
            ResetGUIStyle();

            // Title Radiant Gold
            GUI.color = new Color(1.0f, 0.90f, 0.45f, 1.0f * loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(24 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.Label(new Rect(0, titleY + (20f * uiScale), Screen.width, 36f * uiScale), loadingHeader);
            ResetGUIStyle();

            // Golden Divider Flare Line
            float divW = 160f * uiScale;
            float divY = titleY + (56f * uiScale);
            float divX = (Screen.width - divW) * 0.5f;
            GUI.color = new Color(1.0f, 0.80f, 0.25f, 0.75f * loadingFadeAlpha);
            GUI.DrawTexture(new Rect(divX, divY + (3f * uiScale), divW * 0.42f, 1.2f), whiteTexture);
            GUI.DrawTexture(new Rect(divX + (divW * 0.58f), divY + (3f * uiScale), divW * 0.42f, 1.2f), whiteTexture);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10 * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(divX + (divW * 0.42f), divY - (4f * uiScale), divW * 0.16f, 14f * uiScale), "◆");
            ResetGUIStyle();

            // 7. Sleek Hairline Progress Rail (3px-4px)
            float barW = Mathf.Min(Screen.width - (48f * uiScale), 320f * uiScale);
            float barH = 4f * uiScale;
            float barX = (Screen.width - barW) * 0.5f;
            float barY = divY + (42f * uiScale);

            // Dynamic Sub-status progression
            string displayStatus = loadingStatusText;
            if (loadingStatusText == "INITIALIZING EXPEDITION...")
            {
                if (loadingProgress < 0.28f) displayStatus = "INITIALIZING EXPEDITION";
                else if (loadingProgress < 0.62f) displayStatus = "MAPPING JUNGLE RUINS";
                else if (loadingProgress < 0.92f) displayStatus = "SECURING WEB-LINE ROUTES";
                else displayStatus = "EXPEDITION READY";
            }

            int dotCount = ((int)(Time.unscaledTime * 3f)) % 4;
            string animatedStatus = displayStatus.TrimEnd('.') + new string('.', dotCount);

            // Status Micro-copy Row above Rail
            GUI.color = new Color(0.40f, 0.95f, 0.85f, 0.90f * loadingFadeAlpha);
            GUI.skin.label.fontSize = Mathf.RoundToInt(10f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(barX, barY - (22f * uiScale), barW * 0.70f, 18f * uiScale), animatedStatus);
            ResetGUIStyle();

            // Percentage Readout on Right
            int pct = Mathf.RoundToInt(Mathf.Clamp01(loadingProgress) * 100f);
            GUI.color = new Color(1.0f, 0.88f, 0.35f, loadingFadeAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(barX + (barW * 0.70f), barY - (22f * uiScale), barW * 0.30f, 18f * uiScale), $"[ {pct:D2}% ]");
            ResetGUIStyle();

            // Progress Bar Rail Track
            GUI.color = new Color(0.08f, 0.12f, 0.14f, 0.90f * loadingFadeAlpha);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), whiteTexture);
            DrawBorder(new Rect(barX - 1f, barY - 1f, barW + 2f, barH + 2f), new Color(1f, 1f, 1f, 0.12f * loadingFadeAlpha), 1f);

            // Progress Bar Radiant Fill — smooth trailing glow
            float fillW = barW * Mathf.Clamp01(loadingProgress);
            if (fillW > 0f)
            {
                // Trailing glow (2px gradient fade behind fill)
                GUI.color = new Color(1.0f, 0.82f, 0.20f, 0.55f * loadingFadeAlpha);
                GUI.DrawTexture(new Rect(Mathf.Max(barX, barX + fillW - barH * 2f), barY, barH * 2f, barH), whiteTexture);

                // Main fill
                GUI.color = new Color(1.0f, 0.82f, 0.20f, 0.98f * loadingFadeAlpha);
                GUI.DrawTexture(new Rect(barX, barY, fillW, barH), whiteTexture);

                // Leading Spark Dot — glow tied to fill position, no frame-rate jitter
                float dotSize = barH * 1.5f;
                float dotX = barX + fillW - (dotSize * 0.3f);
                float dotY = barY - (dotSize - barH) * 0.5f;
                float dotAlpha = loadingFadeAlpha * Mathf.Clamp01(loadingProgress * 3f);
                GUI.color = new Color(1f, 1f, 0.85f, dotAlpha);
                GUI.DrawTexture(new Rect(dotX, dotY, dotSize, dotSize), whiteTexture);
            }

            // 8. Elegant Expedition Survival Tip Card (Bottom)
            float tipW = Mathf.Min(Screen.width - (36f * uiScale), 350f * uiScale);
            float tipH = 62f * uiScale;
            float tipX = (Screen.width - tipW) * 0.5f;
            float tipY = Screen.height - safeBottom - tipH - (12f * uiScale);

            Rect tipRect = new Rect(tipX, tipY, tipW, tipH);
            DrawGlassCard(tipRect, new Color(0.04f, 0.07f, 0.09f, 0.90f * loadingFadeAlpha), new Color(1.0f, 0.80f, 0.25f, 0.45f * loadingFadeAlpha), 1.2f);
            DrawCornerBrackets(tipRect, 8f * uiScale, 1.2f, new Color(1.0f, 0.80f, 0.25f, 0.75f * loadingFadeAlpha));

            // Intel Header Tag Bar inside Card
            float headerTagH = 18f * uiScale;
            Rect headerTagRect = new Rect(tipX + (10f * uiScale), tipY + (5f * uiScale), tipW - (20f * uiScale), headerTagH);
            GUI.color = new Color(1.0f, 0.82f, 0.30f, 0.85f * loadingFadeAlpha);
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUI.skin.label.fontSize = Mathf.RoundToInt(9.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(headerTagRect, "✦ EXPEDITION SURVIVAL INTEL");

            // Tip Counter on Right
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUI.color = new Color(0.35f, 0.95f, 0.80f, 0.85f * loadingFadeAlpha);
            GUI.Label(headerTagRect, $"INTEL #{currentTipIndex + 1:D2} / {LoadingTips.Length:D2}");

            // Divider line in Intel Card
            GUI.color = new Color(1f, 1f, 1f, 0.10f * loadingFadeAlpha);
            GUI.DrawTexture(new Rect(tipX + (10f * uiScale), tipY + headerTagH + (4f * uiScale), tipW - (20f * uiScale), 1f), whiteTexture);

            // Tip Body Text
            string currentTip = (currentTipIndex >= 0 && currentTipIndex < LoadingTips.Length)
                ? LoadingTips[currentTipIndex]
                : "🕷️ Swipe UP to leap over obstacles, DOWN to slide!";

            bool prevWrap = GUI.skin.label.wordWrap;
            GUI.skin.label.wordWrap = true;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = Mathf.RoundToInt(10.5f * uiScale);
            GUI.skin.label.fontStyle = FontStyle.Bold;

            Rect textRect = new Rect(tipX + (10f * uiScale), tipY + headerTagH + (6f * uiScale), tipW - (20f * uiScale), tipH - headerTagH - (10f * uiScale));
            GUI.color = new Color(1f, 0.96f, 0.90f, 0.95f * loadingFadeAlpha);
            GUI.Label(textRect, currentTip);

            // Restore GUI Styles
            GUI.skin.label.wordWrap = prevWrap;
            GUI.skin.label.alignment = prevAlign;
            GUI.skin.label.fontSize = prevSize;
            GUI.skin.label.fontStyle = prevStyle;
        }
        #endregion

        #region Parallax Jungle Background Layers
        private void DrawParallaxJungleLayers()
        {
            if (whiteTexture == null) return;

            float w = Screen.width;
            float h = Screen.height;

            // Simple 3-band gradient — no allocation, 3 draw calls total
            GUI.color = new Color(0.03f, 0.06f, 0.05f, 0.50f);
            GUI.DrawTexture(new Rect(0, 0, w, h * 0.35f), whiteTexture);
            GUI.color = new Color(0.02f, 0.04f, 0.03f, 0.35f);
            GUI.DrawTexture(new Rect(0, h * 0.35f, w, h * 0.30f), whiteTexture);
            GUI.color = new Color(0.04f, 0.08f, 0.05f, 0.25f);
            GUI.DrawTexture(new Rect(0, h * 0.65f, w, h * 0.35f), whiteTexture);
        }
        #endregion

        #region AAA Cinematic Overlays
        /// <summary>
        /// Draws a radial vignette overlay for cinematic depth.
        /// The vignette gently pulses when the player is low on health or has been recently hit.
        /// </summary>
        private void DrawCinematicVignette()
        {
            if (vignetteTexture == null) return;

            // Pulse intensity when damaged or low health
            float pulseIntensity = 1.0f;
            if (vignetteTimer > 0f)
            {
                pulseIntensity = 1.0f + 0.5f * (vignetteTimer / 1.0f);
            }
            else if (GameManager.Instance != null && GameManager.Instance.CurrentLives <= 2)
            {
                float danger = (Mathf.Sin(Time.unscaledTime * 3.5f) + 1f) * 0.5f;
                pulseIntensity = 1.0f + danger * 0.25f;
            }

            // Draw vignette scaled to fill screen
            float size = Mathf.Max(Screen.width, Screen.height) * 1.5f;
            float x = (Screen.width - size) * 0.5f;
            float y = (Screen.height - size) * 0.5f;

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp(pulseIntensity, 0f, 1f));
            GUI.DrawTexture(new Rect(x, y, size, size), vignetteTexture, ScaleMode.StretchToFill);
        }

        /// <summary>
        /// Draws a subtle radial speed overlay when running at high velocity.
        /// Simulates the peripheral blur/wind effect of a AAA runner game.
        /// </summary>
        private void DrawSpeedOverlay()
        {
            if (speedLinesTexture == null) return;
            if (speedLineAlpha <= 0.01f) return;

            float size = Mathf.Max(Screen.width, Screen.height) * 1.8f;
            float x = (Screen.width - size) * 0.5f;
            float y = (Screen.height - size) * 0.5f;

            // Tinted white radial for speed blur hint
            GUI.color = new Color(1f, 1f, 1f, speedLineAlpha);
            GUI.DrawTexture(new Rect(x, y, size, size), speedLinesTexture, ScaleMode.StretchToFill);
        }

        /// <summary>
        /// Triggers a damage/impact vignette flash.
        /// </summary>
        public void TriggerDamageVignette()
        {
            vignetteTimer = 1.0f;
        }

        /// <summary>
        /// Captures a screenshot of the death screen and shares it with run stats via native share intent.
        /// </summary>
        private void ShareRunStats()
        {
            StartCoroutine(ShareRunStatsWithScreenshot());
        }

        private System.Collections.IEnumerator ShareRunStatsWithScreenshot()
        {
            yield return new WaitForEndOfFrame();

            Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            byte[] screenshotBytes = tex.EncodeToPNG();
            Destroy(tex);

            string filename = $"SpiderTemple_Run_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string screenshotPath = System.IO.Path.Combine(Application.temporaryCachePath, filename);
            System.IO.File.WriteAllBytes(screenshotPath, screenshotBytes);

            string shareText = GenerateShareText();

#if UNITY_ANDROID && !UNITY_EDITOR
            ShareWithScreenshotAndroid(screenshotPath, filename, screenshotBytes, shareText);
#elif UNITY_IOS && !UNITY_EDITOR
            GUIUtility.systemCopyBuffer = shareText;
            ShowToast("📤", "Run stats copied to clipboard!", 2.5f);
#else
            GUIUtility.systemCopyBuffer = shareText;
            ShowToast("📤", "Run stats copied to clipboard! Paste to share.", 2.5f);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void ShareWithScreenshotAndroid(string screenshotPath, string filename, byte[] pngBytes, string shareText)
        {
            try
            {
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                var envClass = new AndroidJavaClass("android.os.Environment");
                var picturesDir = envClass.CallStatic<AndroidJavaObject>("getExternalPublicDirectory", envClass.GetStatic<string>("DIRECTORY_PICTURES"));
                var folder = new AndroidJavaObject("java.io.File", picturesDir, "SpiderTempleEscape");
                folder.Call<bool>("mkdirs");

                var file = new AndroidJavaObject("java.io.File", folder, filename);
                var fos = new AndroidJavaObject("java.io.FileOutputStream", file);
                var javaBytes = new AndroidJavaObject("java.io.ByteArrayOutputStream");
                javaBytes.Call("write", pngBytes);
                fos.Call("write", javaBytes.Call<byte[]>("toByteArray"));
                fos.Call("flush");
                fos.Call("close");
                javaBytes.Call("close");

                string absolutePath = file.Call<string>("getAbsolutePath");

                var scannerClass = new AndroidJavaClass("android.media.MediaScannerConnection");
                scannerClass.CallStatic("scanFile", currentActivity, new AndroidJavaObject("java.lang.String[]", absolutePath), null, null);

                var uriClass = new AndroidJavaClass("android.net.Uri");
                var fileUri = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + absolutePath);

                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "image/png");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), shareText);
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), fileUri);
                    intent.Call<AndroidJavaObject>("addFlags", 0x00000003);
                    var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Run Stats");
                    currentActivity.Call("startActivity", chooser);
                }

                ShowToast("📤", "Screenshot saved + ready to share!", 2.5f);
            }
            catch (System.Exception e)
            {
                Debug.Log($"[Share] Android share failed: {e.Message}");
                GUIUtility.systemCopyBuffer = shareText;
                ShowToast("📤", "Stats copied to clipboard!", 2.5f);
            }
        }
#endif
        #endregion
    }
}
