using UnityEngine;

public class PhysicsSetup : MonoBehaviour
{
    public string beamLayerName = "Beam";
    public string roadLayerName = "Road";

    void Awake()
    {
        int a = LayerMask.NameToLayer(beamLayerName);
        int b = LayerMask.NameToLayer(roadLayerName);
        if (a >= 0 && b >= 0) Physics2D.IgnoreLayerCollision(a, b, true);
    }
}
