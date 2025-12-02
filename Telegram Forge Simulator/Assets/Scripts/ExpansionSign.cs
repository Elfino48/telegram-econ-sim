using UnityEngine;

public class ExpansionSign : MonoBehaviour
{
    public int chunkX;
    public int chunkY;
    public TMPro.TextMeshPro textMesh; // Optional: To show price

    void OnMouseDown()
    {
        Debug.Log($"Clicked Sign to buy chunk: {chunkX}, {chunkY}");

        // Call the expansion function (We will add this to LobbyManager next)
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.ExpandToChunk(chunkX, chunkY);
        }
    }
}