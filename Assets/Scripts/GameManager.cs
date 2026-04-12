using UnityEngine;
using UnityEngine.UI; // Added to access the Button component
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Menu, Playing, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Settings")]
    public AnimationCurve speedCurve;
    public float lateGameRamp = 0.5f;
    public float worldSpeed;
    public float comboDuration = 2.0f;

    [Header("UI References")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText; [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameHUD;

    [SerializeField] private PlayerController playerController; [Header("PC Taunt Setup")]
    [Tooltip("Drag the Text object inside your Revive Button here")]
    [SerializeField] private TMP_Text reviveButtonText;
    [Tooltip("Drag the Revive Button object itself here")]
    [SerializeField] private Button reviveButton;

    private float score;
    private int kills;

    private int comboMultiplier;
    private float comboTimer;
    private bool isPaused = false;
    private float levelTime = 0f;
    private int deathCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (!TryGetComponent<VisualsInstaller>(out var visualInstaller))
        {
            gameObject.AddComponent<VisualsInstaller>();
        }
        if (!TryGetComponent<MaterialUpgrader>(out var matUpgrader))
        {
            gameObject.AddComponent<MaterialUpgrader>();
        }

        StyleScoreText();
        ShowMainMenu();
    }

    private void StyleScoreText()
    {
        if (scoreText == null) return;

        scoreText.enableVertexGradient = true;
        scoreText.colorGradient = new VertexGradient(
            new Color(0.4f, 0.9f, 1f),
            new Color(0.4f, 0.9f, 1f),
            new Color(1f, 1f, 1f),
            new Color(0.8f, 0.9f, 1f)
        );

        scoreText.characterSpacing = 2f;
    }

    public void ShowMainMenu()
    {
        CurrentState = GameState.Menu;

        score = 0;
        kills = 0;
        comboMultiplier = 0;
        levelTime = 0f;

        SetPanelActive(mainMenuPanel, true);
        gameHUD.SetActive(false);
        SetPanelActive(pausePanel, false);

        worldSpeed = 0f;
        Time.timeScale = 1;
    }

    public void StartGame()
    {
        CurrentState = GameState.Playing;
        levelTime = 0f;
        worldSpeed = speedCurve.Evaluate(0f);

        SetPanelActive(mainMenuPanel, false);
        gameHUD.SetActive(true);
    }

    private void Update()
    {
        if (CurrentState != GameState.Playing) return;
        levelTime += Time.deltaTime;

        if (speedCurve.length > 0)
        {
            float curveDuration = speedCurve.keys[speedCurve.length - 1].time;
            if (levelTime <= curveDuration)
            {
                worldSpeed = speedCurve.Evaluate(levelTime);
            }
            else
            {
                float lastSpeed = speedCurve.Evaluate(curveDuration);
                float timePassedSinceEnd = levelTime - curveDuration;
                worldSpeed = lastSpeed + (timePassedSinceEnd * lateGameRamp);
            }
        }

        float currentMultiplier = 1 + comboMultiplier;
        score += (worldSpeed * Time.deltaTime) * currentMultiplier;

        if (comboTimer > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                comboMultiplier = 0;
                UpdateUI();
            }
        }
        UpdateUI();
    }

    public void AddKill()
    {
        kills++;
        comboMultiplier++;
        comboTimer = comboDuration;
        UpdateUI();
    }

    public void GameOver()
    {
        if (CurrentState == GameState.GameOver) return;

        StartCoroutine(GameOverSequence());
    }

    private System.Collections.IEnumerator GameOverSequence()
    {
        int currentDeaths = PlayerPrefs.GetInt("AdDeathCount", 0);
        currentDeaths++;
        PlayerPrefs.SetInt("AdDeathCount", currentDeaths);

        if (currentDeaths % 3 == 0)
        {
            if (AdManager.Instance != null)
            {
                AdManager.Instance.ShowInterstitial();
            }
        }

        CurrentState = GameState.GameOver;

        CameraShake.Instance.Shake(1.2f, 0.5f);
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(1.0f);
        Time.timeScale = 0;

        float bestScore = PlayerPrefs.GetFloat("BestScore", 0);
        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetFloat("BestScore", score);
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.SubmitScore((long)score);
            }
        }

        int maxKills = PlayerPrefs.GetInt("MaxKills", 0);
        if (kills > maxKills)
        {
            maxKills = kills;
            PlayerPrefs.SetInt("MaxKills", kills);
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.SubmitKills((long)kills);
            }
        }

        gameHUD.SetActive(false);
        SetPanelActive(gameOverPanel, true);

        finalScoreText.text = $"SCORE: {(int)score}\n" +
                              $"BEST: {(int)bestScore}\n\n" +
                              $"KILLS: {kills}\n" +
                              $"MAX KILLS: {maxKills}";
    }

    public void RestartGame()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TogglePause()
    {
        if (CurrentState != GameState.Playing) return;

        isPaused = !isPaused;
        SetPanelActive(pausePanel, isPaused);
        Time.timeScale = isPaused ? 0 : 1;
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1;
        RestartGame();
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null) return;

        if (panel.TryGetComponent<UIAnimator>(out var animator))
        {
            if (active) animator.Show();
            else animator.Hide();
        }
        else
        {
            panel.SetActive(active);
        }
    }

    private int lastDisplayedScore = -1;
    private int lastDisplayedCombo = -1;

    private void UpdateUI()
    {
        int currentScore = (int)score;
        if (currentScore != lastDisplayedScore)
        {
            if (scoreText != null)
                scoreText.SetText("SCORE\n<size=150%>{0}</size>", currentScore);
            lastDisplayedScore = currentScore;
        }

        int displayMult = 1 + comboMultiplier;
        if (displayMult != lastDisplayedCombo)
        {
            if (comboText != null)
            {
                comboText.SetText("x{0}", displayMult);
                comboText.gameObject.SetActive(displayMult > 1);
            }
            lastDisplayedCombo = displayMult;
        }
    }

    public void OnReviveButtonClicked()
    {
#if UNITY_ANDROID
    // --- MOBILE BEHAVIOR (Normal) ---
    try
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedWithLoading((bool success) => {
                if (success) RevivePlayer(); 
            });
        }
    }
    catch (System.Exception e) { Debug.LogError("Revive Error: " + e.Message); }
