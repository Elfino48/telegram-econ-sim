using UnityEngine;

public class AnvilController : SmartObject
{
    [Header("Anvil Settings")]
    public GameObject workerShadow; // The circle sprite object

    public override void UpdateVisuals()
    {
        // Hide shadow by default when placed
        if (workerShadow != null)
            workerShadow.SetActive(false);
    }

    // Called by FurnitureManager when dragging
    public void ShowPreviewShadow(bool show)
    {
        if (workerShadow != null)
            workerShadow.SetActive(show);
    }
}