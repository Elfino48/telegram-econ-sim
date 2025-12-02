using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    // --- NEW: SINGLETON PATTERN ---
    public static LobbyManager Instance;

    public GameObject buttonPrefab;
    public Transform contentContainer;

    [Header("Game Stage UI")]
    public GameObject gameStagePanel;
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI emojiDisplayText;

    void Awake()
    {
        Instance = this;
    }
    // -----------------------------

    void Start()
    {
        StartCoroutine(FetchUserList());
    }

    public void RefreshList()
    {
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
        StartCoroutine(FetchUserList());
    }

    public void ExpandToChunk(int x, int y)
    {
        StartCoroutine(ExpandRoutine(x, y));
    }

    IEnumerator ExpandRoutine(int x, int y)
    {
        string url = "https://telegram-econ-sim.onrender.com/expand";

        ExpandRequestData data = new ExpandRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            chunk_x = x,
            chunk_y = y
        };

        string json = JsonUtility.ToJson(data);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Expansion Successful!");

            if (TelegramManager.Instance.currentUser != null)
            {
                TelegramManager.Instance.UpdateDebugText();
            }

            TelegramManager.Instance.RequestUserData();
        }
        else
        {
            if (TelegramManager.Instance.debugText != null)
                TelegramManager.Instance.debugText.text = "Error: " + request.error;

            Debug.LogError("Expansion Failed: " + request.error);
        }
    }

    IEnumerator FetchUserList()
    {
        string url = "https://telegram-econ-sim.onrender.com/users";
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = "{\"users\":" + request.downloadHandler.text + "}";
            UserList list = JsonUtility.FromJson<UserList>(json);

            foreach (TelegramUser u in list.users)
            {
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

    void CreateUserButton(TelegramUser user)
    {
        GameObject btn = Instantiate(buttonPrefab, contentContainer);
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();

        txt.text = string.IsNullOrEmpty(user.username) ? user.first_name : user.username;

        btn.GetComponent<Button>().onClick.AddListener(() => OnUserClicked(user.telegram_id));
    }

    void OnUserClicked(long targetId)
    {
        if (TelegramManager.Instance != null && TelegramManager.Instance.debugText != null)
        {
            TelegramManager.Instance.debugText.text = "Loading user " + targetId + "...";
        }
        StartCoroutine(JoinUserInstance(targetId));
    }

    IEnumerator JoinUserInstance(long targetId)
    {
        string url = "https://telegram-econ-sim.onrender.com/user/" + targetId;

        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            TelegramUser targetUser = JsonUtility.FromJson<TelegramUser>(request.downloadHandler.text);

            gameStagePanel.SetActive(true);
            shopNameText.text = "Visiting Shop: " + targetUser.first_name;
            emojiDisplayText.text = "";

            if (GridManager.Instance != null)
            {
                GridManager.Instance.GenerateMap(targetUser.owned_chunks);
                GridManager.Instance.SpawnObjects(targetUser);
            }

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.CenterOnChunk(0, 0);
            }
        }
        else
        {
            Debug.LogError("Failed to join instance: " + request.error);
        }
    }

    [System.Serializable]
    public class UserList
    {
        public TelegramUser[] users;
    }

    [System.Serializable]
    public class ExpandRequestData
    {
        public long id;
        public int chunk_x;
        public int chunk_y;
    }
}