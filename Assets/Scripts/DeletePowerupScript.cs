using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float destroyTime; 

    void Start()
    {
        Destroy(gameObject, destroyTime);
    }
}
