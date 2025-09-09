using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseFreeCam : MonoBehaviour
{
    [SerializeField] private float currentSpeed;
    [SerializeField] private float baseSpeed;

    public Quaternion targetRotation;
    public Quaternion lastOrientation;

    [SerializeField] private float mouseSensitivityX;
    [SerializeField] private float mouseSensitivityY;
    [SerializeField] private float rollSensitivity;

    [SerializeField] private float rollInput;
    [SerializeField] private float pitchInput;
    [SerializeField] private float yawInput;

    private float rollChange;
    private float yawChange;
    [SerializeField] private float pitchChange;

    private Quaternion yaw;
    private Quaternion pitch;
    private Quaternion roll;

    private float forward;

    private Vector3 delta;

    // Update is called once per frame
    void Update()
    {
        MovementProtocol();
    }

    protected void MovementProtocol()
    {

        targetRotation = transform.rotation;

        //change ship speed when in atmosphere. Slows down closer it gets
        bool sprint = (Input.GetKey(KeyCode.LeftShift));

        currentSpeed = (sprint ? 4 * baseSpeed : baseSpeed);


        setKeyInputs();

        smoothKey(ref rollInput, rollSensitivity, rollChange);
        smoothKey(ref pitchInput, mouseSensitivityY, pitchChange);
        smoothKey(ref yawInput, mouseSensitivityX, yawChange);


        yaw = Quaternion.AngleAxis(yawInput, transform.up);
        pitch = Quaternion.AngleAxis(-pitchInput, transform.right);
        roll = Quaternion.AngleAxis(-rollInput, transform.forward);

        targetRotation = yaw * pitch * roll * targetRotation;
        lastOrientation = yaw * pitch * roll * lastOrientation;//perhaps delete later.

        transform.rotation = targetRotation;

        /////Moving forward

        ///////////////////////

        delta = (transform.forward * forward) * currentSpeed * Time.deltaTime;

    }

    private void setKeyInputs()
    {
        forward = Input.GetAxis("Vertical");


        yawChange = Input.GetAxis("Mouse X") * mouseSensitivityX;
        pitchChange = Input.GetAxis("Mouse Y") * mouseSensitivityY;
        rollChange = -InputAxis(KeyCode.Q, KeyCode.E) * rollSensitivity;

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



    private void smoothKey(ref float axis, float sensitivity, float axisChange)
    {
        if (axisChange == 0)
        {
            axis *= 1 - (sensitivity * Time.deltaTime); //diminish roll with time if no input change
            if (Mathf.Abs(axis) <= .00001f) axis = 0; //arbitrary small number so there is no creep
        }
        else
        {

            axis += axisChange * Time.deltaTime; //smooth rolling
            axis = Mathf.Clamp(axis, -1f, 1f); //prevents infinite speed increase
        }
    }

    public Vector3 GetDelta()
    {
        return delta;
    }
}
