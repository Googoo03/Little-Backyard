using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Movement : Controllable_Entity
{
    [SerializeField] private float pitchInput;
    [SerializeField] private float yawInput;

    [SerializeField] private float yawChange;
    [SerializeField] private float pitchChange;

    [SerializeField] private float mouseSensitivityX;
    [SerializeField] private float mouseSensitivityY;

    [SerializeField] private GameObject nearbyPlanet;

    [SerializeField] private Rigidbody _rigidbody;

    [SerializeField] private float speed;

    public Quaternion targetRotation;

    public float forward;
    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        camera_offset = new Vector3(0.0f, 1.33f, 0.0f);
        targetRotation = new Quaternion();
    }

    // Update is called once per frame
    void Update()
    {
        if(canMove) MovementProtocol();
        ApplyGravity(false);
    }

    public void setNearbyPlanet(GameObject planet) {nearbyPlanet = planet;}

    protected override void MovementProtocol()
    {
        if (!_camera) return;

        float _speed = (Input.GetKey(KeyCode.LeftShift)) ? 1f : 0.25f;

        targetRotation = _camera.transform.localRotation;

        //change ship speed when in atmosphere. Slows down closer it gets

        _speed *= speed;

        setKeyInputs();


        smoothKey(ref pitchInput, mouseSensitivityY, pitchChange);
        smoothKey(ref yawInput, mouseSensitivityX, yawChange);


        Quaternion yaw = Quaternion.AngleAxis(yawChange, transform.up);
        Quaternion pitch = Quaternion.AngleAxis(-pitchChange, transform.right);

        targetRotation = yaw * pitch * targetRotation;
        Vector3 targetEuler = _camera.transform.localEulerAngles + new Vector3(-pitchChange, yawChange, 0);
        targetEuler.x = targetEuler.x > 180 ? (targetEuler.x - 360) : targetEuler.x; //reverse the coords if above 180.
        targetEuler.x = Mathf.Clamp(targetEuler.x, -90, 90);


        _camera.transform.localEulerAngles = targetEuler;

        /////Moving forward
        Vector3 forwardVec = Vector3.Cross(_camera.transform.right, transform.up);
        ///////////////////////

        transform.position += forwardVec * (forward * speed) * Time.deltaTime;

    }

    private void ApplyGravity(bool negative)
    {

        Vector3 toPlanet = (nearbyPlanet.transform.position - transform.position).normalized;
        toPlanet = negative ? -toPlanet : toPlanet;
        transform.up = -toPlanet;

        if (_rigidbody != null)
        {
            _rigidbody.AddForce(toPlanet);
        }
        return;
    }

    private void setKeyInputs()
    {
        forward = Input.GetAxis("Vertical");

        yawChange = Input.GetAxis("Mouse X") * mouseSensitivityX;
        pitchChange = Input.GetAxis("Mouse Y") * mouseSensitivityY;

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
}

