using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LowerCloudLayer : MonoBehaviour {

	// Use this for initialization
	void Start () {
        transform.localScale = new Vector3( transform.parent.transform.localScale.x - .1f, transform.parent.transform.localScale.y - .1f, transform.parent.transform.localScale.z - .1f);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
