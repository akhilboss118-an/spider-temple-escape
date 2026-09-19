using System;
using System.Collections.Generic;
using UnityEngine;
using Runner.Core;
using Runner.Player;
using Runner.Effects;

namespace Runner.Pickups
{
    public enum PowerUpType
    {
        Shield,
        Speedrun,
        Magnet,
        HeartRevive,
        MultiplierFrenzy
    }

    [Serializable]
    public class PowerUpInstance
    {
        public PowerUpType Type;
        public string DisplayName;
        public string Icon;
        public Color ThemeColor;
        public float TimeRemaining;
        public float TotalDuration;

        public bool IsActive => TimeRemaining > 0.0f;
        public float Progress => Mathf.Clamp01(TimeRemaining / Mathf.Max(0.01f, TotalDuration));
        public float NormalizedProgress => Progress;

        public PowerUpInstance(PowerUpType type, string name, string icon, Color color)
        {
            Type = type;
            DisplayName = name;
            Icon = icon;
            ThemeColor = color;
            TimeRemaining = 0f;
            TotalDuration = 1f;
        }

        public void Activate(float duration)
        {
            TotalDuration = duration;
            TimeRemaining = duration;
        }

        public void Tick(float dt)
        {
            if (TimeRemaining > 0f)
            {
                TimeRemaining = Mathf.Max(0f, TimeRemaining - dt);
            }
        }

        public void Deactivate()
        {
            TimeRemaining = 0f;
        }
    }

    /// <summary>
    /// Unified Power-Up Manager:
    /// Consolidates all power-up mechanics into a unified system so every power-up type
    /// functions seamlessly within a single gameplay session.
    /// Supports multiple simultaneously active power-ups (e.g. Shield + Magnet + Speedrun + MultiplierFrenzy running concurrently).
    /// </summary>
    public class PickupManager : MonoBehaviour
    {
        public static PickupManager Instance { get; private set; }

        [Header("Default Durations")]
        [SerializeField] private float speedrunDuration = 6.0f;
        [SerializeField] private float magnetDuration = 8.0f;
        [SerializeField] private float magnetRadius = 8.0f;
        [SerializeField] private float shieldBaseDuration = 15.0f;
        [SerializeField] private float frenzyDuration = 8.0f;

        // Unified Power-Up State Map
        private readonly Dictionary<PowerUpType, PowerUpInstance> powerUps = new Dictionary<PowerUpType, PowerUpInstance>();
        private readonly List<PowerUpInstance> activePowerUpList = new List<PowerUpInstance>();

        // Legacy accessors preserved for 100% backward compatibility
        public bool HasShield => IsPowerUpActive(PowerUpType.Shield);
        public float ShieldTimeRemaining => GetPowerUpTimeRemaining(PowerUpType.Shield);
        public float ShieldTotalDuration => GetPowerUpTotalDuration(PowerUpType.Shield);

        public bool IsSpeedrunActive => IsPowerUpActive(PowerUpType.Speedrun);
        public float SpeedrunTimeRemaining => GetPowerUpTimeRemaining(PowerUpType.Speedrun);
        public float SpeedrunTotalDuration => GetPowerUpTotalDuration(PowerUpType.Speedrun);

        public bool IsMagnetActive => IsPowerUpActive(PowerUpType.Magnet);
        public float MagnetTimeRemaining => GetPowerUpTimeRemaining(PowerUpType.Magnet);
        public float MagnetTotalDuration => GetPowerUpTotalDuration(PowerUpType.Magnet);
        public float MagnetRadius => magnetRadius;

        public bool IsMultiplierFrenzyActive => IsPowerUpActive(PowerUpType.MultiplierFrenzy);
        public float MultiplierFrenzyTimeRemaining => GetPowerUpTimeRemaining(PowerUpType.MultiplierFrenzy);
        public float MultiplierFrenzyTotalDuration => GetPowerUpTotalDuration(PowerUpType.MultiplierFrenzy);

        // Character-specific bonus flags
        public float MagnetRadiusBonus { get; set; } = 0f;
        public bool AnyaChestBonus { get; set; } = false;

        // Player Shield, Frenzy, Magnet & Speedrun Auras
        private GameObject shieldAuraObj;
        private GameObject frenzyAuraObj;
        private GameObject magnetAuraObj;
        private GameObject speedrunAuraObj;
        private GameObject magnetRing1;
        private GameObject magnetRing2;
        private GameObject speedWindRibbon1;
        private GameObject speedWindRibbon2;

