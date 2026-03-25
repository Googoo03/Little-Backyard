using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipControls : Controllable_Entity
{
    public float speed;
    [SerializeField] private float baseSpeed;
    public float mouseSensitivityX;
    public float mouseSensitivityY;
    public float rollSensitivity;

    //[SerializeField] public GameObject nearbyPlanet;
    [SerializeField] private GameObject shipModel;
    private Vector3 shipOriginalRotation;

    public SolarSystemQuadTree planetQuadTree;
    public List<GameObject> planets = new List<GameObject> { };

    public Quaternion targetRotation;
    public Quaternion lastOrientation;


    [SerializeField] private float rollInput;
    [SerializeField] private float pitchInput;
    [SerializeField] private float yawInput;

    private float rollChange;
    private float yawChange;
    [SerializeField] private float pitchChange;

    private Quaternion yaw;
    private Quaternion pitch;
    private Quaternion roll;

    public float forward;


    [SerializeField] private bool tiltShipPlanet = false;

    //LANDING PROTOCOL
    [SerializeField] private bool landed = false;
    [SerializeField] private bool takeoff = false;
    [SerializeField] private Rigidbody _rigidbody;

    [SerializeField] private float distanceToNearestPlanet;
    [SerializeField] private float initialDistanceThreshold;
    [SerializeField] private float atmosphereDistance;
    [SerializeField] private float pullUpDistance;

    [SerializeField] private float boostAmount;

    private float boostSpeed = 0;

    public float angle;
    private Vector3 horizonDirection = new Vector3(1, 1, 1);

    [SerializeField] private Vector2 FOV; //sets the camera FOV based on sprint



    // Use this for initialization
    void Start() {
        _rigidbody = GetComponent<Rigidbody>();
        camera_offset = new Vector3(0, .0057f, -0.02228f);
        targetRotation = new Quaternion();
        shipOriginalRotation = shipModel.transform.localEulerAngles;

    }

    // Update is called once per frame
    void Update()
    {
        if (landed)
        {
            ApplyGravity(false);
            CheckExit();
        }
        if (takeoff) { ApplyGravity(true); takeoff = false; }

        if (!landed) MitigateForces();

        //traverseQuadTree(planetQuadTree);
        findNearestPlanet();


        //sets the distance each frame
        distanceToNearestPlanet = (nearbyPlanet != null) ? Vector3.Distance(this.transform.position, nearbyPlanet.transform.position) : float.MaxValue;

        LODCheckDistance();

        if (!canMove) return;

        MovementProtocol();
        



        //ISSUE IS THAT THE SPEED KNOCKS IT OUT OF RANGE. SOMEHOW THERE SHOULD BE A BUFFER ZONE
        //if within emergency pullup range, turn on tilt.
        //if out of atmosphere range, turn off tilt 
        if (emergencyPullUp())
        {
            tiltShipPlanet = true;
        }
        else if (!isInAtmosphere()) {
            tiltShipPlanet = false;
        }


        if (tiltShipPlanet) {
            //tilt ship towards nearbyPlanet
            tiltTowardsPlanet();
        }

    }

    private void CheckExit() {
        if (Input.GetKeyDown(KeyCode.E) && canMove) event_manager.set_exitShip(true);
    }
    private void ApplyGravity(bool negative) {

        int takeoffStrength = 20;
        Vector3 toPlanet = (nearbyPlanet.transform.position - transform.position).normalized;
        toPlanet = negative ? -toPlanet : toPlanet;
        toPlanet *= takeoff ? takeoffStrength : 1;
        if (_rigidbody != null)
        {
            _rigidbody.AddForce(toPlanet);
        }
        return;
    }

    private void MitigateForces(){
        if(_rigidbody.velocity.magnitude != 0) _rigidbody.AddForce(-_rigidbody.velocity.normalized);
    }

    private float EaseInOutCubic(float x) {
        return x < 0.5 ? 4 * x * x  : 1 - Mathf.Pow(-2 * x + 2, 2) / 2;
    }

    public void SpeedCalibration() {
        float d = (distanceToNearestPlanet - pullUpDistance) / pullUpDistance;
        speed *= Mathf.Max(EaseInOutCubic(d), .1f); //the 1.1 will be changed to "pullup distance" and the .1 is the minimum speed;
    }

    protected override void InteractionCheck() { }

    protected override void MovementProtocol() {
        
        targetRotation = transform.rotation;

        //change ship speed when in atmosphere. Slows down closer it gets
        bool sprint = (Input.GetKey(KeyCode.LeftShift));
        bool boost = (Input.GetKey(KeyCode.Space) && sprint && !isInAtmosphere()); //add a boost factor is space is held and the player is not in the atmosphere

        float boostDestination = boost ? boostAmount : 0;
        boostSpeed = Mathf.Lerp(boostSpeed, boostDestination, Time.deltaTime);
        boostSpeed = !boost ? 0 : boostSpeed;

        speed = (sprint ? 4 * baseSpeed : baseSpeed) + (boostSpeed);

        float _currentFOV = _camera.GetComponent<Camera>().fieldOfView;
        float _newFOV = sprint ? FOV[1] : FOV[0];
        _camera.GetComponent<Camera>().fieldOfView = Mathf.Lerp(_currentFOV, _newFOV, Time.deltaTime);

        if (isInAtmosphere()) SpeedCalibration();

        setKeyInputs();

        if (landed) return;//dont apply any trasforms if the ship has landed

        smoothKey(ref rollInput, rollSensitivity, rollChange);
        smoothKey(ref pitchInput, mouseSensitivityY, pitchChange);
        smoothKey(ref yawInput, mouseSensitivityX, yawChange);


        yaw = Quaternion.AngleAxis(yawInput, transform.up);
        pitch = Quaternion.AngleAxis(-pitchInput, transform.right);
        roll = Quaternion.AngleAxis(-rollInput, transform.forward);

        //set offset rotation of ship_model
        shipModel.transform.localRotation = Quaternion.Euler(shipOriginalRotation) * Quaternion.Euler(new Vector3(10*sigmoidFunction(-pitchInput), 10 * sigmoidFunction(rollInput)+(10 * sigmoidFunction(yawInput)), 10 * sigmoidFunction(yawInput)));

        targetRotation = yaw * pitch * roll * targetRotation;
        lastOrientation = yaw*pitch*roll*lastOrientation;//perhaps delete later.

        transform.rotation = targetRotation;

        /////Moving forward

        ///////////////////////

        transform.position += (transform.forward * forward) * speed * Time.deltaTime;

    }

    private void setKeyInputs() {
        forward = Input.GetAxis("Vertical");
        

        yawChange = Input.GetAxis("Mouse X") * mouseSensitivityX;
        pitchChange = Input.GetAxis("Mouse Y") * mouseSensitivityY;
        rollChange = -InputAxis(KeyCode.Q, KeyCode.E) * rollSensitivity;

        if (tiltShipPlanet && Input.GetButtonUp("Jump"))
        {
            landed = !landed; // if near a planet, land if the spacebar is pressed
            takeoff = !landed; //turn on takeoff only if we switch from landed to not landed
        }
    }



    private void smoothKey(ref float axis, float sensitivity, float axisChange) {
        if (axisChange == 0)
        {
            axis *= 1 - (sensitivity * Time.deltaTime); //diminish roll with time if no input change
            if (Mathf.Abs(axis) <= .00001f) axis = 0; //arbitrary small number so there is no creep
        }
        else
        {

            axis += axisChange*Time.deltaTime; //smooth rolling
            axis = Mathf.Clamp(axis, -1f, 1f); //prevents infinite speed increase
        }
    }

    private float sigmoidFunction(float x) {
        return (1 / (1 + Mathf.Pow(2.78f, -5 * x))) - 0.5f;
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
        return distanceToNearestPlanet < atmosphereDistance ? true : false;
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

        tiltHorizon();


    }

    private void tiltHorizon() {
        Vector3 toPlanetCenter = nearbyPlanet.transform.position - transform.position; //compute vector to the planet center
        float dotProduct; //find the dot product between horizonDirection and the vector to the planet

        // applies the rotation if and only if the player is looking towards the horizon or below
        dotProduct = Vector3.Dot(transform.forward, toPlanetCenter);
        float rotationSpeed = 1.0f;
        dotProduct = Vector3.Dot(horizonDirection, toPlanetCenter);
        if (Mathf.Abs(dotProduct) > .00001f) //if the calculation is off, which happens only when its moving
        {
            Vector3 newHorizonDirection = Vector3.Cross(transform.right, toPlanetCenter).normalized; //create new vector pointing towards the horizon
            if (Vector3.Dot(transform.forward, newHorizonDirection) < 0) //reorient the new vector if needed
            {
                horizonDirection = -newHorizonDirection;
            }
            else
            {
                horizonDirection = newHorizonDirection;
            }
            lastOrientation = Quaternion.LookRotation(horizonDirection, -toPlanetCenter);
        }

        Quaternion combinedRotation = Quaternion.Slerp(transform.rotation, lastOrientation, Time.deltaTime * rotationSpeed); //makes it smooth
        transform.rotation = combinedRotation;
    }

    //SHOULD THIS BE INHERITED???
    private void findNearestPlanet() {
        float minDistance = float.MaxValue;
        float dist;
        GameObject closestPlanet = null;

        if (planets.Count == 0) return; //dont do anything if the list is empty

        for (int i = 0;i < planets.Count;i++)
        {
            dist = Vector3.Distance(transform.position, planets[i].transform.position);
            closestPlanet = dist < minDistance ? planets[i] : closestPlanet;
            minDistance = dist < minDistance ? dist : minDistance;
        }

        if (nearbyPlanet) nearbyPlanet.transform.GetChild(1).gameObject.SetActive(true);
        nearbyPlanet = closestPlanet;

        initialDistanceThreshold = nearbyPlanet.GetComponent<Sphere>().getInitialDistanceThreshold(); //for LOD loading
        atmosphereDistance = nearbyPlanet.GetComponent<Sphere>().getAtmosphereDistance(); //self explanatory
        pullUpDistance = nearbyPlanet.GetComponent<Sphere>().getRadius() * 1.1f;

        //THESE SHOULD NOT BE IN HERE. IF ANYTHING IT SHOULD BE IN THE EVENT CONTROLLER
        nearbyPlanet.transform.GetChild(1).gameObject.SetActive(false);
        nearbyPlanet.GetComponent<Sphere>().SetRingShader();
        nearbyPlanet.GetComponent<Sphere>().SetAtmoShader();
    }

    private void traverseQuadTree(SolarSystemQuadTree node) {
        
        if (node.getPlanetCount() == 1) { //return if 1 planet. I.E no extra children
            if (nearbyPlanet != node.getPlanet(0))
            {
                nearbyPlanet = node.getPlanet(0);
                initialDistanceThreshold = nearbyPlanet.GetComponent<Sphere>().getInitialDistanceThreshold(); //for LOD loading
                atmosphereDistance = nearbyPlanet.GetComponent<Sphere>().getAtmosphereDistance(); //self explanatory
                pullUpDistance = nearbyPlanet.GetComponent<Sphere>().getRadius() * 1.1f;
                nearbyPlanet.transform.GetChild(1).gameObject.SetActive(false);
                nearbyPlanet.GetComponent<Sphere>().SetRingShader();
            }
        }
        if (node.getPlanetCount() == 0)
        { //return if 1 planet. I.E no extra children
            if(nearbyPlanet) nearbyPlanet.transform.GetChild(1).gameObject.SetActive(true);
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
