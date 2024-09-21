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
    


    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        
        cameraTransform = Camera.main.transform;

        inven = new Inventory();
        GameObject hotbar = GameObject.FindGameObjectWithTag("Hotbar");
        int num_slots = inven.getNum_Slots();
        for (int i = 0; i < num_slots; ++i) { //the 
            inven.setInventory_Slot(hotbar.transform.GetChild(i).GetComponent<Inventory_Slot>(), i); //this is on awake so this should be fine
        }
    }

    void Update()
    {

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

    private void ShootCheck() {
        bool lmbDown = Input.GetMouseButton(0);
        laser.gameObject.SetActive(lmbDown);
        if (lmbDown)
        {
            LaserProtocol();
        }
        else {
            sparks.gameObject.SetActive(false);
        }
    }



    //TO BE INTEGRATED WITH MAIN GAMEPLAY LOOP ---------------------------------------------------------
    private void LaserProtocol()
    {
        //Assign position to the beginning and end of the laser
        Vector3[] positions = new Vector3[2];
        positions[0] = transform.position + (cameraTransform.right * 0.5f);
        positions[1] = cameraTransform.position + (cameraTransform.forward * 5);
        laser.SetPositions(positions);



        //If object is hit
        if (Physics.Raycast(cameraTransform.transform.position, cameraTransform.transform.forward, out hit, 5, 1))
        {
            //Set the sparks particle system at end of laser
            sparks.gameObject.SetActive(true);
            sparks.transform.position = hit.point;
            sparks.transform.rotation = Quaternion.LookRotation(cameraTransform.position - hit.point);

            //Deal damage if an object is hit
            //Debug.Log(hit.transform.name);
            Resource_Class resource = hit.transform.tag == "Resource" ? hit.transform.GetComponent<Resource_Class>() : null;
            if (resource != null) { resource.dealDamage(5 * Time.deltaTime); }
        }
        else {
            sparks.gameObject.SetActive(false);
        }
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