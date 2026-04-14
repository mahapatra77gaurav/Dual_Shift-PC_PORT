using UnityEngine;

public class UsernameButton : MonoBehaviour
{
    public GameObject userNameEntryPanel;

    public void ShowUserNameEntry()
    {
        userNameEntryPanel.SetActive(true);
    }
}
