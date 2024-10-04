using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Event_Manager_Script : MonoBehaviour
{
    //THIS OBJECT IS THE MIDDLE MAN FOR ALL INTERACTIONS BETWEEN THE PLAYER AND THE ENVIRONMENT

    //SOLAR SYSTEM EVENTS
    [SerializeField ]List<GameObject> planets = new List<GameObject> { };
    [SerializeField] private bool updatePlanetList;

    //VEHICLE EVENTS
    [SerializeField] private bool exitShip;
    [SerializeField] private bool enterShip;

    private bool lerpCameraPosition;

    [SerializeField] private GameObject player;
    [SerializeField] private GameObject playerInteract;

    [SerializeField] private GameObject ship;
    [SerializeField] private Camera playerCamera; //used for both the player and vehicles

    //For the solar system states
    [SerializeField] private GameObject sun;
    [SerializeField] private bool generatedSolarSystem = false;

    [SerializeField] private Material atmosphereShader;
    [SerializeField] private Material planetRingShader;
    ///
    [SerializeField] private GalacticBox galacticCenter;

    void Start()
    {
        //INITIALIZE PARAMETERS
        lerpCameraPosition = false;
        exitShip = false;
        enterShip = false;

        //DISABLE THE PLAYER AT START
        disableObject(player);

        //TEMPORARY, ENABLE SHIP AT START
        ship.GetComponent<Controllable_Entity>().setCanMove(true);
        ship.GetComponent<Controllable_Entity>().setCamera(playerCamera);

        //sun.GetComponent<SolarSystemGeneration>().Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        if(exitShip) exitShipProtocol();
        if(enterShip) enterShipProtocol();
        if(lerpCameraPosition) lerpCameraToFromShip();
        if (updatePlanetList) updateShipPlanetList();

        //If a planet is generated, then the shader should be turned on
        if (planets.Count > 0)
        {
            atmosphereShader.SetInt("_Generate", 1);
            planetRingShader.SetInt("_Generate", 1);
        }
        else {
            //turn off the shader since no planets are present
            atmosphereShader.SetInt("_Generate", 0);
            planetRingShader.SetInt("_Generate", 0);
        }

        calculateClosestStar();
        if (sun != null && Vector3.Distance(playerCamera.transform.position, sun.transform.position) > 70 && generatedSolarSystem)
        {
            sun.GetComponent<SolarSystemGeneration>().Uninitialize();
            planets.Clear();

            
            updatePlanetList = true;

            generatedSolarSystem = false;
        }
        else if (sun != null && Vector3.Distance(playerCamera.transform.position, sun.transform.position) < 70 && !generatedSolarSystem) {
            sun.GetComponent<SolarSystemGeneration>().Initialize();
            atmosphereShader.SetVector("_SunPos", sun.transform.position);
            //atmosphereShader.SetFloat("_OceanRad", );
            ////////


            generatedSolarSystem = true;
        }
    }

    private void calculateClosestStar() {
        List<GameObject> stars = galacticCenter.getStars();
        float distance;
        float minDist = float.MaxValue;
        GameObject closestStar = null;

        for (int i = 0; i < stars.Count; ++i) {
            distance = Vector3.Distance(playerCamera.transform.position, stars[i].transform.position);
            closestStar = minDist != Mathf.Min(minDist, distance) ? stars[i] : closestStar;
            minDist = Mathf.Min(minDist, distance);
        }

        //if you change stars, reset the parameters
        if (closestStar != sun)
        {
            generatedSolarSystem = false;
            if(sun) sun.GetComponent<SolarSystemGeneration>().Uninitialize();
            sun = closestStar;
        }
        
    }

    private void updateShipPlanetList() {
        ship.GetComponent<ShipControls>().planets = planets;
        updatePlanetList = false;
    }

    //SHOULD THERE BE STATE MACHINES FOR EACH OBJECT?
    private void exitShipProtocol() {
        //place player at ship location, above it
        player.transform.position = ship.transform.position + (ship.transform.up * .05f); //the 2 is arbitrary
        player.transform.rotation = ship.transform.rotation;
        lerpCameraPosition = true;
        //activate player
        player.GetComponent<Player_Movement>().setNearbyPlanet(ship.GetComponent<ShipControls>().getNearbyPlanet());
        player.GetComponent<Controllable_Entity>().setCanMove(true);
        player.SetActive(true);
    }

    private void enterShipProtocol()
    {
        lerpCameraPosition = true;
        player.GetComponent<Controllable_Entity>().setCanMove(false);
    }

    private void lerpCameraToFromShip() {
        Transform destination = exitShip ? player.transform : playerInteract.transform;
        //Transform fromTranform = exitShip ? playerInteract.transform : player.transform;
        Vector3 offset = destination.GetComponent<Controllable_Entity>().getOffset();
        float scale = destination.transform.lossyScale.x;

        Vector3 offsetRespecttoRotation = ((destination.transform.right*offset.x) + (destination.transform.up*offset.y) + (destination.transform.forward*offset.z))*scale;

        if (Vector3.Distance(playerCamera.transform.position, destination.position+(offsetRespecttoRotation)) > .001f)
        {
            //if not close enough, lerp
            playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, destination.position + (offsetRespecttoRotation), 3*Time.deltaTime);
            playerCamera.transform.rotation = Quaternion.Lerp(playerCamera.transform.rotation, destination.rotation, 10 * Time.deltaTime);
        }
        else {
            //if close enough, set parent to the destination, turn off lerp.
            playerCamera.transform.parent.GetComponent<Controllable_Entity>().setCanMove(false);
            playerCamera.transform.parent.GetComponent<Controllable_Entity>().setCamera(null);

            destination.GetComponent<Controllable_Entity>().setCanMove(true);
            playerCamera.transform.parent = destination;
            playerCamera.transform.localPosition = offset;

            destination.GetComponent<Controllable_Entity>().setCamera(playerCamera);
            //fromTranform.GetComponent<Controllable_Entity>().setCamera(null);
            lerpCameraPosition=false;

            if(destination != player.transform) player.SetActive(false);
            

            //reset needed variables. These should be mutually exclusive?
            exitShip = exitShip ? false : exitShip;
            enterShip = enterShip ? false : enterShip;
        }
    }


    private void disableObject(GameObject obj) { //IM ASSUMING THAT THIS IS FINE WITH NO REFERENCE
        obj.SetActive(false);
        obj.transform.position = Vector3.zero;
    }

    public void set_planetList(bool update, ref List<GameObject> list) {
        planets = list;
        updatePlanetList = update;
    }

    public void set_exitShip(bool exit) {exitShip = exit;}
    public void set_enterShip(bool enter) { enterShip = enter;}

    public void set_PlayerInteract(GameObject obj) {playerInteract = obj;}

    public Camera get_playerCamera() { return playerCamera; }

    //compare distances between stars but how?
    public void set_galacticCenter(GalacticBox gc) {galacticCenter = gc;}

    public GameObject get_sun() { return sun; }

    public GameObject getPlayerObject() { return playerCamera.transform.parent.gameObject; }

    public Controllable_Entity getPlayerObjectScript() { return playerCamera.transform.parent.GetComponent<Controllable_Entity>(); }
}