        // Events
        public event Action<bool> OnShieldStateChanged;
        public event Action<bool, float> OnSpeedrunStateChanged;
        public event Action<bool, float> OnMagnetStateChanged;
        public event Action<bool, float> OnMultiplierFrenzyStateChanged;
        public event Action<PowerUpType, bool> OnPowerUpStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeRegistry();
        }

        private void InitializeRegistry()
        {
            powerUps.Clear();
            powerUps[PowerUpType.Shield] = new PowerUpInstance(
                PowerUpType.Shield,
                "MYSTIC WARD",
                "🛡️",
                new Color(0.15f, 0.70f, 1.0f)
            );
            powerUps[PowerUpType.Speedrun] = new PowerUpInstance(
                PowerUpType.Speedrun,
                "MONSTER BOOST",
                "⚡",
                new Color(0.20f, 0.95f, 0.45f)
            );
            powerUps[PowerUpType.Magnet] = new PowerUpInstance(
                PowerUpType.Magnet,
                "HEART MAGNET",
                "🧲",
                new Color(1.0f, 0.35f, 0.65f)
            );
            powerUps[PowerUpType.MultiplierFrenzy] = new PowerUpInstance(
                PowerUpType.MultiplierFrenzy,
                "FRENZY TOTEM",
                "🔥",
                new Color(1.0f, 0.75f, 0.15f)
            );
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool shieldWasActive = HasShield;
            bool speedrunWasActive = IsSpeedrunActive;
            bool magnetWasActive = IsMagnetActive;
            bool frenzyWasActive = IsMultiplierFrenzyActive;

            // Tick each registered power-up
            foreach (var kvp in powerUps)
            {
                var p = kvp.Value;
                if (p.IsActive)
                {
                    p.Tick(dt);

                    if (!p.IsActive)
                    {
                        // Power-up just expired
                        HandlePowerUpExpired(p.Type);
                    }
                }
            }

            // Fire legacy and updated events
            if (shieldWasActive != HasShield)
            {
                SetShieldAura(HasShield);
                OnShieldStateChanged?.Invoke(HasShield);
                OnPowerUpStateChanged?.Invoke(PowerUpType.Shield, HasShield);
            }

            if (speedrunWasActive != IsSpeedrunActive)
            {
                SetSpeedrunAura(IsSpeedrunActive);
                OnSpeedrunStateChanged?.Invoke(IsSpeedrunActive, SpeedrunTimeRemaining);
                OnPowerUpStateChanged?.Invoke(PowerUpType.Speedrun, IsSpeedrunActive);
            }
            else if (IsSpeedrunActive)
            {
                OnSpeedrunStateChanged?.Invoke(true, SpeedrunTimeRemaining);
            }

            if (magnetWasActive != IsMagnetActive)
            {
                SetMagnetAura(IsMagnetActive);
                OnMagnetStateChanged?.Invoke(IsMagnetActive, MagnetTimeRemaining);
                OnPowerUpStateChanged?.Invoke(PowerUpType.Magnet, IsMagnetActive);
            }
            else if (IsMagnetActive)
            {
                OnMagnetStateChanged?.Invoke(true, MagnetTimeRemaining);
            }

            if (frenzyWasActive != IsMultiplierFrenzyActive)
            {
                SetFrenzyAura(IsMultiplierFrenzyActive);
                OnMultiplierFrenzyStateChanged?.Invoke(IsMultiplierFrenzyActive, MultiplierFrenzyTimeRemaining);
                OnPowerUpStateChanged?.Invoke(PowerUpType.MultiplierFrenzy, IsMultiplierFrenzyActive);
            }
            else if (IsMultiplierFrenzyActive)
            {
                OnMultiplierFrenzyStateChanged?.Invoke(true, MultiplierFrenzyTimeRemaining);
            }

            // Animate AAA Transparent Blue Forcefield Shield & Orbiting Energy Ring
            if (HasShield && shieldAuraObj != null)
            {
                shieldAnimTimer += dt;
                float entryScale = Mathf.Clamp01(shieldAnimTimer * 4.5f);
                float bounce = 1.0f + Mathf.Sin(Time.time * 3.0f) * 0.03f;
                shieldAuraObj.transform.localScale = Vector3.one * (entryScale * bounce);
                shieldAuraObj.transform.Rotate(0, 22f * dt, 0, Space.Self);
                if (shieldRingObj != null)
                {
                    shieldRingObj.transform.Rotate(0, 75f * dt, 0, Space.Self);
                }
            }

            // Animate Magnet Dual Orbiting Electric Flux Rings
            if (IsMagnetActive && magnetAuraObj != null)
            {
                if (magnetRing1 != null) magnetRing1.transform.Rotate(0, 150f * dt, 0, Space.Self);
                if (magnetRing2 != null) magnetRing2.transform.Rotate(0, -130f * dt, 0, Space.Self);
                float magPulse = 1.0f + Mathf.Sin(Time.time * 6.0f) * 0.05f;
                magnetAuraObj.transform.localScale = Vector3.one * magPulse;
            }

            // Animate Speedrun Aerodynamic Sonic Wind Ribbons
            if (IsSpeedrunActive && speedrunAuraObj != null)
            {
                float windStretch = 1.0f + Mathf.Sin(Time.time * 20.0f) * 0.20f;
                if (speedWindRibbon1 != null) speedWindRibbon1.transform.localScale = new Vector3(0.04f, 0.16f, 1.25f * windStretch);
                if (speedWindRibbon2 != null) speedWindRibbon2.transform.localScale = new Vector3(0.04f, 0.16f, 1.25f * windStretch);
            }

            // Animate Radiant Gold/Amethyst Frenzy Aura
            if (IsMultiplierFrenzyActive && frenzyAuraObj != null)
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * 7.0f) * 0.06f;
                frenzyAuraObj.transform.localScale = new Vector3(1.45f, 2.25f, 1.45f) * pulse;
                frenzyAuraObj.transform.Rotate(0, 90f * dt, 0, Space.Self);
            }
        }

        private void HandlePowerUpExpired(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    SetShieldAura(false);
                    OnShieldStateChanged?.Invoke(false);
                    break;
                case PowerUpType.Speedrun:
                    SetSpeedrunAura(false);
                    OnSpeedrunStateChanged?.Invoke(false, 0f);
                    break;
                case PowerUpType.Magnet:
                    SetMagnetAura(false);
                    OnMagnetStateChanged?.Invoke(false, 0f);
                    break;
                case PowerUpType.MultiplierFrenzy:
                    SetFrenzyAura(false);
                    OnMultiplierFrenzyStateChanged?.Invoke(false, 0f);
                    break;
            }
            OnPowerUpStateChanged?.Invoke(type, false);
        }

        #region Unified Power-Up API
        public void ActivatePowerUp(PowerUpType type, float customDuration = -1f)
        {
            if (Runner.Core.GameManager.Instance != null)
                Runner.Core.GameManager.Instance.TriggerHapticLight();

            switch (type)
            {
                case PowerUpType.Shield:
                    ActivateShield(customDuration);
                    break;
                case PowerUpType.Speedrun:
                    ActivateSpeedrun(customDuration);
                    break;
                case PowerUpType.Magnet:
                    ActivateMagnet(customDuration);
                    break;
                case PowerUpType.MultiplierFrenzy:
                    ActivateMultiplierFrenzy(customDuration);
                    break;
                case PowerUpType.HeartRevive:
                    TriggerHeartRevive();
                    break;
            }
        }

        public bool IsPowerUpActive(PowerUpType type)
        {
            if (powerUps.TryGetValue(type, out var inst))
            {
                return inst.IsActive;
            }
            return false;
        }

        public float GetPowerUpTimeRemaining(PowerUpType type)
        {
            if (powerUps.TryGetValue(type, out var inst))
            {
                return inst.TimeRemaining;
            }
            return 0f;
        }

        public float GetPowerUpTotalDuration(PowerUpType type)
        {
            if (powerUps.TryGetValue(type, out var inst))
            {
                return inst.TotalDuration;
            }
            return 1f;
        }

        public float GetPowerUpProgress(PowerUpType type)
        {
            if (powerUps.TryGetValue(type, out var inst))
            {
                return inst.Progress;
            }
            return 0f;
        }

        public List<PowerUpInstance> GetActivePowerUps()
        {
            activePowerUpList.Clear();
            foreach (var kvp in powerUps)
            {
                if (kvp.Value.IsActive)
                {
                    activePowerUpList.Add(kvp.Value);
                }
            }
            return activePowerUpList;
        }
        #endregion

        #region Activation Methods
        private int shieldHitsRemaining = 1;

        public void ActivateShield(float duration = -1f)
        {
            float total = duration > 0 ? duration : shieldBaseDuration;
            if (GameManager.Instance != null)
            {
                total += (GameManager.Instance.ShieldLevel - 1) * 3.0f;
            }

            // Iron Spider Suit (Suit 2) grants 2-hit Shield durability
            shieldHitsRemaining = (GameManager.Instance != null && GameManager.Instance.SelectedSuitIndex == 2) ? 2 : 1;

            if (powerUps.TryGetValue(PowerUpType.Shield, out var inst))
            {
                inst.Activate(total);
            }

            SetShieldAura(true);
            OnShieldStateChanged?.Invoke(true);
            OnPowerUpStateChanged?.Invoke(PowerUpType.Shield, true);
            Runner.Audio.AudioManager.Instance?.PlayPowerUp();
            MissionManager.Instance?.ReportPowerUpUsed();
        }

        /// <summary>
        /// Consumes active shield to absorb an obstacle collision.
        /// Returns true if absorbed, false if player had no shield.
        /// </summary>
        public bool TryAbsorbCollision(Vector3 hitPoint)
        {
            if (!HasShield) return false;

            shieldHitsRemaining--;
            Runner.Audio.AudioManager.Instance?.PlayShieldBreak();

            if (ImpactEffectManager.Instance != null)
            {
                ImpactEffectManager.Instance.PlayShieldBreak(hitPoint);
            }

            if (shieldHitsRemaining <= 0)
            {
                if (powerUps.TryGetValue(PowerUpType.Shield, out var inst))
                {
                    inst.Deactivate();
                }
                SetShieldAura(false);
                OnShieldStateChanged?.Invoke(false);
                OnPowerUpStateChanged?.Invoke(PowerUpType.Shield, false);
            }
            else
            {
                // Visual feedback that 1 layer of shield cracked but 1 remains
                if (Runner.UI.UIManager.Instance != null)
                {
                    Runner.UI.UIManager.Instance.ShowToast("🛡️", "Iron Spider Shield: 1 Hit Remaining!");
                }
            }

            return true;
        }

        public void ActivateSpeedrun(float duration = -1f)
        {
            float boostDuration = speedrunDuration;
            if (GameManager.Instance != null)
            {
                boostDuration += (GameManager.Instance.SpeedLevel - 1) * 2.0f;
            }

            float total = duration > 0 ? duration : boostDuration;
            if (powerUps.TryGetValue(PowerUpType.Speedrun, out var inst))
            {
                inst.Activate(total);
            }

            SetSpeedrunAura(true);
            OnSpeedrunStateChanged?.Invoke(true, total);
            OnPowerUpStateChanged?.Invoke(PowerUpType.Speedrun, true);
            Runner.Audio.AudioManager.Instance?.PlayPowerUp();
            MissionManager.Instance?.ReportPowerUpUsed();
        }

        public void ActivateMagnet(float duration = -1f)
        {
            float pullDuration = magnetDuration;
            float radius = 8.0f;
            if (GameManager.Instance != null)
            {
                pullDuration += (GameManager.Instance.MagnetLevel - 1) * 2.5f;
                radius += (GameManager.Instance.MagnetLevel - 1) * 3.0f;
                if (GameManager.Instance.SelectedSuitIndex == 1)
                {
                    radius *= 1.35f; // Symbiote Suit +35% Magnet Radius!
                }
            }
            magnetRadius = radius;

            float total = duration > 0 ? duration : pullDuration;
            if (powerUps.TryGetValue(PowerUpType.Magnet, out var inst))
            {
                inst.Activate(total);
            }

            SetMagnetAura(true);
            OnMagnetStateChanged?.Invoke(true, total);
            OnPowerUpStateChanged?.Invoke(PowerUpType.Magnet, true);
            Runner.Audio.AudioManager.Instance?.PlayPowerUp();
            MissionManager.Instance?.ReportPowerUpUsed();
        }

        public void ActivateMultiplierFrenzy(float duration = -1f)
        {
            float total = duration > 0 ? duration : frenzyDuration;
            if (GameManager.Instance != null)
            {
                total += (GameManager.Instance.FrenzyLevel - 1) * 2.0f;
            }

            if (powerUps.TryGetValue(PowerUpType.MultiplierFrenzy, out var inst))
            {
                inst.Activate(total);
            }

            SetFrenzyAura(true);
            OnMultiplierFrenzyStateChanged?.Invoke(true, total);
            OnPowerUpStateChanged?.Invoke(PowerUpType.MultiplierFrenzy, true);
            Runner.Audio.AudioManager.Instance?.PlayFrenzyFanfare();
            MissionManager.Instance?.ReportPowerUpUsed();

            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.ShowToast("🔥 MULTIPLIER FRENZY (3X ACTIVE)!", "⚡");
            }
        }

        public void TriggerHeartRevive()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestoreLife(1);
            }
            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.TriggerHeartBloom();
            }
            MissionManager.Instance?.ReportPowerUpUsed();
        }

        public void ResetPowerUps()
        {
            foreach (var kvp in powerUps)
            {
                kvp.Value.Deactivate();
            }

            SetShieldAura(false);
            SetSpeedrunAura(false);
            SetMagnetAura(false);
            SetFrenzyAura(false);

            OnShieldStateChanged?.Invoke(false);
            OnSpeedrunStateChanged?.Invoke(false, 0f);
            OnMagnetStateChanged?.Invoke(false, 0f);
            OnMultiplierFrenzyStateChanged?.Invoke(false, 0f);
        }
        #endregion

        #region Player Aura Visuals
        private GameObject shieldRingObj;
        private float shieldAnimTimer = 0f;

        private void EnsurePlayerAuraObjects()
        {
            if (PlayerController.Instance == null) return;
            Transform playerT = PlayerController.Instance.transform;

            // 1. Create Shield AAA Shimmering Forcefield Ward
            // Blue transparent shield where player is clearly visible from inside
            if (shieldAuraObj == null)
            {
                shieldAuraObj = new GameObject("Player_ShieldAuraRoot");
                shieldAuraObj.transform.SetParent(playerT, false);
                shieldAuraObj.transform.localPosition = new Vector3(0, 1.05f, 0);

                // Core energy sphere
                GameObject coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coreSphere.name = "Shield_EnergyShell";
                coreSphere.transform.SetParent(shieldAuraObj.transform, false);
                coreSphere.transform.localPosition = Vector3.zero;
                coreSphere.transform.localScale = new Vector3(1.4f, 2.1f, 1.4f);
                Destroy(coreSphere.GetComponent<Collider>());

                // Outer orbiting hollow energy ring
                shieldRingObj = CreateHollowRingObject("Shield_OrbitRing", shieldAuraObj.transform, 0.76f, 0.90f);
                shieldRingObj.transform.localPosition = Vector3.zero;
                shieldRingObj.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);

                // Assign AAA ShieldAura shader (crystal-blue transparent center, glowing cyan rim)
                Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                Material shieldMat;
                if (shieldShader != null)
                {
                    shieldMat = new Material(shieldShader);
                    shieldMat.SetColor("_AuraColor", new Color(0.04f, 0.40f, 1.0f, 0.05f)); // Player clearly visible inside
                    shieldMat.SetColor("_RimColor", new Color(0.20f, 0.85f, 1.0f, 0.95f));  // Electric cyan outer edge
                    shieldMat.SetFloat("_RimPower", 3.5f);
                    shieldMat.SetFloat("_RimIntensity", 2.6f);
                    shieldMat.SetFloat("_PulseSpeed", 2.2f);
                    shieldMat.SetFloat("_HexScale", 14.0f);
                    shieldMat.SetFloat("_HexIntensity", 0.18f);
                    shieldMat.SetFloat("_ScanSpeed", 1.8f);
                }
                else
                {
                    shieldMat = MaterialHelper.CreateSafeMaterial();
                    if (shieldMat != null)
                    {
                        shieldMat.name = "TransparentBlueShieldMat";
                        shieldMat.SetFloat("_Mode", 3);
                        shieldMat.SetOverrideTag("RenderType", "Transparent");
                        shieldMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        shieldMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        shieldMat.SetInt("_ZWrite", 0);
                        shieldMat.EnableKeyword("_ALPHABLEND_ON");
                        shieldMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
                        shieldMat.color = new Color(0.05f, 0.50f, 1.0f, 0.15f);
                    }
                }

                if (shieldMat != null)
                {
                    coreSphere.GetComponent<MeshRenderer>().sharedMaterial = shieldMat;
                    shieldRingObj.GetComponent<MeshRenderer>().sharedMaterial = shieldMat;
                }

                shieldAuraObj.SetActive(false);
            }

            // 2. Create Magnet Visual Aura (Electromagnetic Gyroscopic Flux Rings)
            if (magnetAuraObj == null)
            {
                magnetAuraObj = new GameObject("Player_MagnetAuraRoot");
                magnetAuraObj.transform.SetParent(playerT, false);
                magnetAuraObj.transform.localPosition = new Vector3(0, 0.95f, 0);

                magnetRing1 = CreateHollowRingObject("Magnet_RingPrimary", magnetAuraObj.transform, 0.70f, 0.82f);
                magnetRing1.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

                magnetRing2 = CreateHollowRingObject("Magnet_RingSecondary", magnetAuraObj.transform, 0.60f, 0.71f);
                magnetRing2.transform.localRotation = Quaternion.Euler(-45f, 0f, 30f);

                Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                Material magMat1 = shieldShader != null ? new Material(shieldShader) : MaterialHelper.CreateSafeMaterial();
                if (magMat1 != null)
                {
                    magMat1.SetColor("_AuraColor", new Color(0.1f, 0.8f, 1.0f, 0.08f));
                    magMat1.SetColor("_RimColor", new Color(0.2f, 0.95f, 1.0f, 0.95f)); // Neon cyan flux
                    magMat1.SetFloat("_RimPower", 2.2f);
                    magMat1.SetFloat("_RimIntensity", 2.8f);
                    magMat1.SetFloat("_PulseSpeed", 4.0f);
                }
                Material magMat2 = shieldShader != null ? new Material(shieldShader) : MaterialHelper.CreateSafeMaterial();
                if (magMat2 != null)
                {
                    magMat2.SetColor("_AuraColor", new Color(0.7f, 0.15f, 1.0f, 0.08f));
                    magMat2.SetColor("_RimColor", new Color(0.85f, 0.35f, 1.0f, 0.95f)); // Magnetic violet flux
                    magMat2.SetFloat("_RimPower", 2.2f);
                    magMat2.SetFloat("_RimIntensity", 2.8f);
                    magMat2.SetFloat("_PulseSpeed", 4.0f);
                }

                if (magMat1 != null) magnetRing1.GetComponent<MeshRenderer>().sharedMaterial = magMat1;
                if (magMat2 != null) magnetRing2.GetComponent<MeshRenderer>().sharedMaterial = magMat2;

                magnetAuraObj.SetActive(false);
            }

            // 3. Create Speedrun Visual Aura (Supersonic Aerodynamic Slipstream Ribbons)
            if (speedrunAuraObj == null)
            {
                speedrunAuraObj = new GameObject("Player_SpeedrunAuraRoot");
                speedrunAuraObj.transform.SetParent(playerT, false);
                speedrunAuraObj.transform.localPosition = new Vector3(0, 0.85f, 0);

                // Left wind streak
                speedWindRibbon1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                speedWindRibbon1.name = "Speed_WindRibbonLeft";
                speedWindRibbon1.transform.SetParent(speedrunAuraObj.transform, false);
                speedWindRibbon1.transform.localPosition = new Vector3(-0.45f, 0f, -0.4f);
                speedWindRibbon1.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                speedWindRibbon1.transform.localScale = new Vector3(0.04f, 0.15f, 1.2f);
                Destroy(speedWindRibbon1.GetComponent<Collider>());

                // Right wind streak
                speedWindRibbon2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                speedWindRibbon2.name = "Speed_WindRibbonRight";
                speedWindRibbon2.transform.SetParent(speedrunAuraObj.transform, false);
                speedWindRibbon2.transform.localPosition = new Vector3(0.45f, 0f, -0.4f);
                speedWindRibbon2.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
                speedWindRibbon2.transform.localScale = new Vector3(0.04f, 0.15f, 1.2f);
                Destroy(speedWindRibbon2.GetComponent<Collider>());

                // Ground shockwave hollow ring
                GameObject speedRing = CreateHollowRingObject("Speed_ShockwaveRing", speedrunAuraObj.transform, 0.58f, 0.72f);
                speedRing.transform.localPosition = new Vector3(0f, -0.75f, 0f);

                Shader shieldShader = Shader.Find("Custom/AAA_ShieldAura");
                Material speedMat = shieldShader != null ? new Material(shieldShader) : MaterialHelper.CreateSafeMaterial();
                if (speedMat != null)
                {
                    speedMat.SetColor("_AuraColor", new Color(1.0f, 0.85f, 0.15f, 0.12f));
                    speedMat.SetColor("_RimColor", new Color(1.0f, 0.95f, 0.40f, 0.95f)); // Golden sonic streaks
                    speedMat.SetFloat("_RimPower", 1.8f);
                    speedMat.SetFloat("_RimIntensity", 2.6f);
                    speedMat.SetFloat("_PulseSpeed", 5.0f);
                }

                if (speedMat != null)
                {
                    speedWindRibbon1.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                    speedWindRibbon2.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                    speedRing.GetComponent<MeshRenderer>().sharedMaterial = speedMat;
                }

                speedrunAuraObj.SetActive(false);
            }

            // 4. Create Frenzy Radiant Gold / Amber Pulsing Energy Aura
            if (frenzyAuraObj == null)
            {
                frenzyAuraObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                frenzyAuraObj.name = "Player_FrenzyGoldAura";
                frenzyAuraObj.transform.SetParent(playerT, false);
                frenzyAuraObj.transform.localPosition = new Vector3(0, 1.05f, 0);
                frenzyAuraObj.transform.localScale = new Vector3(1.45f, 2.25f, 1.45f);
                Destroy(frenzyAuraObj.GetComponent<Collider>());

                // Use AAA ShieldAura shader for frenzy — gold energy field look
                Shader frenzyShader = Shader.Find("Custom/AAA_ShieldAura");
                Material fMat;
                if (frenzyShader != null)
                {
                    fMat = new Material(frenzyShader);
                    fMat.SetColor("_AuraColor", new Color(1.0f, 0.70f, 0.10f, 0.15f));
                    fMat.SetColor("_RimColor", new Color(1.0f, 0.85f, 0.2f, 1.0f));
                    fMat.SetFloat("_RimPower", 2.0f);
                    fMat.SetFloat("_RimIntensity", 2.5f);
                    fMat.SetFloat("_PulseSpeed", 4.0f);
                    fMat.SetFloat("_ScanSpeed", 3.0f);
                }
                else
                {
                    fMat = MaterialHelper.CreateSafeMaterial();
                    if (fMat != null)
                    {
                        fMat.name = "FrenzyGoldMat";
                        fMat.SetFloat("_Mode", 3);
                        fMat.SetOverrideTag("RenderType", "Transparent");
                        fMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fMat.SetInt("_ZWrite", 0);
                        fMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        fMat.color = new Color(1.0f, 0.80f, 0.15f, 0.35f);
                        if (fMat.HasProperty("_EmissionColor"))
                        {
                            fMat.EnableKeyword("_EMISSION");
                            fMat.SetColor("_EmissionColor", new Color(1.0f, 0.70f, 0.10f) * 0.90f);
                        }
                    }
                }
                frenzyAuraObj.GetComponent<MeshRenderer>().sharedMaterial = fMat;
                frenzyAuraObj.SetActive(false);
            }
        }

        private void SetShieldAura(bool active)
        {
            EnsurePlayerAuraObjects();
            if (shieldAuraObj != null)
            {
                if (active) shieldAnimTimer = 0f;
                shieldAuraObj.SetActive(active);
            }
        }

        private void SetMagnetAura(bool active)
        {
            EnsurePlayerAuraObjects();
            if (magnetAuraObj != null)
            {
                magnetAuraObj.SetActive(active);
            }
        }

        private void SetSpeedrunAura(bool active)
        {
            EnsurePlayerAuraObjects();
            if (speedrunAuraObj != null)
            {
                speedrunAuraObj.SetActive(active);
            }
        }

        private void SetFrenzyAura(bool active)
        {
            EnsurePlayerAuraObjects();
            if (frenzyAuraObj != null)
            {
                frenzyAuraObj.SetActive(active);
            }
        }

        private static GameObject CreateHollowRingObject(string name, Transform parent, float innerRadius, float outerRadius, int segments = 32)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            Mesh mesh = new Mesh();
            mesh.name = $"{name}_Mesh";
            Vector3[] vertices = new Vector3[segments * 2];
            Vector2[] uvs = new Vector2[segments * 2];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);

                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2 + 1;

                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = next * 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>();

            return obj;
        }
        #endregion
    }
}
