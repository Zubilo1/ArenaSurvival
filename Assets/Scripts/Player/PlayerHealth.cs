using System;
using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>Здоровье игрока (100 HP): получение урона, смерть, события для UI и GameManager.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private ProceduralRig rig;

        public int Current { get; private set; }
        public int Max => maxHealth;
        public bool IsDead => Current <= 0;

        /// <summary>(текущее здоровье, максимальное здоровье)</summary>
        public event Action<int, int> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
            if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            Current = Mathf.Max(0, Current - amount);
            HealthChanged?.Invoke(Current, maxHealth);

            if (Current == 0)
            {
                if (rig != null) rig.PlayDeath();
                Died?.Invoke();
            }
            else
            {
                AudioManager.Play(SoundId.PlayerHurt);
            }
        }
    }
}
