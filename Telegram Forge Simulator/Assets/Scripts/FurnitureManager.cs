using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;

    [Header("Configuration")]
    public GameObject[] furniturePrefabs;
    public Tilemap floorLayer;
    public Tilemap wallLayer;

    [Header("Placement Settings")]
    public float placementRadius = 1.0f;

    [Header("Wall Margins")]
    [Range(0, 0.5f)] public float paddingTop = 0.4f;    // Big margin for Top Wall (Face)
    [Range(0, 0.5f)] public float paddingBottom = 0.1f; // Small margin for Bottom
    [Range(0, 0.5f)] public float paddingSides = 0.2f;  // Medium margin for Left/Right

    private GameObject currentGhost;
    private int currentPrefabIndex;
    private bool isPlacing = false;
    private Vector2 touchStartPos;
    private bool isDragging = false;

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

        Vector3 centerScreen = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
        centerScreen.z = 0;

        currentGhost = Instantiate(prefab, centerScreen, Quaternion.identity);

        SpriteRenderer sr = currentGhost.GetComponent<SpriteRenderer>();
        Color c = sr.color;
        c.a = 0.6f;
        sr.color = c;

        // Ghost is always high priority to be visible
        sr.sortingOrder = 10;

        if (currentGhost.GetComponent<BoxCollider2D>() == null)
            currentGhost.AddComponent<BoxCollider2D>();

        isPlacing = true;
    }

    void Update()
    {
        if (!isPlacing || currentGhost == null) return;

        // --- MOBILE INPUT ---
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector3 touchWorldPos = Camera.main.ScreenToWorldPoint(touch.position);
            touchWorldPos.z = 0;

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
                isDragging = false;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                if (Vector2.Distance(touch.position, touchStartPos) > 10f)
                {
                    isDragging = true;
                    Vector3 delta = touch.deltaPosition * 0.01f;
                    Vector3 newPos = currentGhost.transform.position + new Vector3(delta.x, delta.y, 0);
                    newPos.z = 0;
                    currentGhost.transform.position = newPos;
                }
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                if (!isDragging)
                {
                    RaycastHit2D hit = Physics2D.Raycast(touchWorldPos, Vector2.zero);
                    if (hit.collider != null && hit.collider.gameObject == currentGhost)
                    {
                        if (IsPositionValid(currentGhost.transform.position))
                        {
                            PlaceObject(currentGhost.transform.position);
                        }
                    }
                }
            }
        }
        // --- PC INPUT ---
        else if (Input.mousePresent)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            currentGhost.transform.position = mousePos;

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI() && IsPositionValid(mousePos))
            {
                PlaceObject(mousePos);
            }
        }

        bool isValid = IsPositionValid(currentGhost.transform.position);
        SetGhostColor(isValid);
    }

    bool IsPositionValid(Vector3 pos)
    {
        Vector3Int cellPos = floorLayer.WorldToCell(pos);

        if (!floorLayer.HasTile(cellPos)) return false;
        if (wallLayer.HasTile(cellPos)) return false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, placementRadius);
        foreach (var hit in hits)
        {
            if (hit.gameObject != currentGhost) return false;
        }

        // --- NEW PADDING LOGIC ---
        Vector3 cellCenter = floorLayer.GetCellCenterWorld(cellPos);
        Vector3 diff = pos - cellCenter;

        // Check North Wall (Use paddingTop)
        if (diff.y > (0.5f - paddingTop))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(0, 1, 0))) return false;
        }
        // Check South Wall (Use paddingBottom)
        if (diff.y < -(0.5f - paddingBottom))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(0, -1, 0))) return false;
        }
        // Check Right Wall (Use paddingSides)
        if (diff.x > (0.5f - paddingSides))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(1, 0, 0))) return false;
        }
        // Check Left Wall (Use paddingSides)
        if (diff.x < -(0.5f - paddingSides))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(-1, 0, 0))) return false;
        }

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

        // 1. FORCE Z TO 0 STRICTLY
        pos.z = 0;

        GameObject realObj = Instantiate(furniturePrefabs[currentPrefabIndex], pos, Quaternion.identity);

        if (realObj.GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D col = realObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
        }

        // 2. FIX SORTING ORDER
        // Ensure it matches the Wall Layer (which is usually 2)
        // Since Y-Sorting is active (Custom Axis), setting them to the same Order ID
        // allows the Y-position to determine who is in front.
        realObj.GetComponent<SpriteRenderer>().sortingOrder = 2;

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