using System;
using System.Collections.Generic;
using UnityEngine;
using Runner.Core;
using Runner.Player;

namespace Runner.Characters
{
    [Serializable]
    public class CharacterSlot
    {
        public string characterId;
        public string characterName;
        public string characterTitle;
        [TextArea(2, 4)]
        public string description;
        public GameObject characterPrefab; // Drag & drop custom 3D model prefab here!
        public Sprite portraitIcon;        // Drag & drop 2D portrait icon here!
        public int heartUnlockCost;        // Sacred Hearts required to unlock (0 = Free)
        [Range(0.5f, 2.0f)] public float speedRating = 1.0f;
        [Range(0.5f, 2.0f)] public float shieldRating = 1.0f;
        [Range(0.5f, 2.0f)] public float agilityRating = 1.0f;
        public bool isDefaultUnlocked = false;

        public bool IsUnlocked()
        {
            if (isDefaultUnlocked || heartUnlockCost <= 0) return true;
            return PlayerPrefs.GetInt("Runner_CharUnlocked_" + characterId, 0) == 1;
        }

        public void SetUnlocked()
        {
            PlayerPrefs.SetInt("Runner_CharUnlocked_" + characterId, 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Modular Character Manager for Spider Temple Escape.
    /// Manages the playable character roster, unlocking, active selection,
    /// and dynamically binds custom 3D character models and animations to the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        public const string SELECTED_CHAR_KEY = "Runner_SelectedCharacter";

        [Header("Character Roster")]
        [SerializeField] private List<CharacterSlot> characters = new List<CharacterSlot>();

        public int SelectedCharacterIndex { get; private set; } = 0;

        public event Action<int> OnCharacterSelected;
        public event Action<int> OnCharacterUnlocked;

        public static CharacterManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameObject existing = GameObject.Find("CharacterManager");
            if (existing != null)
            {
                Instance = existing.GetComponent<CharacterManager>();
                if (Instance != null) return Instance;
            }
            GameObject go = new GameObject("CharacterManager");
            Instance = go.AddComponent<CharacterManager>();
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

            InitializeDefaultRoster();
            LoadCharacterPrefabs();
            SelectedCharacterIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, Mathf.Max(0, characters.Count - 1));

            // Ensure all character slots are marked unlocked in PlayerPrefs
            foreach (var slot in characters)
            {
                slot.isDefaultUnlocked = true;
                PlayerPrefs.SetInt("Runner_CharUnlocked_" + slot.characterId, 1);
            }
            PlayerPrefs.Save();
        }

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
            }
        }

        private void InitializeDefaultRoster()
        {
            if (characters == null || characters.Count == 0)
            {
                characters = new List<CharacterSlot>
                {
                    new CharacterSlot
                    {
                        characterId = "spiderman_classic",
                        characterName = "Spider-Man",
                        characterTitle = "Temple Runner",
                        description = "Iconic agile web-slinger. Balanced athletic reflexes to leap over fallen jungle timber and slide beneath stone arches.",
                        heartUnlockCost = 0,
                        speedRating = 1.0f,
                        shieldRating = 1.0f,
                        agilityRating = 1.0f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_akhilboss",
                        characterName = "Akhilboss",
                        characterTitle = "Temple Master",
                        description = "Elite temple runner with lightning reflexes, high speed, and fearless agility through ancient ruins.",
                        heartUnlockCost = 0,
                        speedRating = 1.25f,
                        shieldRating = 1.10f,
                        agilityRating = 1.20f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_harika",
                        characterName = "Harika",
                        characterTitle = "Jungle Voyager",
                        description = "Agile jungle explorer capable of fluid dodges, swift barrier leaps, and balanced endurance.",
                        heartUnlockCost = 0,
                        speedRating = 1.15f,
                        shieldRating = 1.05f,
                        agilityRating = 1.30f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_nandini",
                        characterName = "Nandini",
                        characterTitle = "Mystic Scout",
                        description = "Fleet-footed temple scout with sharp reflexes and supernatural recovery navigating treacherous paths.",
                        heartUnlockCost = 0,
                        speedRating = 1.20f,
                        shieldRating = 0.95f,
                        agilityRating = 1.35f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_navaneeth",
                        characterName = "Navaneeth",
                        characterTitle = "Ruin Guardian",
                        description = "Stalwart adventurer with rock-solid stability to withstand rough collisions and power through obstacles.",
                        heartUnlockCost = 0,
                        speedRating = 1.10f,
                        shieldRating = 1.30f,
                        agilityRating = 1.00f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_pavan",
                        characterName = "Pavan",
                        characterTitle = "Wind Striker",
                        description = "High-velocity runner possessing explosive speed bursts and aerial maneuvers.",
                        heartUnlockCost = 0,
                        speedRating = 1.35f,
                        shieldRating = 1.00f,
                        agilityRating = 1.25f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_pravalika",
                        characterName = "Pravalika",
                        characterTitle = "Shadow Runner",
                        description = "Graceful acrobatic athlete specialized in swift low slides and obstacle clearance.",
                        heartUnlockCost = 0,
                        speedRating = 1.20f,
                        shieldRating = 1.15f,
                        agilityRating = 1.30f,
                        isDefaultUnlocked = true
                    },
                    new CharacterSlot
                    {
                        characterId = "char_srikar",
                        characterName = "Srikar",
                        characterTitle = "Expedition Leader",
                        description = "Veteran temple explorer with masterclass attributes across speed, shield defense, and obstacle agility.",
                        heartUnlockCost = 0,
                        speedRating = 1.30f,
                        shieldRating = 1.20f,
                        agilityRating = 1.15f,
                        isDefaultUnlocked = true
                    }
                };
            }
            else
            {
                foreach (var slot in characters)
                {
                    slot.isDefaultUnlocked = true;
                    slot.heartUnlockCost = 0;
                }
            }
        }

        private static readonly Dictionary<string, float> CharacterScalePresets = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Akhilboss", 180.0f },
            { "Harika", 175.0f },
            { "Nandini", 175.0f },
            { "Navaneeth", 180.0f },
            { "Pavan", 180.0f },
            { "Pravalika", 2.081f },
            { "Srikar", 180.0f }
        };

        private void LoadCharacterPrefabs()
        {
            if (characters == null) return;
            for (int i = 0; i < characters.Count; i++)
            {
                var slot = characters[i];
                if (string.IsNullOrEmpty(slot.characterName) || slot.characterId == "spiderman_classic")
                    continue;

                // If already loaded with valid skinned mesh, keep it
                if (slot.characterPrefab != null && slot.characterPrefab.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    continue;

                GameObject loaded = null;
#if UNITY_EDITOR
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/{slot.characterName}/{slot.characterName}_Prefab.prefab");
#endif
                if (loaded == null || loaded.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                {
                    loaded = Resources.Load<GameObject>("Characters/" + slot.characterName);
                }
#if UNITY_EDITOR
                if (loaded == null || loaded.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                {
                    // Fallback directly to the Run FBX animation model
                    string animDir = $"Assets/Characters/{slot.characterName}/Animations";
                    if (System.IO.Directory.Exists(animDir))
                    {
                        string[] files = System.IO.Directory.GetFiles(animDir, "*.fbx");
                        foreach (var f in files)
                        {
                            string norm = System.IO.Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                            if (norm.Contains("run") && !norm.Contains("slide"))
                            {
                                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'));
                                break;
                            }
                        }
                    }
                }
#endif
                if (loaded != null)
                {
                    slot.characterPrefab = loaded;
                }
            }
        }

        public int CharacterCount => characters != null ? characters.Count : 0;

        public CharacterSlot GetCharacter(int index)
        {
            if (characters == null || index < 0 || index >= characters.Count)
                return null;
            return characters[index];
        }

        public CharacterSlot GetActiveCharacter()
        {
            return GetCharacter(SelectedCharacterIndex);
        }

        public bool IsCharacterUnlocked(int index)
        {
            return true; // All characters unlocked for gameplay
        }

        public bool SelectCharacter(int index)
        {
            if (index < 0 || index >= characters.Count) return false;

            SelectedCharacterIndex = index;
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, SelectedCharacterIndex);
            PlayerPrefs.Save();

            if (PlayerController.Instance != null)
            {
                ApplyCharacterModelToPlayer(PlayerController.Instance.gameObject);
            }

            OnCharacterSelected?.Invoke(SelectedCharacterIndex);
            return true;
        }

        public bool UnlockCharacter(int index)
        {
            if (index < 0 || index >= characters.Count) return false;
            var slot = characters[index];
            slot.SetUnlocked();
            SelectCharacter(index);
            OnCharacterUnlocked?.Invoke(index);
            return true;
        }

        /// <summary>
        /// Instantiates or equips the selected character's 3D model onto the player root,
        /// applies texture & materials, and dynamically re-binds PlayerController's Animator and Transform references.
        /// </summary>
        public void ApplyCharacterModelToPlayer(GameObject playerRoot)
        {
            if (playerRoot == null) return;

            LoadCharacterPrefabs();
            var activeSlot = GetActiveCharacter();
            var player = playerRoot.GetComponent<PlayerController>();

            Transform spideyModel = playerRoot.transform.Find("SpiderMan_Model");
            Transform defVisuals = playerRoot.transform.Find("Visuals");

            // Remove any previously instantiated custom models
            for (int i = playerRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = playerRoot.transform.GetChild(i);
                if (child.name.StartsWith("CustomCharacterModel"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            if (activeSlot == null || activeSlot.characterId == "spiderman_classic" || activeSlot.characterPrefab == null)
            {
                // Equip Canonical Spider-Man
                Transform activeDefault = spideyModel != null ? spideyModel : defVisuals;
                if (activeDefault != null)
                {
                    activeDefault.gameObject.SetActive(true);
                    Animator spideyAnim = activeDefault.GetComponentInChildren<Animator>();
                    if (player != null && spideyAnim != null)
                    {
                        player.SetActiveVisualModel(activeDefault, spideyAnim);
                    }
                }
                Debug.Log("<color=cyan>[CharacterManager]</color> Equipped Spider-Man Visuals");
                return;
            }

            // Hide default Spider-Man visuals
            if (spideyModel != null) spideyModel.gameObject.SetActive(false);
            if (defVisuals != null) defVisuals.gameObject.SetActive(false);

            // Instantiate selected custom 3D character prefab directly
            GameObject customModel = Instantiate(activeSlot.characterPrefab, playerRoot.transform);
            customModel.name = "CustomCharacterModel";
            customModel.gameObject.SetActive(true);

            // Ensure layer is 0 (Default) so the main game camera renders it
            foreach (var t in customModel.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 0;
            }

            // Find hips bone to dynamically compute correct human scale
            Transform hips = null;
            Transform head = null;
            Transform foot = null;
            foreach (var t in customModel.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (hips == null && n.Contains("hips")) hips = t;
                if (head == null && (n.Contains("headtop") || n.Contains("head"))) head = t;
                if (foot == null && (n.Contains("foot") || n.Contains("toe"))) foot = t;
            }

            // Presets verified for athletic 1.75m - 1.80m human proportion (hips at ~0.864m)
            float targetScale = 215.0f;
            if (CharacterScalePresets.TryGetValue(activeSlot.characterName, out float presetScale))
            {
                targetScale = presetScale;
            }
            else if (hips != null && hips.localPosition.y > 0.0001f && hips.localPosition.y < 0.05f)
            {
                targetScale = 0.864f / hips.localPosition.y;
            }
            else if (hips != null && hips.localPosition.y >= 0.05f && hips.localPosition.y < 2.0f)
            {
                targetScale = 0.864f / hips.localPosition.y;
            }
            else if (hips != null && hips.localPosition.y >= 2.0f)
            {
                targetScale = 86.4f / hips.localPosition.y;
            }

            customModel.transform.localScale = Vector3.one * targetScale;
            customModel.transform.localPosition = Vector3.zero;
            customModel.transform.localRotation = Quaternion.identity;

            // Apply high-res extracted texture & material
            Texture2D charTex = Resources.Load<Texture2D>($"CharacterTextures/Tex_{activeSlot.characterName}");
#if UNITY_EDITOR
            if (charTex == null)
            {
                charTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Characters/{activeSlot.characterName}/Textures/Tex_{activeSlot.characterName}.png");
            }
#endif
            Material customMat = null;
            if (charTex != null)
            {
                customMat = MaterialHelper.CreateSafeMaterial(Color.white, charTex);
            }

            var allRenderers = customModel.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                r.enabled = true;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true; // Crucial: prevents frustum culling
                }
                if (customMat != null)
                {
                    r.sharedMaterial = customMat;
                    if (Application.isPlaying)
                    {
                        r.material = customMat;
                    }
                }
            }

            // Configure Animator — always use AlwaysAnimate so bones update even off-screen
            Animator customAnim = customModel.GetComponentInChildren<Animator>();
            if (customAnim == null) customAnim = customModel.AddComponent<Animator>();

#if UNITY_EDITOR
            if (customAnim.runtimeAnimatorController == null)
            {
                string controllerPath = $"Assets/Characters/{activeSlot.characterName}/{activeSlot.characterName}_Controller.controller";
                RuntimeAnimatorController ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (ctrl != null) customAnim.runtimeAnimatorController = ctrl;
            }
            if (customAnim.avatar == null)
            {
                string animDir = $"Assets/Characters/{activeSlot.characterName}/Animations";
                if (System.IO.Directory.Exists(animDir))
                {
                    string[] files = System.IO.Directory.GetFiles(animDir, "*.fbx");
                    foreach (var f in files)
                    {
                        string norm = System.IO.Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (norm.Contains("run") && !norm.Contains("slide"))
                        {
                            Avatar av = UnityEditor.AssetDatabase.LoadAssetAtPath<Avatar>(f.Replace('\\', '/'));
                            if (av != null) customAnim.avatar = av;
                            break;
                        }
                    }
                }
            }
#endif
            // CRITICAL: AlwaysAnimate = bones update even when renderer is off-screen / culled
            customAnim.applyRootMotion = false;
            customAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            customAnim.enabled = true;

            // DO NOT call Rebind() — it resets the state machine and stops the Run state
            // Instead, force-evaluate the animator and play the default Run state
            if (Application.isPlaying)
            {
                // Force Speed > 0 so Run animation plays immediately
                customAnim.SetFloat("Speed", 1.0f);
                customAnim.SetBool("IsGrounded", true);
                // Force the Run state to start playing immediately
                customAnim.Play("Run", 0, 0f);
                customAnim.Update(0f);
            }

            // Dynamically bind Animator & VisualTransform to PlayerController
            if (player != null && customAnim != null)
            {
                player.SetActiveVisualModel(customModel.transform, customAnim);
                // Immediately sync Speed so the run anim keeps going
                player.SyncAnimatorSpeed(customAnim);
            }

            Debug.Log($"<color=green>[CharacterManager]</color> Successfully equipped 3D custom hero: {activeSlot.characterName} (Scale: {targetScale:F3}, Pos: {customModel.transform.localPosition})");
        }
    }
}
