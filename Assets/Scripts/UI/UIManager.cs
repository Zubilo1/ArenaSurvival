using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvival
{
    /// <summary>Игровой интерфейс (на русском): здоровье, волна, убийства, отсчёт, пауза, победа, поражение.</summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Источники данных")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private WaveManager waveManager;

        [Header("HUD")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Text waveBanner;

        [Header("Панели")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Text victoryStatsText;

        [Header("Кнопки")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button victoryMenuButton;
        [SerializeField] private Button defeatRestartButton;
        [SerializeField] private Button defeatMenuButton;

        [Header("Оформление")]
        [SerializeField] private Color healthyColor = new Color(0.25f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField, Min(0.5f)] private float bannerDuration = 2f;

        private float bannerHideTime;

        private void Start()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.KillsChanged += OnKillsChanged;

                Bind(resumeButton, gameManager.Resume);
                Bind(pauseMenuButton, gameManager.LoadMainMenu);
                Bind(victoryRestartButton, gameManager.Restart);
                Bind(victoryMenuButton, gameManager.LoadMainMenu);
                Bind(defeatRestartButton, gameManager.Restart);
                Bind(defeatMenuButton, gameManager.LoadMainMenu);
            }
            if (player != null) player.HealthChanged += OnHealthChanged;
            if (waveManager != null)
            {
                waveManager.WaveStarted += OnWaveStarted;
                waveManager.CountdownChanged += OnCountdownChanged;
            }

            // Начальные значения.
            if (player != null) OnHealthChanged(player.Current, player.Max);
            OnKillsChanged(gameManager != null ? gameManager.Kills : 0);
            SetText(waveText, "Волна: 0 / " + (waveManager != null ? waveManager.TotalWaves : 3));
            OnCountdownChanged(waveManager != null ? waveManager.CountdownSeconds : 0);
            if (waveBanner != null) waveBanner.gameObject.SetActive(false);
            OnStateChanged(gameManager != null ? gameManager.State : GameState.Playing);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.KillsChanged -= OnKillsChanged;
            }
            if (player != null) player.HealthChanged -= OnHealthChanged;
            if (waveManager != null)
            {
                waveManager.WaveStarted -= OnWaveStarted;
                waveManager.CountdownChanged -= OnCountdownChanged;
            }
        }

        private void Update()
        {
            if (waveBanner != null && waveBanner.gameObject.activeSelf && Time.unscaledTime >= bannerHideTime)
                waveBanner.gameObject.SetActive(false);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void OnHealthChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            if (healthFill != null) healthFill.anchorMax = new Vector2(ratio, 1f);
            if (healthFillImage != null) healthFillImage.color = Color.Lerp(lowHealthColor, healthyColor, ratio);
            SetText(healthText, "HP: " + current + " / " + max);
        }

        private void OnKillsChanged(int kills)
        {
            SetText(killsText, "Убито: " + kills);
        }

        private void OnWaveStarted(int wave, int total)
        {
            SetText(waveText, "Волна: " + wave + " / " + total);
            if (waveBanner != null)
            {
                waveBanner.text = wave == total ? "ПОСЛЕДНЯЯ ВОЛНА" : "ВОЛНА " + wave;
                waveBanner.gameObject.SetActive(true);
                bannerHideTime = Time.unscaledTime + bannerDuration;
            }
        }

        private void OnCountdownChanged(int seconds)
        {
            if (countdownText == null) return;
            countdownText.gameObject.SetActive(seconds > 0);
            if (seconds > 0) countdownText.text = "Начало через: " + seconds;
        }

        private void OnStateChanged(GameState state)
        {
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (victoryPanel != null) victoryPanel.SetActive(state == GameState.Victory);
            if (defeatPanel != null) defeatPanel.SetActive(state == GameState.Defeat);

            if (state == GameState.Victory)
                SetText(victoryStatsText, "Убито монстров: " + (gameManager != null ? gameManager.Kills : 0));
        }

        private static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
