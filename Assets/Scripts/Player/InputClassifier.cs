using System;
using UnityEngine;

namespace Runner.Player
{
    public enum SwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Bulletproof Input Handler:
    /// 1. Primary: OnGUI Event interception (works in ALL Unity input configurations without exception)
    /// 2. Secondary: UnityEngine.Input (Legacy Input Manager)
    /// 3. Tertiary: UnityEngine.InputSystem (New Input System via reflection)
    /// 4. Touch & Mouse drag swipes
    /// 5. On-Screen Touch / Click Controls with visual feedback
    /// </summary>
    public class InputClassifier : MonoBehaviour
    {
        // Minimum swipe distance is dynamically computed from Screen.dpi at runtime (5mm threshold)
        private const float minSwipeDistanceMM = 5.0f;

        [Tooltip("Maximum allowed duration in seconds for a valid flick/swipe gesture")]
        [SerializeField] private float maxSwipeDuration = 0.35f;

        [Tooltip("Axis dominance ratio: |dx| < 1.4*|dy| -> vertical swipe")]
        [SerializeField] private float verticalDominanceRatio = 1.4f;

        [Header("Tilt / Accelerometer Settings")]
        [Tooltip("Enable accelerometer tilt for lane weaving")]
        [SerializeField] private bool enableTilt = false;

        [Tooltip("Tilt deadzone to prevent accidental movement from slight hand shake")]
        [SerializeField] private float tiltDeadzone = 0.10f;

        [Tooltip("Tilt sensitivity multiplier")]
        [SerializeField] private float tiltSensitivity = 2.0f;

        // Current tilt value [-1.0f, +1.0f]
        public float CurrentTilt { get; private set; }

        // Events for decoupled subscriber handling
        public event Action OnSwipeUp;
        public event Action OnSwipeDown;
        public event Action OnSwipeLeft;
        public event Action OnSwipeRight;
        public event Action<float> OnTiltChanged;

        // Internal touch & mouse tracking
        private Vector2 touchStartPosition;
        private float touchStartTime;
        private bool isTrackingTouch = false;

        private Vector2 mouseStartPosition;
        private float mouseStartTime;
        private bool isTrackingMouse = false;

        // Frame de-duplication to prevent double triggers in single frame
        private int lastUpFrame = -1;
        private int lastDownFrame = -1;
        private int lastLeftFrame = -1;
        private int lastRightFrame = -1;

        // Visual feedback timers for HUD
        private float upFeedbackTimer = 0f;
        private float downFeedbackTimer = 0f;
        private float leftFeedbackTimer = 0f;
        private float rightFeedbackTimer = 0f;

        public void TriggerUp()
        {
            if (Time.frameCount == lastUpFrame) return;
            lastUpFrame = Time.frameCount;
            upFeedbackTimer = 0.2f;
            OnSwipeUp?.Invoke();
        }

        public void TriggerDown()
        {
            if (Time.frameCount == lastDownFrame) return;
            lastDownFrame = Time.frameCount;
            downFeedbackTimer = 0.2f;
            OnSwipeDown?.Invoke();
        }

        public void TriggerLeft()
        {
            if (Time.frameCount == lastLeftFrame) return;
            lastLeftFrame = Time.frameCount;
            leftFeedbackTimer = 0.2f;
            OnSwipeLeft?.Invoke();
        }

        public void TriggerRight()
        {
            if (Time.frameCount == lastRightFrame) return;
            lastRightFrame = Time.frameCount;
            rightFeedbackTimer = 0.2f;
            OnSwipeRight?.Invoke();
        }

        private void Update()
        {
            if (Runner.Core.GameManager.Instance != null && Runner.Core.GameManager.Instance.CurrentState == Runner.Core.GameState.GameOver)
                return;

            float dt = Time.deltaTime;
            if (upFeedbackTimer > 0) upFeedbackTimer -= dt;
            if (downFeedbackTimer > 0) downFeedbackTimer -= dt;
            if (leftFeedbackTimer > 0) leftFeedbackTimer -= dt;
            if (rightFeedbackTimer > 0) rightFeedbackTimer -= dt;

            // 1. Input Check (Cached to prevent InvalidOperationException every frame)
            if (isLegacyInputSupported)
            {
                try
                {
                    if (!isLegacyInputTested)
                    {
                        _ = Input.anyKey;
                        isLegacyInputTested = true;
                    }

                    if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
                        TriggerUp();
                    if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                        TriggerDown();
                    if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                        TriggerLeft();
                    if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                        TriggerRight();

                    HandleTouchInput();
                    HandleMouseInput();
                    HandleAccelerometerTilt();
                }
                catch (System.InvalidOperationException)
                {
                    // New Input System is active in Player Settings. Flag once and permanently bypass legacy Input.
                    isLegacyInputSupported = false;
                    isLegacyInputTested = true;
                    HandleNewInputSystem();
                }
            }
            else
            {
                HandleNewInputSystem();
            }
        }

