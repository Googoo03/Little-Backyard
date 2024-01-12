using System;
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

    public SolarSystemQuadTree planetQuadTree;

    public Quaternion targetRotation;
    public Quaternion lastOrientation;

    private float yawInput;
    private float pitchInput;
    private float rollInput;
    private Quaternion yaw;
    private Quaternion pitch;
    private Quaternion roll;

    public float forward;

    private bool tiltShipPlanet = false;
    public float distanceToNearestPlanet;
    private float pullUpDistance = 1.1f; //should be grabbed from the planet when it's said and done

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

        //sets the distance each frame
        distanceToNearestPlanet = (nearbyPlanet != null) ? Vector3.Distance(this.transform.position, nearbyPlanet.transform.position) : float.MaxValue;

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

    private float EaseInOutCubic(float x) {
        return x < 0.5 ? 4 * x * x  : 1 - Mathf.Pow(-2 * x + 2, 2) / 2;
    }

    public void SpeedCalibration() {
        float d = (distanceToNearestPlanet - pullUpDistance) / pullUpDistance;
        speed *= Mathf.Max(EaseInOutCubic(d), .1f); //the 1.1 will be changed to "pullup distance" and the .1 is the minimum speed;
    }
    private void MovementProtocol() {
        
        targetRotation = transform.rotation;
        
        //change ship speed when in atmosphere. Slows down closer it gets
        speed = (Input.GetKey(KeyCode.LeftShift)) ? 1f : 0.25f;
        if (isInAtmosphere()) SpeedCalibration();

        forward = Input.GetAxis("Vertical");

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
        
        ///////////////////////

        transform.position += transform.forward * (forward * speed) * Time.deltaTime;

    }

    private void LODCheckDistance() { //measures the distance between the player and nearbyPlanet. If close enough
                                      //make new LOD      
            if (distanceToNearestPlanet < 4) {
                nearbyPlanet.GetComponent<Sphere>().checkPatchDistances(this.gameObject);
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
        return distanceToNearestPlanet < pullUpDistance ? true : false;
        //the "2" is a placeholder value. in the future it should be the atmosphere level of the planet
    }

    private bool isInAtmosphere()
    { //used to tilt the ship towards the planet if within range.
        return distanceToNearestPlanet < 1.5f ? true : false;
    }

    private void tiltTowardsPlanet() {

        

        
        Vector3 toPlanetCenter = nearbyPlanet.transform.position - transform.position; //compute vector to the planet center
        float dotProduct; //find the dot product between horizonDirection and the vector to the planet

        // applies the rotation if and only if the player is looking towards the horizon or below
        dotProduct = Vector3.Dot(transform.forward, toPlanetCenter);

        //conditions when a reorientation is not necessary. Case 1 is when player is pointed away from planet
        //Case 2 is when player is looking at planet but is in reverse
        if (dotProduct < -0.2f) return;
        if (dotProduct > -0.2f && forward < 0) return;
        //adjust player position such that it always stays at pullUpdistance when circling
        //HERE
        ///

        float rotationSpeed = 1.0f;
        dotProduct = Vector3.Dot(horizonDirection, toPlanetCenter);
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
        transform.rotation = combinedRotation;


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
