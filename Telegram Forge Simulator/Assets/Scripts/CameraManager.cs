using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;
    private Camera cam;

    [Header("Zoom Settings")]
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 10f;

    [Header("Pan Settings")]
    public float panSpeed = 0f; // 0 means 1:1 movement with mouse
    public Vector2 minLimit = new Vector2(-10, -10);
    public Vector2 maxLimit = new Vector2(20, 20);

    private Vector3 dragOrigin;
    private bool isDragging = false;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // Default center on (0,0) chunk (approx 3,3 in world space for a 6x6 chunk)
        CenterOnChunk(0, 0);
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            float newSize = cam.orthographicSize - (scroll * zoomSpeed);
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }
    }

    void HandlePan()
    {
        // 1. Mouse Down - Start Drag
        if (Input.GetMouseButtonDown(0))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        // 2. Mouse Up - Stop Drag
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        // 3. Dragging
        if (isDragging)
        {
            Vector3 currentPos = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 difference = dragOrigin - currentPos; // Calculate delta

            Vector3 newPos = transform.position + difference;

            // Clamp position to limits
            newPos.x = Mathf.Clamp(newPos.x, minLimit.x, maxLimit.x);
            newPos.y = Mathf.Clamp(newPos.y, minLimit.y, maxLimit.y);

            transform.position = newPos;
        }
    }

    public void CenterOnChunk(int x, int y)
    {
        // A chunk is 6x6. The center is roughly (x*6 + 3, y*6 + 3)
        // We set z to -10 so the camera stays in front of the 2D plane
        float centerX = (x * 6) + 3f;
        float centerY = (y * 6) + 3f;
        transform.position = new Vector3(centerX, centerY, -10f);
    }
}