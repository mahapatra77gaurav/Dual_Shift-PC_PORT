using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;
#else
// PC/Web: PlayFab namespaces
using PlayFab;
using PlayFab.ClientModels;
#endif

public class LeaderboardManager : MonoBehaviour
{
    [Header("Custom UI")]
    [Tooltip("The panel containing the custom leaderboard UI")]
    [SerializeField] private GameObject customLeaderboardPanel;
    [Tooltip("Container for leaderboard rows")]
    [SerializeField] private Transform rowContainer;
    [Tooltip("Prefab for leaderboard row")]
    [SerializeField] private GameObject rowPrefab;
    [Tooltip("The row displaying the player's own score")]
    [SerializeField] private LeaderboardRowUI myScoreRow;

    public static LeaderboardManager Instance;

    [Header("Help UI")]
    [Tooltip("Button to show when player score is missing")]
    [SerializeField] private GameObject helpButton;
    [Tooltip("Popup explaining privacy settings")]
    [SerializeField] private GameObject helpPopup;

    [Header("PC Username UI")]
    public GameObject pcNamePanel;
    public TMP_InputField pcNameInput;

    public void OpenHelpPopup()
    {
        if (helpPopup != null) helpPopup.SetActive(true);
    }

    public void CloseHelpPopup()
    {
        if (helpPopup != null) helpPopup.SetActive(false);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

#if UNITY_ANDROID
        try
        {
            PlayGamesPlatform.DebugLogEnabled = true;
            PlayGamesPlatform.Activate();
            Social.Active = PlayGamesPlatform.Instance;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GPGS] Failed to Activate: " + e.Message);
        }
#endif
    }

    private void Start()
    {
#if UNITY_ANDROID
        try
        {
            Debug.Log("[GPGS] Starting Authentication...");
            PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GPGS] Failed to Authenticate: " + e.Message);
        }
#else
        // PC BEHAVIOR: Silent Login
        LoginToPlayFab();
#endif
    }

#if !UNITY_ANDROID
    // --- PLAYFAB LOGIN LOGIC ---
    private void LoginToPlayFab()
    {
        Debug.Log("PC/Mac: Attempting PlayFab Login...");

        // 1. Check if we already created an ID for this player
        string customId = PlayerPrefs.GetString("MyCustomPlayFabID", "");

        // 2. If they don't have one, generate a random secure ID and save it forever
        if (string.IsNullOrEmpty(customId))
        {
            customId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("MyCustomPlayFabID", customId);
            PlayerPrefs.Save();
        }

        // 3. Log in using our custom ID instead of the Mac Hardware ID
        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnPlayFabError);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("PC: PlayFab Login Successful!");
        CheckForPlayerName();
    }

    private void OnPlayFabError(PlayFabError error)
    {
        Debug.LogError("PC: PlayFab Error: " + error.GenerateErrorReport());
    }
#endif

#if UNITY_ANDROID
    private void ProcessAuthentication(SignInStatus status)
    {
        if (status == SignInStatus.Success)
        {
            Debug.Log("[GPGS] Authenticated successfully.");
            Social.Active = PlayGamesPlatform.Instance;
            SyncCloudScores();
        }
        else
        {
            Debug.LogError($"[GPGS] Authentication Failed. Status: {status}");
        }
        
        SettingsManager[] settingsFn = FindObjectsByType<SettingsManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var settings in settingsFn)
        {
             settings.UpdateGPGSButton();
        }
    }

    private void SyncCloudScores()
    {
        if (PlayGamesPlatform.Instance == null || !PlayGamesPlatform.Instance.IsAuthenticated()) return;

        PlayGamesPlatform.Instance.LoadScores(
            GPGSIds.leaderboard_high_scores,
            LeaderboardStart.PlayerCentered,
            1,
            LeaderboardCollection.Public,
            LeaderboardTimeSpan.AllTime,
            (data) =>
            {
                if (data.Valid && data.PlayerScore != null)
                {
                    long cloudScore = data.PlayerScore.value;
                    float localScore = PlayerPrefs.GetFloat("BestScore", 0);
                    if (cloudScore > localScore)
                    {
                        PlayerPrefs.SetFloat("BestScore", cloudScore);
                        PlayerPrefs.Save();
                    }
                }
            }
        );
    }
#endif

    public void SubmitScore(long score)
    {
#if UNITY_ANDROID
        try
        {
            if (PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated())
            {
                PlayGamesPlatform.Instance.ReportScore(score, GPGSIds.leaderboard_high_scores, (bool success) => {
                    if (success) Debug.Log($"[GPGS] SubmitScore Success: {score}");
                });
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[GPGS] SubmitScore error: " + e.Message); }
#else
        // PC BEHAVIOR: Submit to PlayFab
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = "HighScore", Value = (int)score }
            }
        };
        PlayFabClientAPI.UpdatePlayerStatistics(request, result => Debug.Log("PC: Score Submitted to PlayFab"), OnPlayFabError);
#endif
    }

    public void SubmitKills(long kills)
    {
#if UNITY_ANDROID
        try
        {
            if (PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated())
            {
                PlayGamesPlatform.Instance.ReportScore(kills, GPGSIds.leaderboard_max_kills, (bool success) => {
                     if (success) Debug.Log($"[GPGS] SubmitKills Success: {kills}");
                });
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[GPGS] SubmitKills error: " + e.Message); }
#else
        // PC BEHAVIOR: Submit Max Kills to PlayFab (Optional: Create a "MaxKills" statistic in PlayFab dashboard)
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = "MaxKills", Value = (int)kills }
            }
        };
        PlayFabClientAPI.UpdatePlayerStatistics(request, result => Debug.Log("PC: Kills Submitted to PlayFab"), OnPlayFabError);
