using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class returnbutton : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            // Subscribe our method to the button's click event
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogError("No Button component found on this GameObject!");
        }
    }

    private void OnButtonClick()
    {
        // Change scene
        SceneManager.LoadScene("BjornMenu");
    }
}