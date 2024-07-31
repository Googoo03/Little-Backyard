using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Event_Manager_Script : MonoBehaviour
{
    // Start is called before the first frame update
    private bool exitShip;
    private bool enterShip;

    private bool lerpCameraPosition;

    [SerializeField] private GameObject player;
    [SerializeField] private GameObject ship;
    [SerializeField] private Camera playerCamera; //used for both the player and vehicles

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
    }

    // Update is called once per frame
    void Update()
    {
        if(exitShip) exitShipProtocol();
        if(lerpCameraPosition) lerpCameraToFromShip();
    }

    //SHOULD THERE BE STATE MACHINES FOR EACH OBJECT?
    private void exitShipProtocol() {
        //place player at ship location, above it
        player.transform.position = ship.transform.position + (ship.transform.up * .05f); //the 2 is arbitrary
        lerpCameraPosition = true;
        //activate player
        player.GetComponent<Player_Movement>().setNearbyPlanet(ship.GetComponent<ShipControls>().nearbyPlanet);
        player.GetComponent<Controllable_Entity>().setCanMove(true);
        player.SetActive(true);
    }

    private void lerpCameraToFromShip() {
        Transform destination = exitShip ? player.transform : ship.transform;
        Vector3 offset = destination.GetComponent<Controllable_Entity>().getOffset();
        float scale = destination.transform.lossyScale.x;

        Vector3 offsetRespecttoRotation = ((destination.transform.right*offset.x) + (destination.transform.up*offset.y) + (destination.transform.forward*offset.z))*scale;

        if (Vector3.Distance(playerCamera.transform.position, destination.position+(offsetRespecttoRotation)) > .01f)
        {
            //if not close enough, lerp
            playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, destination.position + (offsetRespecttoRotation), 3*Time.deltaTime);
        }
        else {
            //if close enough, set parent to the destination, turn off lerp.
            playerCamera.transform.parent.GetComponent<Controllable_Entity>().setCanMove(false);
            playerCamera.transform.parent = destination;
            playerCamera.transform.localPosition = offset;
            destination.GetComponent<Controllable_Entity>().setCamera(playerCamera);
            lerpCameraPosition=false;

            //reset needed variables. These should be mutually exclusive?
            exitShip = exitShip ? false : exitShip;
            enterShip = enterShip ? false : enterShip;
        }
    }


    private void disableObject(GameObject obj) { //IM ASSUMING THAT THIS IS FINE WITH NO REFERENCE
        obj.SetActive(false);
        obj.transform.position = Vector3.zero;
    }

    public void set_exitShip(bool exit) {exitShip = exit;}
    public void set_enterShip(bool enter) { enterShip = enter;}
}
