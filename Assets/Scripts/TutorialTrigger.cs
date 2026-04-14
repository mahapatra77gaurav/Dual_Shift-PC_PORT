using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [Tooltip("Text to display in the tutorial prompt")]
    [TextArea] public string textToDisplay = "SPACE TO SWITCH";
    [Tooltip("Type of action required (Switch/Attack)")]
    public string actionType = "Switch";

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.CompareTag("Player"))
        {
            if (PlayerPrefs.GetInt("TutorialDone", 0) == 0)
            {
                TutorialManager.Instance.TriggerTutorial(textToDisplay, actionType);
            }
            
            Destroy(gameObject);
        }
    }
}