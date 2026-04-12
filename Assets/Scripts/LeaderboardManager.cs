using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;
#endif

public class LeaderboardManager : MonoBehaviour
{
    [Header("Custom UI")]
    [Tooltip("The panel containing the custom leaderboard UI")]
    [SerializeField] private GameObject customLeaderboardPanel; [Tooltip("Container for leaderboard rows")]
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
            // Explicitly force the assignment to be sure
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
            // Use ManuallyAuthenticate to consistent behavior with button click
            PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GPGS] Failed to Authenticate: " + e.Message);
        }
#endif
    }

    // 2. Wrap this whole method because SignInStatus is a mobile-only variable type
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

        Debug.Log("[GPGS] Starting Score Sync...");

        // 1. Sync High Score
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

        // 2. Sync Max Kills
        PlayGamesPlatform.Instance.LoadScores(
             GPGSIds.leaderboard_max_kills,
             LeaderboardStart.PlayerCentered,
             1,
             LeaderboardCollection.Public,
             LeaderboardTimeSpan.AllTime,
             (data) =>
             {
                 if (data.Valid && data.PlayerScore != null)
                 {
                     long cloudKills = data.PlayerScore.value;
                     int localKills = PlayerPrefs.GetInt("MaxKills", 0);
                     
                     if (cloudKills > localKills)
                     {
                         PlayerPrefs.SetInt("MaxKills", (int)cloudKills);
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
                    else Debug.LogError($"[GPGS] SubmitScore Failed: {score}");
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GPGS] SubmitScore error: " + e.Message);
        }
#else
        Debug.Log($"[PC Build] Fake Submit Score: {score}. (Google Play is disabled on PC)");
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
                     else Debug.LogError($"[GPGS] SubmitKills Failed: {kills}");
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GPGS] SubmitKills error: " + e.Message);
        }
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
                    if (status == SignInStatus.Success)
                    {
                        if (customLeaderboardPanel != null)
                        {
                            customLeaderboardPanel.SetActive(true);
                            OpenHighScoreTab();
                        }
                    }
                });
                return;
            }

            if (customLeaderboardPanel != null)
            {
                customLeaderboardPanel.SetActive(true);
                OpenHighScoreTab();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GPGS] ShowLeaderboard error: " + e.Message);
        }
#else
        Debug.Log("[PC Build] Leaderboard button clicked, but leaderboards are disabled on PC.");
#endif
    }

    public void CloseLeaderboard()
    {
        if (customLeaderboardPanel != null)
            customLeaderboardPanel.SetActive(false);
    }

    public void OpenHighScoreTab()
    {
#if UNITY_ANDROID
        RefreshBoard(GPGSIds.leaderboard_high_scores);
#endif
    }

    public void OpenMaxKillsTab()
    {
#if UNITY_ANDROID
        RefreshBoard(GPGSIds.leaderboard_max_kills);
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
        catch (System.Exception e)
        {
            Debug.LogWarning("[GPGS] SignIn error: " + e.Message);
        }
#endif
    }

    // 3. Wrapped the GPGS dependent methods
#if UNITY_ANDROID
    private void RefreshBoard(string leaderboardId)
    {
        if (PlayGamesPlatform.Instance == null) return;
        
        if (rowContainer != null)
        {
            foreach (Transform child in rowContainer) Destroy(child.gameObject);
        }

        PlayGamesPlatform.Instance.LoadScores(
            leaderboardId,
            LeaderboardStart.TopScores,
            10,
            LeaderboardCollection.Public,
            LeaderboardTimeSpan.AllTime,
            (data) =>
            {
                if (data.Valid)
                {
                    List<string> userIds = new List<string>();
                    foreach (var score in data.Scores)
                    {
                        userIds.Add(score.userID);
                    }

                    ((ISocialPlatform)PlayGamesPlatform.Instance).LoadUsers(userIds.ToArray(), (users) =>
                    {
                        Dictionary<string, string> names = new Dictionary<string, string>();
                        
                        if (users != null)
                        {
                            foreach (var user in users) names[user.id] = user.userName;
                        }

                        foreach (var score in data.Scores)
                        {
                            GameObject rowObj = Instantiate(rowPrefab, rowContainer);
                            LeaderboardRowUI rowScript = rowObj.GetComponent<LeaderboardRowUI>();

                            string displayName = score.userID; 

                            if (names.ContainsKey(score.userID)) displayName = names[score.userID];
                            
                            if (score.userID == PlayGamesPlatform.Instance.localUser.id)
                            {
                                displayName = $"YOU ({PlayGamesPlatform.Instance.localUser.userName})";
                            }

                            rowScript.SetData(score.rank.ToString(), displayName, score.value.ToString());
                        }
                    });
                    
                    if (data.PlayerScore != null)
                    {
                        long cloudScore = data.PlayerScore.value;
                        long displayScore = cloudScore;

                        if (leaderboardId == GPGSIds.leaderboard_high_scores)
                        {
                            long localScore = (long)PlayerPrefs.GetFloat("BestScore", 0);
                            if (localScore > cloudScore) displayScore = localScore;
                        }
                        else if (leaderboardId == GPGSIds.leaderboard_max_kills)
                        {
                            long localKills = (long)PlayerPrefs.GetInt("MaxKills", 0);
                            if (localKills > cloudScore) displayScore = localKills;
                        }

                        myScoreRow.gameObject.SetActive(true);
                        myScoreRow.SetData(
                            data.PlayerScore.rank.ToString(),
                            $"YOU ({PlayGamesPlatform.Instance.localUser.userName})", 
                            displayScore.ToString()
                        );
                        
                        if (helpButton != null) helpButton.SetActive(false);
                    }
                    else
                    {
                        myScoreRow.gameObject.SetActive(false);
                        if (helpButton != null) helpButton.SetActive(true);
                    }
                }
            }
        );
    }
    
    public void FetchAndShowCustomUI()
    {
        if (!PlayGamesPlatform.Instance.IsAuthenticated()) return;

        customLeaderboardPanel.SetActive(true);
        foreach (Transform child in rowContainer) Destroy(child.gameObject);

        PlayGamesPlatform.Instance.LoadScores(
            GPGSIds.leaderboard_high_scores,
            LeaderboardStart.TopScores,
            10,
            LeaderboardCollection.Public,
            LeaderboardTimeSpan.AllTime,
            (data) =>
            {
                if (data.Valid)
                {
                    foreach (var score in data.Scores)
                    {
                        GameObject rowObj = Instantiate(rowPrefab, rowContainer);
                        LeaderboardRowUI rowScript = rowObj.GetComponent<LeaderboardRowUI>();
                        
                        rowScript.SetData(score.rank.ToString(), score.userID, score.value.ToString());
                    }
                }
            }
        );
    }
#endif
}