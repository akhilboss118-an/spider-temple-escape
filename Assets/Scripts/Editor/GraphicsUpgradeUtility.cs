#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Runner.EditorTools
{
    public static class GraphicsUpgradeUtility
    {
        private const string PINE_TEXTURE_DIR = "Assets/Models/Jungle/pine-tree/textures/";
        private const string MONSTERA_TEXTURE_DIR = "Assets/Models/Jungle/monstera-tree/textures/";
        private const string TEXTURE_DIR = "Assets/Textures/";
        private const string MATERIAL_DIR = "Assets/Materials/";

        [MenuItem("Runner/Graphics/Upgrade All Graphics & Textures")]
        public static void UpgradeAllGraphics()
        {
            Debug.Log("[GraphicsUpgradeUtility] Starting graphics & texture upgrade...");

            EnsureTextureReadability(PINE_TEXTURE_DIR + "Leavs_basecolor_.tga.png");
            EnsureTextureReadability(PINE_TEXTURE_DIR + "Leavs_Opacity.png");
            EnsureTextureReadability(MONSTERA_TEXTURE_DIR + "ALBEDO-monstera.png");

            ProcessPineLeafCutout();
            ProcessMonsteraCutout();
            ConfigureMaterials();
            ConfigureSkybox();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GraphicsUpgradeUtility] All graphics and materials successfully upgraded!");
        }

        private static void EnsureTextureReadability(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void ProcessPineLeafCutout()
        {
            string colorPath = PINE_TEXTURE_DIR + "Leavs_basecolor_.tga.png";
            string opacityPath = PINE_TEXTURE_DIR + "Leavs_Opacity.png";
            string outputPath = PINE_TEXTURE_DIR + "Leavs_basecolor_cutout.png";

            Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            Texture2D opacityTex = AssetDatabase.LoadAssetAtPath<Texture2D>(opacityPath);

            if (colorTex == null || opacityTex == null) return;

            int w = colorTex.width;
            int h = colorTex.height;
            Texture2D resultTex = new Texture2D(w, h, TextureFormat.RGBA32, true);

            Color[] colorPixels = colorTex.GetPixels();
            Color[] opacityPixels = (opacityTex.width == w && opacityTex.height == h) 
                ? opacityTex.GetPixels() 
                : GetResampledPixels(opacityTex, w, h);

            Color[] outPixels = new Color[colorPixels.Length];
            for (int i = 0; i < colorPixels.Length; i++)
            {
                Color c = colorPixels[i];
                float alpha = opacityPixels[i].r;

                // Check if this pixel belongs to the branch wood (top-left UV area where red > green)
                bool isBranchWood = (c.r > c.g + 0.03f) && (c.r > 0.15f);

                if (isBranchWood)
                {
                    // Natural weathered tree branch bark (warm earthy brown)
                    outPixels[i] = new Color(
                        Mathf.Clamp01(c.r * 1.15f),
                        Mathf.Clamp01(c.g * 0.95f),
                        Mathf.Clamp01(c.b * 0.85f),
                        alpha > 0.30f ? 1.0f : 0.0f
                    );
                }
                else
                {
                    // Lush tropical rainforest evergreen green (natural rich foliage, NOT neon lime)
                    float foliageG = Mathf.Clamp(c.g * 1.08f + 0.05f, 0.22f, 0.62f);
                    float foliageR = foliageG * 0.38f;
                    float foliageB = foliageG * 0.30f;
                    outPixels[i] = new Color(foliageR, foliageG, foliageB, alpha > 0.35f ? 1.0f : 0.0f);
                }
            }

            resultTex.SetPixels(outPixels);
            resultTex.Apply();

            byte[] pngData = resultTex.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            TextureImporter outImp = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (outImp != null)
            {
                outImp.alphaIsTransparency = true;
                outImp.mipmapEnabled = true;
                outImp.textureCompression = TextureImporterCompression.Compressed;
                outImp.SaveAndReimport();
            }

            Debug.Log("[GraphicsUpgradeUtility] Generated natural lush pine leaf cutout: " + outputPath);
        }

        private static void ProcessMonsteraCutout()
        {
            string colorPath = MONSTERA_TEXTURE_DIR + "ALBEDO-monstera.png";
            string outputPath = MONSTERA_TEXTURE_DIR + "monstera_cutout.png";

            Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            if (colorTex == null) return;

            int w = colorTex.width;
            int h = colorTex.height;
            Texture2D resultTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color[] pixels = colorTex.GetPixels();
            Color[] outPixels = new Color[pixels.Length];

            // Background color is exactly at corner (0,0)
            Color bgColor = pixels[0];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float dr = c.r - bgColor.r;
                float dg = c.g - bgColor.g;
                float db = c.b - bgColor.b;
                float distSq = dr * dr + dg * dg + db * db;

                // Only key out if very close to the solid background green
                if (distSq < 0.0025f)
                {
                    outPixels[i] = new Color(0, 0, 0, 0);
                }
                else
                {
                    outPixels[i] = new Color(c.r * 0.95f, c.g * 1.05f, c.b * 0.95f, 1.0f);
                }
            }

            resultTex.SetPixels(outPixels);
            resultTex.Apply();

            byte[] pngData = resultTex.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            TextureImporter outImp = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (outImp != null)
            {
                outImp.alphaIsTransparency = true;
                outImp.mipmapEnabled = true;
                outImp.textureCompression = TextureImporterCompression.Compressed;
                outImp.SaveAndReimport();
            }

            Debug.Log("[GraphicsUpgradeUtility] Generated monstera leaf cutout: " + outputPath);
        }

        private static Color[] GetResampledPixels(Texture2D source, int targetW, int targetH)
        {
            Color[] result = new Color[targetW * targetH];
            float xRatio = (float)source.width / targetW;
            float yRatio = (float)source.height / targetH;

            for (int y = 0; y < targetH; y++)
            {
                int srcY = Mathf.Clamp(Mathf.FloorToInt(y * yRatio), 0, source.height - 1);
                for (int x = 0; x < targetW; x++)
                {
                    int srcX = Mathf.Clamp(Mathf.FloorToInt(x * xRatio), 0, source.width - 1);
                    result[y * targetW + x] = source.GetPixel(srcX, srcY);
                }
            }
            return result;
        }

        public static void ConfigureMaterials()
        {
            // 1. Mat_Track.mat: PBR Ancient Aztec/Mayan Flagstone Runway (Vibrant Golden Ancient Limestone)
            Texture2D runwayTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Tex_TempleRunway.jpg");
            if (runwayTex != null)
            {
                Material trackMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Track.mat");
                trackMat.shader = Shader.Find("Standard");
                trackMat.mainTexture = runwayTex;
                trackMat.mainTextureScale = new Vector2(1.5f, 3.5f);
                trackMat.color = new Color(1.08f, 1.05f, 0.98f);
                trackMat.SetFloat("_Glossiness", 0.30f);
                trackMat.SetFloat("_Metallic", 0.06f);
                trackMat.EnableKeyword("_EMISSION");
                trackMat.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.20f) * 0.25f);
                trackMat.enableInstancing = true;
                EditorUtility.SetDirty(trackMat);

                Material resTrackMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Track.mat");
                resTrackMat.CopyPropertiesFromMaterial(trackMat);
                resTrackMat.enableInstancing = true;
                EditorUtility.SetDirty(resTrackMat);
            }

            // 2. Mat_Curb.mat: Carved Ancient Temple Stone Balustrade
            Texture2D curbTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Tex_TempleCurb.jpg");
            if (curbTex != null)
            {
                Material curbMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Curb.mat");
                curbMat.shader = Shader.Find("Standard");
                curbMat.mainTexture = curbTex;
                curbMat.mainTextureScale = new Vector2(1.0f, 3.0f);
                curbMat.color = new Color(1.05f, 1.02f, 0.95f);
                curbMat.SetFloat("_Glossiness", 0.22f);
                curbMat.SetFloat("_Metallic", 0.02f);
                curbMat.enableInstancing = true;
                EditorUtility.SetDirty(curbMat);

                Material resCurbMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Curb.mat");
                resCurbMat.CopyPropertiesFromMaterial(curbMat);
                resCurbMat.enableInstancing = true;
                EditorUtility.SetDirty(resCurbMat);
            }

            // 3. Mat_JungleGround.mat: Dense Lush Rainforest Undergrowth
            Texture2D groundTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Tex_JungleGround.jpg");
            if (groundTex != null)
            {
                Material groundMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_JungleGround.mat");
                groundMat.shader = Shader.Find("Standard");
                groundMat.mainTexture = groundTex;
                groundMat.mainTextureScale = new Vector2(2.0f, 4.0f);
                groundMat.color = new Color(1.02f, 1.10f, 1.00f);
                groundMat.SetFloat("_Glossiness", 0.20f);
                groundMat.SetFloat("_Metallic", 0.0f);
                groundMat.enableInstancing = true;
                EditorUtility.SetDirty(groundMat);

                Material resGroundMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_JungleGround.mat");
                resGroundMat.CopyPropertiesFromMaterial(groundMat);
                resGroundMat.enableInstancing = true;
                EditorUtility.SetDirty(resGroundMat);
            }

            // 4. Mat_CanopyLeaves.mat: Cutout mode with natural lush forest needles
            Texture2D leafCutout = AssetDatabase.LoadAssetAtPath<Texture2D>(PINE_TEXTURE_DIR + "Leavs_basecolor_cutout.png");
            if (leafCutout != null)
            {
                Material leafMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_CanopyLeaves.mat");
                leafMat.shader = Shader.Find("Standard");
                SetMaterialCutout(leafMat);
                leafMat.mainTexture = leafCutout;
                leafMat.color = new Color(0.96f, 1.02f, 0.96f);
                leafMat.SetFloat("_Cutoff", 0.38f);
                leafMat.SetFloat("_Glossiness", 0.22f);
                leafMat.enableInstancing = true;
                EditorUtility.SetDirty(leafMat);

                Material resLeafMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_CanopyLeaves.mat");
                resLeafMat.CopyPropertiesFromMaterial(leafMat);
                resLeafMat.enableInstancing = true;
                EditorUtility.SetDirty(resLeafMat);
            }

            // 5. Mat_Monstera.mat: Cutout tropical foliage
            Texture2D monsteraCutout = AssetDatabase.LoadAssetAtPath<Texture2D>(MONSTERA_TEXTURE_DIR + "monstera_cutout.png");
            if (monsteraCutout != null)
            {
                Material mMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Monstera.mat");
                mMat.shader = Shader.Find("Standard");
                SetMaterialCutout(mMat);
                mMat.mainTexture = monsteraCutout;
                mMat.color = new Color(0.96f, 1.05f, 0.96f);
                mMat.SetFloat("_Cutoff", 0.30f);
                mMat.SetFloat("_Glossiness", 0.30f);
                mMat.enableInstancing = true;
                EditorUtility.SetDirty(mMat);

                Material resMMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Monstera.mat");
                resMMat.CopyPropertiesFromMaterial(mMat);
                resMMat.enableInstancing = true;
                EditorUtility.SetDirty(resMMat);
            }

            // 5b. Mat_CanopyBark.mat: Rich ancient jungle timber trunk bark
            Material barkMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_CanopyBark.mat");
            barkMat.shader = Shader.Find("Standard");
            Texture2D trunkDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(PINE_TEXTURE_DIR + "Trank_basecolor.tga.png");
            if (trunkDiff != null) barkMat.mainTexture = trunkDiff;
            Texture2D trunkNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(PINE_TEXTURE_DIR + "Trank_normal.tga.png");
            if (trunkNorm != null)
            {
                barkMat.SetTexture("_BumpMap", trunkNorm);
                barkMat.EnableKeyword("_NORMALMAP");
            }
            barkMat.color = new Color(0.85f, 0.72f, 0.58f);
            barkMat.SetFloat("_Glossiness", 0.20f);
            barkMat.enableInstancing = true;
            EditorUtility.SetDirty(barkMat);

            Material resBarkMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_CanopyBark.mat");
            resBarkMat.CopyPropertiesFromMaterial(barkMat);
            resBarkMat.enableInstancing = true;
            EditorUtility.SetDirty(resBarkMat);

            // 6. Mat_Coin: Radiant Aztec Gold Medallion
            Material coinMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Coin.mat");
            coinMat.shader = Shader.Find("Standard");
            coinMat.color = new Color(1.0f, 0.88f, 0.20f);
            coinMat.SetFloat("_Metallic", 0.88f);
            coinMat.SetFloat("_Glossiness", 0.82f);
            coinMat.EnableKeyword("_EMISSION");
            coinMat.SetColor("_EmissionColor", new Color(1.0f, 0.70f, 0.05f) * 0.80f);
            coinMat.enableInstancing = true;
            EditorUtility.SetDirty(coinMat);

            Material resCoinMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Coin.mat");
            resCoinMat.CopyPropertiesFromMaterial(coinMat);
            resCoinMat.enableInstancing = true;
            EditorUtility.SetDirty(resCoinMat);

            // 7. Mat_DeadTree & Mat_TreeBranch_Jump: Rich weathered ancient timber
            Material deadTreeMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_DeadTree.mat");
            deadTreeMat.color = new Color(0.88f, 0.80f, 0.70f);
            deadTreeMat.SetFloat("_Glossiness", 0.22f);
            deadTreeMat.enableInstancing = true;
            EditorUtility.SetDirty(deadTreeMat);

            Material resDeadTree = GetOrCreateMaterial("Assets/Resources/Materials/Mat_DeadTree.mat");
            resDeadTree.CopyPropertiesFromMaterial(deadTreeMat);
            resDeadTree.enableInstancing = true;
            EditorUtility.SetDirty(resDeadTree);

            // Update jump & slide tree branch materials
            string[] branchMatNames = { "Mat_TreeBranch_Jump", "Mat_TreeBranch_Jump_0", "Mat_TreeBranch_Jump_1", "Mat_TreeBranch_Jump_2", "Mat_TreeBranch_Slide" };
            foreach (var bName in branchMatNames)
            {
                Material bMat = GetOrCreateMaterial(MATERIAL_DIR + bName + ".mat");
                bMat.color = new Color(0.88f, 0.80f, 0.70f);
                bMat.SetFloat("_Glossiness", 0.22f);
                bMat.enableInstancing = true;
                EditorUtility.SetDirty(bMat);

                Material resBMat = GetOrCreateMaterial("Assets/Resources/Materials/" + bName + ".mat");
                resBMat.CopyPropertiesFromMaterial(bMat);
                resBMat.enableInstancing = true;
                EditorUtility.SetDirty(resBMat);
            }

            // 8. Mat_Torch & Mat_Torch_Flame: Low-poly Torch Brazier & Radiant Flame
            string torchNormalPath = TEXTURE_DIR + "Environment/tex_torch_normal.png";
            TextureImporter normImp = AssetImporter.GetAtPath(torchNormalPath) as TextureImporter;
            if (normImp != null && normImp.textureType != TextureImporterType.NormalMap)
            {
                normImp.textureType = TextureImporterType.NormalMap;
                normImp.SaveAndReimport();
            }

            Material torchMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Torch.mat");
            torchMat.shader = Shader.Find("Standard");
            Texture2D torchDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Environment/tex_torch_albedo.png");
            if (torchDiff != null) torchMat.mainTexture = torchDiff;
            Texture2D torchNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(torchNormalPath);
            if (torchNorm != null)
            {
                torchMat.SetTexture("_BumpMap", torchNorm);
                torchMat.EnableKeyword("_NORMALMAP");
            }
            torchMat.color = Color.white;
            torchMat.SetFloat("_Glossiness", 0.35f);
            torchMat.SetFloat("_Metallic", 0.25f);
            torchMat.enableInstancing = true;
            EditorUtility.SetDirty(torchMat);

            Material resTorchMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Torch.mat");
            resTorchMat.CopyPropertiesFromMaterial(torchMat);
            resTorchMat.enableInstancing = true;
            EditorUtility.SetDirty(resTorchMat);

            Material flameMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_Torch_Flame.mat");
            flameMat.shader = Shader.Find("Standard");
            SetMaterialCutout(flameMat);
            flameMat.SetFloat("_Cutoff", 0.10f);
            Texture2D flameDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Environment/tex_torch_flame_albedo.png");
            if (flameDiff != null) flameMat.mainTexture = flameDiff;
            Texture2D flameEmis = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_DIR + "Environment/tex_torch_flame_emissive.png");
            flameMat.EnableKeyword("_EMISSION");
            flameMat.SetColor("_EmissionColor", new Color(1.0f, 0.55f, 0.08f) * 3.5f);
            if (flameEmis != null) flameMat.SetTexture("_EmissionMap", flameEmis);
            if (flameMat.HasProperty("_Cull")) flameMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            flameMat.color = new Color(1.1f, 1.0f, 0.9f);
            flameMat.enableInstancing = true;
            EditorUtility.SetDirty(flameMat);

            Material resFlameMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_Torch_Flame.mat");
            resFlameMat.CopyPropertiesFromMaterial(flameMat);
            resFlameMat.enableInstancing = true;
            EditorUtility.SetDirty(resFlameMat);
        }

        public static void ConfigureSkybox()
        {
            string skyPath = TEXTURE_DIR + "Environment/Tex_TempleSky.jpg";
            Texture2D skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(skyPath);
            if (skyTex == null) return;

            TextureImporter skyImp = AssetImporter.GetAtPath(skyPath) as TextureImporter;
            if (skyImp != null)
            {
                skyImp.wrapMode = TextureWrapMode.Repeat;
                skyImp.filterMode = FilterMode.Bilinear;
                skyImp.textureCompression = TextureImporterCompression.Compressed;
                skyImp.maxTextureSize = 2048;
                skyImp.SaveAndReimport();
            }

            Shader skyShader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Mobile/Skybox");
            if (skyShader != null)
            {
                Material skyMat = GetOrCreateMaterial(MATERIAL_DIR + "Mat_TempleSkybox.mat");
                skyMat.shader = skyShader;
                if (skyMat.HasProperty("_MainTex")) skyMat.SetTexture("_MainTex", skyTex);
                if (skyMat.HasProperty("_Tex")) skyMat.SetTexture("_Tex", skyTex);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.35f);
                if (skyMat.HasProperty("_Tint")) skyMat.SetColor("_Tint", new Color(1.02f, 1.02f, 1.02f));
                if (skyMat.HasProperty("_ImageType")) skyMat.SetFloat("_ImageType", 0); // 360 degrees
                EditorUtility.SetDirty(skyMat);

                Material resSkyMat = GetOrCreateMaterial("Assets/Resources/Materials/Mat_TempleSkybox.mat");
                resSkyMat.CopyPropertiesFromMaterial(skyMat);
                EditorUtility.SetDirty(resSkyMat);

                RenderSettings.skybox = skyMat;
                Debug.Log("[GraphicsUpgradeUtility] Configured vibrant tropical blue skybox: Mat_TempleSkybox.mat");
            }
        }

        private static Material GetOrCreateMaterial(string path)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void SetMaterialCutout(Material mat)
        {
            mat.SetFloat("_Mode", 1);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
    }
}
#endif
