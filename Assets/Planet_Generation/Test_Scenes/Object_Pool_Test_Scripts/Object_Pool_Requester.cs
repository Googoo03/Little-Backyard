using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Object_Pool_Requester : MonoBehaviour
{
    [SerializeField] private Object_Pool_Test objPool_Manager;
    [SerializeField] int numToRequest;
    [SerializeField] private List<GameObject> objs;
    // Start is called before the first frame update
    void Start()
    {
        objs.Clear();
        objPool_Manager.requestPoolObjs(ref objs,numToRequest);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
