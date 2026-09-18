using UnityEngine;

namespace Runner.Obstacles
{
    /// <summary>
    /// Animates spinning blade obstacle with rotation and optional pulsing glow.
    /// </summary>
    public class SpinningBladeAnimator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 360f;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 2f;

        private Transform bladeMesh;
        private Vector3 baseLocalPos;

        private void Start()
        {
            bladeMesh = transform.Find("BladeMesh") ?? transform.Find("SpinningBlade_Visual");
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
