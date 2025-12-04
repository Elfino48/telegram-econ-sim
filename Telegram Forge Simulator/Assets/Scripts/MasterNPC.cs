using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Needed for sorting

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
                    yield return StartCoroutine(MoveToTarget(GetInteractionPos(myChest.transform.position)));
                    if (!isMoving) currentState = State.Collecting;
                    break;
                case State.Collecting:
                    yield return StartCoroutine(DoCollecting());
                    break;
                case State.GoingToAnvil:
                    yield return StartCoroutine(MoveToTarget(GetInteractionPos(myAnvil.transform.position)));
                    if (!isMoving) currentState = State.Working;
                    break;
                case State.Working:
                    yield return StartCoroutine(DoWorking());
                    break;
                case State.Depositing:
                    yield return StartCoroutine(MoveToTarget(GetInteractionPos(myChest.transform.position)));
                    if (!isMoving) currentState = State.Idle;
                    break;
            }
            yield return null;
        }
    }

    IEnumerator DoIdle()
    {
        // 1. Release any held objects
        ReleaseResources();

        // 2. Find Nearest Available Chest with Resources
        myChest = FindBestChest();

        if (myChest != null)
        {
            // Reserve it immediately!
            myChest.isOccupied = true;

            // Now find an Anvil
            myAnvil = FindBestAnvil();
            if (myAnvil != null)
            {
                myAnvil.isOccupied = true; // Reserve anvil too
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

        // Wander if no work found
        Vector2Int randomSpot = PathfindingManager.Instance.GetRandomWalkableNode();
        yield return StartCoroutine(MoveToTarget(randomSpot));
        yield return new WaitForSeconds(2f);
    }

    ChestController FindBestChest()
    {
        var chests = FindObjectsOfType<ChestController>();

        // Sort by distance + Check availability
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

    IEnumerator DoCollecting()
    {
        SetDirection(Vector2.up);
        if (myChest != null)
        {
            myChest.SetOpenState(true);
            yield return new WaitForSeconds(collectDuration);

            // Deduct Logic
            myChest.resourceCount--;
            myChest.customData["resources"] = myChest.resourceCount.ToString(); // Save to memory
            myChest.UpdateVisuals(); // Update text
            myChest.SetOpenState(false);

            currentState = State.GoingToAnvil;
        }
        else
        {
            currentState = State.Idle; // Lost chest? Abort.
        }
    }

    IEnumerator DoWorking()
    {
        SetDirection(Vector2.up);
        yield return new WaitForSeconds(workDuration);
        currentState = State.Depositing;
    }

    // --- MOVEMENT (Same as before) ---
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

    Vector2Int GetInteractionPos(Vector3 targetWorldPos)
    {
        Vector3Int cell = PathfindingManager.Instance.floorLayer.WorldToCell(targetWorldPos);
        return new Vector2Int(cell.x, cell.y - 1);
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