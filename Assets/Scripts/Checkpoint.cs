using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public bool checkpointReached = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Car"))
        {
            checkpointReached = true;
            Debug.Log("Car crossed bridge successfully!");
        }
    }
}

