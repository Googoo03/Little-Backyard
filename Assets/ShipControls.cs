using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipControls : MonoBehaviour
{
	public float speed;
	public float mouseSensitivityX = 250;
	public float mouseSensitivityY = 250;
    public float rollSensitivity;

    public GameObject nearbyPlanet;
	
	
	Vector3 moveAmount;
	Vector3 smoothMoveVelocity;

	Transform cameraTransform;

    public SolarSystemQuadTree planetQuadTree;

    public Quaternion targetRotation;
    public Quaternion lastOrientation;


    //for testing purposes, should be private when its all said and done
    public float yawInput;
    public float pitchInput;
    public float rollInput;
    public Quaternion yaw;
    public Quaternion pitch;
    public Quaternion roll;

    private bool tiltShipPlanet = false;

    public float angle;
    private Vector3 horizonDirection = new Vector3(1,1,1);

    bool inAtmosphere; //would it be smart to initialize to false?
    
	// Use this for initialization
	void Start () {
        targetRotation = new Quaternion();
	}
	
	// Update is called once per frame
	void Update ()
	{




        MovementProtocol();

        traverseQuadTree(planetQuadTree);
        LODCheckDistance();

        //ISSUE IS THAT THE SPEED KNOCKS IT OUT OF RANGE. SOMEHOW THERE SHOULD BE A BUFFER ZONE
        //if within emergency pullup range, turn on tilt.
        //if out of atmosphere range, turn off tilt 
        if (emergencyPullUp())
        {
            tiltShipPlanet = true;
        }
        else if(!isInAtmosphere()){
            tiltShipPlanet = false;
        }


        if (tiltShipPlanet) {
            //tilt ship towards nearbyPlanet
            tiltTowardsPlanet();
        }

    }

    private void MovementProtocol() {
        
        targetRotation = transform.rotation;
        
        speed = (Input.GetKey(KeyCode.LeftShift)) ? 1f : 0.25f;


        yawInput = Input.GetAxis("Mouse X") * mouseSensitivityX;
        pitchInput = Input.GetAxis("Mouse Y") * mouseSensitivityY;


        float rollChange = -InputAxis(KeyCode.Q, KeyCode.E) * rollSensitivity * Time.deltaTime;
        if (rollChange == 0)
        {
            rollInput *= 1-(rollSensitivity*Time.deltaTime); //diminish roll with time if no input change
            if (Mathf.Abs(rollInput) <= .00001f) rollInput = 0; //arbitrary small number so there is no creep
        }
        else {
            rollInput += rollChange; //smooth rolling
            rollInput = Mathf.Clamp(rollInput, -1f, 1f); //prevents infinite speed increase
        }

        

        yaw = Quaternion.AngleAxis(yawInput, transform.up);
        pitch = Quaternion.AngleAxis(-pitchInput, transform.right);
        roll = Quaternion.AngleAxis(-rollInput, transform.forward);

        targetRotation = yaw * pitch * roll * targetRotation;
        lastOrientation = yaw*pitch*roll*lastOrientation;//perhaps delete later.
        float strength = Mathf.Min(0.5f * Time.deltaTime, 1);

        //transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation,strength);
        transform.rotation = targetRotation;
        
        /////Moving forward
        float forward = Input.GetAxis("Vertical");
        ///////////////////////

        transform.position += transform.forward * (forward * speed) * Time.deltaTime;

    }

    private void LODCheckDistance() { //measures the distance between the player and nearbyPlanet. If close enough
                                      //make new LOD
        if (nearbyPlanet != null)
        {
            if (Vector3.Distance(this.transform.position, nearbyPlanet.transform.position) < 4) {
                nearbyPlanet.GetComponent<Sphere>().checkPatchDistances(this.gameObject);
            }
            
        }
        
    }

    private int InputAxis(KeyCode buttonA, KeyCode buttonB) {
        bool pressed_A = Input.GetKey(buttonA);
        bool pressed_B = Input.GetKey(buttonB);
        int result = 0;
        

        if (pressed_A && !pressed_B) { result = 1; }
        else if (!pressed_A && pressed_B) { result = -1; }
        else { result = 0; }

        return result;
    }

    private bool emergencyPullUp() { //used to tilt the ship towards the planet if within range.
        if (nearbyPlanet != null)
        {
            //the "2" is a placeholder value. in the future it should be the atmosphere level of the planet
            if (Vector3.Distance(this.transform.position, nearbyPlanet.transform.position) < 1.1f) return true;
        }
        return false;
    }

    private bool isInAtmosphere()
    { //used to tilt the ship towards the planet if within range.
        if (nearbyPlanet != null)
        {
            //the "2" is a placeholder value. in the future it should be the atmosphere level of the planet
            if (Vector3.Distance(this.transform.position, nearbyPlanet.transform.position) < 1.5f) return true;
        }
        return false;
    }

    private void tiltTowardsPlanet() {
        

        //EITHER IS TOO HARSH WHEN FIRST ENTERING, OR IS TOO LENIENT WHEN CIRCLING NORMALLY.
        //WE WANT LENIENT WHEN FIRST ENTERING, HARSH WHEN CIRCLING.
        float rotationSpeed = 1.0f;
        Vector3 toPlanetCenter = nearbyPlanet.transform.position - transform.position; //compute vector to the planet center
        float dotProduct = Vector3.Dot(horizonDirection, toPlanetCenter); //find the dot product between horizonDirection and the vector to the planet

        if (Mathf.Abs(dotProduct) >.00001f) //if the calculation is off, which happens only when its moving
        {
            Vector3 newHorizonDirection = Vector3.Cross(transform.right, toPlanetCenter).normalized; //create new vector pointing towards the horizon
            if (Vector3.Dot(transform.forward, newHorizonDirection) < 0) //reorient the new vector if needed
            {
                horizonDirection = -newHorizonDirection;
            }
            else {
                horizonDirection = newHorizonDirection;
            }
            lastOrientation = Quaternion.LookRotation(horizonDirection, -toPlanetCenter);
        }
        
        Quaternion combinedRotation = Quaternion.Slerp(transform.rotation, lastOrientation, Time.deltaTime * rotationSpeed); //makes it smooth

        // applies the rotation if and only if the player is looking towards the horizon or below
        dotProduct = Vector3.Dot(transform.forward, toPlanetCenter);
        if (dotProduct > -0.2f) transform.rotation = combinedRotation;
    }

    private void traverseQuadTree(SolarSystemQuadTree node) {
        
        if (node.getPlanetCount() == 1) { //return if 1 planet. I.E no extra children
            nearbyPlanet = node.getPlanet(0);
        }
        if (node.getPlanetCount() == 0)
        { //return if 1 planet. I.E no extra children
            
            nearbyPlanet = null;
        }
        if (node.getChildCount() == 0) return; //return if no children

        bool eastSector = ((node.bounds.x + node.size / 2) - transform.position.x < 0); //determines if to the north of root
        bool northSector = (transform.position.z - (node.bounds.y + node.size / 2) < 0); //determines if to the east of root

        if (!northSector && !eastSector) { traverseQuadTree(node.getChild(2)); }
        else if (!northSector && eastSector) { traverseQuadTree(node.getChild(3)); }
        else if (northSector && !eastSector) { traverseQuadTree(node.getChild(0)); }
        else if (northSector && eastSector) { traverseQuadTree(node.getChild(1)); }

        return;
    }
}
