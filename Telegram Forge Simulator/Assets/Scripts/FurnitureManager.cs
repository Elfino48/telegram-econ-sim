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
    public GameObject furnitureSelectionPanel; // Assign your UI Panel here

    [Header("Placement Settings")]
    public float placementRadius = 1.0f;
    [Range(0, 0.5f)] public float paddingTop = 0.4f;    // 0.4 prevents clipping into North walls
    [Range(0, 0.5f)] public float paddingBottom = 0.1f;
    [Range(0, 0.5f)] public float paddingSides = 0.2f;

    private GameObject currentGhost;
    private int currentPrefabIndex;
    private bool isPlacing = false;

    // Drag State
    private bool isDraggingGhost = false;
    private Vector2 touchStartPos;
    private float dragThreshold = 10f; // Pixels required to count as a "Drag"

    void Awake()
    {
        Instance = this;
    }

    // --- UI METHODS ---

    public void ToggleFurniturePanel()
    {
        if (furnitureSelectionPanel != null)
            furnitureSelectionPanel.SetActive(!furnitureSelectionPanel.activeSelf);
    }

    // Connect this to your UI Buttons (0=Chest, 1=Anvil)
    public void SelectItemToPlace(int index)
    {
        if (index < 0 || index >= furniturePrefabs.Length) return;

        currentPrefabIndex = index;

        // Hide panel automatically
        if (furnitureSelectionPanel != null) furnitureSelectionPanel.SetActive(false);

        StartPlacementMode();
    }

    // --- PLACEMENT LOGIC ---

    void StartPlacementMode()
    {
        GameObject prefab = furniturePrefabs[currentPrefabIndex];

        if (currentGhost != null) Destroy(currentGhost);

        // Spawn in center of camera view (User friendly for mobile)
        Vector3 centerScreen = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
        centerScreen.z = 0;

        currentGhost = Instantiate(prefab, centerScreen, Quaternion.identity);

        // Setup Visuals (Semi-transparent)
        SpriteRenderer sr = currentGhost.GetComponent<SpriteRenderer>();
        Color c = sr.color;
        c.a = 0.6f;
        sr.color = c;
        sr.sortingOrder = 10; // High order to be always visible while dragging

        // Ensure collider exists for touch detection
        if (currentGhost.GetComponent<BoxCollider2D>() == null)
            currentGhost.AddComponent<BoxCollider2D>();

        // Special Logic: Anvil Shadow Preview
        AnvilController anvil = currentGhost.GetComponent<AnvilController>();
        if (anvil != null) anvil.ShowPreviewShadow(true);

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

                // Check if we touched the GHOST
                RaycastHit2D hit = Physics2D.Raycast(touchWorldPos, Vector2.zero);
                if (hit.collider != null && hit.collider.gameObject == currentGhost)
                {
                    isDraggingGhost = true;
                    // Lock camera so we don't pan map while dragging furniture
                    if (CameraManager.Instance != null) CameraManager.Instance.SetLock(true);
                }
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                if (isDraggingGhost)
                {
                    // Snap ghost to finger
                    currentGhost.transform.position = touchWorldPos;
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                // Unlock Camera
                if (CameraManager.Instance != null) CameraManager.Instance.SetLock(false);

                // TAP CHECK: Did we move significantly?
                float dist = Vector2.Distance(touch.position, touchStartPos);

                if (isDraggingGhost && dist < dragThreshold)
                {
                    // It was a TAP. Try to place.
                    if (IsPositionValid(currentGhost.transform.position))
                    {
                        PlaceObject(currentGhost.transform.position);
                    }
                }

                isDraggingGhost = false;
            }
        }
        // --- PC INPUT (Mouse) ---
        else if (Input.mousePresent)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            currentGhost.transform.position = mousePos;

            // Place on Click
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI() && IsPositionValid(mousePos))
            {
                PlaceObject(mousePos);
            }
        }

        // Update Green/Red Color
        bool isValid = IsPositionValid(currentGhost.transform.position);
        SetGhostColor(isValid);
    }

    bool IsPositionValid(Vector3 pos)
    {
        // 1. Force Z to 0 for strict 2D checks
        pos.z = 0;

        Vector3Int cellPos = floorLayer.WorldToCell(pos);

        // 2. Must be on Floor
        if (!floorLayer.HasTile(cellPos)) return false;

        // 3. Must NOT be on Wall
        if (wallLayer.HasTile(cellPos)) return false;

        // 4. Radius Check (Other Furniture)
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, placementRadius);
        foreach (var hit in hits)
        {
            if (hit.gameObject != currentGhost) return false;
        }

        // 5. PADDING CHECK (Prevents clipping into walls)
        Vector3 cellCenter = floorLayer.GetCellCenterWorld(cellPos);
        Vector3 diff = pos - cellCenter;

        // Check North (Top Wall Face)
        if (diff.y > (0.5f - paddingTop))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(0, 1, 0))) return false;
        }

        // Check South
        if (diff.y < -(0.5f - paddingBottom))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(0, -1, 0))) return false;
        }

        // Check Right
        if (diff.x > (0.5f - paddingSides))
        {
            if (wallLayer.HasTile(cellPos + new Vector3Int(1, 0, 0))) return false;
        }

        // Check Left
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
        pos.z = 0; // Force Z to 0

        GameObject realObj = Instantiate(furniturePrefabs[currentPrefabIndex], pos, Quaternion.identity);

        // Setup Collider (if missing) for future collision checks
        if (realObj.GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D col = realObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
        }

        // FIX SORTING: Set Order to 2 to match Walls (allows Y-sorting to work)
        if (realObj.GetComponent<SpriteRenderer>())
            realObj.GetComponent<SpriteRenderer>().sortingOrder = 2;

        // --- SMART DATA HANDLING ---
        Dictionary<string, string> data = new Dictionary<string, string>();

        // If Chest, set default resources
        ChestController chest = realObj.GetComponent<ChestController>();
        if (chest != null)
        {
            data.Add("resources", "10");
            chest.LoadData(data);
        }

        // If Anvil, hide shadow
        AnvilController anvil = realObj.GetComponent<AnvilController>();
        if (anvil != null)
        {
            anvil.ShowPreviewShadow(false);
        }

        Destroy(currentGhost);

        // Send to Server
        StartCoroutine(SaveObjectRoutine(pos.x, pos.y, furniturePrefabs[currentPrefabIndex].name, data));
        PathfindingManager.Instance.ScanMap(); 
    }

    IEnumerator SaveObjectRoutine(float x, float y, string typeId, Dictionary<string, string> customData)
    {
        string url = "https://telegram-econ-sim.onrender.com/place_object";

        // Simple data wrapper for JSON
        PlaceRequestData data = new PlaceRequestData
        {
            id = TelegramManager.Instance.currentUser.telegram_id,
            x = x,
            y = y,
            type_id = typeId,
            // Check if resources exist in dict, otherwise send null or empty
            data = (customData != null && customData.ContainsKey("resources")) ?
                   new SimpleData { resources = customData["resources"] } : new SimpleData()
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
    class PlaceRequestData
    {
        public long id;
        public float x;
        public float y;
        public string type_id;
        public SimpleData data;
    }

    [System.Serializable]
    class SimpleData { public string resources; }
}