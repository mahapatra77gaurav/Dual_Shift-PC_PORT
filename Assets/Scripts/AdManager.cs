using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

// 1. Wrap the AdMob namespaces
#if UNITY_ANDROID
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    // TEST IDs
    private string interstitialId = "ca-app-pub-2195761497058047/7260698795";
    private string rewardedId = "ca-app-pub-2195761497058047/4682636913";

    // 2. Wrap AdMob specific variables
#if UNITY_ANDROID
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;
#endif

    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private Action<bool> _onRewardedAdLoadComplete;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Log("<color=red>Background Action Crash: " + e.Message + "\n" + e.StackTrace + "</color>");
            }
        }
    }

    private void Start()
    {
        Log("Starting AdManager...");

#if UNITY_ANDROID
        var debugSettings = new ConsentDebugSettings
        {
            DebugGeography = DebugGeography.EEA,
            TestDeviceHashedIds = new List<string>() { "YOUR_DEVICE_HASH_HERE" } 
        };

        ConsentRequestParameters request = new ConsentRequestParameters
        {
            TagForUnderAgeOfConsent = false,
            ConsentDebugSettings = debugSettings
        };

        Log("Checking Consent...");
        ConsentInformation.Update(request, OnConsentInfoUpdated);
#else
        Log("PC Build: Ads and Consent forms are disabled.");
#endif
    }

    private void Log(string msg) { Debug.Log("[AdManager] " + msg); }

    // 3. Wrap Consent Info because 'FormError' is an AdMob class
#if UNITY_ANDROID
    private void OnConsentInfoUpdated(FormError error)
    {
        mainThreadActions.Enqueue(() =>
        {
            if (error != null) Log("Consent Info Error: " + error.Message);

            Log("Consent Info Updated. Loading Form...");
            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
            {
                mainThreadActions.Enqueue(() =>
                {
                    if (formError != null)
                    {
                        Log("Consent Form Error: " + formError.Message);
                        return;
                    }
                    
                    Log("CanRequestAds: " + ConsentInformation.CanRequestAds());

                    if (ConsentInformation.CanRequestAds())
                    {
                        Log("Initializing MobileAds...");
                        MobileAds.Initialize(initStatus =>
                        {
                            mainThreadActions.Enqueue(() => 
                            {
                                Log("MobileAds Initialized.");
                                LoadInterstitial();
                                LoadRewarded();
                            });
                        });
                    }
                    else
                    {
                        Log("Cannot Request Ads (Consent false).");
                    }
                });
            });
        });
    }
#endif

    // --- INTERSTITIAL ---
    public void LoadInterstitial()
    {
#if UNITY_ANDROID
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        Log("Loading Interstitial...");
        InterstitialAd.Load(interstitialId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                    Log($"Interstitial Load Failed: {error.GetMessage()}");
                    return;
            }
            Log("Interstitial Loaded.");
            interstitialAd = ad;
            interstitialAd.OnAdFullScreenContentClosed += LoadInterstitial;
        });
#endif
    }

    public void ShowInterstitial()
    {
#if UNITY_ANDROID
        if (interstitialAd != null && interstitialAd.CanShowAd()) interstitialAd.Show();
        else { Log("Interstitial not ready."); LoadInterstitial(); }
#else
        Log("PC Build: Skipping Interstitial Ad.");
#endif
    }

    // --- REWARDED ---
    public void LoadRewarded()
    {
#if UNITY_ANDROID
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        Log("Loading Rewarded...");
        RewardedAd.Load(rewardedId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            mainThreadActions.Enqueue(() => 
            {
                if (error != null)
                {
                     string err = $"Rewarded Load Failed: {error.GetMessage()} Code:{error.GetCode()}";
                     Log(err);
                     if(error.GetCode() == 3)
                     {
                         Log("<color=red>CODE 3 DETECTED: Account Config Issue!</color>");
                     }

                     _onRewardedAdLoadComplete?.Invoke(false);
                     _onRewardedAdLoadComplete = null;
                     return;
                }
                
                Log("Rewarded Loaded Successfully.");
                rewardedAd = ad;
                rewardedAd.OnAdFullScreenContentClosed += LoadRewarded;
                rewardedAd.OnAdFullScreenContentFailed += (AdError adError) =>
                {
                    Log("Rewarded Show Failed: " + adError.GetMessage());
                    LoadRewarded();
                };

                _onRewardedAdLoadComplete?.Invoke(true);
                _onRewardedAdLoadComplete = null;
            });
        });
#endif
    }

    public void ShowRewardedWithLoading(Action<bool> onReward)
    {
#if UNITY_ANDROID
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            ShowRewarded(onReward);
        }
        else
        {
            Log("Ad not ready. Showing Loading Screen...");
            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.ShowLoadingScreen("Loading Ad...");
            else
                Log("LoadingScreenManager not found!");
            
            _onRewardedAdLoadComplete = (bool success) => 
            {
                if (LoadingScreenManager.Instance != null)
                    LoadingScreenManager.Instance.HideLoadingScreen();

                if (success)
                {
                    Log("Ad loaded during wait. Showing now.");
                    ShowRewarded(onReward);
                }
                else
                {
                    Log("Ad failed to load after wait.");
                    onReward?.Invoke(false);
                }
            };

            LoadRewarded();
        }
#else
        Log("PC Build: Giving Reward instantly (No loading screen needed).");
        onReward?.Invoke(true);
#endif
    }

    public void ShowRewarded(Action<bool> onReward)
    {
#if UNITY_ANDROID
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            RewardedAd adToShow = rewardedAd;
            bool rewardEarned = false;
            
            void HandleAdClosed()
            {
                try
                {
                    if (adToShow != null)
                    {
                        adToShow.OnAdFullScreenContentClosed -= HandleAdClosed;
                    }

                    mainThreadActions.Enqueue(() => 
                    {
                        try
                        {
                            if (rewardEarned)
                            {
                                Log("User earned reward. Invoking callback...");
                                if (onReward == null) Log("onReward is NULL!");
                                else onReward.Invoke(true);
                            }
                            else
                            {
                                Log("User closed without reward. Invoking callback...");
                                onReward?.Invoke(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"<color=red>Crash invoking callback: {ex.Message}\n{ex.StackTrace}</color>");
                        }
                        
                        LoadRewarded();
                    });
                }
                catch (Exception e)
                {
                    mainThreadActions.Enqueue(() => 
                    {
                        Log($"<color=red>HandleAdClosed CRASHED: {e.Message}</color>");
                        onReward?.Invoke(rewardEarned); 
                        LoadRewarded();
                    });
                }
            }

            adToShow.OnAdFullScreenContentClosed += HandleAdClosed;
            adToShow.Show((Reward reward) => { rewardEarned = true; });
        }
        else
        {
            Log("Rewarded Ad not ready.");
            onReward?.Invoke(false);
            LoadRewarded();
        }
#else
        Log("PC Build: Denying reward to taunt the player.");
        onReward?.Invoke(false);
#endif
    }
}