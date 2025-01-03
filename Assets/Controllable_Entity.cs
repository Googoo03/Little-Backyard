using Inven;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Controllable_Entity : MonoBehaviour
{
    //
    //THIS CLASS WILL STORE THINGS THAT ALL CONTROLLABLE ENTITIES WILL UTILIZE
    //SUCH AS INVENTORY, SEMAPHORES, MOVEMENT, ETC.
    [SerializeField] protected bool canMove;
    [SerializeField] protected Camera _camera;
    [SerializeField] protected Vector3 camera_offset;
    [SerializeField] protected float reach; //for interaction
    [SerializeField] protected GameObject nearbyPlanet;
    [SerializeField] protected Sphere nearbyPlanetScript;

    //EVENT MANAGER
    [SerializeField] protected Event_Manager_Script event_manager;

    //Inventory
    [SerializeField] protected Inventory inven;
    [SerializeField] protected Animator inventory_animator;
    void Start()
    {
        
    }


    protected abstract void MovementProtocol();

    protected abstract void InteractionCheck();

    public void setCanMove(bool move) { canMove = move; }

    public void setCamera(Camera cam) { _camera = cam; }

    public Vector3 getOffset() { return camera_offset; }

    public GameObject getNearbyPlanet() { return nearbyPlanet; }

    public Inventory getInventory() { return inven; }

    protected void LODCheckDistance()
    { //measures the distance between the player and nearbyPlanet. If close enough
      //make new LOD
      //
        if (!canMove || !nearbyPlanet) return; //if you are not in control, don't bother checking
        float distanceToNearestPlanet = Vector3.Distance(transform.position, nearbyPlanet.transform.position);
        float initialDistanceThreshold = nearbyPlanet.GetComponent<Sphere>().getInitialDistanceThreshold();

        nearbyPlanet.GetComponent<Sphere>().checkPatchDistances(transform.position);

        if (distanceToNearestPlanet < initialDistanceThreshold)
        {
            
        }
    }

    protected void CheckFallThroughPlanet() {
        if (canMove && Vector3.Distance(transform.position,nearbyPlanet.transform.position) < nearbyPlanetScript.getRadius()*0.5f) {
            //reset player object if fall through planet

            //get direction, normalize it. Times by a factor, set player to it.
            Vector3 toPlanetVector = (transform.position - nearbyPlanet.transform.position).normalized;
            toPlanetVector *= nearbyPlanetScript.getRadius() * 1.1f;

            transform.position = nearbyPlanet.transform.position + toPlanetVector;

        }
    }


    public void setNearbyPlanet(GameObject planet) { nearbyPlanet = planet; nearbyPlanetScript = nearbyPlanet.GetComponent<Sphere>(); }
}
