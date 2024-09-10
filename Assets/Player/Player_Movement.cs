using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public class Player_Movement : Controllable_Entity
{
    [SerializeField] private float pitchInput;
    [SerializeField] private float yawInput;

    [SerializeField] private float yawChange;
    [SerializeField] private float pitchChange;

    [SerializeField] private float mouseSensitivityX;
    [SerializeField] private float mouseSensitivityY;

    //[SerializeField] private GameObject nearbyPlanet;

    [SerializeField] private Rigidbody _rigidbody;

    [SerializeField] private float speed;

    [SerializeField] private RaycastHit hit;

    public Quaternion targetRotation;

    public float forward;
    private float side;
    private bool jump;
    [SerializeField] private bool onGround=true;
    [SerializeField] private float jumpForce;

    [SerializeField] private float t;

    [SerializeField] private bool headBob = false;
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
        if (!canMove) return;
        MovementProtocol(); //move

        //Assign head bobbing accordingly
        if (headBob && _camera)
        {
            HeadBob();
        }
        else if(_camera){ _camera.transform.localPosition = camera_offset; }

        if (jump && onGround) JumpProtocol();

        InteractionCheck(); //interact raycast
        InteractInput(); //check if interact button pressed
        ApplyGravity(false); //apply gravity to nearest planet
        LODCheckDistance(); //update the planet LOD if needed
    }

    //public void setNearbyPlanet(GameObject planet) {nearbyPlanet = planet;}


    //It should be researched if this is slow. I would imagine it is
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.tag == "Planet") onGround = true;
    }

    private void JumpProtocol() {
        onGround = false;

        Vector3 toPlanet = (nearbyPlanet.transform.position - transform.position).normalized;
        //_rigidbody.AddForce(-toPlanet*jumpForce);
    }


    private void InteractInput() {
        if (Input.GetKeyDown(KeyCode.E) && canMove) objectInteraction();
    }

    private void HeadBob() {
        float forward_side_factor = Mathf.Clamp(forward+side,-1,1);
        if (Mathf.Abs(forward + side) < 0.001f) forward_side_factor = Mathf.Max(forward, side);
        t += (forward_side_factor * Time.deltaTime)*6.5f;
        t = t > 6.28f ? 0.0f : t;

        _camera.transform.localPosition = camera_offset + (new Vector3(Mathf.Sin(t),Mathf.Cos(2*t), 0)*0.3f);
    }

    protected override void InteractionCheck() {
        int layerMask = 1;
        if (!_camera) return;

        if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out hit, reach, layerMask))
        {
            Debug.DrawRay(_camera.transform.position, _camera.transform.forward * hit.distance, Color.green);
            //set interaction object to hit transform
            //objectInteraction();
        }
        else {
            Debug.DrawRay(_camera.transform.position, _camera.transform.forward * reach, Color.red);
        }
    }

    //DETERMINES WHAT TO DO BASED ON THE OBJECT THE PLAYER INTERACTS WITH
    private void objectInteraction() {
        Debug.Log(hit.transform);
        if (!hit.transform) return;
        switch (hit.transform.tag) {
            case ("Vehicle"):
                event_manager.set_PlayerInteract(hit.transform.gameObject);
                event_manager.set_enterShip(true);
                return;
            default:
                event_manager.set_PlayerInteract(null);
                return;
        }
    }

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
        Vector3 forwardVec = Vector3.Cross(_camera.transform.right,transform.up);
        forwardVec = forwardVec.normalized;
        ///////////////////////
        ///
        Debug.DrawLine(transform.position, transform.position+_camera.transform.right, Color.cyan);
        Debug.DrawLine(transform.position, transform.position+transform.up, Color.white);
        Debug.DrawLine(transform.position, transform.position + forwardVec, Color.red);

        transform.position += ((forwardVec*forward) + (_camera.transform.right * side)) * speed * Time.deltaTime;

    }

    private void ApplyGravity(bool negative)
    {

        Vector3 toPlanet = (nearbyPlanet.transform.position - transform.position).normalized;
        toPlanet = negative ? -toPlanet : toPlanet;
        //transform.up = Vector3.Slerp(transform.up, -toPlanet, Time.deltaTime);

        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, -toPlanet) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime);

        if (_rigidbody != null)
        {
            _rigidbody.AddForce(toPlanet);
            
        }
        return;
    }

    private void setKeyInputs()
    {
        forward = Input.GetAxis("Vertical");
        side = Input.GetAxis("Horizontal");
        jump = Input.GetKey(KeyCode.Space);

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

