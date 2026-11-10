using LastStand.Core;
using UnityEngine;

namespace LastStand.Units
{
    /// <summary>
    /// Процедурная анимация для low-poly заглушек без скелета: шаги, замах оружием, каст, прыжок, смерть.
    /// Когда появятся модели с анимациями, этот компонент заменится на Animator с теми же вызовами.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class UnitAnimator : MonoBehaviour
    {
        [Tooltip("Корень визуальной модели (смещается и наклоняется).")]
        [SerializeField] private Transform model;
        [Tooltip("Шарнир оружия (вращается при атаке).")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Vector3 swingAxis = Vector3.right;
        [SerializeField] private float swingBackAngle = -75f;
        [SerializeField] private float swingStrikeAngle = 55f;
        [SerializeField] private float bobAmplitude = 0.07f;
        [SerializeField] private float stepLength = 1.6f;
        [SerializeField] private float leanAngle = 6f;
        [SerializeField] private float deathSink = 1.4f;

        private Unit unit;
        private UnitMotor motor;
        private Vector3 modelBasePos;
        private Quaternion modelBaseRot;
        private Quaternion weaponBaseRot;

        private float walkPhase;
        private float lean;
        private float attackStart = -10f;
        private float attackWindup = 0.3f;
        private float castStart = -10f;
        private float castDuration = 0.3f;
        private float deathStart = -1f;
        private float airHeight;
        private float hitFlash;

        public Transform Model => model;

        public void Configure(Transform modelRoot, Transform weapon)
        {
            model = modelRoot;
            weaponPivot = weapon;
            CacheBase();
        }

        private void Awake()
        {
            unit = GetComponent<Unit>();
            motor = GetComponent<UnitMotor>();
            CacheBase();
        }

        private void CacheBase()
        {
            if (model != null)
            {
                modelBasePos = model.localPosition;
                modelBaseRot = model.localRotation;
            }
            if (weaponPivot != null)
                weaponBaseRot = weaponPivot.localRotation;
        }

        private void OnEnable()
        {
            unit.Died += OnDied;
            unit.Revived += OnRevived;
            unit.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            unit.Died -= OnDied;
            unit.Revived -= OnRevived;
            unit.Damaged -= OnDamaged;
        }

        public void PlayAttack(float windup)
        {
            attackStart = Time.time;
            attackWindup = Mathf.Max(0.05f, windup);
        }

        public void CancelAttack() => attackStart = -10f;

        public void PlayCast(float duration)
        {
            castStart = Time.time;
            castDuration = Mathf.Max(0.15f, duration);
        }

        public void SetAirHeight(float height) => airHeight = height;

        private void OnDied(Unit u, Unit killer) => deathStart = Time.time;

        private void OnRevived(Unit u)
        {
            deathStart = -1f;
            attackStart = castStart = -10f;
        }

        private void OnDamaged(Unit u, DamageInfo info, float amount)
        {
            if (amount > 0f) hitFlash = 1f;
        }

        private void LateUpdate()
        {
            if (model == null)
                return;

            float dt = Time.deltaTime;
            Vector3 pos = modelBasePos;
            Quaternion rot = modelBaseRot;

            if (deathStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - deathStart) / 0.6f);
                float sink = Mathf.Clamp01((Time.time - deathStart - 1.2f) / 1.6f);
                rot = modelBaseRot * Quaternion.Euler(Mathf.SmoothStep(0f, 82f, t), 0f, 0f);
                pos += Vector3.down * (deathSink * sink) + Vector3.up * (0.25f * t);
                model.SetLocalPositionAndRotation(pos, rot);
                return;
            }

            // Ходьба: подпрыгивание и наклон вперёд по скорости.
            float speed = motor != null ? CombatMath.Flat(motor.Velocity).magnitude : 0f;
            walkPhase += speed / Mathf.Max(0.1f, stepLength) * Mathf.PI * dt;
            float moving = Mathf.Clamp01(speed / Mathf.Max(0.1f, unit.MoveSpeed));
            pos.y += Mathf.Abs(Mathf.Sin(walkPhase)) * bobAmplitude * moving;
            lean = Mathf.Lerp(lean, leanAngle * moving, 1f - Mathf.Exp(-10f * dt));

            // Отдача от попадания.
            hitFlash = Mathf.Max(0f, hitFlash - dt * 6f);
            float flinch = hitFlash * hitFlash * 5f;

            // Каст: юнит вытягивается и чуть приподнимается.
            float castT = (Time.time - castStart) / (castDuration + 0.25f);
            float castPose = castT >= 0f && castT <= 1f ? Mathf.Sin(castT * Mathf.PI) : 0f;
            pos.y += castPose * 0.12f;

            pos.y += airHeight;
            rot = modelBaseRot * Quaternion.Euler(lean - flinch - castPose * 8f, 0f, 0f);
            model.SetLocalPositionAndRotation(pos, rot);

            if (weaponPivot != null)
                weaponPivot.localRotation = weaponBaseRot * Quaternion.AngleAxis(WeaponAngle() - castPose * 60f, swingAxis);
        }

        private float WeaponAngle()
        {
            float t = Time.time - attackStart;
            if (t < 0f)
                return 0f;
            if (t < attackWindup)
                return Mathf.Lerp(0f, swingBackAngle, Ease(t / attackWindup));
            t -= attackWindup;
            const float strike = 0.09f, recover = 0.28f;
            if (t < strike)
                return Mathf.Lerp(swingBackAngle, swingStrikeAngle, t / strike);
            t -= strike;
            if (t < recover)
                return Mathf.Lerp(swingStrikeAngle, 0f, Ease(t / recover));
            return 0f;
        }

        private static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
