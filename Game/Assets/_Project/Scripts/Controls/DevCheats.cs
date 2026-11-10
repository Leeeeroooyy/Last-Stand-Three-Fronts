using LastStand.Abilities;
using LastStand.Core;
using LastStand.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastStand.Controls
{
    /// <summary>
    /// Клавиши для тестирования (только в редакторе и отладочных сборках):
    /// F5 — полное здоровье, мана и сброс перезарядок героя; F6 — убить своего героя (проверка возрождения);
    /// F9 — разрушить вражеский Ancient (проверка победы); F10 — свой Ancient (проверка поражения).
    /// </summary>
    public sealed class DevCheats : MonoBehaviour
    {
        private void Awake()
        {
#if !UNITY_EDITOR && !DEBUG
            Destroy(this);
#endif
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            MatchManager match = MatchManager.Instance;
            if (kb == null || match == null)
                return;

            if (kb.f5Key.wasPressedThisFrame && match.PlayerHero != null)
            {
                Unit hero = match.PlayerHero;
                hero.Heal(hero.MaxHealth);
                hero.RestoreMana(hero.MaxMana);
                var abilities = hero.GetComponent<HeroAbilities>();
                if (abilities != null)
                    for (int i = 0; i < abilities.Count; i++)
                        abilities[i].ResetCooldown();
                Debug.Log("[Cheat] Герой восстановлен, перезарядки сброшены.");
            }

            if (kb.f6Key.wasPressedThisFrame && match.PlayerHero != null && match.PlayerHero.IsAlive)
            {
                match.PlayerHero.Kill();
                Debug.Log("[Cheat] Герой убит (проверка возрождения).");
            }

            if (kb.f9Key.wasPressedThisFrame)
                DestroyAncient(CombatMath.Opponent(match.PlayerTeam));
            if (kb.f10Key.wasPressedThisFrame)
                DestroyAncient(match.PlayerTeam);
        }

        private static void DestroyAncient(Map.TeamSide side)
        {
            var units = UnitRegistry.All;
            for (int i = units.Count - 1; i >= 0; i--)
            {
                Unit u = units[i];
                if (u.Kind == UnitKind.Ancient && u.Team == side && u.IsAlive)
                {
                    u.SetProtected(false);
                    u.Kill();
                    Debug.Log($"[Cheat] Ancient стороны {side} разрушен.");
                }
            }
        }
    }
}
