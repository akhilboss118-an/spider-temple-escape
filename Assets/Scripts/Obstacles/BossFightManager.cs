using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Obstacles
{
    /// <summary>
    /// Manages boss fight encounters every 5000m.
    /// Spawns a boss entity with obstacle gauntlet and alerts the player.
    /// </summary>
    public class BossFightManager : MonoBehaviour
    {
        public static BossFightManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<BossFightManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BossFightManager");
                        _instance = go.AddComponent<BossFightManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Boss Spawn Settings")]
        [Tooltip("Distance interval between boss fights (meters)")]
        [SerializeField] private float bossInterval = 5000.0f;

        [Tooltip("Warning distance before boss appears")]
        [SerializeField] private float warningDistance = 50f;

        [Header("Boss Prefabs")]
        [SerializeField] private GameObject bossPrefab;

        private int nextBossLevel = 1;
        private float nextBossDistance = 5000.0f;
        private bool bossWarningShown = false;
        private float bossSpawnTimer = 0f;
        private bool bossSpawnPending = false;
        private BossEntity activeBoss;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ResetBossTracking();
        }

        public void ResetBossTracking()
        {
            nextBossLevel = 1;
            nextBossDistance = bossInterval;
            bossWarningShown = false;
            bossSpawnTimer = 0f;
            bossSpawnPending = false;
            if (activeBoss != null)
            {
                activeBoss.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;
            if (PlayerController.Instance == null) return;

            float distance = GameManager.Instance.DistanceTraveled;

            // Check if we're approaching a boss fight
            if (distance >= nextBossDistance - warningDistance && !bossWarningShown)
            {
                bossWarningShown = true;
                ShowBossWarning();
            }

            // Spawn boss at the right distance
            if (distance >= nextBossDistance && !bossSpawnPending)
            {
                bossSpawnPending = true;
                bossSpawnTimer = 2.0f; // 2 second delay for dramatic effect
            }

            // Countdown to boss spawn
            if (bossSpawnPending)
            {
                bossSpawnTimer -= Time.deltaTime;
                if (bossSpawnTimer <= 0f)
                {
                    SpawnBoss();
                    bossSpawnPending = false;
                    nextBossDistance += bossInterval;
                    nextBossLevel++;
                    bossWarningShown = false;
                }
            }
        }

        private void ShowBossWarning()
        {
            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.ShowToast("⚠️", $"BOSS APPROACHING — Level {nextBossLevel}!", 3.0f);
            }
        }

        private void SpawnBoss()
        {
            if (PlayerController.Instance == null) return;

            Vector3 spawnPos = PlayerController.Instance.transform.position +
                              PlayerController.Instance.ForwardDirection * 40f +
                              Vector3.up * 1.5f;

            if (bossPrefab != null)
            {
                GameObject bossObj = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
                activeBoss = bossObj.GetComponent<BossEntity>();
                if (activeBoss != null)
                {
                    activeBoss.ConfigureBoss(nextBossLevel);
                }
            }
            else
            {
                // Fallback: create procedural boss entity
                GameObject bossObj = CreateProceduralBoss(spawnPos);
                activeBoss = bossObj.GetComponent<BossEntity>();
                if (activeBoss != null)
                {
                    activeBoss.ConfigureBoss(nextBossLevel);
                }
            }

            if (Runner.UI.UIManager.Instance != null)
            {
                Runner.UI.UIManager.Instance.ShowToast("👹", $"BOSS FIGHT — Level {nextBossLevel}!", 2.5f);
            }

            Runner.Audio.AudioManager.Instance?.PlayMonsterRoar(1f);
        }

        private GameObject CreateProceduralBoss(Vector3 position)
        {
            GameObject boss = new GameObject("BossEntity_Lv" + nextBossLevel);
            boss.transform.position = position;

            // Main body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(boss.transform, false);
            body.transform.localScale = new Vector3(3f, 4f, 2f);
            body.transform.localPosition = Vector3.up * 3f;
            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material = new Material(Shader.Find("Standard"));
                bodyRenderer.material.color = new Color(0.6f, 0.15f, 0.1f);
                if (bodyRenderer.material.HasProperty("_EmissionColor"))
                {
                    bodyRenderer.material.EnableKeyword("_EMISSION");
                    bodyRenderer.material.SetColor("_EmissionColor", Color.red * 1.5f);
                }
            }

            // Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(boss.transform, false);
            head.transform.localScale = new Vector3(2f, 1.8f, 1.8f);
            head.transform.localPosition = Vector3.up * 7f;
            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.material = new Material(Shader.Find("Standard"));
                headRenderer.material.color = new Color(0.7f, 0.1f, 0.05f);
                if (headRenderer.material.HasProperty("_EmissionColor"))
                {
                    headRenderer.material.EnableKeyword("_EMISSION");
                    headRenderer.material.SetColor("_EmissionColor", Color.red * 2f);
                }
            }

            // Eyes (glowing)
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(head.transform, false);
                eye.transform.localScale = new Vector3(0.35f, 0.25f, 0.25f);
                eye.transform.localPosition = new Vector3(i * 0.5f, 0.2f, 0.7f);
                var eyeRenderer = eye.GetComponent<Renderer>();
                if (eyeRenderer != null)
                {
                    eyeRenderer.material = new Material(Shader.Find("Unlit/Color"));
                    eyeRenderer.material.color = Color.yellow;
                }
            }

            // Collider
            BoxCollider col = boss.AddComponent<BoxCollider>();
            col.size = new Vector3(3f, 8f, 2f);
            col.center = Vector3.up * 4f;

            boss.AddComponent<BossEntity>();

            // Light glow
            GameObject lightObj = new GameObject("BossGlow");
            lightObj.transform.SetParent(boss.transform, false);
            lightObj.transform.localPosition = Vector3.up * 5f;
            Light bossLight = lightObj.AddComponent<Light>();
            bossLight.type = LightType.Point;
            bossLight.color = Color.red;
            bossLight.intensity = 3f;
            bossLight.range = 15f;

            return boss;
        }
    }
}
