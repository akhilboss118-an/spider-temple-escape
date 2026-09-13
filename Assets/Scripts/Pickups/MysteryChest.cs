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

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(obj); return; }
#endif
            Destroy(obj);
        }

        private void BuildChestVisual()
        {
            // Clear previous children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                SafeDestroy(transform.GetChild(i).gameObject);
            }

            // High Quality Textures
            Texture2D woodTex = Resources.Load<Texture2D>("Textures/Tex_Obstacle");
#if UNITY_EDITOR
            if (woodTex == null)
                woodTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_Obstacle.png")
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Tex_TempleCurb.jpg");
#endif

            // Ancient Carved Mahogany / Temple Stone Body Material
            Material bodyMat = MaterialHelper.CreateSafeMaterial(new Color(0.42f, 0.26f, 0.18f), woodTex);
            if (bodyMat != null)
            {
                if (bodyMat.HasProperty("_Glossiness")) bodyMat.SetFloat("_Glossiness", 0.65f);
                if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.20f);
            }

            // Ornate Antique Gold Bands Material
            Material goldMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.82f, 0.20f));
            if (goldMat != null)
            {
                if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.96f);
                if (goldMat.HasProperty("_Glossiness")) goldMat.SetFloat("_Glossiness", 0.92f);
                if (goldMat.HasProperty("_EmissionColor"))
                {
                    goldMat.EnableKeyword("_EMISSION");
                    goldMat.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.15f) * 0.45f);
                }
            }

            // Glowing Mystic Relic Gemstone Material
            Material gemMat = MaterialHelper.CreateSafeMaterial(new Color(0.12f, 0.95f, 0.88f));
            if (gemMat != null)
            {
                if (gemMat.HasProperty("_EmissionColor"))
                {
                    gemMat.EnableKeyword("_EMISSION");
                    gemMat.SetColor("_EmissionColor", new Color(0.12f, 0.95f, 0.88f) * 2.2f);
                }
            }

            // 1. Solid Chest Base Plinth / Lower Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.30f, 0);
            body.transform.localScale = new Vector3(0.96f, 0.55f, 0.68f);
            SafeDestroy(body.GetComponent<Collider>());
            if (bodyMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // 2. Iconic Barrel-Vault Arched Chest Lid (smooth horizontal cylinder)
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lid.name = "ChestLidArched";
            lid.transform.SetParent(transform, false);
            lid.transform.localPosition = new Vector3(0, 0.58f, 0);
            lid.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            lid.transform.localScale = new Vector3(0.68f, 0.48f, 0.68f);
            SafeDestroy(lid.GetComponent<Collider>());
            if (bodyMat != null) lid.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // 3. Left Gold Band (encircling both body & arched lid)
            GameObject leftBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftBand.name = "LeftGoldBand";
            leftBand.transform.SetParent(transform, false);
            leftBand.transform.localPosition = new Vector3(-0.30f, 0.46f, 0);
            leftBand.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            leftBand.transform.localScale = new Vector3(0.70f, 0.06f, 0.70f);
            SafeDestroy(leftBand.GetComponent<Collider>());
            if (goldMat != null) leftBand.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // 4. Right Gold Band (encircling both body & arched lid)
            GameObject rightBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightBand.name = "RightGoldBand";
            rightBand.transform.SetParent(transform, false);
            rightBand.transform.localPosition = new Vector3(0.30f, 0.46f, 0);
            rightBand.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            rightBand.transform.localScale = new Vector3(0.70f, 0.06f, 0.70f);
            SafeDestroy(rightBand.GetComponent<Collider>());
            if (goldMat != null) rightBand.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // 5. Center Rim Band
            GameObject centerBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerBand.name = "CenterRimBand";
            centerBand.transform.SetParent(transform, false);
            centerBand.transform.localPosition = new Vector3(0, 0.57f, 0);
            centerBand.transform.localScale = new Vector3(0.98f, 0.05f, 0.70f);
            SafeDestroy(centerBand.GetComponent<Collider>());
            if (goldMat != null) centerBand.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // 6. Golden Front Escutcheon Plate (Lock)
            GameObject lockPlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lockPlate.name = "LockPlate";
            lockPlate.transform.SetParent(transform, false);
            lockPlate.transform.localPosition = new Vector3(0, 0.48f, -0.35f);
            lockPlate.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            lockPlate.transform.localScale = new Vector3(0.18f, 0.04f, 0.22f);
            SafeDestroy(lockPlate.GetComponent<Collider>());
            if (goldMat != null) lockPlate.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            // 7. Glowing Mystic Power Gem on Lock
            GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "LockGem";
            gem.transform.SetParent(transform, false);
            gem.transform.localPosition = new Vector3(0, 0.48f, -0.38f);
            gem.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
            SafeDestroy(gem.GetComponent<Collider>());
            if (gemMat != null) gem.GetComponent<MeshRenderer>().sharedMaterial = gemMat;

            // 8. Subtle magical beacon glow light
            GameObject glowLightObj = new GameObject("ChestBeaconLight");
            glowLightObj.transform.SetParent(transform, false);
            glowLightObj.transform.localPosition = new Vector3(0, 0.55f, -0.25f);
            Light chestLight = glowLightObj.AddComponent<Light>();
            chestLight.type = LightType.Point;
            chestLight.color = new Color(0.2f, 0.95f, 0.85f);
            chestLight.range = 3.5f;
            chestLight.intensity = 1.2f;
            chestLight.shadows = LightShadows.None;
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
