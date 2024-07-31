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
    void Start()
    {
        
    }

    protected abstract void MovementProtocol();

    public void setCanMove(bool move) { canMove = move; }

    public void setCamera(Camera cam) { _camera = cam; }

    public Vector3 getOffset() { return camera_offset; }
}
