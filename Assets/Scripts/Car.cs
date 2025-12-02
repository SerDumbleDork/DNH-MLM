using UnityEngine;

public class Car : MonoBehaviour
{
    public float maxSpeed = 3f;

    public bool isGrounded = false;
    public float groundedTime = 0f;

    Rigidbody2D rb;
    public float startX { get; private set; }

    public float DistanceTravelled
    {
        get
        {
            if (rb == null) return 0f;
            return rb.position.x - startX;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (rb != null)
            startX = rb.position.x;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (isGrounded)
            groundedTime += Time.fixedDeltaTime;
        else
            return;

        Vector2 v = rb.linearVelocity;
        v.x = maxSpeed;
        rb.linearVelocity = v;

        rb.angularVelocity = 0f;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        isGrounded = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.y > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}
