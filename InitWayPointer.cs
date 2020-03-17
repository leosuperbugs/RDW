using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitWayPointer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setInitPosition(Vector3 location, Vector3 direction)
    {
        transform.localPosition = location;
        transform.localRotation = Quaternion.LookRotation(direction);
    }
}
