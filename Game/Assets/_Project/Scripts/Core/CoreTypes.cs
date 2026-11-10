using System;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Core
{
    public enum UnitKind
    {
        Hero = 0,
        Creep = 1,
        Tower = 2,
        Barracks = 3,
        Ancient = 4
    }

    public enum DamageType
    {
        Physical = 0,
        Magical = 1,
        Pure = 2
    }

    /// <summary>Стихия способности. Нужна для погоды (дождь ослабляет огонь) и будущих механик.</summary>
    public enum Element
    {
        None = 0,
        Fire = 1,
        Frost = 2,
        Lightning = 3,
        Arcane = 4,
        Nature = 5
    }

    /// <summary>Чьи юниты затрагивает эффект относительно стороны-источника.</summary>
    public enum TeamFilter
    {
        Enemies = 0,
        Allies = 1,
        All = 2
    }

    [Flags]
    public enum UnitStates
    {
        None = 0,
        Stunned = 1 << 0,
        Silenced = 1 << 1,
        Rooted = 1 << 2,
        Disarmed = 1 << 3,
        Invulnerable = 1 << 4
    }

    /// <summary>Описание удара. Финальный урон считает получатель (броня, сопротивление, неуязвимость).</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public Element Element;
        public Unit Source;
        public bool IsAttack;

        public DamageInfo(float amount, DamageType type, Unit source, bool isAttack = false, Element element = Element.None)
        {
            Amount = amount;
            Type = type;
            Source = source;
            IsAttack = isAttack;
            Element = element;
        }
    }

    public static class CombatMath
    {
        /// <summary>Множитель физического урона от брони (формула Dota 2).</summary>
        public static float ArmorMultiplier(float armor) =>
            1f - 0.06f * armor / (1f + 0.06f * Mathf.Abs(armor));

        public static bool IsEnemy(TeamSide a, TeamSide b) =>
            a != b && a != TeamSide.Neutral && b != TeamSide.Neutral;

        public static bool Matches(TeamFilter filter, TeamSide source, TeamSide target) => filter switch
        {
            TeamFilter.Enemies => IsEnemy(source, target),
            TeamFilter.Allies => source == target,
            _ => true
        };

        public static TeamSide Opponent(TeamSide side) =>
            side == TeamSide.Dawn ? TeamSide.Dusk : side == TeamSide.Dusk ? TeamSide.Dawn : TeamSide.Neutral;

        /// <summary>Значение по уровню (уровень с 1). Если массив короче — берётся последний элемент.</summary>
        public static float ByLevel(float[] values, int level)
        {
            if (values == null || values.Length == 0)
                return 0f;
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
        }

        /// <summary>Расстояние между краями юнитов по горизонтали.</summary>
        public static float EdgeDistance(Unit a, Unit b)
        {
            Vector3 d = b.Position - a.Position;
            d.y = 0f;
            return Mathf.Max(0f, d.magnitude - a.Radius - b.Radius);
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
