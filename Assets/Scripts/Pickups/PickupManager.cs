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
        HeartRevive
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
    /// Supports multiple simultaneously active power-ups (e.g. Shield + Magnet + Speedrun running concurrently).
    /// </summary>
    public class PickupManager : MonoBehaviour
    {
        public static PickupManager Instance { get; private set; }

        [Header("Default Durations")]
        [SerializeField] private float speedrunDuration = 6.0f;
        [SerializeField] private float magnetDuration = 8.0f;
        [SerializeField] private float magnetRadius = 8.0f;
        [SerializeField] private float shieldBaseDuration = 15.0f;

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

        // Player Shield Aura
        private GameObject shieldAuraObj;

        // Events
        public event Action<bool> OnShieldStateChanged;
        public event Action<bool, float> OnSpeedrunStateChanged;
        public event Action<bool, float> OnMagnetStateChanged;
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
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool shieldWasActive = HasShield;
            bool speedrunWasActive = IsSpeedrunActive;
            bool magnetWasActive = IsMagnetActive;

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
                OnMagnetStateChanged?.Invoke(IsMagnetActive, MagnetTimeRemaining);
                OnPowerUpStateChanged?.Invoke(PowerUpType.Magnet, IsMagnetActive);
            }
            else if (IsMagnetActive)
            {
                OnMagnetStateChanged?.Invoke(true, MagnetTimeRemaining);
            }

            // Animate Transparent Blue Shield Oval
            if (HasShield && shieldAuraObj != null)
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * 3.5f) * 0.025f;
                shieldAuraObj.transform.localScale = new Vector3(1.35f, 2.15f, 1.35f) * pulse;
                shieldAuraObj.transform.Rotate(0, 30f * dt, 0, Space.Self);
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
                    OnMagnetStateChanged?.Invoke(false, 0f);
                    break;
            }
            OnPowerUpStateChanged?.Invoke(type, false);
        }

        #region Unified Power-Up API
        public void ActivatePowerUp(PowerUpType type, float customDuration = -1f)
        {
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

            OnMagnetStateChanged?.Invoke(true, total);
            OnPowerUpStateChanged?.Invoke(PowerUpType.Magnet, true);
            Runner.Audio.AudioManager.Instance?.PlayPowerUp();
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
        }

        public void ResetPowerUps()
        {
            foreach (var kvp in powerUps)
            {
                kvp.Value.Deactivate();
            }

            SetShieldAura(false);
            SetSpeedrunAura(false);

            OnShieldStateChanged?.Invoke(false);
            OnSpeedrunStateChanged?.Invoke(false, 0f);
            OnMagnetStateChanged?.Invoke(false, 0f);
        }
        #endregion

        #region Player Aura Visuals
        private void EnsurePlayerAuraObjects()
        {
            if (PlayerController.Instance == null) return;
            Transform playerT = PlayerController.Instance.transform;

            // 1. Create Shield Transparent Blue Oval Aura around character
            if (shieldAuraObj == null)
            {
                shieldAuraObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shieldAuraObj.name = "Player_ShieldBlueOval";
                shieldAuraObj.transform.SetParent(playerT, false);
                shieldAuraObj.transform.localPosition = new Vector3(0, 1.05f, 0);
                shieldAuraObj.transform.localScale = new Vector3(1.35f, 2.15f, 1.35f);
                Destroy(shieldAuraObj.GetComponent<Collider>());

                Material mat = MaterialHelper.CreateSafeMaterial();
                if (mat == null) return;
                mat.name = "TransparentBlueShieldMat";

                // Configure Standard Shader to Mode = 3 (Transparent)
                mat.SetFloat("_Mode", 3);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                // Translucent electric azure blue
                mat.color = new Color(0.12f, 0.60f, 1.0f, 0.28f);

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.94f);

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.10f, 0.65f, 1.0f) * 0.40f);
                }

                shieldAuraObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
                shieldAuraObj.SetActive(false);
            }
        }

        private void SetShieldAura(bool active)
        {
            EnsurePlayerAuraObjects();
            if (shieldAuraObj != null)
            {
                shieldAuraObj.SetActive(active);
            }
        }

        private void SetSpeedrunAura(bool active)
        {
            // Pure running effect: 1.6x speed boost, dynamic camera FOV, and fast running animation
            // No intrusive down yellow bar on the ground
        }
        #endregion
    }
}
