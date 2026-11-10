using System;
using LastStand.Abilities;
using LastStand.Core;
using LastStand.Heroes;
using LastStand.Match;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LastStand.Controls
{
    /// <summary>
    /// Управление героем игрока в стиле Dota 2:
    /// ПКМ — идти/атаковать, Q/W/E/R — способности (ЛКМ — подтвердить, ПКМ/Esc — отмена),
    /// A + ЛКМ — атака с ходу, S — стоп, H — удерживать позицию.
    /// Клавиши физические (раскладка не важна).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerCommander : MonoBehaviour
    {
        private enum Mode
        {
            Normal,
            TargetAbility,
            AttackMove
        }

        [SerializeField] private Camera viewCamera;
        [SerializeField] private AbilityIndicator indicator;
        [SerializeField] private ClickMarker clickMarker;
        [SerializeField] private CursorSet cursors;
        [SerializeField] private float holdRepathInterval = 0.12f;

        private Unit hero;
        private HeroController controller;
        private HeroAbilities abilities;
        private Mode mode;
        private int pendingSlot = -1;
        private float nextRepath;

        public Unit HoveredUnit { get; private set; }
        /// <summary>Слот способности в режиме прицеливания (−1 — нет).</summary>
        public int TargetingSlot => mode == Mode.TargetAbility ? pendingSlot : -1;

        /// <summary>Сообщение игроку (ошибки приказов).</summary>
        public event Action<string> Notify;

        public static PlayerCommander Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (controller != null) controller.OrderError -= OnOrderError;
            if (cursors != null) cursors.ResetCursor();
        }

        private void Update()
        {
            MatchManager match = MatchManager.Instance;
            BindHero(match != null ? match.PlayerHero : null);

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || viewCamera == null)
                return;

            if (match != null && match.Phase == MatchPhase.Ended)
            {
                CancelMode();
                HoveredUnit = null;
                if (cursors != null) cursors.Apply(CursorKind.Default);
                return;
            }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = viewCamera.ScreenPointToRay(mousePos);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            HoveredUnit = overUI ? null : UnitPicker.Pick(ray);

            if (hero == null)
            {
                UpdateCursor();
                return;
            }

            HandleHotkeys(keyboard);

            switch (mode)
            {
                case Mode.TargetAbility:
                    UpdateTargeting(mouse, ray, overUI);
                    break;
                case Mode.AttackMove:
                    UpdateAttackMove(mouse, ray, overUI);
                    break;
                default:
                    UpdateNormal(mouse, ray, overUI);
                    break;
            }

            UpdateCursor();
        }

        private void BindHero(Unit unit)
        {
            if (unit == hero)
                return;
            if (controller != null)
                controller.OrderError -= OnOrderError;

            hero = unit;
            controller = unit != null ? unit.GetComponent<HeroController>() : null;
            abilities = unit != null ? unit.GetComponent<HeroAbilities>() : null;
            if (controller != null)
                controller.OrderError += OnOrderError;
            CancelMode();
        }

        private void OnOrderError(string message) => Notify?.Invoke(message);

        // ---------- Клавиатура ----------

        private void HandleHotkeys(Keyboard kb)
        {
            if (kb.escapeKey.wasPressedThisFrame)
                CancelMode();

            if (kb.qKey.wasPressedThisFrame) BeginAbility(0);
            if (kb.wKey.wasPressedThisFrame) BeginAbility(1);
            if (kb.eKey.wasPressedThisFrame) BeginAbility(2);
            if (kb.rKey.wasPressedThisFrame) BeginAbility(3);

            if (kb.aKey.wasPressedThisFrame && hero.IsAlive)
            {
                mode = Mode.AttackMove;
                pendingSlot = -1;
            }
            if (kb.sKey.wasPressedThisFrame)
            {
                CancelMode();
                controller.IssueStop();
            }
            if (kb.hKey.wasPressedThisFrame)
            {
                CancelMode();
                controller.IssueHold();
            }
        }

        /// <summary>Нажатие клавиши способности (также вызывается кликом по иконке в HUD).</summary>
        public void BeginAbility(int slot)
        {
            if (abilities == null || controller == null)
                return;

            AbilityCastError error = abilities.CanCast(slot);
            if (error != AbilityCastError.None)
            {
                Notify?.Invoke(HeroAbilities.ErrorText(error));
                return;
            }

            AbilityDefinition def = abilities[slot].Definition;
            if (def.targeting == AbilityTargeting.NoTarget)
            {
                CancelMode();
                controller.IssueCast(slot, null, hero.Position);
                return;
            }

            mode = Mode.TargetAbility;
            pendingSlot = slot;
        }

        private void CancelMode()
        {
            mode = Mode.Normal;
            pendingSlot = -1;
            if (indicator != null) indicator.Hide();
        }

        // ---------- Режимы ----------

        private void UpdateNormal(Mouse mouse, Ray ray, bool overUI)
        {
            if (indicator != null) indicator.Hide();
            if (overUI)
                return;

            bool pressed = mouse.rightButton.wasPressedThisFrame;
            bool held = mouse.rightButton.isPressed && Time.unscaledTime >= nextRepath;
            if (!pressed && !held)
                return;
            nextRepath = Time.unscaledTime + holdRepathInterval;

            if (pressed && HoveredUnit != null && CombatMath.IsEnemy(hero.Team, HoveredUnit.Team) && !HoveredUnit.IsInvulnerable)
            {
                controller.IssueAttack(HoveredUnit);
                if (clickMarker != null) clickMarker.Show(HoveredUnit.Position, true);
                return;
            }

            if (controller.CurrentOrder == OrderType.AttackUnit && !pressed)
                return;

            if (TryGroundPoint(ray, out Vector3 point))
            {
                controller.IssueMove(point);
                if (pressed && clickMarker != null) clickMarker.Show(point, false);
            }
        }

        private void UpdateAttackMove(Mouse mouse, Ray ray, bool overUI)
        {
            if (indicator != null) indicator.ShowAttackRange(hero);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelMode();
                UpdateNormal(mouse, ray, overUI);
                return;
            }
            if (overUI || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (HoveredUnit != null && CombatMath.IsEnemy(hero.Team, HoveredUnit.Team) && !HoveredUnit.IsInvulnerable)
            {
                controller.IssueAttack(HoveredUnit);
                if (clickMarker != null) clickMarker.Show(HoveredUnit.Position, true);
            }
            else if (TryGroundPoint(ray, out Vector3 point))
            {
                controller.IssueAttackMove(point);
                if (clickMarker != null) clickMarker.Show(point, true);
            }
            CancelMode();
        }

        private void UpdateTargeting(Mouse mouse, Ray ray, bool overUI)
        {
            AbilitySlot slot = abilities[pendingSlot];
            if (!slot.IsLearned)
            {
                CancelMode();
                return;
            }

            bool hasPoint = TryGroundPoint(ray, out Vector3 point);
            if (indicator != null && hasPoint)
                indicator.ShowAbility(hero, slot.Definition, slot.Level, point, HoveredUnit);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                // Как в Dota: ПКМ отменяет прицеливание и сразу отдаёт обычный приказ.
                CancelMode();
                UpdateNormal(mouse, ray, overUI);
                return;
            }

            if (overUI || !mouse.leftButton.wasPressedThisFrame)
                return;

            AbilityDefinition def = slot.Definition;
            if (def.targeting == AbilityTargeting.UnitTarget)
            {
                if (HoveredUnit == null || !def.IsValidTarget(hero, HoveredUnit))
                {
                    Notify?.Invoke(HeroAbilities.ErrorText(AbilityCastError.InvalidTarget));
                    return;
                }
                if (controller.IssueCast(pendingSlot, HoveredUnit, HoveredUnit.Position))
                    CancelMode();
                return;
            }

            if (!hasPoint)
                return;
            if (controller.IssueCast(pendingSlot, null, point))
                CancelMode();
        }

        private void UpdateCursor()
        {
            if (cursors == null)
                return;
            if (mode != Mode.Normal)
                cursors.Apply(CursorKind.Target);
            else if (HoveredUnit != null && hero != null && CombatMath.IsEnemy(hero.Team, HoveredUnit.Team))
                cursors.Apply(CursorKind.Attack);
            else
                cursors.Apply(CursorKind.Default);
        }

        private static bool TryGroundPoint(Ray ray, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(ray, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            // Клик по лесу или постройке — идём к ближайшей проходимой точке.
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 6f, NavMesh.AllAreas) ||
                NavMesh.SamplePosition(hit.point, out navHit, 30f, NavMesh.AllAreas))
            {
                point = navHit.position;
                return true;
            }
            point = hit.point;
            return true;
        }
    }
}
