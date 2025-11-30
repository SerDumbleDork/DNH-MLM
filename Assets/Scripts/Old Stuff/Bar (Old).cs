// Decompiled with JetBrains decompiler
// Type: BridgeBuilder
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 0F2EBE17-2F1C-4B5A-8C95-8AC83B5D15D6
// Assembly location: C:\Users\user\Downloads\BridgeGame\Bridge Building_Data\Managed\Assembly-CSharp.dll

/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

#nullable disable
public class BridgeBuilder : MonoBehaviour
{
  public Camera cam;
  public GameObject nodePrefab;
  public GameObject beamPrefab;
  [Header("Bridge Settings")]
  public float nodeRadius = 0.15f;
  public float maxBeamLength = 4f;
  public float minNodeSpacing = 0.3f;
  public float stiffness = 50f;
  public float breakForce = 200f;
  private List<GameObject> nodes = new List<GameObject>();
  private List<BridgeBuilder.BeamLink> beams = new List<BridgeBuilder.BeamLink>();
  private GameObject firstNode;
  private GameObject previewBeam;
  private Transform beamParent;
  private Transform nodeParent;
  private bool simulationMode = false;
  [SerializeField]
  private BridgeBuilder.BuildMode currentMode = BridgeBuilder.BuildMode.NodePlacement;

  private void Start()
  {
    this.beamParent = new GameObject("Beams").transform;
    this.nodeParent = new GameObject("Nodes").transform;
  }

  private void Update()
  {
    Mouse current = Mouse.current;
    if (current == null || (Object) EventSystem.current != (Object) null && EventSystem.current.IsPointerOverGameObject())
      return;
    if (Keyboard.current.digit1Key.wasPressedThisFrame)
    {
      this.currentMode = BridgeBuilder.BuildMode.NodePlacement;
      this.ClearPreview();
    }
    if (Keyboard.current.digit2Key.wasPressedThisFrame)
      this.currentMode = BridgeBuilder.BuildMode.BeamPlacement;
    if (Keyboard.current.spaceKey.wasPressedThisFrame)
      this.ToggleSimulation();
    Vector2 worldPoint = (Vector2) this.cam.ScreenToWorldPoint((Vector3) Mouse.current.position.ReadValue() with
    {
      z = -this.cam.transform.position.z
    });
    if (!this.simulationMode && current.leftButton.wasPressedThisFrame)
    {
      if (this.currentMode == BridgeBuilder.BuildMode.NodePlacement)
        this.PlaceNode(worldPoint);
      else if (this.currentMode == BridgeBuilder.BuildMode.BeamPlacement)
        this.TryConnectNodes(worldPoint);
    }
    if (!this.simulationMode && current.rightButton.wasPressedThisFrame)
      this.DeleteAtPosition(worldPoint);
    if (!this.simulationMode && this.currentMode == BridgeBuilder.BuildMode.BeamPlacement && (Object) this.firstNode != (Object) null)
      this.UpdatePreview(worldPoint);
    if (!this.simulationMode)
      return;
    this.UpdateBeams();
  }

  private void PlaceNode(Vector2 pos)
  {
    foreach (GameObject node in this.nodes)
    {
      if ((double) Vector2.Distance(pos, (Vector2) node.transform.position) < (double) this.minNodeSpacing)
        return;
    }
    GameObject node1 = Object.Instantiate<GameObject>(this.nodePrefab, (Vector3) pos, Quaternion.identity, this.nodeParent);
    node1.name = "Node_" + this.nodes.Count.ToString();
    this.SetupNode(node1);
    this.nodes.Add(node1);
  }

  private void SetupNode(GameObject node)
  {
    Rigidbody2D rigidbody2D = node.GetComponent<Rigidbody2D>() ?? node.AddComponent<Rigidbody2D>();
    rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
    rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
    rigidbody2D.simulated = true;
    CircleCollider2D circleCollider2D = node.GetComponent<CircleCollider2D>() ?? node.AddComponent<CircleCollider2D>();
    circleCollider2D.radius = this.nodeRadius;
    circleCollider2D.isTrigger = false;
    SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>() ?? node.AddComponent<SpriteRenderer>();
    spriteRenderer.color = Color.yellow;
    spriteRenderer.sortingOrder = 2;
    node.tag = "Node";
  }

