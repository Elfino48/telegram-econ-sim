using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro; // Needed for the Name Text

public class MasterNPC : MonoBehaviour
{
    public enum State { Idle, GoingToChest, Collecting, GoingToAnvil, Working, Depositing }
    public State currentState = State.Idle;

    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Sprite faceUp, faceDown, faceLeft, faceRight;
    public TextMeshPro nameText; // Drag the Text (TMP) child here

    [Header("Animation Settings")]
    public float moveSpeed = 2.0f;
    public float dollShakeSpeed = 10f;
    public float dollShakeAmount = 5f;

    [Header("Timings")]
    public float workDuration = 10f;
    public float collectDuration = 3f;

    [Header("Positioning Tweaks")]
    [Tooltip("How close to the target point the NPC stops (Lower = Closer)")]
    public float stopDistance = 0.1f;

    [Tooltip("Offset from the furniture pivot where the NPC should stand. (0, -0.7) means slightly below.")]
    public Vector2 interactionOffset = new Vector2(0, -0.7f);

    // Current Targets
    private ChestController myChest;
    private AnvilController myAnvil;

    private List<Vector2Int> currentPath;
    private bool isMoving = false;

    void Start()
    {
        StartCoroutine(LifeCycle());
    }

    public void SetDisplayName(string name)
    {
        if (nameText != null) nameText.text = name;
        this.name = "Master_" + name; // Update GameObject name for easier debugging
    }

    IEnumerator LifeCycle()
    {
        while (true)
        {
            switch (currentState)
            {
                case State.Idle:
                    yield return StartCoroutine(DoIdle());
                    break;
                case State.GoingToChest:
                    if (myChest == null) { currentState = State.Idle; break; }
                    yield return StartCoroutine(MoveToTarget(GetGridTarget(myChest.transform.position)));
                    if (!isMoving) currentState = State.Collecting;
                    break;
                case State.Collecting:
                    yield return StartCoroutine(DoCollecting());
                    break;
                case State.GoingToAnvil:
                    if (myAnvil == null) { currentState = State.Idle; break; }
                    yield return StartCoroutine(MoveToTarget(GetGridTarget(myAnvil.transform.position)));
                    if (!isMoving) currentState = State.Working;
                    break;
                case State.Working:
                    yield return StartCoroutine(DoWorking());
                    break;
                case State.Depositing:
                    if (myChest == null) { currentState = State.Idle; break; }
                    yield return StartCoroutine(MoveToTarget(GetGridTarget(myChest.transform.position)));
                    if (!isMoving) currentState = State.Idle;
                    break;
            }
            yield return null;
        }
    }

    // --- STATES ---

    IEnumerator DoIdle()
    {
        ReleaseResources();

        // 1. Find Nearest Available Chest
        myChest = FindBestChest();

        if (myChest != null)
        {
            myChest.isOccupied = true;

            // 2. Find Nearest Available Anvil
            myAnvil = FindBestAnvil();
            if (myAnvil != null)
            {
                myAnvil.isOccupied = true;
                currentState = State.GoingToChest;
                yield break;
            }
            else
            {
                // No anvil? Release chest and wait
                myChest.isOccupied = false;
                myChest = null;
            }
        }

        // Wander randomly if no work found
        if (PathfindingManager.Instance != null)
        {
            Vector2Int randomSpot = PathfindingManager.Instance.GetRandomWalkableNode();
            yield return StartCoroutine(MoveToTarget(randomSpot));
        }

        yield return new WaitForSeconds(2f);
    }

    IEnumerator DoCollecting()
    {
        // 1. Walk close to the object (Visual Fix)
        if (myChest != null) yield return StartCoroutine(ApproachObject(myChest.transform));

        SetDirection(Vector2.up);

        // Safety Check: Did chest vanish while we walked here?
        if (myChest == null)
        {
            currentState = State.Idle;
            yield break;
        }

        myChest.SetOpenState(true);
        yield return new WaitForSeconds(collectDuration);

        // Safety Check 2
        if (myChest == null)
        {
            currentState = State.Idle;
            yield break;
        }

        myChest.resourceCount--;

        // Update persistent data
        if (myChest.customData.ContainsKey("resources"))
            myChest.customData["resources"] = myChest.resourceCount.ToString();

        myChest.UpdateVisuals();
        myChest.SetOpenState(false);

        currentState = State.GoingToAnvil;
    }

