using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    [Header("List UI")]
    public GameObject buttonPrefab;
    public Transform contentContainer;
    public GameObject lobbyScrollView; // Drag your ScrollView here!

    [Header("Game Stage UI")]
    public GameObject gameStagePanel;
    public TextMeshProUGUI shopNameText;
    public TextMeshProUGUI emojiDisplayText;

    [Header("Main Buttons")]
    public GameObject furnitureButton; // Drag Furniture Button
    public GameObject backHomeButton;  // Drag Home Button
    public GameObject hireButton;      // Drag Hire Button (New)

    [Header("NPCs")]
    public GameObject masterPrefab; // Drag Master Prefab

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Default to Home mode
        SetEditMode(true);

        // Fetch list with slight delay to ensure login is complete
        StartCoroutine(FetchListWithDelay());
    }

    IEnumerator FetchListWithDelay()
    {
        yield return new WaitForSeconds(1.0f);
        StartCoroutine(FetchUserList());
    }

    // --- NAVIGATION LOGIC ---

    public void GoHome()
    {
        if (TelegramManager.Instance != null && TelegramManager.Instance.currentUser != null)
        {
            long myId = TelegramManager.Instance.currentUser.telegram_id;
            StartCoroutine(JoinUserInstance(myId));
        }
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

    // --- VISIBILITY LOGIC ---

    void SetEditMode(bool isHome)
    {
        // 1. Toggle UI Buttons
        if (furnitureButton != null) furnitureButton.SetActive(isHome);
        if (hireButton != null) hireButton.SetActive(isHome);
        if (backHomeButton != null) backHomeButton.SetActive(!isHome);

        // 2. Toggle Expansion Signs (World Objects)
        GameObject[] signs = GameObject.FindGameObjectsWithTag("ExpansionSign");
        foreach (GameObject sign in signs)
        {
            sign.SetActive(isHome);
        }
    }

    // --- HIRING LOGIC ---

    public void HireSpecificMaster(int candidateIndex)
    {
        if (GridManager.Instance == null || PathfindingManager.Instance == null) return;

        // 1. Refresh grid to ensure valid spawn point
        PathfindingManager.Instance.ScanMap();

        // 2. Find spawn spot
        Vector2Int spawnNode = PathfindingManager.Instance.GetRandomWalkableNode();
        Vector3 spawnWorld = PathfindingManager.Instance.floorLayer.GetCellCenterWorld(new Vector3Int(spawnNode.x, spawnNode.y, 0));

        // 3. Send to Server (We wait for server before spawning locally to get correct Name)
        StartCoroutine(SaveMaster(spawnWorld.x, spawnWorld.y, candidateIndex));
    }

    IEnumerator SaveMaster(float x, float y, int index)
    {
        string url = "https://telegram-econ-sim.onrender.com/hire_master";

        MasterRequestData data = new MasterRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            x = x,
            y = y,
            candidate_index = index
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
            Debug.Log("Master Hired!");
            // Reload user data. This will trigger SpawnObjects in GridManager automatically.
            TelegramManager.Instance.RequestUserData();
        }
        else
        {
            Debug.LogError("Failed to hire master: " + request.error);
        }
    }

    // --- EXPANSION LOGIC ---

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
            if (TelegramManager.Instance.debugText != null)
                TelegramManager.Instance.debugText.text = "Error: " + request.error;

            Debug.LogError("Expansion Failed: " + request.error);
        }
    }

    // --- USER LIST & VISITING ---

    IEnumerator FetchUserList()
    {
        string url = "https://telegram-econ-sim.onrender.com/users";
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = "{\"users\":" + request.downloadHandler.text + "}";
            UserList list = JsonUtility.FromJson<UserList>(json);

            long myId = -1;
            if (TelegramManager.Instance != null && TelegramManager.Instance.currentUser != null)
            {
                myId = TelegramManager.Instance.currentUser.telegram_id;
            }

            foreach (TelegramUser u in list.users)
            {
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
            if (string.IsNullOrEmpty(request.downloadHandler.text))
            {
                Debug.LogError("Received empty data");
                yield break;
            }

            TelegramUser targetUser = null;
            try
            {
                targetUser = JsonUtility.FromJson<TelegramUser>(request.downloadHandler.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("JSON Parse Error: " + e.Message);
                yield break;
            }

            // 1. Hide List
            if (lobbyScrollView != null) lobbyScrollView.SetActive(false);

            // 2. Check Ownership
            long myId = TelegramManager.Instance.currentUser.telegram_id;
            bool isHome = (targetId == myId);

            // 3. Set UI Mode
            SetEditMode(isHome);

            // 4. Update Header Text
            if (gameStagePanel != null) gameStagePanel.SetActive(true);

            if (isHome)
                if (shopNameText != null) shopNameText.text = "My Shop";
                else
                if (shopNameText != null) shopNameText.text = "Visiting: " + targetUser.first_name;

            if (emojiDisplayText != null) emojiDisplayText.text = "";

            // 5. Load Map & Objects
            if (GridManager.Instance != null)
            {
                if (targetUser.owned_chunks == null) targetUser.owned_chunks = new Chunk[0];
                if (targetUser.objects_list == null) targetUser.objects_list = new ObjectData[0];
                if (targetUser.masters_list == null) targetUser.masters_list = new MasterData[0];

                GridManager.Instance.GenerateMap(targetUser.owned_chunks);
                GridManager.Instance.SpawnObjects(targetUser);
            }

            // 6. Reset Camera
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

    [System.Serializable]
    class MasterRequestData
    {
        public long id;
        public float x;
        public float y;
        public int candidate_index;
    }
}