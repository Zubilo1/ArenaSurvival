using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>
    /// Простая процедурная анимация для моделей из примитивов (рыцарь и монстр).
    /// Состояния: Idle (стоит), Walk (идёт), Run (бежит), Attack (удар), Death (смерть).
    /// Внешних анимационных файлов не требуется — части тела поворачиваются кодом.
    /// </summary>
    public class ProceduralRig : MonoBehaviour
    {
        public enum AnimState { Idle, Walk, Run, Attack, Death }

        [Header("Части модели")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform body;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        [Header("Ходьба и бег (по скорости движения)")]
        [SerializeField, Min(0f)] private float walkThreshold = 0.1f;
        [SerializeField, Min(0f)] private float runThreshold = 4.5f;
        [SerializeField, Min(0.1f)] private float walkFrequency = 7f;
        [SerializeField, Min(0.1f)] private float runFrequency = 11f;
        [SerializeField, Range(0f, 80f)] private float walkSwing = 30f;
        [SerializeField, Range(0f, 80f)] private float runSwing = 50f;
        [SerializeField, Range(0f, 30f)] private float runLean = 12f;
        [SerializeField, Min(0f)] private float bobHeight = 0.04f;

        [Header("Атака")]
        [SerializeField, Min(0.1f)] private float attackDuration = 0.45f;
        [SerializeField] private float attackWindupAngle = 60f;
        [SerializeField] private float attackSwingAngle = -100f;

        [Header("Смерть")]
        [SerializeField, Min(0.1f)] private float deathDuration = 0.6f;

        public AnimState Current { get; private set; } = AnimState.Idle;

        private float speed;
        private float phase;
        private float attackTimer;
        private bool dead;
        private float deathTimer;
        private Vector3 bodyBasePosition;
        private float legL, legR, armL, armR, lean;

        private void Awake()
        {
            if (modelRoot == null) modelRoot = transform;
            if (body != null) bodyBasePosition = body.localPosition;
        }

        /// <summary>Текущая скорость движения (единиц в секунду) — по ней выбирается Idle / Walk / Run.</summary>
        public void SetSpeed(float value) => speed = value;

        public void PlayAttack()
        {
            if (!dead) attackTimer = attackDuration;
        }

        public void PlayDeath()
        {
            if (dead) return;
            dead = true;
            deathTimer = 0f;
            Current = AnimState.Death;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dead)
            {
                UpdateDeath(dt);
                return;
            }

            if (attackTimer > 0f) attackTimer -= dt;

            bool run = speed >= runThreshold;
            bool walk = speed >= walkThreshold;
            float frequency = run ? runFrequency : (walk ? walkFrequency : 1.5f);
            float swing = run ? runSwing : (walk ? walkSwing : 3f);

            phase += dt * frequency;
            float s = Mathf.Sin(phase);
            float k = 1f - Mathf.Exp(-18f * dt);

            float targetLegL = s * swing;
            float targetLegR = -s * swing;
            float targetArmL = -s * swing * 0.8f;
            float targetArmR = s * swing * 0.8f;

            bool attacking = attackTimer > 0f;
            float armRateR = k;
            if (attacking)
            {
                targetArmR = AttackAngle(1f - attackTimer / attackDuration);
                armRateR = 1f - Mathf.Exp(-40f * dt);
            }

            legL = Mathf.Lerp(legL, targetLegL, k);
            legR = Mathf.Lerp(legR, targetLegR, k);
            armL = Mathf.Lerp(armL, targetArmL, k);
            armR = Mathf.Lerp(armR, targetArmR, armRateR);
            lean = Mathf.Lerp(lean, run ? runLean : 0f, k);

            SetX(leftLeg, legL);
            SetX(rightLeg, legR);
            SetX(leftArm, armL);
            SetX(rightArm, armR);

            if (body != null)
            {
                body.localRotation = Quaternion.Euler(lean, 0f, 0f);
                float bob = walk ? Mathf.Abs(s) * bobHeight : Mathf.Sin(Time.time * 2f) * 0.01f;
                body.localPosition = bodyBasePosition + Vector3.up * bob;
            }

            Current = attacking ? AnimState.Attack : (run ? AnimState.Run : (walk ? AnimState.Walk : AnimState.Idle));
        }

        // Кривая удара: замах назад -> быстрый взмах вперёд -> возврат.
        private float AttackAngle(float t)
        {
            if (t < 0.25f) return Mathf.Lerp(0f, attackWindupAngle, t / 0.25f);
            if (t < 0.55f) return Mathf.Lerp(attackWindupAngle, attackSwingAngle, (t - 0.25f) / 0.3f);
            return Mathf.Lerp(attackSwingAngle, 0f, (t - 0.55f) / 0.45f);
        }

        private void UpdateDeath(float dt)
        {
            deathTimer += dt;
            float t = Mathf.Clamp01(deathTimer / deathDuration);
            float smooth = t * t * (3f - 2f * t);

            modelRoot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -90f, smooth), 0f, 0f);
            modelRoot.localPosition = Vector3.up * Mathf.Lerp(0f, 0.25f, smooth);

            SetX(leftLeg, Mathf.Lerp(legL, 0f, smooth));
            SetX(rightLeg, Mathf.Lerp(legR, 0f, smooth));
            SetX(leftArm, Mathf.Lerp(armL, -20f, smooth));
            SetX(rightArm, Mathf.Lerp(armR, -20f, smooth));
        }

        private static void SetX(Transform t, float angle)
        {
            if (t != null) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
