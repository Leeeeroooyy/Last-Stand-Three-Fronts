using UnityEngine;
using UnityEngine.InputSystem;

namespace LastStand.CameraControl
{
    /// <summary>
    /// Камера в стиле Dota 2: наклонный вид сверху, прокрутка краем экрана и стрелками,
    /// перетаскивание средней кнопкой мыши, зум колесом, Space — центр на герое.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobaCameraController : MonoBehaviour
    {
        [Header("Цель")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool centerOnTargetAtStart = true;

        [Header("Ракурс")]
        [SerializeField] private float yaw;
        [SerializeField] private float pitchNear = 50f;
        [SerializeField] private float pitchFar = 60f;

        [Header("Зум")]
        [SerializeField] private float distance = 34f;
        [SerializeField] private float minDistance = 18f;
        [SerializeField] private float maxDistance = 50f;
        [SerializeField] private float zoomStep = 4f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("Прокрутка")]
        [SerializeField] private float panSpeed = 1.1f;
        [SerializeField] private bool edgePan = true;
        [SerializeField] private float edgeSize = 12f;
        [SerializeField] private float dragSensitivity = 0.0022f;

        [Header("Границы (XZ)")]
        [SerializeField] private Vector2 boundsMin = new Vector2(-76f, -76f);
        [SerializeField] private Vector2 boundsMax = new Vector2(76f, 76f);
        [SerializeField] private float groundHeight = 2f;

        private Vector3 focus;
        private float targetDistance;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        private void Start()
        {
            targetDistance = distance;
            focus = followTarget != null && centerOnTargetAtStart
                ? followTarget.position
                : ProjectToGround(transform.position, transform.forward);
            Apply(0f);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.upArrowKey.isPressed) move.y += 1f;
            }

            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                bool inside = p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
                if (edgePan && Application.isFocused && inside && !mouse.middleButton.isPressed)
                {
                    if (p.x <= edgeSize) move.x -= 1f;
                    else if (p.x >= Screen.width - edgeSize) move.x += 1f;
                    if (p.y <= edgeSize) move.y -= 1f;
                    else if (p.y >= Screen.height - edgeSize) move.y += 1f;
                }

                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    focus -= yawRot * new Vector3(delta.x, 0f, delta.y) * (distance * dragSensitivity);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
            }

            if (move.sqrMagnitude > 0f)
            {
                move = Vector2.ClampMagnitude(move, 1f);
                focus += Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y) * (panSpeed * distance * dt);
            }

            if (followTarget != null && keyboard != null && keyboard.spaceKey.isPressed)
                focus = followTarget.position;

            Apply(dt);
        }

        public void CenterOn(Vector3 worldPosition) => focus = worldPosition;

        private void Apply(float dt)
        {
            distance = dt > 0f
                ? Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSmoothing * dt))
                : targetDistance;

            focus.x = Mathf.Clamp(focus.x, boundsMin.x, boundsMax.x);
            focus.z = Mathf.Clamp(focus.z, boundsMin.y, boundsMax.y);
            focus.y = groundHeight;

            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            Quaternion rot = Quaternion.Euler(Mathf.Lerp(pitchNear, pitchFar, t), yaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }

        private Vector3 ProjectToGround(Vector3 origin, Vector3 dir)
        {
            if (Mathf.Abs(dir.y) < 0.01f)
                return new Vector3(origin.x, groundHeight, origin.z);
            float k = (groundHeight - origin.y) / dir.y;
            return origin + dir * Mathf.Max(0f, k);
        }
    }
}
