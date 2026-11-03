using System;
using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>Здоровье монстра (50 HP). Смерть сообщается ровно один раз, затем объект удаляется.</summary>
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 50;
        [SerializeField, Min(0.1f)] private float corpseLifetime = 1.5f;
        [SerializeField] private ProceduralRig rig;

        private int current;
        private bool isDead;

        public int Current => current;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;

        public event Action<EnemyHealth> Died;

        private void Awake()
        {
            current = maxHealth;
            if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0) return;

            current = Mathf.Max(0, current - amount);
            if (current == 0) Die();
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            // Выключаем ИИ и коллизии, чтобы труп не мешал и не атаковал.
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            if (rig != null) rig.PlayDeath();
            AudioManager.Play(SoundId.MonsterDeath);

            Died?.Invoke(this);
            Destroy(gameObject, corpseLifetime);
        }
    }
}
