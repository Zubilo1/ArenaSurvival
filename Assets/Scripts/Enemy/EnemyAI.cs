using System.Collections.Generic;
using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>
    /// ИИ монстра: ждёт -> замечает игрока (расстояние + прямая видимость) -> преследует ->
    /// подходит на дистанцию атаки -> бьёт с перезарядкой. Без NavMesh: простое движение к цели,
    /// отталкивание от других монстров (чтобы не слипались) и обход колонн.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(EnemyHealth))]
    public class EnemyAI : MonoBehaviour
    {
        private static readonly List<EnemyAI> ActiveEnemies = new List<EnemyAI>();

        [Header("Характеристики")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.8f;
        [SerializeField, Min(0f)] private float detectionRange = 18f;
        [SerializeField, Min(0.5f)] private float attackRange = 1.8f;
        [SerializeField, Min(0)] private int damage = 8;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;
        [Tooltip("Пауза перед первым ударом, когда монстр только подошёл")]
        [SerializeField, Min(0f)] private float firstAttackDelay = 0.5f;
        [Tooltip("Через сколько секунд после замаха удар реально наносит урон")]
        [SerializeField, Min(0f)] private float hitDelay = 0.35f;

        [Header("Движение")]
        [SerializeField, Min(30f)] private float rotationSpeed = 360f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(0.1f)] private float separationRadius = 1.4f;
        [SerializeField, Min(0f)] private float separationWeight = 1.2f;
        [SerializeField, Min(0.1f)] private float avoidDistance = 1.2f;

        [Header("Окружение и анимация")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private ProceduralRig rig;

        private CharacterController controller;
        private EnemyHealth health;
        private PlayerHealth target;
        private bool aggro;
        private bool wasInRange;
        private bool attackPending;
        private float nextAttackTime;
        private float hitTime;
        private float verticalVelocity;
        private float avoidSide = 1f;

        public EnemyHealth Health => health;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<EnemyHealth>();
            if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
            if (obstacleMask.value == 0) obstacleMask = LayerMask.GetMask("Obstacle");
        }

        private void OnEnable()
        {
            if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
        }

        /// <summary>Вызывается WaveManager при создании монстра.</summary>
        public void Initialize(PlayerHealth playerHealth)
        {
            target = playerHealth;
        }

        private void Update()
        {
            if (health.IsDead) return;

            if (target == null || target.IsDead || !GameManager.IsPlaying)
            {
                attackPending = false;
                if (rig != null) rig.SetSpeed(0f);
                Move(Vector3.zero);
                return;
            }

            Vector3 position = transform.position;
            Vector3 toTarget = target.transform.position - position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            Vector3 eye = position + Vector3.up * 1f;
            Vector3 targetEye = target.transform.position + Vector3.up * 1f;
            bool visible = !Physics.Linecast(eye, targetEye, obstacleMask, QueryTriggerInteraction.Ignore);

            if (!aggro && distance <= detectionRange && visible) aggro = true;

            if (!aggro)
            {
                if (rig != null) rig.SetSpeed(0f);
                Move(Vector3.zero);
                return;
            }

            FaceDirection(toTarget);

            // Удар наносит урон через hitDelay после замаха, если игрок всё ещё рядом и виден.
            if (attackPending && Time.time >= hitTime)
            {
                attackPending = false;
                if (distance <= attackRange * 1.4f && visible) target.TakeDamage(damage);
            }

            bool inRange = distance <= attackRange && visible;
            Vector3 velocity = Vector3.zero;

            if (inRange)
            {
                if (!wasInRange) nextAttackTime = Mathf.Max(nextAttackTime, Time.time + firstAttackDelay);

                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    hitTime = Time.time + hitDelay;
                    attackPending = true;
                    if (rig != null) rig.PlayAttack();
                }
            }
            else if (distance > 0.01f)
            {
                velocity = ChooseDirection(position, toTarget / distance) * moveSpeed;
            }

            wasInRange = inRange;
            if (rig != null) rig.SetSpeed(velocity.magnitude);
            Move(velocity);
        }

        private void Move(Vector3 horizontal)
        {
            if (controller == null || !controller.enabled) return;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }

        // Направление к игроку + отталкивание от соседей + обход препятствий.
        private Vector3 ChooseDirection(Vector3 position, Vector3 desired)
        {
            Vector3 separation = Vector3.zero;
            for (int i = 0; i < ActiveEnemies.Count; i++)
            {
                EnemyAI other = ActiveEnemies[i];
                if (other == this || other == null || other.health.IsDead) continue;

                Vector3 offset = position - other.transform.position;
                offset.y = 0f;
                float d = offset.magnitude;
                if (d > 0.001f && d < separationRadius)
                    separation += offset / d * (1f - d / separationRadius);
            }

            Vector3 direction = desired + separation * separationWeight;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = desired;
            direction.Normalize();

            Vector3 origin = position + Vector3.up * 0.9f;
            if (IsFree(origin, direction)) return direction;

            for (int step = 1; step <= 3; step++)
            {
                float angle = 45f * step;

                Vector3 first = Quaternion.Euler(0f, angle * avoidSide, 0f) * direction;
                if (IsFree(origin, first)) return first;

                Vector3 second = Quaternion.Euler(0f, -angle * avoidSide, 0f) * direction;
                if (IsFree(origin, second))
                {
                    avoidSide = -avoidSide;
                    return second;
                }
            }
            return direction;
        }

        private bool IsFree(Vector3 origin, Vector3 direction)
        {
            return !Physics.SphereCast(origin, 0.35f, direction, out RaycastHit _, avoidDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        }
    }
}
