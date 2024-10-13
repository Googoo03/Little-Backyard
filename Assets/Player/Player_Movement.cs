using Inven;
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

    //Laser FX
    [SerializeField] private LineRenderer laser;
    [SerializeField] private GameObject sparks;
    [SerializeField] private RaycastHit laser_hit;

    //Mining Laser Reach
    [SerializeField] private float laser_reach;

    //Inventory
    //[SerializeField] private Inventory inven;
    //[SerializeField] private Animator inventory_animator;


    // Start is called before the first frame update

    private void Awake()
    {
        InitializeInventory();
    }

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        camera_offset = new Vector3(0.0f, 1.33f, 0.0f);
        targetRotation = new Quaternion();
    }

    // Update is called once per frame
    void Update()
    {
        //Check inventory button
        InventoryOpenCheck();

        if (!canMove) return;
        MovementProtocol(); //move

        //Assign head bobbing accordingly
        if (headBob && _camera)
        {
            HeadBob();
        }
        else if(_camera){ _camera.transform.localPosition = camera_offset; }

        if (jump && onGround) JumpProtocol();

        ShootCheck(); //check shoot button
        InteractionCheck(); //interact raycast
        InteractInput(); //check if interact button pressed
        ApplyGravity(false); //apply gravity to nearest planet
        LODCheckDistance(); //update the planet LOD if needed
        CheckFallThroughPlanet();
    }
    private void InventoryOpenCheck()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (inventory_animator.GetCurrentAnimatorStateInfo(0).IsName("Inventory_Idle"))
            {
                inventory_animator.SetTrigger("Hide");
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                canMove = true;
            }
            else if (inventory_animator.GetCurrentAnimatorStateInfo(0).IsName("Inventory_Idle_HIde"))
            {
                inventory_animator.SetTrigger("Show");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                canMove = false;
            }
        }
    }

    private void ShootCheck()
    {
        bool lmbDown = Input.GetMouseButton(0);
        laser.gameObject.SetActive(lmbDown);
        if (lmbDown)
        {
            LaserProtocol();
        }
        else
        {
            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = false;

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = false;

            //t = 0;
        }
    }



    //TO BE INTEGRATED WITH MAIN GAMEPLAY LOOP ---------------------------------------------------------
    private void LaserProtocol()
    {
        //Assign position to the beginning and end of the laser
        Vector3[] positions = new Vector3[2];
        positions[0] = transform.position + (_camera.transform.right * transform.localScale.x);


        Vector3 endPoint = new Vector3();

        //If object is hit
        if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out laser_hit, reach, 1))
        {

            //Turn on respective particle systems-----------------------------------------------
            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = true;
            ParticleSystem spark = sparks.transform.GetChild(0).GetComponent<ParticleSystem>();
            if (!spark.isPlaying) spark.Play();

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = true;
            ParticleSystem dust = sparks.transform.GetChild(1).GetComponent<ParticleSystem>();
            if (!dust.isPlaying) dust.Play();
            //----------------------------------------------------------------------------------

            //Set the sparks particle system at end of laser
            sparks.transform.position = laser_hit.point;
            sparks.transform.rotation = Quaternion.LookRotation(_camera.transform.position - laser_hit.point);


            //Set Laser end to where it hits
            endPoint = laser_hit.point;

            //Deal damage if an object is hit
            Resource_Class resource = laser_hit.transform.tag == "Resource" ? laser_hit.transform.GetComponent<Resource_Class>() : null;
            if (resource != null) { resource.dealDamage(5 * Time.deltaTime); }
        }
        else
        {

            endPoint = _camera.transform.position + (_camera.transform.forward * reach);

            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = false;

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = false;

        }

        //t += t > 1 ? 0 : 3f * Time.deltaTime;
        positions[1] = endPoint;//(endPoint - positions[0])*t + positions[0];
        laser.SetPositions(positions);
    }

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

    private void InitializeInventory() {
        Cursor.lockState = CursorLockMode.Locked;

        inven = new Inventory();

        GameObject hotbar = GameObject.FindGameObjectWithTag("Hotbar");
        GameObject inventory = GameObject.FindGameObjectWithTag("Inventory");

        inventory_animator = inventory.GetComponent<Animator>();
        int hotbar_slots = inven.getHotbarNum_Slots();
        int inven_slots = inven.getInvenNum_Slots();
        int i = 0;

        for (; i < hotbar_slots; ++i)
        { //the 
            inven.setInventory_Slot(hotbar.transform.GetChild(i).GetChild(0).GetComponent<Inventory_Slot>(), i); //this is on awake so this should be fine
        }
        for (int j = 0; j < inven_slots; ++j)
        {
            inven.setInventory_Slot(inventory.transform.GetChild(j).GetChild(0).GetComponent<Inventory_Slot>(), i + j);
        }
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

