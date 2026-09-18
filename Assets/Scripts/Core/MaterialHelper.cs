using UnityEngine;

namespace Runner.Core
{
    public static class MaterialHelper
    {
        private static Shader _cachedShader;

        public static Shader GetSafeShader()
        {
            if (_cachedShader != null) return _cachedShader;

            _cachedShader = Shader.Find("Standard") 
                         ?? Shader.Find("Universal Render Pipeline/Lit") 
                         ?? Shader.Find("Mobile/Diffuse") 
                         ?? Shader.Find("Sprites/Default") 
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Unlit/Color");

            if (_cachedShader == null)
            {
                Material fallbackMat = Resources.Load<Material>("Materials/Mat_Track") 
                                    ?? Resources.Load<Material>("Materials/Mat_Player");
                if (fallbackMat != null && fallbackMat.shader != null)
                {
                    _cachedShader = fallbackMat.shader;
                }
            }

            return _cachedShader;
        }

        public static Material CreateSafeMaterial(Shader preferredShader = null)
        {
            Shader s = preferredShader ?? GetSafeShader();
            if (s != null)
            {
                return new Material(s);
            }

            Material fallbackMat = Resources.Load<Material>("Materials/Mat_Track") 
                                ?? Resources.Load<Material>("Materials/Mat_Player");
            if (fallbackMat != null)
            {
                return new Material(fallbackMat);
            }

            return null;
        }

        public static Material CreateSafeMaterial(Color color, Texture2D texture = null, Shader preferredShader = null)
        {
            Material mat = CreateSafeMaterial(preferredShader);
            if (mat != null)
            {
                if (texture != null)
                {
                    mat.mainTexture = texture;
                }
                mat.color = color;
            }
            return mat;
        }

        public static Material CreatePBRMaterial(Color color, Texture2D albedo = null, Texture2D normal = null,
            Texture2D metallic = null, Texture2D roughness = null, Texture2D emission = null,
            float metallicValue = 0f, float smoothness = 0.5f, Color emissionColor = default,
            bool cutout = false, float cutoff = 0.5f, Shader preferredShader = null)
        {
            Material mat = CreateSafeMaterial(preferredShader);
            if (mat == null) return null;

            if (albedo != null)
            {
                mat.mainTexture = albedo;
            }
            mat.color = color;

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_Metallic"))
            {
                if (metallic != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallic);
                }
                else
                {
                    mat.SetFloat("_Metallic", metallicValue);
                }
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (emission != null && mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.EnableKeyword("_EMISSION");
                if (emissionColor != default) mat.SetColor("_EmissionColor", emissionColor);
            }
            else if (emissionColor != default && emissionColor != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }

            if (cutout)
            {
                mat.SetFloat("_Cutoff", cutoff);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            return mat;
        }
    }
}