  private void TryConnectNodes(Vector2 mousePos)
  {
    Collider2D collider2D = Physics2D.OverlapPoint(mousePos);
    GameObject gameObject1 = !(bool) (Object) collider2D || !collider2D.CompareTag("Node") ? (GameObject) null : collider2D.gameObject;
    if ((Object) this.firstNode == (Object) null)
    {
      if (!((Object) gameObject1 != (Object) null))
        return;
      this.firstNode = gameObject1;
    }
    else
    {
      GameObject gameObject2 = gameObject1;
      if ((Object) gameObject2 == (Object) null)
      {
        Vector2 position = (Vector2) this.firstNode.transform.position;
        Vector2 normalized = (mousePos - position).normalized;
        float num = Mathf.Min(Vector2.Distance(position, mousePos), this.maxBeamLength);
        gameObject2 = Object.Instantiate<GameObject>(this.nodePrefab, (Vector3) (position + normalized * num), Quaternion.identity, this.nodeParent);
        this.SetupNode(gameObject2);
        this.nodes.Add(gameObject2);
      }
      if ((Object) gameObject2 != (Object) this.firstNode)
        this.CreateConnection(this.firstNode, gameObject2);
      this.firstNode = (GameObject) null;
      this.ClearPreview();
    }
  }

  private void CreateConnection(GameObject a, GameObject b)
  {
    float targetDistance = Vector2.Distance((Vector2) a.transform.position, (Vector2) b.transform.position);
    DistanceJoint2D joint1 = a.AddComponent<DistanceJoint2D>();
    joint1.connectedBody = b.GetComponent<Rigidbody2D>();
    joint1.autoConfigureDistance = false;
    joint1.distance = targetDistance;
    joint1.breakForce = this.breakForce;
    DistanceJoint2D joint2 = b.AddComponent<DistanceJoint2D>();
    joint2.connectedBody = a.GetComponent<Rigidbody2D>();
    joint2.autoConfigureDistance = false;
    joint2.distance = targetDistance;
    joint2.breakForce = this.breakForce;
    this.StartCoroutine(this.EnforceJointDistance(joint1, targetDistance));
    this.StartCoroutine(this.EnforceJointDistance(joint2, targetDistance));
    GameObject gameObject = Object.Instantiate<GameObject>(this.beamPrefab, this.beamParent);
    gameObject.name = $"Beam_{a.name}_{b.name}";
    SpriteRenderer component = gameObject.GetComponent<SpriteRenderer>();
    if ((bool) (Object) component)
      component.color = Color.gray;
    gameObject.tag = "Beam";
    this.beams.Add(new BridgeBuilder.BeamLink(gameObject, a, b));
    this.UpdateSingleBeam(gameObject, (Vector2) a.transform.position, (Vector2) b.transform.position);
    BeamVisual beamVisual = gameObject.GetComponent<BeamVisual>();
    if ((Object) beamVisual == (Object) null)
      beamVisual = gameObject.AddComponent<BeamVisual>();
    beamVisual.nodeA = a;
    beamVisual.nodeB = b;
    beamVisual.restLength = targetDistance;
  }

  private IEnumerator EnforceJointDistance(DistanceJoint2D joint, float targetDistance)
  {
    Rigidbody2D rb = joint.GetComponent<Rigidbody2D>();
    while ((Object) joint != (Object) null && (Object) rb != (Object) null)
    {
      if ((Object) joint.connectedBody != (Object) null)
      {
        float currentDist = Vector2.Distance(rb.position, joint.connectedBody.position);
        float diff = currentDist - targetDistance;
        Vector2 dir = (joint.connectedBody.position - rb.position).normalized;
        rb.AddForce(dir * diff * -this.stiffness);
        dir = new Vector2();
      }
      yield return (object) new WaitForFixedUpdate();
    }
  }

