using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResolutionManager : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Dropdown resolutionDropdown;

    private Resolution[] _allResolutions;
    private List<Resolution> _filteredResolutions = new List<Resolution>();

    private void Start()
    {
        BuildResolutionList();
        LoadSavedResolution();

        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void BuildResolutionList()
    {
        _allResolutions = Screen.resolutions;
        _filteredResolutions.Clear();
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        int savedIndex = 0;
        int savedWidth = PlayerPrefs.GetInt("ResWidth", Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt("ResHeight", Screen.currentResolution.height);

        // Filter: keep only the highest refresh rate per unique width×height
        var seen = new HashSet<string>();
        for (int i = 0; i < _allResolutions.Length; i++)
        {
            Resolution r = _allResolutions[i];
            string key = $"{r.width}x{r.height}";

            if (seen.Contains(key)) continue;
            seen.Add(key);

            _filteredResolutions.Add(r);
            options.Add(key);

            if (r.width == savedWidth && r.height == savedHeight)
                savedIndex = _filteredResolutions.Count - 1;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = savedIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void LoadSavedResolution()
    {
        // Apply the saved resolution at startup
        if (_filteredResolutions.Count > 0)
        {
            Resolution r = _filteredResolutions[resolutionDropdown.value];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        }
    }

    private void OnResolutionChanged(int index)
    {
        Resolution r = _filteredResolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);

        PlayerPrefs.SetInt("ResWidth", r.width);
        PlayerPrefs.SetInt("ResHeight", r.height);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
    }
}