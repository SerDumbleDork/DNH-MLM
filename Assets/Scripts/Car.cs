using UnityEngine;

public class Car : MonoBehaviour
{
    public float rollForce = 10f;
    public float maxSpeed = 3f;

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

    bool grounded = false;

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

        if (!grounded)
            return;

        if (rb.angularVelocity > -maxSpeed * 50f)
            rb.AddTorque(-rollForce, ForceMode2D.Force);

        if (rb.linearVelocity.magnitude < maxSpeed)
            rb.AddForce(Vector2.right * rollForce * 0.2f, ForceMode2D.Force);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        grounded = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.y > 0.5f)
            {
                grounded = true;
                break;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        grounded = false;
    }
}
