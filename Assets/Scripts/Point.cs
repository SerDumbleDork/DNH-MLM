using UnityEngine;

public class Point : MonoBehaviour
{
    public bool isAnchored = false;
    public bool destructable = true;

    [HideInInspector] public Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Anchored nodes NEVER move
        if (isAnchored)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
        else
        {
            // Free nodes start as Kinematic (build mode)
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }
}
