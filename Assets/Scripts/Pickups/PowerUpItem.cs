using UnityEngine;
using Runner.Core;
using Runner.Player;
using Runner.Effects;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runner.Pickups
{
    /// <summary>
    /// Floating 3D collectible power-up in track lanes.
    /// Loads real 3D models:
    ///   - Shield   : shield.fbx  (FBX + PBR textures)
    ///   - Speedrun : speedrun.obj (Monster Energy drink can)
    ///   - Magnet   : magnet.obj   (Horseshoe magnet with red body & silver tips)
    /// Rotates smoothly 360 degrees, bobs up/down, and activates power-up on trigger.
    /// </summary>
    public class PowerUpItem : MonoBehaviour
    {
        [SerializeField] private PowerUpType powerUpType = PowerUpType.Shield;
        [SerializeField] private float rotationSpeed = 90.0f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobFrequency = 2.5f;

        private float baseLocalY;
        private float bobOffset;
        private bool isCollected = false;

        public PowerUpType Type => powerUpType;

        public void Initialize(PowerUpType type)
        {
            powerUpType = type;
            BuildVisual();
        }

        private void Awake()
        {
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
            baseLocalY = transform.localPosition.y;
        }

        private void Start()
        {
            if (transform.childCount == 0)
                BuildVisual();
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            #if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(obj); return; }
            #endif
            Destroy(obj);
        }

        private void BuildVisual()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                SafeDestroy(transform.GetChild(i).gameObject);

            bool loaded = TryLoadRealModel();
            if (!loaded)
                BuildFallbackPlaceholder();

            ApplyGlowEffect();
            EnsureTrigger();
        }

        private bool TryLoadRealModel()
        {
            string resPath = powerUpType switch
            {
                PowerUpType.Shield      => "PowerUps/shield",
                PowerUpType.Speedrun    => "PowerUps/monster",
                PowerUpType.Magnet      => "PowerUps/magnet",
                PowerUpType.HeartRevive => "PowerUps/heart",
                _ => null
            };

            string assetPath = powerUpType switch
            {
                PowerUpType.Shield      => "Assets/Models/PowerUps/Shield/shield.fbx",
                PowerUpType.Speedrun    => "Assets/Models/PowerUps/Speedrun/monster.glb",
                PowerUpType.Magnet      => "Assets/Models/PowerUps/Magnet/magnet.obj",
                PowerUpType.HeartRevive => "Assets/Models/PowerUps/Heart/heart.obj",
                _ => null
            };

            GameObject prefab = null;
            if (!string.IsNullOrEmpty(resPath))
            {
                prefab = Resources.Load<GameObject>(resPath);
            }

            // Fallback for speedrun model naming
            if (prefab == null && powerUpType == PowerUpType.Speedrun)
            {
                prefab = Resources.Load<GameObject>("PowerUps/speedrun");
            }

#if UNITY_EDITOR
            if (prefab == null && !string.IsNullOrEmpty(assetPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            if (prefab == null && powerUpType == PowerUpType.Speedrun)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/PowerUps/Speedrun/speedrun.obj");
            }
#endif

            if (prefab == null) return false;

            GameObject visual = Instantiate(prefab, transform);
            visual.name = "PowerUpModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Remove colliders from imported model (we use the parent root trigger)
            foreach (var col in visual.GetComponentsInChildren<Collider>())
                SafeDestroy(col);

            // Normalize size and center the model exactly at local origin
            MeshFilter[] mfs = visual.GetComponentsInChildren<MeshFilter>();
            if (mfs.Length > 0 && mfs[0].sharedMesh != null)
            {
                Bounds localBounds = mfs[0].sharedMesh.bounds;
                for (int i = 1; i < mfs.Length; i++)
                {
                    if (mfs[i].sharedMesh != null)
                        localBounds.Encapsulate(mfs[i].sharedMesh.bounds);
                }

                float maxDim = Mathf.Max(localBounds.size.x, localBounds.size.y, localBounds.size.z);
                if (maxDim > 0.001f)
                {
                    float targetSize = powerUpType == PowerUpType.Speedrun ? 1.25f : 0.85f;
                    float scale = targetSize / maxDim;
                    visual.transform.localScale = Vector3.one * scale;
                    visual.transform.localPosition = -localBounds.center * scale;
                }
            }
            else
            {
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (maxDim > 0.001f)
                    {
                        float targetSize = powerUpType == PowerUpType.Speedrun ? 1.25f : 0.85f;
                        float scale = targetSize / maxDim;
                        visual.transform.localScale = Vector3.one * scale;
                    }
                }
            }

            // Apply high quality shaders & materials
            if (powerUpType == PowerUpType.Shield)
            {
                ApplyShieldTextures(visual);
            }
            else if (powerUpType == PowerUpType.Magnet)
            {
                ApplyMagnetMaterials(visual);
            }
            else if (powerUpType == PowerUpType.Speedrun)
            {
                ApplySpeedrunMaterials(visual);
            }
            else if (powerUpType == PowerUpType.HeartRevive)
            {
                ApplyHeartMaterials(visual);
            }

            return true;
        }

        private void ApplyShieldTextures(GameObject model)
        {
            Texture2D albedo = Resources.Load<Texture2D>("PowerUps/Shield_Base_Color");
            Texture2D normal = Resources.Load<Texture2D>("PowerUps/Shield_Normal_OpenGL");
            Texture2D metallic = Resources.Load<Texture2D>("PowerUps/Shield_Metallic");
            Texture2D roughness = Resources.Load<Texture2D>("PowerUps/Shield_Roughness");
            Texture2D ao = Resources.Load<Texture2D>("PowerUps/shield_AO");

#if UNITY_EDITOR
            const string texDir = "Assets/Models/PowerUps/Shield/";
            if (albedo == null) albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Shield_Base_Color.jpg");
            if (normal == null) normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Shield_Normal_OpenGL.jpg");
            if (metallic == null) metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Shield_Metallic.jpg");
            if (roughness == null) roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Shield_Roughness.jpg");
            if (ao == null) ao = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "shield_AO.jpg");
#endif

            Material mat = MaterialHelper.CreateSafeMaterial();
            if (mat == null) return;
            mat.name = "ShieldPBR";
            if (albedo != null) mat.SetTexture("_MainTex", albedo);
            else mat.color = new Color(0.2f, 0.7f, 1.0f);

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1.0f);
            }
            if (metallic != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Glossiness", 0.75f);
            }
            else
            {
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.8f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.75f);
            }
            if (ao != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", ao);
            }

            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                mr.sharedMaterials = mats;
            }
        }

        private void ApplyMagnetMaterials(GameObject model)
        {
            // Vibrant cherry red for magnet horseshoe body
            Material redMat = MaterialHelper.CreateSafeMaterial();
            if (redMat == null) return;
            redMat.name = "MagnetRed";
            redMat.color = new Color(0.95f, 0.08f, 0.12f);
            if (redMat.HasProperty("_Metallic")) redMat.SetFloat("_Metallic", 0.40f);
            if (redMat.HasProperty("_Glossiness")) redMat.SetFloat("_Glossiness", 0.90f);
            if (redMat.HasProperty("_EmissionColor"))
            {
                redMat.EnableKeyword("_EMISSION");
                redMat.SetColor("_EmissionColor", new Color(0.95f, 0.08f, 0.12f) * 0.35f);
            }

            // Gleaming chrome silver for magnet tips
            Material silverMat = MaterialHelper.CreateSafeMaterial();
            if (silverMat == null) return;
            silverMat.name = "MagnetSilver";
            silverMat.color = new Color(0.92f, 0.94f, 0.98f);
            if (silverMat.HasProperty("_Metallic")) silverMat.SetFloat("_Metallic", 0.98f);
            if (silverMat.HasProperty("_Glossiness")) silverMat.SetFloat("_Glossiness", 0.95f);

            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                string objName = mr.gameObject.name.ToLower();
                if (objName.Contains("white") || objName.Contains("tip") || objName.Contains("silver"))
                {
                    mr.sharedMaterial = silverMat;
                }
                else
                {
                    mr.sharedMaterial = redMat;
                }
            }
        }

        private void ApplySpeedrunMaterials(GameObject model)
        {
            // Monster Energy Drink can: Load authentic can texture extracted from monster GLB
            Texture2D monsterTex = Resources.Load<Texture2D>("PowerUps/monster_can_albedo");
#if UNITY_EDITOR
            if (monsterTex == null)
                monsterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/PowerUps/Speedrun/monster_can_albedo.png");
#endif

            Material monsterBodyMat = MaterialHelper.CreateSafeMaterial();
            if (monsterBodyMat != null)
            {
                monsterBodyMat.name = "MonsterEnergyBody";
                if (monsterTex != null)
                {
                    monsterBodyMat.SetTexture("_MainTex", monsterTex);
                    monsterBodyMat.color = Color.white;
                }
                else
                {
                    monsterBodyMat.color = new Color(0.12f, 0.95f, 0.25f);
                }

                if (monsterBodyMat.HasProperty("_Metallic")) monsterBodyMat.SetFloat("_Metallic", 0.65f);
                if (monsterBodyMat.HasProperty("_Glossiness")) monsterBodyMat.SetFloat("_Glossiness", 0.88f);
                if (monsterBodyMat.HasProperty("_EmissionColor"))
                {
                    monsterBodyMat.EnableKeyword("_EMISSION");
                    monsterBodyMat.SetColor("_EmissionColor", new Color(0.12f, 0.85f, 0.25f) * 0.45f);
                }
            }

            Material rimMat = MaterialHelper.CreateSafeMaterial();
            if (rimMat != null)
            {
                rimMat.name = "MonsterCanRim";
                rimMat.color = new Color(0.95f, 0.98f, 1.0f);
                if (rimMat.HasProperty("_Metallic")) rimMat.SetFloat("_Metallic", 0.98f);
                if (rimMat.HasProperty("_Glossiness")) rimMat.SetFloat("_Glossiness", 0.95f);
            }

            int index = 0;
            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = (index % 4 == 0) ? rimMat : monsterBodyMat;
                index++;
            }

            // Add an electric green beacon ring hovering around the can for maximum visibility
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            halo.name = "SpeedrunHalo";
            halo.transform.SetParent(model.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = new Vector3(1.25f, 0.05f, 1.25f);
            SafeDestroy(halo.GetComponent<Collider>());
            Material haloMat = MaterialHelper.CreateSafeMaterial(new Color(0.2f, 1.0f, 0.4f, 0.7f));
            if (haloMat != null)
            {
                if (haloMat.HasProperty("_EmissionColor"))
                {
                    haloMat.EnableKeyword("_EMISSION");
                    haloMat.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 0.85f);
                }
                halo.GetComponent<MeshRenderer>().sharedMaterial = haloMat;
            }
        }

        private void ApplyHeartMaterials(GameObject model)
        {
            Material pinkMat = MaterialHelper.CreateSafeMaterial();
            if (pinkMat == null) return;
            pinkMat.name = "HeartPinkGlow";
            pinkMat.color = new Color(1.0f, 0.20f, 0.50f);
            if (pinkMat.HasProperty("_EmissionColor"))
            {
                pinkMat.EnableKeyword("_EMISSION");
                pinkMat.SetColor("_EmissionColor", new Color(1.0f, 0.20f, 0.50f) * 0.45f);
            }
            if (pinkMat.HasProperty("_Metallic")) pinkMat.SetFloat("_Metallic", 0.35f);
            if (pinkMat.HasProperty("_Glossiness")) pinkMat.SetFloat("_Glossiness", 0.90f);

            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = pinkMat;
            }
        }

        private void BuildFallbackPlaceholder()
        {
            switch (powerUpType)
            {
                case PowerUpType.Shield:
                {
                    // 1. Crystal Diamond Core
                    GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    core.name = "ShieldCore";
                    core.transform.SetParent(transform, false);
                    core.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                    core.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                    Destroy(core.GetComponent<Collider>());
                    Material mat = MaterialHelper.CreateSafeMaterial(new Color(0.10f, 0.85f, 1.0f, 0.90f));
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.95f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.8f);
                        core.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    }

                    // 2. Outer Protective Energy Ring
                    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = "ShieldRing";
                    ring.transform.SetParent(transform, false);
                    ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    ring.transform.localScale = new Vector3(0.85f, 0.04f, 0.85f);
                    Destroy(ring.GetComponent<Collider>());
                    Material ringMat = MaterialHelper.CreateSafeMaterial(new Color(0.35f, 0.95f, 1.0f, 0.60f));
                    if (ringMat != null) ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;
                    break;
                }
                case PowerUpType.Speedrun:
                {
                    // Golden Double-Chevron Rocket / Lightning
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    body.name = "SpeedBody";
                    body.transform.SetParent(transform, false);
                    body.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                    body.transform.localScale = new Vector3(0.42f, 0.45f, 0.42f);
                    Destroy(body.GetComponent<Collider>());
                    Material mat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.65f, 0.05f));
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.90f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
                        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    }

                    // Arrow Tip
                    GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tip.name = "SpeedTip";
                    tip.transform.SetParent(transform, false);
                    tip.transform.localPosition = new Vector3(0, 0.35f, 0);
                    tip.transform.localRotation = Quaternion.Euler(45f, 45f, 0);
                    tip.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                    Destroy(tip.GetComponent<Collider>());
                    Material tipMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.92f, 0.20f));
                    if (tipMat != null) tip.GetComponent<MeshRenderer>().sharedMaterial = tipMat;
                    break;
                }
                case PowerUpType.Magnet:
                {
                    // Classic 3D Horseshoe Magnet
                    // Base curved bridge
                    GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bridge.name = "MagnetBridge";
                    bridge.transform.SetParent(transform, false);
                    bridge.transform.localPosition = new Vector3(0, 0.25f, 0);
                    bridge.transform.localScale = new Vector3(0.65f, 0.16f, 0.18f);
                    Destroy(bridge.GetComponent<Collider>());
                    Material redMat = MaterialHelper.CreateSafeMaterial(new Color(0.95f, 0.15f, 0.18f));
                    if (redMat != null)
                    {
                        if (redMat.HasProperty("_Glossiness")) redMat.SetFloat("_Glossiness", 0.85f);
                        bridge.GetComponent<MeshRenderer>().sharedMaterial = redMat;
                    }

                    // Left Leg
                    GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftLeg.name = "LeftLeg";
                    leftLeg.transform.SetParent(transform, false);
                    leftLeg.transform.localPosition = new Vector3(-0.25f, 0.05f, 0);
                    leftLeg.transform.localScale = new Vector3(0.15f, 0.35f, 0.18f);
                    Destroy(leftLeg.GetComponent<Collider>());
                    if (redMat != null) leftLeg.GetComponent<MeshRenderer>().sharedMaterial = redMat;

                    // Right Leg
                    GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightLeg.name = "RightLeg";
                    rightLeg.transform.SetParent(transform, false);
                    rightLeg.transform.localPosition = new Vector3(0.25f, 0.05f, 0);
                    rightLeg.transform.localScale = new Vector3(0.15f, 0.35f, 0.18f);
                    Destroy(rightLeg.GetComponent<Collider>());
                    if (redMat != null) rightLeg.GetComponent<MeshRenderer>().sharedMaterial = redMat;

                    // Silver Chrome North Tip
                    Material silverMat = MaterialHelper.CreateSafeMaterial(new Color(0.90f, 0.92f, 0.95f));
                    if (silverMat != null)
                    {
                        if (silverMat.HasProperty("_Metallic")) silverMat.SetFloat("_Metallic", 0.95f);
                        if (silverMat.HasProperty("_Glossiness")) silverMat.SetFloat("_Glossiness", 0.95f);
                    }

                    GameObject leftTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftTip.name = "LeftTip";
                    leftTip.transform.SetParent(transform, false);
                    leftTip.transform.localPosition = new Vector3(-0.25f, -0.16f, 0);
                    leftTip.transform.localScale = new Vector3(0.16f, 0.12f, 0.19f);
                    Destroy(leftTip.GetComponent<Collider>());
                    if (silverMat != null) leftTip.GetComponent<MeshRenderer>().sharedMaterial = silverMat;

                    // Silver Chrome South Tip
                    GameObject rightTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightTip.name = "RightTip";
                    rightTip.transform.SetParent(transform, false);
                    rightTip.transform.localPosition = new Vector3(0.25f, -0.16f, 0);
                    rightTip.transform.localScale = new Vector3(0.16f, 0.12f, 0.19f);
                    Destroy(rightTip.GetComponent<Collider>());
                    if (silverMat != null) rightTip.GetComponent<MeshRenderer>().sharedMaterial = silverMat;
                    break;
                }
                default:
                {
                    // Heart Revive
                    GameObject heart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    heart.name = "HeartReviveCore";
                    heart.transform.SetParent(transform, false);
                    heart.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                    Destroy(heart.GetComponent<Collider>());
                    Material heartMat = MaterialHelper.CreateSafeMaterial(new Color(1.0f, 0.25f, 0.65f));
                    if (heartMat != null) heart.GetComponent<MeshRenderer>().sharedMaterial = heartMat;
                    break;
                }
            }
        }

        private void ApplyGlowEffect()
        {
            Color glowColor = powerUpType switch
            {
                PowerUpType.Shield   => new Color(0.0f, 0.8f, 1.0f),
                PowerUpType.Speedrun => new Color(0.1f, 1.0f, 0.25f),
                PowerUpType.Magnet   => new Color(0.85f, 0.1f, 0.2f),
                _ => Color.white
            };
            foreach (var mr in GetComponentsInChildren<MeshRenderer>())
                foreach (var mat in mr.sharedMaterials)
                    if (mat != null && (!mat.HasProperty("_MainTex") || mat.GetTexture("_MainTex") == null))
                        if (mat.HasProperty("_EmissionColor"))
                        { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", glowColor * 0.35f); }
        }

        private void EnsureTrigger()
        {
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.05f;
            sc.center = Vector3.zero;
        }

        private void OnEnable()
        {
            isCollected = false;
            baseLocalY = transform.localPosition.y;
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
            if (player != null && player.State != PlayerState.Dead) Collect();
        }

        private void Collect()
        {
            if (isCollected) return;
            isCollected = true;
            if (PickupManager.Instance != null)
            {
                PickupManager.Instance.ActivatePowerUp(powerUpType);
            }
            if (ImpactEffectManager.Instance != null)
                ImpactEffectManager.Instance.PlayHeartCollect(transform.position);
            gameObject.SetActive(false);
        }
    }
}
