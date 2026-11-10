using System.Collections.Generic;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Structures
{
    public enum GuardRule
    {
        [Tooltip("Уязвима, когда разрушена любая из построек-защитников.")]
        AnyDestroyed = 0,
        [Tooltip("Уязвима, когда разрушены все постройки-защитники.")]
        AllDestroyed = 1
    }

    /// <summary>
    /// Защита построек по ярусам (как в Dota 2): T2 неуязвима, пока стоит T1 той же линии, и т.д.
    /// Список защитников заполняет бутстрап матча.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class StructureGuard : MonoBehaviour
    {
        [SerializeField] private GuardRule rule = GuardRule.AllDestroyed;
        [SerializeField] private List<Unit> protectors = new List<Unit>();

        private Unit unit;

        public bool IsProtected { get; private set; }

        private void Awake() => unit = GetComponent<Unit>();

        private void OnEnable()
        {
            Unit.AnyDied += OnAnyDied;
            Evaluate();
        }

        private void OnDisable() => Unit.AnyDied -= OnAnyDied;

        public void SetProtectors(GuardRule guardRule, IEnumerable<Unit> units)
        {
            rule = guardRule;
            protectors.Clear();
            foreach (Unit u in units)
                if (u != null)
                    protectors.Add(u);
            Evaluate();
        }

        private void OnAnyDied(Unit dead, Unit killer)
        {
            if (protectors.Contains(dead))
                Evaluate();
        }

        private void Evaluate()
        {
            if (unit == null)
                return;

            bool isProtected;
            if (protectors.Count == 0)
                isProtected = false;
            else if (rule == GuardRule.AnyDestroyed)
            {
                isProtected = true;
                foreach (Unit p in protectors)
                    if (p == null || !p.IsAlive) { isProtected = false; break; }
            }
            else
            {
                isProtected = false;
                foreach (Unit p in protectors)
                    if (p != null && p.IsAlive) { isProtected = true; break; }
            }

            IsProtected = isProtected;
            unit.SetProtected(isProtected);
        }
    }
}
