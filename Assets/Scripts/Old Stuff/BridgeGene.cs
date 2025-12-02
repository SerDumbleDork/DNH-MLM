using UnityEngine;

[System.Serializable]
public class BridgeGene
{
    public Vector2 start;
    public Vector2 end;
    public AIController.BarType type;
    [System.NonSerialized] public bool broken;
}