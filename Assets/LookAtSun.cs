using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtSun : MonoBehaviour {
	// Use this for initialization
	void Start () {

        Vector3 lookPos = GameObject.Find("Sun").transform.position;
        transform.LookAt(lookPos);
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y - 90, transform.eulerAngles.z);
    }
	
	// Update is called once per frame
	void Update () {
    }
}