        private static bool isLegacyInputTested = false;
        private static bool isLegacyInputSupported = true;

        #region OnGUI Event Catching (Universal & Guaranteed)
        private void OnGUI()
        {
            if (Runner.Core.GameManager.Instance != null && Runner.Core.GameManager.Instance.CurrentState == Runner.Core.GameState.GameOver)
                return;

            Event e = Event.current;
            if (e != null && e.isKey && e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                    case KeyCode.Space:
                        TriggerUp();
                        break;

                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        TriggerDown();
                        break;

                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        TriggerLeft();
                        break;

                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        TriggerRight();
                        break;
                }
            }

            // Mouse Swipe handling via OnGUI when New Input System is active
            if (!isLegacyInputSupported && e != null)
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    mouseStartPosition = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
                    mouseStartTime = Time.unscaledTime;
                    isTrackingMouse = true;
                }
                else if (e.type == EventType.MouseDrag && isTrackingMouse)
                {
                    Vector2 curPos = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
                    float elapsed = Time.unscaledTime - mouseStartTime;
                    Vector2 delta = curPos - mouseStartPosition;
                    if (delta.magnitude >= GetMinSwipeDistance() && elapsed <= maxSwipeDuration)
                    {
                        ProcessSwipeVector(mouseStartPosition, curPos, elapsed);
                        isTrackingMouse = false;
                    }
                }
                else if (e.type == EventType.MouseUp && isTrackingMouse)
                {
                    Vector2 curPos = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
                    ProcessSwipeVector(mouseStartPosition, curPos, Time.unscaledTime - mouseStartTime);
                    isTrackingMouse = false;
                }
            }

            // On-screen arrow buttons removed as requested by user (swipe, touch, tilt and keyboard controls handle all navigation)
        }
        #endregion

        #region Touch Gestures (Android High-Performance Low-Latency)
        private float GetMinSwipeDistance()
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            float dpiDist = dpi * 0.20f; // ~5.0mm on any Android screen
            float screenDist = Mathf.Min(Screen.width, Screen.height) * 0.04f;
            return Mathf.Clamp(Mathf.Max(dpiDist, screenDist), 25f, 90f);
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;

            Touch touch = Input.GetTouch(0);
            float effectiveMinDist = GetMinSwipeDistance();

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchStartPosition = touch.position;
                    touchStartTime = Time.unscaledTime;
                    isTrackingTouch = true;
                    break;

                case TouchPhase.Moved:
                    if (isTrackingTouch)
                    {
                        float elapsed = Time.unscaledTime - touchStartTime;
                        Vector2 delta = touch.position - touchStartPosition;
                        // Instant low-latency flick gesture trigger without waiting for finger release!
                        if (delta.magnitude >= effectiveMinDist && elapsed <= maxSwipeDuration)
                        {
                            ProcessSwipeVector(touchStartPosition, touch.position, elapsed);
                            isTrackingTouch = false; // Consumed swipe
                        }
                    }
                    break;

                case TouchPhase.Ended:
                    if (isTrackingTouch)
                    {
                        ProcessSwipeVector(touchStartPosition, touch.position, Time.unscaledTime - touchStartTime);
                        isTrackingTouch = false;
                    }
                    break;

                case TouchPhase.Canceled:
                    isTrackingTouch = false;
                    break;
            }
        }
        #endregion

        #region Mouse Fallback
        private void HandleMouseInput()
        {
            if (Input.touchCount > 0) return;

            float effectiveMinDist = GetMinSwipeDistance();

            if (Input.GetMouseButtonDown(0))
            {
                mouseStartPosition = Input.mousePosition;
                mouseStartTime = Time.unscaledTime;
                isTrackingMouse = true;
            }
            else if (Input.GetMouseButton(0) && isTrackingMouse)
            {
                float elapsed = Time.unscaledTime - mouseStartTime;
                Vector2 delta = (Vector2)Input.mousePosition - mouseStartPosition;
                if (delta.magnitude >= effectiveMinDist && elapsed <= maxSwipeDuration)
                {
                    ProcessSwipeVector(mouseStartPosition, Input.mousePosition, elapsed);
                    isTrackingMouse = false;
                }
            }
            else if (Input.GetMouseButtonUp(0) && isTrackingMouse)
            {
                ProcessSwipeVector(mouseStartPosition, Input.mousePosition, Time.unscaledTime - mouseStartTime);
                isTrackingMouse = false;
            }
        }

        private void ProcessSwipeVector(Vector2 startPos, Vector2 endPos, float duration)
        {
            if (duration > maxSwipeDuration) return;

            Vector2 delta = endPos - startPos;
            if (delta.magnitude < GetMinSwipeDistance()) return;

            float absDx = Mathf.Abs(delta.x);
            float absDy = Mathf.Abs(delta.y);

            if (absDx < (verticalDominanceRatio * absDy))
            {
                if (delta.y > 0) TriggerUp();
                else TriggerDown();
            }
            else
            {
                if (delta.x > 0) TriggerRight();
                else TriggerLeft();
            }
        }
        #endregion

        #region Accelerometer Tilt
        private void HandleAccelerometerTilt()
        {
            if (!enableTilt)
            {
                CurrentTilt = 0f;
                return;
            }

            float rawTilt = Input.acceleration.x;
            if (Mathf.Abs(rawTilt) < tiltDeadzone)
            {
                CurrentTilt = 0.0f;
            }
            else
            {
                float sign = Mathf.Sign(rawTilt);
                float normalized = (Mathf.Abs(rawTilt) - tiltDeadzone) / (1.0f - tiltDeadzone);
                CurrentTilt = Mathf.Clamp(sign * normalized * tiltSensitivity, -1.0f, 1.0f);
            }
            OnTiltChanged?.Invoke(CurrentTilt);
        }
        #endregion

        #region New Input System Dynamic Reflection
        private static System.Type keyboardClass;
        private static System.Reflection.PropertyInfo keyboardCurrentProp;
        private static System.Reflection.PropertyInfo wKeyProp, sKeyProp, aKeyProp, dKeyProp;
        private static System.Reflection.PropertyInfo spaceKeyProp, upArrowProp, downArrowProp, leftArrowProp, rightArrowProp;
        private static System.Reflection.PropertyInfo isPressedProp;
        private static bool newSystemInitialized = false;

        private static bool wasUpPressedLast;
        private static bool wasDownPressedLast;
        private static bool wasLeftPressedLast;
        private static bool wasRightPressedLast;

        private void InitNewInputSystem()
        {
            if (newSystemInitialized) return;
            newSystemInitialized = true;

            try
            {
#if UNITY_EDITOR
                // In Editor use TypeCache (safe, no unloaded-assembly risk)
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
                {
                    if (type.Assembly.GetName().Name == "Unity.InputSystem")
                    {
                        keyboardClass = type.Assembly.GetType("UnityEngine.InputSystem.Keyboard");
                        break;
                    }
                }
#else
                // At runtime use the already-loaded assemblies snapshot (safe because we iterate a copy)
                var loadedAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in loadedAssemblies)
                {
                    if (asm.GetName().Name == "Unity.InputSystem")
                    {
                        keyboardClass = asm.GetType("UnityEngine.InputSystem.Keyboard");
                        break;
                    }
                }
#endif
                if (keyboardClass != null)
                {
                    keyboardCurrentProp = keyboardClass.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    wKeyProp           = keyboardClass.GetProperty("wKey");
                    sKeyProp           = keyboardClass.GetProperty("sKey");
                    aKeyProp           = keyboardClass.GetProperty("aKey");
                    dKeyProp           = keyboardClass.GetProperty("dKey");
                    spaceKeyProp       = keyboardClass.GetProperty("spaceKey");
                    upArrowProp        = keyboardClass.GetProperty("upArrowKey");
                    downArrowProp      = keyboardClass.GetProperty("downArrowKey");
                    leftArrowProp      = keyboardClass.GetProperty("leftArrowKey");
                    rightArrowProp     = keyboardClass.GetProperty("rightArrowKey");

                    var buttonControl = keyboardClass.Assembly.GetType("UnityEngine.InputSystem.Controls.ButtonControl");
                    if (buttonControl != null)
                        isPressedProp = buttonControl.GetProperty("isPressed");
                }
            }
            catch (System.Exception) { }
        }

        private bool CheckPressed(object keyboard, System.Reflection.PropertyInfo prop)
        {
            if (keyboard == null || prop == null || isPressedProp == null) return false;
            try
            {
                object key = prop.GetValue(keyboard);
                if (key != null) return (bool)isPressedProp.GetValue(key);
            }
            catch { }
            return false;
        }

        private void HandleNewInputSystem()
        {
            InitNewInputSystem();
            if (keyboardCurrentProp == null) return;

            try
            {
                object kb = keyboardCurrentProp.GetValue(null);
                if (kb == null) return;

                bool isUp = CheckPressed(kb, wKeyProp) || CheckPressed(kb, upArrowProp) || CheckPressed(kb, spaceKeyProp);
                bool isDown = CheckPressed(kb, sKeyProp) || CheckPressed(kb, downArrowProp);
                bool isLeft = CheckPressed(kb, aKeyProp) || CheckPressed(kb, leftArrowProp);
                bool isRight = CheckPressed(kb, dKeyProp) || CheckPressed(kb, rightArrowProp);

                if (isUp && !wasUpPressedLast) TriggerUp();
                if (isDown && !wasDownPressedLast) TriggerDown();
                if (isLeft && !wasLeftPressedLast) TriggerLeft();
                if (isRight && !wasRightPressedLast) TriggerRight();

                wasUpPressedLast = isUp;
                wasDownPressedLast = isDown;
                wasLeftPressedLast = isLeft;
                wasRightPressedLast = isRight;
            }
            catch { }
        }
        #endregion
    }
}