  private void UpdateSingleBeam(GameObject beam, Vector2 start, Vector2 end)
  {
    Vector2 vector2 = end - start;
    beam.transform.position = (Vector3) ((start + end) * 0.5f);
    beam.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(vector2.x, vector2.y, 0.0f));
    beam.transform.localScale = new Vector3(vector2.magnitude, 0.1f, 1f);
  }

  private void UpdateBeams()
  {
    foreach (BridgeBuilder.BeamLink beam in this.beams)
    {
      if (!((Object) beam.nodeA == (Object) null) && !((Object) beam.nodeB == (Object) null) && !((Object) beam.beam == (Object) null))
        this.UpdateSingleBeam(beam.beam, (Vector2) beam.nodeA.transform.position, (Vector2) beam.nodeB.transform.position);
    }
  }

  private void UpdatePreview(Vector2 mousePos)
  {
    if ((Object) this.firstNode == (Object) null)
      return;
    if ((Object) this.previewBeam == (Object) null)
    {
      this.previewBeam = Object.Instantiate<GameObject>(this.beamPrefab, this.beamParent);
      this.previewBeam.name = "Beam_Preview";
      SpriteRenderer component = this.previewBeam.GetComponent<SpriteRenderer>();
      if ((bool) (Object) component)
        component.color = new Color(1f, 1f, 1f, 0.35f);
    }
    Vector2 position = (Vector2) this.firstNode.transform.position;
    Vector2 vector2 = mousePos - position;
    float x = Mathf.Min(vector2.magnitude, this.maxBeamLength);
    this.previewBeam.transform.position = (Vector3) (position + vector2 * 0.5f);
    this.previewBeam.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(vector2.x, vector2.y, 0.0f));
    this.previewBeam.transform.localScale = new Vector3(x, 0.1f, 1f);
  }

  private void ClearPreview()
  {
    if ((bool) (Object) this.previewBeam)
      Object.Destroy((Object) this.previewBeam);
    this.previewBeam = (GameObject) null;
  }

  private void DeleteAtPosition(Vector2 mousePos)
  {
    Collider2D collider2D = Physics2D.OverlapPoint(mousePos);
    if ((Object) collider2D == (Object) null)
      return;
    GameObject gameObject = collider2D.gameObject;
    if (gameObject.CompareTag("Beam"))
    {
      BridgeBuilder.BeamLink link = (BridgeBuilder.BeamLink) null;
      foreach (BridgeBuilder.BeamLink beam in this.beams)
      {
        if ((Object) beam.beam == (Object) gameObject)
        {
          link = beam;
          break;
        }
      }
      if (link == null)
        return;
      this.CleanupBeam(link);
      this.beams.Remove(link);
      Object.Destroy((Object) link.beam);
    }
    else
    {
      if (!gameObject.CompareTag("Node"))
        return;
      List<BridgeBuilder.BeamLink> beamLinkList = new List<BridgeBuilder.BeamLink>();
      foreach (BridgeBuilder.BeamLink beam in this.beams)
      {
        if ((Object) beam.nodeA == (Object) gameObject || (Object) beam.nodeB == (Object) gameObject)
          beamLinkList.Add(beam);
      }
      foreach (BridgeBuilder.BeamLink link in beamLinkList)
      {
        this.CleanupBeam(link);
        this.beams.Remove(link);
        Object.Destroy((Object) link.beam);
      }
      Object.Destroy((Object) gameObject);
      this.nodes.Remove(gameObject);
    }
  }

  private void CleanupBeam(BridgeBuilder.BeamLink link)
  {
    if (!((Object) link.nodeA != (Object) null) || !((Object) link.nodeB != (Object) null))
      return;
    DistanceJoint2D[] components1 = link.nodeA.GetComponents<DistanceJoint2D>();
    DistanceJoint2D[] components2 = link.nodeB.GetComponents<DistanceJoint2D>();
    foreach (DistanceJoint2D distanceJoint2D in components1)
    {
      if ((Object) distanceJoint2D.connectedBody == (Object) link.nodeB.GetComponent<Rigidbody2D>())
        Object.Destroy((Object) distanceJoint2D);
    }
    foreach (DistanceJoint2D distanceJoint2D in components2)
    {
      if ((Object) distanceJoint2D.connectedBody == (Object) link.nodeA.GetComponent<Rigidbody2D>())
        Object.Destroy((Object) distanceJoint2D);
    }
  }

  private void ToggleSimulation()
  {
    this.simulationMode = !this.simulationMode;
    Debug.Log(this.simulationMode ? (object) "▶ Simulation Started" : (object) "⏸ Build Mode");
    for (int index = 0; index < this.nodes.Count; ++index)
    {
      Rigidbody2D component = this.nodes[index].GetComponent<Rigidbody2D>();
      if (!((Object) component == (Object) null))
      {
        bool flag = index < 2;
        if (this.simulationMode)
        {
          component.bodyType = flag ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
          component.gravityScale = flag ? 0.0f : 1f;
        }
        else
        {
          component.bodyType = RigidbodyType2D.Kinematic;
          component.gravityScale = 0.0f;
          component.linearVelocity = Vector2.zero;
          component.angularVelocity = 0.0f;
        }
      }
    }
  }

  public void SetMode(int mode)
  {
    switch (mode)
    {
      case 1:
        this.currentMode = BridgeBuilder.BuildMode.NodePlacement;
        break;
      case 2:
        this.currentMode = BridgeBuilder.BuildMode.BeamPlacement;
        break;
    }
    this.ClearPreview();
  }

  public string GetCurrentMode() => this.currentMode.ToString();

  public void ToggleSimulationExternally() => this.ToggleSimulation();

  public void ResetBridge()
  {
    foreach (BridgeBuilder.BeamLink beam in this.beams)
    {
      if ((bool) (Object) beam.beam)
        Object.Destroy((Object) beam.beam);
    }
    this.beams.Clear();
    foreach (GameObject node in this.nodes)
    {
      if ((bool) (Object) node)
        Object.Destroy((Object) node);
    }
    this.nodes.Clear();
    this.firstNode = (GameObject) null;
    this.ClearPreview();
    this.simulationMode = false;
    Debug.Log((object) "Bridge Reset");
  }

  private enum BuildMode
  {
    NodePlacement,
    BeamPlacement,
  }

  private class BeamLink
  {
    public GameObject beam;
    public GameObject nodeA;
    public GameObject nodeB;

    public BeamLink(GameObject b, GameObject a, GameObject c)
    {
      this.beam = b;
      this.nodeA = a;
      this.nodeB = c;
    }
  }
}
*/