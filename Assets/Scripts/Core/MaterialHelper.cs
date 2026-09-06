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
    }
}
