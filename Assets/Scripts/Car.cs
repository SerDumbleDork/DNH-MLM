using UnityEngine;

public class Car : MonoBehaviour
{
    public float maxSpeed = 6f;

    public bool isGrounded = false;
    public float groundedTime = 0f;

    Rigidbody2D rb;
    public float startX { get; private set; }

    public float DistanceTravelled
    {
        get { return rb.position.x - startX; }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        startX = rb.position.x;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        float speedX = isGrounded ? maxSpeed : 3f;

        if (isGrounded)
            groundedTime += Time.fixedDeltaTime;

        Vector2 v = rb.linearVelocity;
        v.x = speedX;
        rb.linearVelocity = v;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D col)
    {
        isGrounded = false;
    }
}
