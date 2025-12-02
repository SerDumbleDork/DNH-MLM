using UnityEngine;

public class BeamBar : MonoBehaviour
{
    public BridgeGene geneReference;

    public Point nodeA;
    public Point nodeB;

    private float spriteWidth;
    private float baseY;
    private SpriteRenderer sr;

    [HideInInspector] public DistanceJoint2D jointA;
    [HideInInspector] public DistanceJoint2D jointB;

    // Stress colors
    public Color normalColor = Color.white;
    public Color stressColor = Color.red;

    // Breaking force threshold
    public float breakForce = 450f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        spriteWidth = sr.sprite.bounds.size.x;
        if (spriteWidth < 0.001f) spriteWidth = 1;

        baseY = transform.localScale.y;

        // Beams are visuals only, no RBs
        if (GetComponent<Rigidbody2D>())
            Destroy(GetComponent<Rigidbody2D>());
        if (GetComponent<Collider2D>())
            Destroy(GetComponent<Collider2D>());
    }

    void Start()
    {
        // They were added in BarCreator
        jointA = nodeA.GetComponent<DistanceJoint2D>();
        jointB = nodeB.GetComponent<DistanceJoint2D>();
    }

    void Update()
    {
        if (nodeA && nodeB)
        {
            Vector2 a = nodeA.rb.position;
            Vector2 b = nodeB.rb.position;

            UpdateVisual(a, b);
            UpdateStressColorAndBreak();
        }
    }

    void UpdateStressColorAndBreak()
    {
        if (jointA == null || jointB == null) return;

        // Joint force
        float fA = jointA.reactionForce.magnitude;
        float fB = jointB.reactionForce.magnitude;
        float load = Mathf.Max(fA, fB);

        // Stress color
        float t = Mathf.InverseLerp(0, breakForce * 0.8f, load);
        sr.color = Color.Lerp(normalColor, stressColor, t);

        // BREAK
        if (load >= breakForce)
        {
            BreakLinks();
        }
    }

    void BreakLinks()
    {
        if (geneReference != null)
            geneReference.broken = true;

        foreach (var j in nodeA.GetComponents<DistanceJoint2D>())
            if (j.connectedBody == nodeB.rb)
                Destroy(j);

        foreach (var j in nodeB.GetComponents<DistanceJoint2D>())
            if (j.connectedBody == nodeA.rb)
                Destroy(j);

        Destroy(gameObject);
    }

    public void UpdateVisual(Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;

        transform.position = a + dir * 0.5f;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        transform.localScale = new Vector3(len / spriteWidth, baseY, 1);
    }
}
