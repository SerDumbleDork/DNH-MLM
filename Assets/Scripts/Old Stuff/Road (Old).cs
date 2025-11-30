using UnityEngine;

public class RoadOld : MonoBehaviour
{
    [Header("Node References")]
    public Point startPoint;
    public Point endPoint;

    [Header("Visual Settings")]
    public float thickness = 0.3f;
    public Color startColor = Color.white;
    public Color endColor = Color.red;

    [Header("Breaking Settings")]
    public bool allowBreaking = true;
    public float breakThreshold = 1.15f; // 15% stretch allowed
    public float stressColorMultiplier = 5f;

    private SpriteRenderer sr;
    private float restLength;
    private bool previewMode;
    private bool broken;
    private Rigidbody2D rb;

    void Awake()
    {
        // Setup visuals
        sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = startColor;

        // Add kinematic RB so this can collide with terrain + car
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void FixedUpdate()
    {
        if (broken || startPoint == null || endPoint == null)
            return;

        Vector2 posA = startPoint.transform.position;
        Vector2 posB = endPoint.transform.position;

        if (restLength <= 0.01f)
            restLength = Vector2.Distance(posA, posB);

        float dist = Vector2.Distance(posA, posB);
        float ratio = dist / restLength;

        // Visual alignment
        UpdateBar(posA, posB);

        // Color stress
        if (sr != null)
        {
            float t = Mathf.Clamp01((ratio - 1f) * stressColorMultiplier);
            sr.color = Color.Lerp(startColor, endColor, t);
        }

        // Breaking
        if (allowBreaking && ratio > breakThreshold)
            Break();
    }

    public void UpdateBar(Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        transform.position = (start + end) * 0.5f;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        transform.localScale = new Vector3(dir.magnitude, thickness, 1);
    }

    public void FinalizeBar()
    {
        restLength = Vector2.Distance(startPoint.transform.position, endPoint.transform.position);
        previewMode = false;
    }

    public void SetPreviewMode(bool s) => previewMode = s;

    private void Break()
    {
        if (broken) return;
        broken = true;
        Destroy(gameObject);
    }
}
