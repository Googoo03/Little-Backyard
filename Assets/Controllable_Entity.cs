using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Controllable_Entity : MonoBehaviour
{
    //
    //THIS CLASS WILL STORE THINGS THAT ALL CONTROLLABLE ENTITIES WILL UTILIZE
    //SUCH AS INVENTORY, SEMAPHORES, MOVEMENT, ETC.
    protected bool canMove;
    [SerializeField] protected Camera _camera;
    [SerializeField] protected Vector3 camera_offset;
    [SerializeField] protected float reach; //for interaction
    [SerializeField] protected GameObject nearbyPlanet;

    //EVENT MANAGER
    [SerializeField] protected Event_Manager_Script event_manager;
    void Start()
    {
        
    }

    protected abstract void MovementProtocol();

    protected abstract void InteractionCheck();

    public void setCanMove(bool move) { canMove = move; }

    public void setCamera(Camera cam) { _camera = cam; }

    public Vector3 getOffset() { return camera_offset; }

    public GameObject getNearbyPlanet() { return nearbyPlanet; }

    protected void LODCheckDistance()
    { //measures the distance between the player and nearbyPlanet. If close enough
      //make new LOD
      //
        if (!canMove || !nearbyPlanet) return; //if you are not in control, don't bother checking
        float distanceToNearestPlanet = Vector3.Distance(transform.position, nearbyPlanet.transform.position);
        float initialDistanceThreshold = nearbyPlanet.GetComponent<Sphere>().getInitialDistanceThreshold();

        if (distanceToNearestPlanet < initialDistanceThreshold)
        {
            nearbyPlanet.GetComponent<Sphere>().checkPatchDistances(transform.position);
        }
    }


    public void setNearbyPlanet(GameObject planet) { nearbyPlanet = planet; }
}
