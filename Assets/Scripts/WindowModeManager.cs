using UnityEngine;
using TMPro;

public class WindowModeManager : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Dropdown windowModeDropdown;

    private void Start()
    {
        // Load saved preference (defaults to 0 = Fullscreen)
        int saved = PlayerPrefs.GetInt("WindowMode", 0);
        windowModeDropdown.value = saved;
        ApplyWindowMode(saved); // Apply on start

        // Listen for changes
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
    }

    private void OnWindowModeChanged(int index)
    {
        ApplyWindowMode(index);
        PlayerPrefs.SetInt("WindowMode", index);
        PlayerPrefs.Save();
    }

    private void ApplyWindowMode(int index)
    {
        switch (index)
        {
            case 0: // Fullscreen
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                Screen.fullScreen = true;
                break;

            case 1: // Borderless Fullscreen
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
                break;

            case 2: // Windowed
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.fullScreen = false;
                // Optional: set a default windowed resolution
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                break;
        }
    }

    private void OnDestroy()
    {
        windowModeDropdown.onValueChanged.RemoveListener(OnWindowModeChanged);
    }
}