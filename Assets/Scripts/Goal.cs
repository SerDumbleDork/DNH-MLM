using UnityEngine;

public class Goal : MonoBehaviour
{
    public bool endReached = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Car"))
        {
            endReached = true;
            Debug.Log("Car crossed bridge successfully!");
        }
    }
}

