using UnityEngine;
using System.Collections.Generic;

public class SmartObject : MonoBehaviour
{
    // Dictionary to hold custom data (resources, items, etc)
    public Dictionary<string, string> customData = new Dictionary<string, string>();
    public bool isOccupied = false; // NEW: Track if a master is using this

    // Called when loading from server
    public virtual void LoadData(Dictionary<string, string> data)
    {
        if (data != null) customData = data;
        UpdateVisuals();
    }

    // Override this in children (Chest, Anvil)
    public virtual void UpdateVisuals() { }
}