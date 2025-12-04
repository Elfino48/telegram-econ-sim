using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

[System.Serializable]
public class Chunk
{
    public int x;
    public int y;
}

[System.Serializable]
public class SimpleData
{
    public string resources;
}

[System.Serializable]
public class ObjectData
{
    public float x;
    public float y;
    public string type_id;
    public SimpleData data; // <--- This was missing!
}

[System.Serializable]
public class MasterData
{
    public float x;
    public float y;
}

[System.Serializable]
public class TelegramUser
{
    public long id;
    public string first_name;
    public string username;
    public long telegram_id;
    public int gold;
    public Chunk[] owned_chunks;
    public ObjectData[] objects_list;
    public MasterData[] masters_list;
}

public class TelegramManager : MonoBehaviour
{
    public static TelegramManager Instance;
    public TelegramUser currentUser;
    public TextMeshProUGUI debugText;

    [DllImport("__Internal")]
    private static extern string GetTelegramUserData();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        RequestUserData();
    }

    public void RequestUserData()
    {
        string json = "";

#if UNITY_EDITOR
        json = "{\"id\": 99999, \"first_name\": \"Editor\", \"username\": \"editor_dev\"}";
#else
        try {
            json = GetTelegramUserData();
        } catch (System.Exception e) {
            if(debugText) debugText.text = "JS Error: " + e.Message;
            return;
        }
#endif

        if (debugText != null) debugText.text = "Raw JSON: " + json;

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                TelegramUser tempUser = JsonUtility.FromJson<TelegramUser>(json);
                StartCoroutine(LoginToServer(tempUser));
            }
            catch (System.Exception e)
            {
                if (debugText != null) debugText.text = "JSON Parse Error: " + e.Message;
            }
        }
        else
        {
            if (debugText != null) debugText.text = "Error: JSON was empty.";
        }
    }

    public void UpdateDebugText()
    {
        if (debugText != null && currentUser != null)
        {
            int chunkCount = currentUser.owned_chunks != null ? currentUser.owned_chunks.Length : 0;
            debugText.text = $"Welcome {currentUser.first_name}!\nGold: {currentUser.gold}\nChunks: {chunkCount}";
        }
    }

    IEnumerator LoginToServer(TelegramUser localUser)
    {
        string url = "https://telegram-econ-sim.onrender.com/login";

        string json = JsonUtility.ToJson(localUser);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            currentUser = JsonUtility.FromJson<TelegramUser>(request.downloadHandler.text);

            if (debugText != null)
            {
                UpdateDebugText();
            }

            if (GridManager.Instance != null)
            {
                GridManager.Instance.GenerateMap(currentUser.owned_chunks);
                GridManager.Instance.SpawnObjects(currentUser);
            }
        }
        else
        {
            if (debugText != null) debugText.text = "Error: " + request.error;
        }
    }
}