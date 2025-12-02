using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using System.Collections;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;

    [Header("Configuration")]
    public GameObject[] furniturePrefabs;
    public Tilemap floorLayer;
    public Tilemap wallLayer;

    private GameObject currentGhost;
    private int currentPrefabIndex;
    private bool isPlacing = false;

    void Awake()
    {
        Instance = this;
    }

    public void StartPlacementMode()
    {
        if (furniturePrefabs.Length == 0) return;

        currentPrefabIndex = Random.Range(0, furniturePrefabs.Length);
        GameObject prefab = furniturePrefabs[currentPrefabIndex];

        if (currentGhost != null) Destroy(currentGhost);

        currentGhost = Instantiate(prefab);
        SpriteRenderer sr = currentGhost.GetComponent<SpriteRenderer>();

        Color c = sr.color;
        c.a = 0.6f;
        sr.color = c;

        isPlacing = true;
    }

    void Update()
    {
        if (!isPlacing || currentGhost == null) return;

        Vector3 targetPos = Vector3.zero;

        if (Input.touchCount > 0)
        {
            targetPos = Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position);
        }
        else
        {
            targetPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }
        targetPos.z = 0;

        currentGhost.transform.position = targetPos;

        bool isValid = IsPositionValid(targetPos);
        SetGhostColor(isValid);

        if ((Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) && isValid)
        {
            if (!IsPointerOverUI())
            {
                PlaceObject(targetPos);
            }
        }
    }

    bool IsPositionValid(Vector3 pos)
    {
        Vector3Int cellPos = floorLayer.WorldToCell(pos);

        if (!floorLayer.HasTile(cellPos)) return false;

        if (wallLayer.HasTile(cellPos)) return false;

        return true;
    }

    void SetGhostColor(bool valid)
    {
        SpriteRenderer sr = currentGhost.GetComponent<SpriteRenderer>();
        if (valid) sr.color = new Color(0, 1, 0, 0.6f);
        else sr.color = new Color(1, 0, 0, 0.6f);
    }

    void PlaceObject(Vector3 pos)
    {
        isPlacing = false;

        GameObject realObj = Instantiate(furniturePrefabs[currentPrefabIndex], pos, Quaternion.identity);

        Destroy(currentGhost);

        StartCoroutine(SaveObjectRoutine(pos.x, pos.y, furniturePrefabs[currentPrefabIndex].name));
    }

    IEnumerator SaveObjectRoutine(float x, float y, string typeId)
    {
        string url = "https://telegram-econ-sim.onrender.com/place_object";

        PlaceRequestData data = new PlaceRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            x = x,
            y = y,
            type_id = typeId
        };

        string json = JsonUtility.ToJson(data);

        UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to save object: " + request.error);
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject() || (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId));
    }

    [System.Serializable]
    class PlaceRequestData { public long id; public float x; public float y; public string type_id; }
}