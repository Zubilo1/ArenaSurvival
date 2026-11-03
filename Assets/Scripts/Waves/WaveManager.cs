using System;
using UnityEngine;

namespace ArenaSurvival
{
    [Serializable]
    public class WaveDefinition
    {
        [Min(1)] public int enemyCount = 3;
    }

    /// <summary>
    /// Управление волнами: отсчёт 3 с -> волна 1 (3 монстра) -> волна 2 (5) -> волна 3 (7) -> победа.
    /// Следующая волна начинается только когда все монстры предыдущей уничтожены.
    /// Работает на таймерах в Update (без корутин) и замирает на паузе и после конца игры.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        private enum Phase { Countdown, Spawning, Fighting, Waiting, Finished }

        [Header("Ссылки")]
        [SerializeField] private EnemyAI enemyPrefab;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform enemiesRoot;

        [Header("Волны")]
        [SerializeField] private WaveDefinition[] waves =
        {
            new WaveDefinition { enemyCount = 3 },
            new WaveDefinition { enemyCount = 5 },
            new WaveDefinition { enemyCount = 7 }
        };

        [Header("Тайминги и появление")]
        [SerializeField, Min(0f)] private float countdownDuration = 3f;
        [SerializeField, Min(0f)] private float delayBetweenWaves = 2f;
        [SerializeField, Min(0f)] private float spawnInterval = 0.4f;
        [SerializeField, Min(0f)] private float spawnJitter = 1f;
        [Tooltip("Слои, на которых нельзя появляться монстру (стены, колонны, игрок, другие монстры)")]
        [SerializeField] private LayerMask spawnBlockMask;

        private Phase phase = Phase.Countdown;
        private int waveIndex = -1;
        private int spawnedInWave;
        private int aliveEnemies;
        private float timer;
        private int spawnCursor;
        private int lastCountdownSecond = -1;
        private bool configured;

        public int CurrentWave => waveIndex + 1;
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int AliveEnemies => aliveEnemies;

        /// <summary>Сколько секунд осталось до первой волны (целое, 0 — отсчёт закончен).</summary>
        public int CountdownSeconds => phase == Phase.Countdown ? Mathf.Max(0, Mathf.CeilToInt(timer)) : 0;

        /// <summary>(номер волны с 1, всего волн)</summary>
        public event Action<int, int> WaveStarted;
        public event Action<int> CountdownChanged;

        private void Start()
        {
            configured = enemyPrefab != null && player != null && gameManager != null
                         && spawnPoints != null && spawnPoints.Length > 0
                         && waves != null && waves.Length > 0;

            if (!configured)
            {
                Debug.LogError("WaveManager: не все ссылки назначены (enemyPrefab / player / gameManager / spawnPoints / waves).", this);
                enabled = false;
                return;
            }

            timer = countdownDuration;
        }

        private void Update()
        {
            if (!configured || !GameManager.IsPlaying) return;

            switch (phase)
            {
                case Phase.Countdown:
                    timer -= Time.deltaTime;
                    NotifyCountdown();
                    if (timer <= 0f) StartNextWave();
                    break;

                case Phase.Waiting:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) StartNextWave();
                    break;

                case Phase.Spawning:
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                    {
                        SpawnEnemy();
                        spawnedInWave++;
                        timer = spawnInterval;
                        if (spawnedInWave >= waves[waveIndex].enemyCount) phase = Phase.Fighting;
                    }
                    break;

                case Phase.Fighting:
                    if (aliveEnemies <= 0) FinishWave();
                    break;
            }
        }

        private void NotifyCountdown()
        {
            int second = Mathf.Max(0, Mathf.CeilToInt(timer));
            if (second == lastCountdownSecond) return;
            lastCountdownSecond = second;
            CountdownChanged?.Invoke(second);
        }

        private void StartNextWave()
        {
            waveIndex++;
            spawnedInWave = 0;
            timer = 0f;
            phase = Phase.Spawning;
            CountdownChanged?.Invoke(0);
            WaveStarted?.Invoke(CurrentWave, TotalWaves);
        }

        private void FinishWave()
        {
            if (waveIndex >= waves.Length - 1)
            {
                phase = Phase.Finished;
                gameManager.WinGame();
            }
            else
            {
                phase = Phase.Waiting;
                timer = delayBetweenWaves;
            }
        }

        private void SpawnEnemy()
        {
            Vector3 position = FindSpawnPosition();
            Vector3 toPlayer = player.transform.position - position;
            toPlayer.y = 0f;
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

            EnemyAI enemy = Instantiate(enemyPrefab, position, rotation, enemiesRoot);
            enemy.Initialize(player);
            enemy.Health.Died += OnEnemyDied;
            aliveEnemies++;
        }

        private Vector3 FindSpawnPosition()
        {
            Vector3 result = spawnPoints[0].position;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Transform point = spawnPoints[spawnCursor % spawnPoints.Length];
                spawnCursor++;
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnJitter;
                result = point.position + new Vector3(jitter.x, 0f, jitter.y);
                if (!Physics.CheckSphere(result + Vector3.up * 1f, 0.6f, spawnBlockMask, QueryTriggerInteraction.Ignore))
                    return result;
            }
            return result;
        }

        private void OnEnemyDied(EnemyHealth enemy)
        {
            enemy.Died -= OnEnemyDied;
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            gameManager.AddKill();
        }
    }
}
