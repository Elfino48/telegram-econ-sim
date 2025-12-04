using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;
    private Camera cam;

    [Header("Zoom Settings")]
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 15f;
    // Mobile specific settings
    public float mobileZoomSpeed = 0.01f;

    [Header("Pan Settings")]
    public float panSpeed = 0f;
    public Vector2 minLimit = new Vector2(-10, -10);
    public Vector2 maxLimit = new Vector2(20, 20);

    private Vector3 dragOrigin;
    private bool isDragging = false;

    // NEW: Lock state to prevent camera movement when interacting with UI or Objects
    public bool isLocked = false;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        CenterOnChunk(0, 0);
    }

    void Update()
    {
        // Detect Platform
        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseInput();
        }
    }

    // NEW: Public method to toggle lock from other scripts
    public void SetLock(bool locked)
    {
        isLocked = locked;
        isDragging = false; // Force stop any current drag to prevent "sticking"
    }

    void HandleMouseInput()
    {
        // If locked, allow Zooming but block Panning? 
        // Usually better to block everything or just panning. Let's block panning.

        // 1. Desktop Zoom (Scroll Wheel) - Always allowed or check lock if desired
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            float newSize = cam.orthographicSize - (scroll * zoomSpeed);
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }

        if (isLocked) return;

        // 2. Desktop Pan (Mouse Drag)
        if (Input.GetMouseButtonDown(0))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 currentPos = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 difference = dragOrigin - currentPos;
            MoveCamera(difference);
        }
    }

    void HandleTouchInput()
    {
        // 1. Mobile Zoom (Two Finger Pinch)
        // We usually allow zooming even if locked (unless you want to strictly block it)
        if (Input.touchCount == 2)
        {
            // Stop panning if we are zooming
            isDragging = false;

            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            // Find the position in the previous frame of each touch.
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            // Find the magnitude of the vector (the distance) between the touches in each frame.
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // Find the difference in the distances between each frame.
            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            // Apply zoom
            float newSize = cam.orthographicSize + (deltaMagnitudeDiff * mobileZoomSpeed);
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }
        // 2. Mobile Pan (One Finger Drag)
        else if (Input.touchCount == 1)
        {
            if (isLocked) return; // Block panning if locked (e.g. dragging furniture)

            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                dragOrigin = cam.ScreenToWorldPoint(touch.position);
                isDragging = true;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                Vector3 currentPos = cam.ScreenToWorldPoint(touch.position);
                Vector3 difference = dragOrigin - currentPos;
                MoveCamera(difference);
            }
        }
    }

    void MoveCamera(Vector3 difference)
    {
        Vector3 newPos = transform.position + difference;

        // Clamp position to limits
        newPos.x = Mathf.Clamp(newPos.x, minLimit.x, maxLimit.x);
        newPos.y = Mathf.Clamp(newPos.y, minLimit.y, maxLimit.y);

        transform.position = newPos;
    }

    public void CenterOnChunk(int x, int y)
    {
        float centerX = (x * 6) + 3f;
        float centerY = (y * 6) + 3f;
        transform.position = new Vector3(centerX, centerY, -10f);
    }
}