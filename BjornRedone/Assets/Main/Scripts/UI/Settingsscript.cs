using UnityEngine;
using UnityEngine.UI;

public class Settingsscript : MonoBehaviour
{
    [SerializeField] private Button toggleButton;      // The button to listen to
    [SerializeField] private GameObject objectToToggle; // The object to show/hide

    private void Awake()
    {
        // Find button automatically if not assigned
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();

        // Optional: find the object if not assigned
        if (objectToToggle == null)
            objectToToggle = GameObject.Find("SliderObject"); // Replace with your slider name

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleObject);
    }

    private void ToggleObject()
    {
        if (objectToToggle != null)
        {
            objectToToggle.SetActive(!objectToToggle.activeSelf);
        }
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleObject);
    }
}
