using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuickSpinScript : MonoBehaviour
{
    // Start is called before the first frame update
    public float yawInput;
    public float pitchInput;
    public float rollInput;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Quaternion yaw = Quaternion.AngleAxis(yawInput * Time.deltaTime, transform.up);
        Quaternion pitch = Quaternion.AngleAxis(-pitchInput * Time.deltaTime, transform.right);
        Quaternion roll = Quaternion.AngleAxis(-rollInput * Time.deltaTime, transform.forward);

        transform.rotation = transform.rotation * yaw * pitch * roll;
    }
}
