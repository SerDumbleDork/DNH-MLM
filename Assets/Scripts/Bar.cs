using UnityEngine;

public class Bar : MonoBehaviour
{
    [Header("Strength / Stress")]
    public float breakForce = 200f;
    public Color stressColor = Color.red;

    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Vector3 endPosition;

    [Header("Joints")]
    public HingeJoint2D startJoint;
    public HingeJoint2D endJoint;

    private SpriteRenderer sr;
    private BoxCollider2D box;
    private Rigidbody2D rb;

    private float spriteWidth;    // world width of the sprite at scale 1,1,1
    private float baseYScale;     // original Y scale
    private Color startColor;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        box = GetComponent<BoxCollider2D>();
        rb  = GetComponent<Rigidbody2D>();

        startColor = sr != null ? sr.color : Color.white;

        baseYScale = transform.localScale.y;

        // Try to get sprite width from bounds
        if (sr != null)
            spriteWidth = sr.bounds.size.x;

        // If bounds failed (0), try sprite texture width
        if (spriteWidth <= 0.0001f && sr != null && sr.sprite != null)
        {
            spriteWidth = sr.sprite.bounds.size.x;  // this is in sprite local units
        }

        // LAST RESORT - prevent division by zero
        if (spriteWidth <= 0.0001f)
            spriteWidth = 1f;     // safe default
    }

    /// <summary>
    /// Called once when we start previewing: sets the start position.
    /// </summary>
    public void SetStart(Vector2 worldStart)
    {
        startPosition = worldStart;
        transform.position = worldStart;
    }

    public void UpdateCreatingBar(Vector3 toPosition)
    {
        endPosition = toPosition;

        Vector2 s = (Vector2)startPosition;
        Vector2 e = (Vector2)endPosition;
        Vector2 dir = e - s;

        float length = dir.magnitude;
        if (length < 0.01f) length = 0.01f;

        // Midpoint
        Vector2 mid = s + dir * 0.5f;
        transform.position = mid;

        // Rotation
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // ----- Correct visual scaling -----
        float scaleX = (spriteWidth > 0.0001f) ? (length / spriteWidth) : length;
        transform.localScale = new Vector3(scaleX, baseYScale, 1);

        // ----- Correct collider size -----
        if (box != null)
        {
            box.size = new Vector2(length, box.size.y);
            box.offset = new Vector2(length * 0.5f, 0f);
        }
    }

    void FixedUpdate()
    {
        if (startJoint == null || endJoint == null || sr == null)
            return;

        float loadA = startJoint.reactionForce.magnitude;
        float loadB = endJoint.reactionForce.magnitude;
        float load  = Mathf.Max(loadA, loadB);

        float t = Mathf.Clamp01(load / breakForce);
        sr.color = Color.Lerp(startColor, stressColor, t);

        if (load >= breakForce)
        {
            Destroy(gameObject);
        }
    }
}