    IEnumerator DoWorking()
    {
        if (myAnvil != null) yield return StartCoroutine(ApproachObject(myAnvil.transform));

        SetDirection(Vector2.up);

        if (myAnvil == null)
        {
            currentState = State.Idle;
            yield break;
        }

        yield return new WaitForSeconds(workDuration);
        currentState = State.Depositing;
    }

    // --- HELPERS ---

    ChestController FindBestChest()
    {
        var chests = FindObjectsOfType<ChestController>();
        // Find chests that have resources (>0) AND are not occupied by another master
        var validChests = chests.Where(c => !c.isOccupied && c.HasResources())
                                .OrderBy(c => Vector3.Distance(transform.position, c.transform.position));
        return validChests.FirstOrDefault();
    }

    AnvilController FindBestAnvil()
    {
        var anvils = FindObjectsOfType<AnvilController>();
        var validAnvils = anvils.Where(a => !a.isOccupied)
                                .OrderBy(a => Vector3.Distance(transform.position, a.transform.position));
        return validAnvils.FirstOrDefault();
    }

    void ReleaseResources()
    {
        if (myChest != null) { myChest.isOccupied = false; myChest = null; }
        if (myAnvil != null) { myAnvil.isOccupied = false; myAnvil = null; }
    }

    // --- MOVEMENT ---

    IEnumerator MoveToTarget(Vector2Int gridTarget)
    {
        if (PathfindingManager.Instance == null) yield break;

        Vector3Int cellPos = PathfindingManager.Instance.floorLayer.WorldToCell(transform.position);
        Vector2Int startPos = new Vector2Int(cellPos.x, cellPos.y);

        currentPath = PathfindingManager.Instance.FindPath(startPos, gridTarget);

        if (currentPath == null || currentPath.Count == 0)
        {
            isMoving = false;
            yield break;
        }

        isMoving = true;

        foreach (Vector2Int step in currentPath)
        {
            Vector3 worldStep = PathfindingManager.Instance.floorLayer.GetCellCenterWorld(new Vector3Int(step.x, step.y, 0));

            // Move smoothly to tile center
            while (Vector3.Distance(transform.position, worldStep) > 0.05f)
            {
                Vector3 dir = (worldStep - transform.position).normalized;
                transform.position += dir * moveSpeed * Time.deltaTime;
                SetDirection(dir);
                AnimateDoll();
                yield return null;
            }
            transform.position = worldStep;
        }

        transform.rotation = Quaternion.identity;
        isMoving = false;
    }

    // Walks directly towards the object (ignoring grid) for the final few steps
    IEnumerator ApproachObject(Transform target)
    {
        if (target == null) yield break;

        Vector3 exactTargetPos = target.position + (Vector3)interactionOffset;

        while (Vector3.Distance(transform.position, exactTargetPos) > stopDistance)
        {
            Vector3 dir = (exactTargetPos - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            SetDirection(dir);
            AnimateDoll();
            yield return null;
        }

        transform.rotation = Quaternion.identity;
    }

    Vector2Int GetGridTarget(Vector3 targetWorldPos)
    {
        if (PathfindingManager.Instance == null) return Vector2Int.zero;

        // Find the tile closest to our desired "Interaction Point" (Target + Offset)
        Vector3 idealStandingSpot = targetWorldPos + (Vector3)interactionOffset;
        Vector3Int cell = PathfindingManager.Instance.floorLayer.WorldToCell(idealStandingSpot);
        return new Vector2Int(cell.x, cell.y);
    }

    void SetDirection(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) spriteRenderer.sprite = (dir.x > 0) ? faceRight : faceLeft;
        else spriteRenderer.sprite = (dir.y > 0) ? faceUp : faceDown;
    }

    void AnimateDoll()
    {
        float rotZ = Mathf.Sin(Time.time * dollShakeSpeed) * dollShakeAmount;
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
    }
}