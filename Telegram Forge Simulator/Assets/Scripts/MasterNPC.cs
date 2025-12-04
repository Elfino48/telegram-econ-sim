using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MasterNPC : MonoBehaviour
{
    public enum State { Idle, GoingToChest, Collecting, GoingToAnvil, Working, Depositing }
    public State currentState = State.Idle;

    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Sprite faceUp, faceDown, faceLeft, faceRight;
    public float moveSpeed = 2.0f;
    public float dollShakeSpeed = 10f;
    public float dollShakeAmount = 5f;

    [Header("Timings")]
    public float workDuration = 10f;
    public float collectDuration = 3f;

    [Header("Positioning Tweaks")]
    [Tooltip("How close to the target point the NPC stops (Lower = Closer)")]
    public float stopDistance = 0.1f;

    [Tooltip("Offset from the furniture pivot where the NPC should stand. (0, -1) means 1 tile below.")]
    public Vector2 interactionOffset = new Vector2(0, -0.7f); // Try -0.5 or -0.2 to get closer

    // Current Targets
    private ChestController myChest;
    private AnvilController myAnvil;

    private List<Vector2Int> currentPath;
    private bool isMoving = false;

    void Start()
    {
        StartCoroutine(LifeCycle());
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

    IEnumerator DoIdle()
    {
        ReleaseResources();

        myChest = FindBestChest();

        if (myChest != null)
        {
            myChest.isOccupied = true;

            myAnvil = FindBestAnvil();
            if (myAnvil != null)
            {
                myAnvil.isOccupied = true;
                currentState = State.GoingToChest;
                yield break;
            }
            else
            {
                myChest.isOccupied = false;
                myChest = null;
            }
        }

        Vector2Int randomSpot = PathfindingManager.Instance.GetRandomWalkableNode();
        yield return StartCoroutine(MoveToTarget(randomSpot));
        yield return new WaitForSeconds(2f);
    }

    // --- APPROACH LOGIC ---
    IEnumerator ApproachObject(Transform target)
    {
        if (target == null) yield break;

        // Calculate the exact world position we want to stand at
        Vector3 exactTargetPos = target.position + (Vector3)interactionOffset;

        // Walk towards that point until within 'stopDistance'
        while (Vector3.Distance(transform.position, exactTargetPos) > stopDistance)
        {
            Vector3 dir = (exactTargetPos - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            SetDirection(dir);
            AnimateDoll();
            yield return null;
        }

        // Final snap logic (optional, keeps them crisp)
        // transform.position = exactTargetPos; 

        transform.rotation = Quaternion.identity;
    }

    IEnumerator DoCollecting()
    {
        if (myChest != null) yield return StartCoroutine(ApproachObject(myChest.transform));

        SetDirection(Vector2.up);

        // SAFETY CHECK 1: Is chest still valid?
        if (myChest == null || myChest.gameObject == null)
        {
            currentState = State.Idle;
            yield break;
        }

        myChest.SetOpenState(true);
        yield return new WaitForSeconds(collectDuration);

        // SAFETY CHECK 2: Did it get destroyed while we waited?
        if (myChest == null || myChest.gameObject == null)
        {
            currentState = State.Idle;
            yield break;
        }

        myChest.resourceCount--;
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

        // SAFETY CHECK
        if (myAnvil == null || myAnvil.gameObject == null)
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

    IEnumerator MoveToTarget(Vector2Int gridTarget)
    {
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

    // UPDATED: Now uses the offset logic to find the best grid cell to walk to
    Vector2Int GetGridTarget(Vector3 targetWorldPos)
    {
        // Add the offset (e.g. -0.7 Y) to find the tile "below" or "near" the object
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