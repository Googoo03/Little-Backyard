using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseFreeCamProperties
{
    public Vector3 position;
    public Vector3 delta;
}


public class BaseFreeCam : MonoBehaviour
{
    [SerializeField] protected float currentSpeed;
    [SerializeField] protected float baseSpeed;
    protected float clampedSpeed;

    public Quaternion targetRotation;
    public Quaternion lastOrientation;

    [SerializeField] private float mouseSensitivityX, mouseSensitivityY, rollSensitivity;

    private float rollInput, pitchInput, yawInput;


    private float rollChange, yawChange, pitchChange;

    private Quaternion yaw, pitch, roll;

    private float forward;

    protected Vector3 delta;
    protected Vector3 position;
    void Start() { clampedSpeed = baseSpeed; position = transform.position; }

    void Update()
    {
        MovementProtocol();
    }

    void LateUpdate()
    {
        ApplyForwardDelta();
        delta = Vector3.zero;
    }

    protected void MovementProtocol()
    {

        targetRotation = transform.rotation;

        //change ship speed when in atmosphere. Slows down closer it gets
        bool sprint = (Input.GetKey(KeyCode.LeftShift));

        currentSpeed = (sprint ? 4 * clampedSpeed : clampedSpeed);


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

        delta += (transform.forward * forward) * currentSpeed * Time.deltaTime;

    }

    protected virtual void ApplyForwardDelta()
    {
        position += delta;
        transform.position += delta;
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
            axis *= Mathf.Exp(-sensitivity * Time.deltaTime); //diminish roll with time if no input change
            if (Mathf.Abs(axis) <= .00001f) axis = 0; //arbitrary small number so there is no creep
        }
        else
        {

            axis += axisChange * Mathf.Min(0.01f, Time.deltaTime); //smooth rolling
            axis = Mathf.Clamp(axis, -sensitivity, sensitivity); //prevents infinite speed increase
        }
    }

    public Vector3 GetDelta() { return delta; }

    public void AddDelta(Vector3 delta_) { delta += delta_; }

    public void SetDelta(Vector3 delta_) { delta = delta_; }

    public Vector3 GetPosition() { return position; }
    public void SetPosition(Vector3 position_) { position = position_; }
}
