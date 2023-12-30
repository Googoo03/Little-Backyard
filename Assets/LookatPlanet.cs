using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookatPlanet : MonoBehaviour {
    public GameObject planet;
	// Use this for initialization
	void Start () {
        planet = GameObject.Find("CubeSphere");
        transform.LookAt(2 * transform.position - planet.transform.position);
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
