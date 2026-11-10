using LastStand.Abilities;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Controls
{
    /// <summary>
    /// Индикаторы прицеливания на земле в стиле Art Deco: кольцо дальности каста, область у курсора,
    /// линия снаряда, кольцо под выбранной целью. Золото — можно применить, красный — нельзя.
    /// </summary>
    public sealed class AbilityIndicator : MonoBehaviour
    {
        [SerializeField] private Material rangeMaterial;
        [SerializeField] private Material areaMaterial;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material targetMaterial;
        [SerializeField] private Color validColor = new Color(1f, 0.83f, 0.48f, 0.9f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.32f, 0.9f);
        [SerializeField] private Color rangeColor = new Color(1f, 0.86f, 0.55f, 0.55f);

        private GroundDecal range;
        private GroundDecal area;
        private GroundDecal line;
        private GroundDecal target;

        public void Configure(Material rangeMat, Material areaMat, Material lineMat, Material targetMat)
        {
            rangeMaterial = rangeMat;
            areaMaterial = areaMat;
            lineMaterial = lineMat;
            targetMaterial = targetMat;
        }

        private void Awake()
        {
            range = new GroundDecal("Range", rangeMaterial, transform, 1);
            area = new GroundDecal("Area", areaMaterial, transform, 2);
            line = new GroundDecal("Line", lineMaterial, transform, 2);
            target = new GroundDecal("Target", targetMaterial, transform, 3);
        }

        public void ShowAbility(Unit caster, AbilityDefinition def, int level, Vector3 cursor, Unit hovered)
        {
            if (caster == null || def == null)
            {
                Hide();
                return;
            }

            float castRange = def.CastRange(level);
            Vector3 origin = caster.Position;
            Vector3 toCursor = CombatMath.Flat(cursor - origin);
            Quaternion yaw = toCursor.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCursor) : caster.transform.rotation;

            switch (def.targeting)
            {
                case AbilityTargeting.Direction:
                {
                    range.Hide();
                    area.Hide();
                    target.Hide();
                    float width = Mathf.Max(0.6f, def.lineWidth);
                    Vector3 center = origin + yaw * Vector3.forward * (castRange * 0.5f);
                    line.Show(center, width, castRange, yaw, validColor, 0.3f);
                    break;
                }
                case AbilityTargeting.UnitTarget:
                {
                    line.Hide();
                    area.Hide();
                    float d = (castRange + caster.Radius) * 2f;
                    range.Show(origin, d, d, Quaternion.identity, rangeColor, 0.35f);
                    bool valid = hovered != null && def.IsValidTarget(caster, hovered);
                    if (hovered != null)
                    {
                        float size = hovered.Radius * 2f + 1.2f;
                        target.Show(hovered.Position, size, size, Quaternion.identity, valid ? validColor : invalidColor, 0.3f);
                    }
                    else
                        target.Hide();
                    break;
                }
                case AbilityTargeting.Point:
                {
                    line.Hide();
                    target.Hide();
                    float d = castRange * 2f;
                    range.Show(origin, d, d, Quaternion.identity, rangeColor, 0.35f);
                    float radius = def.AreaRadius(level);
                    if (radius > 0f)
                    {
                        bool inRange = toCursor.magnitude <= castRange;
                        area.Show(cursor, radius * 2f, radius * 2f, Quaternion.identity,
                            inRange ? validColor : validColor * new Color(1f, 1f, 1f, 0.55f), 0.3f);
                    }
                    else
                        area.Hide();
                    break;
                }
                default:
                {
                    line.Hide();
                    target.Hide();
                    range.Hide();
                    float radius = def.AreaRadius(level);
                    if (radius > 0f)
                        area.Show(origin, radius * 2f, radius * 2f, Quaternion.identity, validColor, 0.3f);
                    else
                        area.Hide();
                    break;
                }
            }
        }

        /// <summary>Кольцо дальности атаки героя (режим атаки с ходу).</summary>
        public void ShowAttackRange(Unit caster)
        {
            line.Hide();
            area.Hide();
            target.Hide();
            float d = (caster.AttackRange + caster.Radius) * 2f;
            range.Show(caster.Position, d, d, Quaternion.identity, invalidColor * new Color(1f, 1f, 1f, 0.7f), 0.35f);
        }

        public void Hide()
        {
            range?.Hide();
            area?.Hide();
            line?.Hide();
            target?.Hide();
        }
    }
}
