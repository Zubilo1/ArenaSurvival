using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArenaSurvival
{
    public enum GameState { Playing, Paused, Victory, Defeat }

    /// <summary>Состояние игры: игра / пауза / победа / поражение, счётчик убийств, перезапуск, выход в меню.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>true, пока идёт активная игра (не пауза и не конец игры).</summary>
        public static bool IsPlaying => Instance != null && Instance.State == GameState.Playing;

        [SerializeField] private PlayerHealth player;
        [SerializeField] private string menuSceneName = "MainMenu";

        public GameState State { get; private set; } = GameState.Playing;
        public int Kills { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> KillsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            if (player != null) player.Died += OnPlayerDied;
            else Debug.LogWarning("GameManager: не назначен PlayerHealth — поражение не будет обработано.", this);
        }

        private void OnDestroy()
        {
            if (player != null) player.Died -= OnPlayerDied;
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (GameInput.PausePressed) TogglePause();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void AddKill()
        {
            Kills++;
            KillsChanged?.Invoke(Kills);
        }

        public void WinGame()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Victory);
        }

        public void LoseGame()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Defeat);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(menuSceneName))
                SceneManager.LoadScene(menuSceneName);
            else
                Debug.LogWarning("GameManager: сцена '" + menuSceneName + "' не найдена в Build Settings.", this);
        }

        private void OnPlayerDied() => LoseGame();

        private void SetState(GameState newState)
        {
            if (State == newState) return;
            State = newState;
            Time.timeScale = newState == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(newState);
        }
    }
}
