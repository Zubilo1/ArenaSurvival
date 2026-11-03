using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArenaSurvival
{
    /// <summary>
    /// Атака мечом (ЛКМ). После нажатия запускается анимация, а через короткую задержку (момент
    /// взмаха) один раз проверяется область перед игроком: каждый монстр получает урон не более
    /// одного раза за удар. Есть дальность, угол, перезарядка и проверка стены между целью и игроком.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Параметры атаки")]
        [SerializeField, Min(1)] private int damage = 25;
        [SerializeField, Min(0.5f)] private float attackRange = 2.2f;
        [SerializeField, Range(10f, 360f)] private float attackArc = 110f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.6f;
        [Tooltip("Через сколько секунд после нажатия меч реально наносит урон (момент взмаха)")]
        [SerializeField, Min(0f)] private float hitDelay = 0.18f;

        [Header("Слои")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private LayerMask obstacleMask;

        [Header("Анимация")]
        [SerializeField] private ProceduralRig rig;

        private readonly HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();
        private float nextAttackTime;
        private float hitTime;
        private bool hitPending;

        private void Awake()
        {
            if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
            if (enemyMask.value == 0) enemyMask = LayerMask.GetMask("Enemy");
            if (obstacleMask.value == 0) obstacleMask = LayerMask.GetMask("Obstacle");
        }

        private void Update()
        {
            if (!GameManager.IsPlaying)
            {
                hitPending = false;
                return;
            }

            if (hitPending && Time.time >= hitTime)
            {
                hitPending = false;
                DealDamage();
            }

            if (GameInput.AttackPressed && Time.time >= nextAttackTime && !IsPointerOverUi())
                StartAttack();
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void StartAttack()
        {
            nextAttackTime = Time.time + attackCooldown;
            hitTime = Time.time + hitDelay;
            hitPending = true;

            if (rig != null) rig.PlayAttack();
            AudioManager.Play(SoundId.SwordSwing);
        }

        private void DealDamage()
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Collider[] colliders = Physics.OverlapSphere(origin, attackRange, enemyMask, QueryTriggerInteraction.Ignore);

            alreadyHit.Clear();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                EnemyHealth enemy = c.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !alreadyHit.Add(enemy)) continue;

                Vector3 center = c.bounds.center;
                Vector3 flat = center - origin;
                flat.y = 0f;

                // Монстр должен быть в секторе перед игроком...
                if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > attackArc * 0.5f) continue;
                // ...и не за стеной или колонной.
                if (Physics.Linecast(origin, center, obstacleMask, QueryTriggerInteraction.Ignore)) continue;

                enemy.TakeDamage(damage);
            }
        }
    }
}
