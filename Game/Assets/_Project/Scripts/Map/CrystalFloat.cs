using UnityEngine;

namespace LastStand.Map
{
    /// <summary>Лёгкая анимация кристаллов: вращение и плавное покачивание.</summary>
    public sealed class CrystalFloat : MonoBehaviour
    {
        [SerializeField] private float spinSpeed = 30f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 0.6f;

        private Vector3 basePosition;
        private float phase;

        private void Awake()
        {
            basePosition = transform.localPosition;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            float y = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + phase) * bobAmplitude;
            transform.localPosition = basePosition + new Vector3(0f, y, 0f);
        }
    }
}
