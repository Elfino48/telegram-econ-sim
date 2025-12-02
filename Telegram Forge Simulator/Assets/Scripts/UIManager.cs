using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject lobbyScrollView; // The list we want to hide/show

    [Header("Buttons")]
    public Button toggleListButton;

    private bool isListVisible = false; // Or false if you want it hidden by default

    void Start()
    {
        // 1. Setup Button Listener
        if (toggleListButton != null)
        {
            toggleListButton.onClick.AddListener(ToggleLobbyList);
        }

        // 2. Set Initial State
        UpdateVisibility();
    }

    void ToggleLobbyList()
    {
        isListVisible = !isListVisible;
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        if (lobbyScrollView != null)
        {
            lobbyScrollView.SetActive(isListVisible);
        }
    }
}