#endif
    }

    public void ShowLeaderboard()
    {
#if UNITY_ANDROID
        try
        {
            if (PlayGamesPlatform.Instance == null) return;
            if (!PlayGamesPlatform.Instance.IsAuthenticated())
            {
                PlayGamesPlatform.Instance.ManuallyAuthenticate((status) => {
                    ProcessAuthentication(status);
                    if (status == SignInStatus.Success && customLeaderboardPanel != null)
                    {
                        customLeaderboardPanel.SetActive(true);
                        OpenHighScoreTab();
                    }
                });
                return;
            }
            if (customLeaderboardPanel != null) {
                customLeaderboardPanel.SetActive(true);
                OpenHighScoreTab();
            }
        }
        catch (System.Exception e) { Debug.LogError("[GPGS] ShowLeaderboard error: " + e.Message); }
#else
        // PC BEHAVIOR: Open the panel and fetch scores
        if (customLeaderboardPanel != null)
        {
            customLeaderboardPanel.SetActive(true);
            RefreshPlayFabBoard("HighScore");
        }
#endif
    }

    public void CloseLeaderboard()
    {
        if (customLeaderboardPanel != null)
            customLeaderboardPanel.SetActive(false);
    }

#if !UNITY_ANDROID
    // --- PC SPECIFIC REFRESH LOGIC ---
    public void RefreshPlayFabBoard(string statName)
    {
        var request = new GetLeaderboardRequest
        {
            StatisticName = statName,
            StartPosition = 0,
            MaxResultsCount = 10
        };

        PlayFabClientAPI.GetLeaderboard(request, result => {
            foreach (Transform child in rowContainer) Destroy(child.gameObject);

            foreach (var item in result.Leaderboard)
            {
                GameObject rowObj = Instantiate(rowPrefab, rowContainer);
                LeaderboardRowUI rowScript = rowObj.GetComponent<LeaderboardRowUI>();

                string displayName = string.IsNullOrEmpty(item.DisplayName) ? "Guest_" + item.PlayFabId.Substring(0, 5) : item.DisplayName;

                // Highlight the player's own row if it's them
                if (item.PlayFabId == result.Leaderboard[0].PlayFabId) { /* Optional logic */ }

                rowScript.SetData((item.Position + 1).ToString(), displayName, item.StatValue.ToString());
            }
        }, OnPlayFabError);
    }
#endif

    public void OpenHighScoreTab()
    {
#if UNITY_ANDROID
        RefreshBoard(GPGSIds.leaderboard_high_scores);
#else
        RefreshPlayFabBoard("HighScore");
#endif
    }

    public void OpenMaxKillsTab()
    {
#if UNITY_ANDROID
        RefreshBoard(GPGSIds.leaderboard_max_kills);
#else
        RefreshPlayFabBoard("MaxKills");
#endif
    }

    public void SignIn()
    {
#if UNITY_ANDROID
        try
        {
            if (PlayGamesPlatform.Instance != null && !PlayGamesPlatform.Instance.IsAuthenticated())
            {
                PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[GPGS] SignIn error: " + e.Message); }
#endif
    }

#if UNITY_ANDROID
    private void RefreshBoard(string leaderboardId)
    {
        if (PlayGamesPlatform.Instance == null) return;
        if (rowContainer != null) { foreach (Transform child in rowContainer) Destroy(child.gameObject); }

        PlayGamesPlatform.Instance.LoadScores(
            leaderboardId, LeaderboardStart.TopScores, 10, LeaderboardCollection.Public, LeaderboardTimeSpan.AllTime,
            (data) => {
                if (data.Valid) {
                    List<string> userIds = new List<string>();
                    foreach (var score in data.Scores) userIds.Add(score.userID);

                    ((ISocialPlatform)PlayGamesPlatform.Instance).LoadUsers(userIds.ToArray(), (users) => {
                        Dictionary<string, string> names = new Dictionary<string, string>();
                        if (users != null) { foreach (var user in users) names[user.id] = user.userName; }
                        foreach (var score in data.Scores) {
                            GameObject rowObj = Instantiate(rowPrefab, rowContainer);
                            LeaderboardRowUI rowScript = rowObj.GetComponent<LeaderboardRowUI>();
                            string displayName = names.ContainsKey(score.userID) ? names[score.userID] : score.userID;
                            rowScript.SetData(score.rank.ToString(), displayName, score.value.ToString());
                        }
                    });
                }
            }
        );
    }
#endif
#if !UNITY_ANDROID
    // 1. We call this after login to see if we need to show the popup
    public void CheckForPlayerName()
    {
        // If PlayerPrefs is 0, they haven't set a name yet
        if (PlayerPrefs.GetInt("HasSetPCName", 0) == 0)
        {
            if (pcNamePanel != null) pcNamePanel.SetActive(true);
        }
    }

    // 2. We link this directly to the "Save Name" Button
    public void SaveNameFromUI()
    {
        if (pcNameInput == null || string.IsNullOrEmpty(pcNameInput.text)) return;

        string chosenName = pcNameInput.text;

        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = chosenName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request, result => {
            Debug.Log("PC: Player Name updated to: " + result.DisplayName);

            // Save a flag so we never show the popup again
            PlayerPrefs.SetInt("HasSetPCName", 1);
            PlayerPrefs.Save();

            // Hide the UI panel
            if (pcNamePanel != null) pcNamePanel.SetActive(false);

        }, (PlayFab.PlayFabError error) => {
            Debug.LogError("PC: Name Update Error: " + error.GenerateErrorReport());
        });
    }
#endif
}
