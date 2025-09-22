using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudCubeParameters : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Material cloudMaterial;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 scale;

    [SerializeField] private Transform sunTransform;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        position = transform.position;
        scale = transform.localScale / 2f;
        cloudMaterial.SetVector("_Position", position);
        cloudMaterial.SetVector("_Scale", scale);
        cloudMaterial.SetVector("_SunPos", sunTransform.position);
    }
}
