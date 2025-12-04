using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    public GameObject buttonPrefab;
    public Transform contentContainer;

    [Header("UI Panels")]
    public GameObject lobbyScrollView; // Drag your ScrollView here!

    [Header("Game Stage UI")]
    public GameObject gameStagePanel;
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI emojiDisplayText;

    [Header("UI Buttons")]
    public GameObject furnitureButton;
    public GameObject backHomeButton;

    [Header("NPCs")]
    public GameObject masterPrefab;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetEditMode(true);
        // We do NOT fetch list here anymore to avoid the "Self not hidden" race condition.
        // We will fetch it only when needed or after a short delay.
        StartCoroutine(FetchListWithDelay());
    }

    IEnumerator FetchListWithDelay()
    {
        // Wait 1 second for Login to finish so we know who "Self" is
        yield return new WaitForSeconds(1.0f);
        StartCoroutine(FetchUserList());
    }

    public void HireMaster()
    {
        // 1. Safety Checks
        if (GridManager.Instance == null || PathfindingManager.Instance == null)
        {
            Debug.LogError("Cannot hire master: Managers are missing.");
            return;
        }

        // 2. Refresh grid to ensure we don't spawn inside a wall or new furniture
        PathfindingManager.Instance.ScanMap();

        // 3. Find a random safe spot
        Vector2Int spawnNode = PathfindingManager.Instance.GetRandomWalkableNode();
        Vector3 spawnWorld = PathfindingManager.Instance.floorLayer.GetCellCenterWorld(new Vector3Int(spawnNode.x, spawnNode.y, 0));

        // 4. Spawn locally immediately
        Instantiate(masterPrefab, spawnWorld, Quaternion.identity);

        // 5. Save to Server
        StartCoroutine(SaveMaster(spawnWorld.x, spawnWorld.y));
    }

    IEnumerator SaveMaster(float x, float y)
    {
        string url = "https://telegram-econ-sim.onrender.com/hire_master";

        // Simple JSON payload
        MasterRequestData data = new MasterRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            x = x,
            y = y
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
            Debug.Log("Master Saved!");
            // Optionally refresh local user data
            TelegramManager.Instance.RequestUserData();
        }
        else
        {
            Debug.LogError("Failed to save master: " + request.error);
        }
    }

    // Helper class for JSON serialization
    [System.Serializable]
    class MasterRequestData
    {
        public long id;
        public float x;
        public float y;
    }

    public void RefreshList()
    {
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
        StartCoroutine(FetchUserList());
    }

    public void ToggleLobby()
    {
        if (lobbyScrollView != null)
            lobbyScrollView.SetActive(!lobbyScrollView.activeSelf);
    }

    public void GoHome()
    {
        Debug.Log("GoHome clicked!"); // <--- ADD THIS

        if (TelegramManager.Instance != null && TelegramManager.Instance.currentUser != null)
        {
            long myId = TelegramManager.Instance.currentUser.telegram_id;
            Debug.Log("Going to ID: " + myId); // <--- ADD THIS
            StartCoroutine(JoinUserInstance(myId));
        }
        else
        {
            Debug.LogError("Cannot Go Home: User ID is null. Are we logged in?");
        }
    }

    void SetEditMode(bool isHome)
    {
        if (furnitureButton != null) furnitureButton.SetActive(isHome);
        if (backHomeButton != null) backHomeButton.SetActive(!isHome);

        GameObject[] signs = GameObject.FindGameObjectsWithTag("ExpansionSign");
        foreach (GameObject sign in signs)
        {
            sign.SetActive(isHome);
        }
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
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Expansion Successful!");
            TelegramManager.Instance.RequestUserData();
        }
        else
        {
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

            // Get My ID safely
            long myId = -1;
            if (TelegramManager.Instance != null && TelegramManager.Instance.currentUser != null)
            {
                myId = TelegramManager.Instance.currentUser.telegram_id;
            }

            foreach (TelegramUser u in list.users)
            {
                // Filter out myself
                if (u.telegram_id == myId) continue;

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
            if (string.IsNullOrEmpty(request.downloadHandler.text)) yield break;

            TelegramUser targetUser = JsonUtility.FromJson<TelegramUser>(request.downloadHandler.text);

            // --- HIDE THE LIST AUTOMATICALLY ---
            if (lobbyScrollView != null)
                lobbyScrollView.SetActive(false);

            long myId = TelegramManager.Instance.currentUser.telegram_id;
            bool isHome = (targetId == myId);

            SetEditMode(isHome);

            if (gameStagePanel != null) gameStagePanel.SetActive(true);

            if (isHome)
                if (shopNameText != null) shopNameText.text = "My Shop";
                else
                if (shopNameText != null) shopNameText.text = "Visiting: " + targetUser.first_name;

            if (emojiDisplayText != null) emojiDisplayText.text = "";

            if (GridManager.Instance != null)
            {
                if (targetUser.owned_chunks == null) targetUser.owned_chunks = new Chunk[0];
                if (targetUser.objects_list == null) targetUser.objects_list = new ObjectData[0];

                GridManager.Instance.GenerateMap(targetUser.owned_chunks);
                GridManager.Instance.SpawnObjects(targetUser);
                GridManager.Instance.SpawnMasters(targetUser);
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