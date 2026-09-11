using UnityEngine;
using Runner.Core;
using Runner.Effects;
using Runner.Player;
using Runner.UI;

namespace Runner.Pickups
{
    /// <summary>
    /// 3D Ancient Mystery Artifact Chest collectible.
    /// Spawns procedurally in runner lanes.
    /// On collection:
    /// - Bursts into golden coin sparkles
    /// - Plays discovery fanfare
    /// - Grants a high-value randomized expedition reward (Coins, Shield, Heart Revive, Speedrun, or Multiplier Frenzy)
    /// - Advances daily and lifetime quest progress
    /// </summary>
    public class MysteryChest : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 65.0f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 2.2f;

        private float baseLocalY;
        private float bobOffset;
        private bool isCollected = false;

        private void Awake()
        {
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
            baseLocalY = transform.localPosition.y;
            BuildChestVisual();
            EnsureTrigger();
        }

        private void OnEnable()
        {
            isCollected = false;
            baseLocalY = transform.localPosition.y;
        }

        private void EnsureTrigger()
        {
            BoxCollider bc = GetComponent<BoxCollider>();
            if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.6f, 1.6f, 1.6f);
            bc.center = new Vector3(0, 0.5f, 0);
        }

        private void BuildChestVisual()
        {
            // Clear previous children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // Ancient Obsidian / Mahogany Wood Chest Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.35f, 0);
            body.transform.localScale = new Vector3(0.95f, 0.65f, 0.70f);
            Destroy(body.GetComponent<Collider>());

            Material bodyMat = MaterialHelper.CreateSafeMaterial(new Color(0.25f, 0.14f, 0.08f));
            if (bodyMat != null)
            {
                if (bodyMat.HasProperty("_Glossiness")) bodyMat.SetFloat("_Glossiness", 0.45f);
                body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            }

            // Arched Chest Lid
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "ChestLid";
            lid.transform.SetParent(transform, false);
            lid.transform.localPosition = new Vector3(0, 0.72f, 0);
            lid.transform.localScale = new Vector3(0.98f, 0.22f, 0.74f);
            Destroy(lid.GetComponent<Collider>());
            if (bodyMat != null) lid.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Radiant Gold Straps & Lock
            Material goldMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.82f, 0.20f));
            if (goldMat != null)
            {
                if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.95f);
                if (goldMat.HasProperty("_Glossiness")) goldMat.SetFloat("_Glossiness", 0.90f);
                if (goldMat.HasProperty("_EmissionColor"))
                {
                    goldMat.EnableKeyword("_EMISSION");
                    goldMat.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.15f) * 0.45f);
                }
            }

            // Left Gold Band
            GameObject leftBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBand.name = "LeftGoldBand";
            leftBand.transform.SetParent(transform, false);
            leftBand.transform.localPosition = new Vector3(-0.32f, 0.45f, 0);
            leftBand.transform.localScale = new Vector3(0.12f, 0.80f, 0.76f);
            Destroy(leftBand.GetComponent<Collider>());
            if (goldMat != null) leftBand.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // Right Gold Band
            GameObject rightBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBand.name = "RightGoldBand";
            rightBand.transform.SetParent(transform, false);
            rightBand.transform.localPosition = new Vector3(0.32f, 0.45f, 0);
            rightBand.transform.localScale = new Vector3(0.12f, 0.80f, 0.76f);
            Destroy(rightBand.GetComponent<Collider>());
            if (goldMat != null) rightBand.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // Golden Front Lock Plate
            GameObject lockPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lockPlate.name = "LockPlate";
            lockPlate.transform.SetParent(transform, false);
            lockPlate.transform.localPosition = new Vector3(0, 0.58f, -0.37f);
            lockPlate.transform.localScale = new Vector3(0.18f, 0.22f, 0.08f);
            Destroy(lockPlate.GetComponent<Collider>());
            if (goldMat != null) lockPlate.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // Glowing Mystic Gem on Lock
            GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "LockGem";
            gem.transform.SetParent(transform, false);
            gem.transform.localPosition = new Vector3(0, 0.58f, -0.42f);
            gem.transform.localScale = new Vector3(0.10f, 0.10f, 0.10f);
            Destroy(gem.GetComponent<Collider>());

            Material gemMat = MaterialHelper.CreateSafeMaterial(new Color(0.10f, 0.95f, 1.0f));
            if (gemMat != null)
            {
                if (gemMat.HasProperty("_EmissionColor"))
                {
                    gemMat.EnableKeyword("_EMISSION");
                    gemMat.SetColor("_EmissionColor", new Color(0.10f, 0.95f, 1.0f) * 1.5f);
                }
                gem.GetComponent<MeshRenderer>().sharedMaterial = gemMat;
            }
        }

        private void Update()
        {
            if (isCollected) return;

            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
            float newY = baseLocalY + Mathf.Sin(Time.time * bobFrequency + bobOffset) * bobAmplitude;
            Vector3 p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, newY, p.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;
            PlayerController player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null && player.State != PlayerState.Dead)
            {
                CollectChest();
            }
        }

        private void CollectChest()
        {
            if (isCollected) return;
            isCollected = true;

            // 1. Effects and Sound
            if (ImpactEffectManager.Instance != null)
            {
                ImpactEffectManager.Instance.PlayChestBurst(transform.position + Vector3.up * 0.5f);
            }

            if (Runner.Audio.AudioManager.Instance != null)
            {
                Runner.Audio.AudioManager.Instance.PlayMysteryChestOpen();
            }

            // 2. Report to Mission Manager
            MissionManager.Instance?.ReportChestOpened();

            // 3. Roll Reward
            RollAndGrantReward();

            // Hide
            gameObject.SetActive(false);
        }

        private void RollAndGrantReward()
        {
            int roll = Random.Range(0, 100);

            if (roll < 40)
            {
                // 40% Chance: Relic Bonanza (+35 to +75 banked coins)
                int coins = Random.Range(35, 76);
                GameManager.Instance?.AddCoins(coins);
                UIManager.Instance?.ShowToast($"🎁 MYSTERY CHEST: +{coins} RELICS!", "💰");
            }
            else if (roll < 60)
            {
                // 20% Chance: Instant Mystic Shield
                PickupManager.Instance?.ActivatePowerUp(PowerUpType.Shield);
                UIManager.Instance?.ShowToast("🎁 MYSTERY CHEST: MYSTIC WARD SHIELD!", "🛡️");
            }
            else if (roll < 75)
            {
                // 15% Chance: Instant Monster Speedrun Boost
                PickupManager.Instance?.ActivatePowerUp(PowerUpType.Speedrun);
                UIManager.Instance?.ShowToast("🎁 MYSTERY CHEST: MONSTER BOOST!", "⚡");
            }
            else if (roll < 90)
            {
                // 15% Chance: Multiplier Frenzy (3x Score Rush)
                PickupManager.Instance?.ActivatePowerUp(PowerUpType.MultiplierFrenzy);
                UIManager.Instance?.ShowToast("🎁 MYSTERY CHEST: MULTIPLIER FRENZY (3X)!", "🔥");
            }
            else
            {
                // 10% Chance: Extra Life / Heart Revive
                GameManager.Instance?.RestoreLife(1);
                UIManager.Instance?.ShowToast("🎁 MYSTERY CHEST: +1 EXTRA LIFE!", "💖");
            }
        }
    }
}
