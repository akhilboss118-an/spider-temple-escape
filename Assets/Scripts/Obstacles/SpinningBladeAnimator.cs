using UnityEngine;

namespace Runner.Obstacles
{
    /// <summary>
    /// Animates spinning blade obstacle with rotation and optional pulsing glow.
    /// Rotates the entire visual child of the blade root so all meshes spin together.
    /// </summary>
    public class SpinningBladeAnimator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 400f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 2.5f;

        private Transform bladeMesh;
        private Vector3 baseLocalPos;

        private void Start()
        {
            // Find the visual child - try common names from GLB instantiation
            bladeMesh = transform.Find("SpinningBlade_Visual");
            if (bladeMesh == null)
                bladeMesh = transform.Find("BladeMesh");

            // Fallback: find first child with a Renderer
            if (bladeMesh == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponentInChildren<Renderer>() != null)
                    {
                        bladeMesh = child;
                        break;
                    }
                }
            }

            if (bladeMesh != null)
                baseLocalPos = bladeMesh.localPosition;
        }

        private void Update()
        {
            if (bladeMesh != null)
            {
                bladeMesh.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
                bladeMesh.localPosition = baseLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
            }
        }
    }
}
