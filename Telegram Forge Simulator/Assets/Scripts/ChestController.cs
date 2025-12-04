using UnityEngine;
using TMPro;

public class ChestController : SmartObject
{
    [Header("Chest Settings")]
    public int resourceCount = 10;
    public TextMeshProUGUI textMesh;
    public Sprite openSprite;
    public Sprite closedSprite;
    public SpriteRenderer spriteRenderer;

    public override void UpdateVisuals()
    {
        // RESET LOGIC: Ensure we start with 10 if data is missing or on load
        if (!customData.ContainsKey("resources"))
        {
            customData["resources"] = "10";
        }

        // Parse current value
        int.TryParse(customData["resources"], out resourceCount);

        if (textMesh != null)
            textMesh.text = $"Res: {resourceCount}";

        spriteRenderer.sprite = closedSprite;
    }

    public bool HasResources()
    {
        return resourceCount > 0;
    }

    public void SetOpenState(bool isOpen)
    {
        spriteRenderer.sprite = isOpen ? openSprite : closedSprite;
    }
}