#else
        string[] trollMessages = {
        "GIT GUD",
        "SKILL ISSUE",
        "No ads to save you now.",
        "NICE TRY LOL",
        "JUST RESTART"
    };

        // 2. Pick a random index from the list
        int randomIndex = UnityEngine.Random.Range(0, trollMessages.Length);
        string selectedMessage = trollMessages[randomIndex];

        // 3. Update the UI
        if (reviveButtonText != null)
        {
            reviveButtonText.text = selectedMessage;
            reviveButtonText.color = Color.red;
        }

        if (reviveButton != null)
        {
            // We keep it interactable for ONE click so they see the first message, 
            // then we disable it so they can't spam it.
            reviveButton.interactable = false;
        }

        Debug.Log($"PC Player Taunted with: {selectedMessage}");
#endif
    }

    private void RevivePlayer()
    {
        Debug.Log("RevivePlayer called.");

        if (gameOverPanel == null) Debug.LogError("RevivePlayer: gameOverPanel is NULL");
        else gameOverPanel.SetActive(false);

        if (gameHUD == null) Debug.LogError("RevivePlayer: gameHUD is NULL");
        else gameHUD.SetActive(true);

        CurrentState = GameState.Playing;
        Time.timeScale = 1;

        if (playerController != null)
        {
            Debug.Log("RevivePlayer: Resetting PlayerController.");
            playerController.ResetAnimationState();
            playerController.ActivateReviveInvincibility();
        }
        else
        {
            Debug.LogError("RevivePlayer: playerController is NULL! Cannot reset.");
        }
    }
}