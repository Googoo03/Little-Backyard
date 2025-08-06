using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.UI;

public class Camera_Shake : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("Camera Settings")]
    [SerializeField] private float trauma;

    [SerializeField] private float translational_max;
    [SerializeField] private float rotational_max;
    [SerializeField] private Vector3 base_pos;

    void Start()
    {
        base_pos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        //transform.position = base_pos + new Vector3(Random.Range(-translational_max,translational_max), Random.Range(-translational_max, translational_max), Random.Range(-translational_max, translational_max)) * Mathf.Pow(trauma,3);
        transform.eulerAngles = new Vector3(Random.Range(-translational_max, translational_max), Random.Range(-translational_max, translational_max), Random.Range(-translational_max, translational_max)) * Mathf.Pow(trauma, 3);
        if (Input.GetKeyDown(KeyCode.Mouse0)) { 
            
        }
    }
}
