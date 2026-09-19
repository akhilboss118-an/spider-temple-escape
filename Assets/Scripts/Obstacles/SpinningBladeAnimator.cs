using UnityEngine;

namespace Runner.Obstacles
{
    /// <summary>
    /// Animates spinning blade obstacle with rotation and optional pulsing glow.
    /// Rotates the entire visual child of the blade root so all meshes spin together.
    /// Uses OnEnable for proper pool-reactivation and unscaledDeltaTime for animation
    /// that continues during slow-mo death freeze.
    /// </summary>
    public class SpinningBladeAnimator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 400f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 2.5f;

        private Transform bladeMesh;
        private Vector3 baseLocalPos;
        private bool isInitialized = false;

        private void OnEnable()
        {
            FindBladeMesh();
        }

        private void Start()
        {
            FindBladeMesh();
        }

        private void FindBladeMesh()
        {
            if (isInitialized && bladeMesh != null) return;

            bladeMesh = transform.Find("SpinningBlade_Visual");
            if (bladeMesh == null)
                bladeMesh = transform.Find("BladeMesh");

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
            {
                baseLocalPos = bladeMesh.localPosition;
                isInitialized = true;
            }
        }

        private void Update()
        {
            if (bladeMesh == null)
            {
                FindBladeMesh();
                if (bladeMesh == null) return;
            }

            bladeMesh.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.Self);
            bladeMesh.localPosition = baseLocalPos + Vector3.up * (Mathf.Sin(Time.unscaledTime * bobFrequency) * bobAmplitude);
        }
    }
}
