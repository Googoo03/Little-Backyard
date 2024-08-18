using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePlayerController : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private float speed;

    void Start()
    {
        
    }

    private int InputAxis(KeyCode buttonA, KeyCode buttonB)
    {
        bool pressed_A = Input.GetKey(buttonA);
        bool pressed_B = Input.GetKey(buttonB);
        int result = 0;


        if (pressed_A && !pressed_B) { result = 1; }
        else if (!pressed_A && pressed_B) { result = -1; }
        else { result = 0; }

        return result;
    }

    // Update is called once per frame
    void Update()
    {
        float xAxis = Input.GetAxis("Vertical");
        float zAxis = Input.GetAxis("Horizontal");
        float yAxis = -InputAxis(KeyCode.Q, KeyCode.E);
        transform.position += new Vector3(xAxis,yAxis,zAxis) * speed * Time.deltaTime;
    }
}
