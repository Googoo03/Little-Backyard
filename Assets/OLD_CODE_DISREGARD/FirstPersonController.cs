using UnityEngine;
using System.Collections;

//[RequireComponent(typeof(GravityBody))]

using Inven;
public class FirstPersonController : MonoBehaviour
{

    // public vars
    public float mouseSensitivityX = 250;
    public float mouseSensitivityY = 250;
    public float walkSpeed = 6;
    public float jumpForce = 220;
    public LayerMask groundedMask;

    // System vars
    bool grounded;
    Vector3 moveAmount;
    Vector3 smoothMoveVelocity;
    float verticalLookRotation;
    Transform cameraTransform;

    [SerializeField] private LineRenderer laser;
    [SerializeField] private GameObject sparks;
    [SerializeField] private RaycastHit hit;

    //Inventory
    [SerializeField] private Inventory inven;
    [SerializeField] private Animator inventory_animator;

    //Mining Laser Reach
    [SerializeField] private float reach;

    private bool can_move;

    private float t;

    void Awake()
    {
        can_move = true;
        Cursor.lockState = CursorLockMode.Locked;
        
        cameraTransform = Camera.main.transform;

        inven = new Inventory();

        GameObject hotbar = GameObject.FindGameObjectWithTag("Hotbar");
        GameObject inventory = GameObject.FindGameObjectWithTag("Inventory");

        inventory_animator = inventory.GetComponent<Animator>();
        int hotbar_slots = inven.getHotbarNum_Slots();
        int inven_slots = inven.getInvenNum_Slots();
        int i = 0;

        for (; i < hotbar_slots; ++i) { //the 
            inven.setInventory_Slot(hotbar.transform.GetChild(i).GetChild(0).GetComponent<Inventory_Slot>(), i); //this is on awake so this should be fine
        }
        for (int j = 0; j < inven_slots; ++j) {
            inven.setInventory_Slot(inventory.transform.GetChild(j).GetChild(0).GetComponent<Inventory_Slot>(), i+j);
        }
    }

    void Update()
    {

        //Check inventory button
        InventoryOpenCheck();

        if (!can_move) return;

        // Look rotation:
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime);
        verticalLookRotation += Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -60, 60);
        cameraTransform.localEulerAngles = Vector3.left * verticalLookRotation;

        // Calculate movement:
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(inputX, 0, inputY).normalized;
        Vector3 targetMoveAmount = moveDir * walkSpeed;
        moveAmount = Vector3.SmoothDamp(moveAmount, targetMoveAmount, ref smoothMoveVelocity, .15f);

        // Jump
        if (Input.GetButtonDown("Jump"))
        {
            if (grounded)
            {
                GetComponent<Rigidbody>().AddForce(transform.up * jumpForce);
            }
        }

        // Grounded check
        Ray ray = new Ray(transform.position, -transform.up);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1 + .1f, groundedMask))
        {
            grounded = true;
        }
        else
        {
            grounded = false;
        }

        //Check shoot button.
        ShootCheck();

        

    }


    //Should be in the parent class
    private void InventoryOpenCheck() {
        if (Input.GetKeyDown(KeyCode.Tab)) {
            if (inventory_animator.GetCurrentAnimatorStateInfo(0).IsName("Inventory_Idle")) {
                inventory_animator.SetTrigger("Hide");
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                can_move = true;
            } else if (inventory_animator.GetCurrentAnimatorStateInfo(0).IsName("Inventory_Idle_HIde")) {
                inventory_animator.SetTrigger("Show");
                moveAmount = Vector3.zero;
                Cursor.lockState= CursorLockMode.None;
                Cursor.visible = true;
                can_move = false;
            }
        }
    }

    private void ShootCheck() {
        bool lmbDown = Input.GetMouseButton(0);
        laser.gameObject.SetActive(lmbDown);
        if (lmbDown)
        {
            LaserProtocol();
        }
        else {
            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = false;

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = false;

            t = 0;
        }
    }



    //TO BE INTEGRATED WITH MAIN GAMEPLAY LOOP ---------------------------------------------------------
    private void LaserProtocol()
    {
        //Assign position to the beginning and end of the laser
        Vector3[] positions = new Vector3[2];
        positions[0] = transform.position + (cameraTransform.right * 0.5f);


        Vector3 endPoint = new Vector3();

        //If object is hit
        if (Physics.Raycast(cameraTransform.transform.position, cameraTransform.transform.forward, out hit, reach, 1))
        {
            
            //Turn on respective particle systems-----------------------------------------------
            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = true;
            ParticleSystem spark = sparks.transform.GetChild(0).GetComponent<ParticleSystem>();
            if(!spark.isPlaying) spark.Play();

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = true;
            ParticleSystem dust = sparks.transform.GetChild(1).GetComponent<ParticleSystem>();
            if (!dust.isPlaying) dust.Play();
            //----------------------------------------------------------------------------------

            //Set the sparks particle system at end of laser
            sparks.transform.position = hit.point;
            sparks.transform.rotation = Quaternion.LookRotation(cameraTransform.position - hit.point);

            
            //Set Laser end to where it hits
            endPoint = hit.point;

            //Deal damage if an object is hit
            Resource_Class resource = hit.transform.tag == "Resource" ? hit.transform.GetComponent<Resource_Class>() : null;
            if (resource != null) { resource.dealDamage(5 * Time.deltaTime); }
        }
        else {

            endPoint = cameraTransform.position + (cameraTransform.forward * reach);

            var sparks_main = sparks.transform.GetChild(0).GetComponent<ParticleSystem>().main;
            sparks_main.loop = false;

            var dust_main = sparks.transform.GetChild(1).GetComponent<ParticleSystem>().main;
            dust_main.loop = false;

        }

        t += t > 1 ? 0 : 3f*Time.deltaTime;
        positions[1] = endPoint;//(endPoint - positions[0])*t + positions[0];
        laser.SetPositions(positions);
    }

    public Inventory getInventory() { return inven; }

    //-----------------------------------------------------------------------------------------


    void FixedUpdate()
    {
        // Apply movement to rigidbody
        Vector3 localMove = transform.TransformDirection(moveAmount) * Time.fixedDeltaTime;
        GetComponent<Rigidbody>().MovePosition(GetComponent<Rigidbody>().position + localMove);
    }
}