using UnityEngine;
using Runner.Core;

namespace Runner.Pickups
{
    /// <summary>
    /// Generates a smooth, watertight, beveled 3D Pink Heart mesh and glowing pink material.
    /// Used for all in-game collectibles in place of coins.
    /// </summary>
    public static class HeartMeshBuilder
    {
        private static Mesh cachedMesh;
        private static Material cachedMaterial;

        private static float HeartX(float t)
        {
            return Mathf.Pow(Mathf.Sin(t), 3) * 0.52f;
        }

        private static float HeartY(float t)
        {
            return (0.8125f * Mathf.Cos(t) - 0.3125f * Mathf.Cos(2f * t) - 0.125f * Mathf.Cos(3f * t) - 0.0625f * Mathf.Cos(4f * t)) * 0.52f + 0.08f;
        }

        public static Mesh GetHeartMesh()
        {
            if (cachedMesh != null) return cachedMesh;

            cachedMesh = new Mesh();
            cachedMesh.name = "ProceduralPinkHeartMesh";

            int segments = 28;
            int vertCount = 2 + segments * 3;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            // 0: Front center vertex, 1: Back center vertex
            vertices[0] = new Vector3(0, 0.08f, 0.18f);
            vertices[1] = new Vector3(0, 0.08f, -0.18f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            uvs[1] = new Vector2(0.5f, 0.5f);

            int fBase = 2;
            int mBase = 2 + segments;
            int bBase = 2 + segments * 2;

            for (int i = 0; i < segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                float x = HeartX(t);
                float y = HeartY(t);

                // Front bevel ring
                vertices[fBase + i] = new Vector3(x * 0.82f, y * 0.82f, 0.10f);
                uvs[fBase + i] = new Vector2((x + 0.6f) / 1.2f, (y + 0.6f) / 1.2f);

                // Mid outer rim
                vertices[mBase + i] = new Vector3(x, y, 0.0f);
                uvs[mBase + i] = new Vector2((x + 0.6f) / 1.2f, (y + 0.6f) / 1.2f);

                // Back bevel ring
                vertices[bBase + i] = new Vector3(x * 0.82f, y * 0.82f, -0.10f);
                uvs[bBase + i] = new Vector2((x + 0.6f) / 1.2f, (y + 0.6f) / 1.2f);
            }

            int triCount = segments * 6 * 3; // 6 triangles per segment
            int[] triangles = new int[triCount];
            int triIdx = 0;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                // 1. Front center fan
                triangles[triIdx++] = 0;
                triangles[triIdx++] = fBase + i;
                triangles[triIdx++] = fBase + next;

                // 2. Front bevel to Mid rim (Quad = 2 tris)
                triangles[triIdx++] = fBase + i;
                triangles[triIdx++] = mBase + i;
                triangles[triIdx++] = mBase + next;

                triangles[triIdx++] = fBase + i;
                triangles[triIdx++] = mBase + next;
                triangles[triIdx++] = fBase + next;

                // 3. Mid rim to Back bevel (Quad = 2 tris)
                triangles[triIdx++] = mBase + i;
                triangles[triIdx++] = bBase + i;
                triangles[triIdx++] = bBase + next;

                triangles[triIdx++] = mBase + i;
                triangles[triIdx++] = bBase + next;
                triangles[triIdx++] = mBase + next;

                // 4. Back center fan
                triangles[triIdx++] = 1;
                triangles[triIdx++] = bBase + next;
                triangles[triIdx++] = bBase + i;
            }

            cachedMesh.vertices = vertices;
            cachedMesh.uv = uvs;
            cachedMesh.triangles = triangles;
            cachedMesh.RecalculateNormals();
            cachedMesh.RecalculateTangents();
            cachedMesh.RecalculateBounds();

            return cachedMesh;
        }

        public static Material GetHeartMaterial()
        {
            if (cachedMaterial != null) return cachedMaterial;

            cachedMaterial = MaterialHelper.CreateSafeMaterial();
            if (cachedMaterial == null) return null;
            cachedMaterial.name = "PinkHeartMat";

            // Glowing radiant pink with high specular sheen
            Color pinkColor = new Color(1.0f, 0.28f, 0.65f); // Vibrant hot pink
            cachedMaterial.color = pinkColor;

            if (cachedMaterial.HasProperty("_Glossiness"))
            {
                cachedMaterial.SetFloat("_Glossiness", 0.88f); // Glossy gem-like reflection
            }
            if (cachedMaterial.HasProperty("_Metallic"))
            {
                cachedMaterial.SetFloat("_Metallic", 0.25f);
            }
            if (cachedMaterial.HasProperty("_EmissionColor"))
            {
                cachedMaterial.EnableKeyword("_EMISSION");
                cachedMaterial.SetColor("_EmissionColor", new Color(0.40f, 0.05f, 0.20f));
            }

            return cachedMaterial;
        }
    }
}
