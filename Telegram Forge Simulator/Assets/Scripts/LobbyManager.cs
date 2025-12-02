using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform contentContainer;
    [Header("Game Stage UI")]
    public GameObject gameStagePanel;
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI emojiDisplayText;

    public static LobbyManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }
    void Start()
    {
        StartCoroutine(FetchUserList());
    }

    public void ExpandToChunk(int x, int y)
    {
        StartCoroutine(ExpandRoutine(x, y));
    }

    System.Collections.IEnumerator ExpandRoutine(int x, int y)
    {
        string url = "https://telegram-econ-sim.onrender.com/expand";

        // Create a temporary object to hold the data we want to send
        ExpandRequestData data = new ExpandRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            chunk_x = x,
            chunk_y = y
        };

        string json = JsonUtility.ToJson(data);

        UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.Log("Expansion Successful!");

            // Reload the map to show the new room!
            // We re-login basically to get fresh data
            TelegramManager.Instance.RequestUserData();
        }
        else
        {
            Debug.LogError("Expansion Failed: " + request.error);
        }
    }

    [System.Serializable]
    public class ExpandRequestData
    {
        public long id;
        public int chunk_x;
        public int chunk_y;
    }

    IEnumerator JoinUserInstance(long targetId)
    {
        string url = "https://telegram-econ-sim.onrender.com/user/" + targetId;
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            TelegramUser targetUser = JsonUtility.FromJson<TelegramUser>(request.downloadHandler.text);

            // Update UI
            gameStagePanel.SetActive(true);
            shopNameText.text = "Visiting Shop: " + targetUser.first_name;
            //emojiDisplayText.text = ""; // Clear old text

            // GENERATE THE MAP
            if (GridManager.Instance != null)
            {
                GridManager.Instance.GenerateMap(targetUser.owned_chunks);
            }

            if (CameraManager.Instance != null)
            {
                // We assume everyone starts at 0,0 for now
                CameraManager.Instance.CenterOnChunk(0, 0);
            }
        }
        else
        {
            Debug.LogError("Failed to join instance: " + request.error);
        }
    }



    IEnumerator FetchUserList()
    {
        string url = "https://telegram-econ-sim.onrender.com/users";
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // We need a wrapper because JsonUtility cannot parse top-level arrays
            string json = "{\"users\":" + request.downloadHandler.text + "}";
            UserList list = JsonUtility.FromJson<UserList>(json);

            foreach (TelegramUser u in list.users)
            {
                // Don't show myself in the list
                if (TelegramManager.Instance != null && u.telegram_id == TelegramManager.Instance.currentUser.telegram_id)
                    continue;

                CreateUserButton(u);
            }
        }
        else
        {
            Debug.LogError("Failed to fetch users: " + request.error);
        }
    }

    public void RefreshList()
    {
        // Clear existing buttons first to avoid duplicates
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        Debug.Log("Refreshing list...");
        StartCoroutine(FetchUserList());
    }

    void CreateUserButton(TelegramUser user)
    {
        GameObject btn = Instantiate(buttonPrefab, contentContainer);
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();

        // Display Name
        txt.text = string.IsNullOrEmpty(user.username) ? user.first_name : user.username;

        // Setup Click Event (We will implement the logic in the next step)
        btn.GetComponent<Button>().onClick.AddListener(() => OnUserClicked(user.telegram_id));
    }

    void OnUserClicked(long targetId)
    {
        Debug.Log("Clicked on user: " + targetId);
        StartCoroutine(JoinUserInstance(targetId));
    }
}

// Helper class for JSON parsing
[System.Serializable]
public class UserList
{
    public TelegramUser[] users;
}