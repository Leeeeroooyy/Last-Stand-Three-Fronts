using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LastStand.Prototype
{
    /// <summary>
    /// ВРЕМЕННЫЙ тестовый герой для обкатки карты: движение по ПКМ через NavMesh, S — стоп.
    /// Будет заменён полноценным контроллером героя.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class TestHeroMover : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float holdRepathInterval = 0.12f;

        private NavMeshAgent agent;
        private float nextRepathTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        private void Start()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.enabled = true;
            }
            else
            {
                Debug.LogWarning($"{name}: NavMesh рядом не найден — перегенерируйте карту (Last Stand ▸ Map ▸ Generate).", this);
            }
        }

        private void Update()
        {
            if (!agent.enabled || !agent.isOnNavMesh)
                return;

            Mouse mouse = Mouse.current;
            if (mouse != null && viewCamera != null)
            {
                bool clicked = mouse.rightButton.wasPressedThisFrame;
                bool held = mouse.rightButton.isPressed && Time.time >= nextRepathTime;
                if (clicked || held)
                {
                    nextRepathTime = Time.time + holdRepathInterval;
                    Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(ray, out RaycastHit rayHit, 500f, ~0, QueryTriggerInteraction.Ignore) &&
                        NavMesh.SamplePosition(rayHit.point, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(navHit.position);
                    }
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.sKey.wasPressedThisFrame)
                agent.ResetPath();
        }
    }
}
