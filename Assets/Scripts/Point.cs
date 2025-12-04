using UnityEngine;

public class Point : MonoBehaviour
{
    public bool isAnchored = false;
    public bool destructable = true;

    [HideInInspector] public Rigidbody2D rb;

    // NEW: Tracks structural connectivity
    [HideInInspector] public bool isFloating = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (isAnchored)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            isFloating = false; // Anchors are never floating
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Check if connected to anything at start
            UpdateFloating();
        }
    }

    // -------------------------------------------------------
    // NEW: Call this after a bar connects to this point
    // -------------------------------------------------------
    public void UpdateFloating()
    {
        // Anchors are always considered non-floating
        if (isAnchored)
        {
            isFloating = false;
            return;
        }

        // If the point has any DistanceJoint2D, it is connected
        DistanceJoint2D joint = GetComponent<DistanceJoint2D>();
        if (joint != null)
        {
            isFloating = false;
            return;
        }

        // If no joints found, it's floating
        isFloating = true;
    }
}
