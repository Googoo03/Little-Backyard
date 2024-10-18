using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wind : MonoBehaviour
{
    // Start is called before the first frame update
    private Object_Pool_Manager wind_manager;
    private void Awake()
    {
        wind_manager = GameObject.FindWithTag("Wind_Manager").GetComponent<Object_Pool_Manager>();
    }
    private void OnDisable()
    {
        List<GameObject> releaseList = new List<GameObject>() { transform.gameObject };

        wind_manager.releasePoolObjs(ref releaseList);
    }
}
