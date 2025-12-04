using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class HiringUI : MonoBehaviour
{
    public static HiringUI Instance;

    [Header("UI Elements")]
    public GameObject panel;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText; // Assign text saying "Sold Out" or "Refreshing..."

    [Header("Slots")]
    public Transform slotsContainer;
    public GameObject slotPrefab; // Prefab with Name(TMP), Price(TMP), and Hire Button

    private bool isOpen = false;
    private bool refreshTriggered = false;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (statusText) statusText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isOpen && TelegramManager.Instance.currentUser != null)
        {
            UpdateTimer();
        }
    }

    public void TogglePanel()
    {
        isOpen = !isOpen;
        panel.SetActive(isOpen);

        if (isOpen)
        {
            RefreshUI();
        }
    }

    void UpdateTimer()
    {
        // 1. Get Target Time from Server Data
        if (TelegramManager.Instance.currentUser.hire_shop == null) return;

        long targetTime = TelegramManager.Instance.currentUser.hire_shop.next_refresh;

        // 2. Get Current Universal Time (Matches Server Date.now())
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long diff = targetTime - now;

        if (diff <= 0)
        {
            timerText.text = "00:00";

            // 3. Auto-Refresh Logic
            if (!refreshTriggered)
            {
                refreshTriggered = true;
                StartCoroutine(RefreshShopRoutine());
            }
        }
        else
        {
            refreshTriggered = false; // Reset flag so next refresh can happen

            System.TimeSpan ts = System.TimeSpan.FromMilliseconds(diff);
            // Format: 09:59
            timerText.text = string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
        }
    }

    IEnumerator RefreshShopRoutine()
    {
        if (statusText)
        {
            statusText.text = "Refreshing...";
            statusText.gameObject.SetActive(true);
        }

        // Ask server for fresh data (Server logic will detect time passed and generate new list)
        TelegramManager.Instance.RequestUserData();

        yield return new WaitForSeconds(1.0f); // Small visual delay

        RefreshUI();
    }

    public void RefreshUI()
    {
        // 1. Clear old slots
        foreach (Transform child in slotsContainer) Destroy(child.gameObject);

        // 2. Get Shop Data
        var shop = TelegramManager.Instance.currentUser.hire_shop;

        // Safety check
        if (shop == null || shop.candidates == null) return;

        if (shop.candidates.Length == 0)
        {
            if (statusText)
            {
                statusText.text = "All masters hired! Wait for refresh.";
                statusText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (statusText) statusText.gameObject.SetActive(false);

            // 3. Spawn Slots
            foreach (var candidate in shop.candidates)
            {
                GameObject slot = Instantiate(slotPrefab, slotsContainer);

                // Assuming Slot Prefab Structure:
                // - Text 1: Name
                // - Text 2: Price
                // - Button: Hire

                TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = candidate.name;
                if (texts.Length > 1) texts[1].text = candidate.price.ToString() + " G";

                Button btn = slot.GetComponentInChildren<Button>();

                // Capture values for lambda
                int idx = candidate.index;
                int cost = candidate.price;

                btn.onClick.AddListener(() => OnHireClicked(idx, cost));
            }
        }
    }

    void OnHireClicked(int index, int cost)
    {
        // 1. Gold Check
        if (TelegramManager.Instance.currentUser.gold < cost)
        {
            Debug.Log("Not enough gold!");
            // Optional: Shake UI or show red text
            return;
        }

        // 2. Execute Hire
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.HireSpecificMaster(index);
            TogglePanel(); // Close panel on success
        }
    }
}