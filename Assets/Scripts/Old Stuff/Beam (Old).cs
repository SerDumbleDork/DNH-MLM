using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BeamOld : MonoBehaviour
{
    [Header("Node References")]
    public Point startPoint;
    public Point endPoint;

    [Header("Geometry")]
    public float thickness = 0.15f;
    private float restLength;

    [Header("Structural Settings")]
    public float breakThreshold = 1.20f; // 20% stretch allowed
    public float baseStiffness = 40f;
    public float baseBreakForce = 150f;
    public float stressColorMultiplier = 5f;

    [Header("Visuals")]
    public Color startColor = Color.white;
    public Color endColor = Color.red;

    [Header("Terrain Support")]
    public bool collideWithTerrain = false;

    private SpriteRenderer sr;
    private DistanceJoint2D jointA;
    private DistanceJoint2D jointB;
    private bool broken;

    // reinforcement store
    private static readonly Dictionary<string, ReinforcementData> reinforcement =
        new Dictionary<string, ReinforcementData>();

    private class ReinforcementData
    {
        public float stiffness;
        public float breakForce;
        public int count;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = startColor;

        // If beam is allowed to collide with terrain
        if (collideWithTerrain)
        {
            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
            rb.freezeRotation = true;

            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.usedByComposite = false;
            col.isTrigger = false;
        }
    }

    void FixedUpdate()
    {
        if (broken || startPoint == null || endPoint == null) return;

        Vector2 posA = startPoint.transform.position;
        Vector2 posB = endPoint.transform.position;

        if (restLength <= 0.01f)
            restLength = Vector2.Distance(posA, posB);

        float dist = Vector2.Distance(posA, posB);
        float ratio = dist / restLength;

        // Visual update
        UpdateBar(posA, posB);

        // Color stress
        if (sr)
        {
            float t = Mathf.Clamp01((ratio - 1f) * stressColorMultiplier);
            sr.color = Color.Lerp(startColor, endColor, t);
        }

        // Break on overstretch
        if (ratio > breakThreshold)
        {
            Break();
        }
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

        Rigidbody2D rbA = startPoint.GetComponent<Rigidbody2D>();
        Rigidbody2D rbB = endPoint.GetComponent<Rigidbody2D>();

        if (!rbA || !rbB) return;

        string key = GetKey(rbA, rbB);

        // Get or create reinforcement info
        if (!reinforcement.TryGetValue(key, out ReinforcementData data))
        {
            data = new ReinforcementData
            {
                stiffness = baseStiffness,
                breakForce = baseBreakForce,
                count = 0
            };
            reinforcement[key] = data;
        }

        data.count++;
        data.stiffness += baseStiffness * 0.5f;
        data.breakForce += baseBreakForce * 0.5f;

        // Create joints
        jointA = rbA.gameObject.AddComponent<DistanceJoint2D>();
        jointA.connectedBody = rbB;
        jointA.autoConfigureDistance = false;
        jointA.distance = restLength;
        jointA.breakForce = data.breakForce;
        jointA.enableCollision = false;

        jointB = rbB.gameObject.AddComponent<DistanceJoint2D>();
        jointB.connectedBody = rbA;
        jointB.autoConfigureDistance = false;
        jointB.distance = restLength;
        jointB.breakForce = data.breakForce;
        jointB.enableCollision = false;

        StartCoroutine(EnforceDistance(jointA, data.stiffness));
        StartCoroutine(EnforceDistance(jointB, data.stiffness));
    }

    private IEnumerator EnforceDistance(DistanceJoint2D joint, float stiffness)
    {
        Rigidbody2D rb = joint.GetComponent<Rigidbody2D>();

        while (joint != null && rb != null)
        {
            if (joint.connectedBody != null)
            {
                float currentDist =
                    Vector2.Distance(rb.position, joint.connectedBody.position);

                float diff = currentDist - joint.distance;
                Vector2 dir =
                    (joint.connectedBody.position - rb.position).normalized;

                rb.AddForce(dir * diff * -stiffness);
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private void Break()
    {
        if (broken) return;
        broken = true;

        if (jointA) Destroy(jointA);
        if (jointB) Destroy(jointB);

        Destroy(gameObject);
    }

    private static string GetKey(Rigidbody2D a, Rigidbody2D b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();
        return idA < idB ? $"{idA}-{idB}" : $"{idB}-{idA}";
    }
}
