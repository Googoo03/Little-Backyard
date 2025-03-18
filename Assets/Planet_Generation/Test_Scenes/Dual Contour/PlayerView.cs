using chunk_events;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerView : MonoBehaviour
{
    enum PLAYER_STATES { 
        IDLE,
        SPIN,
    };

    enum DIG_MODE { 
        NORMAL,
        FLATTEN
    };


    // Start is called before the first frame update
    [SerializeField] private Vector3 prevCameraTransform;
    [SerializeField] private Camera playerCam;
    [SerializeField] private float panSpeed;
    [SerializeField] private float scrollSpeed;
    private bool cameraSpin;


    private PLAYER_STATES playerState;
    private DIG_MODE dig_mode = DIG_MODE.NORMAL;
    private Vector3 start_dig_pos;


    private bool reverseSpin;
    private float spinT;

    //Testing
    [SerializeField] Vector3 testCubePosition;
    [SerializeField] GameObject testCube;

    //Tick Manager
    [SerializeField] private Tick_Manager tickManager;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        switch (playerState) { 
            case PLAYER_STATES.IDLE:

                if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.A)))
                {
                    playerState = PLAYER_STATES.SPIN;

                    reverseSpin = Input.GetKeyDown(KeyCode.A);
                    prevCameraTransform = transform.rotation.eulerAngles;
                }
                if (Input.GetKey(KeyCode.W)) transform.position += new Vector3(playerCam.transform.forward.x, 0, playerCam.transform.forward.z).normalized * Time.deltaTime * panSpeed;
                playerCam.GetComponent<Camera>().orthographicSize += Input.mouseScrollDelta.y * scrollSpeed;


                if (Input.GetKeyDown(KeyCode.F)) dig_mode = dig_mode == DIG_MODE.NORMAL ? DIG_MODE.FLATTEN : DIG_MODE.NORMAL;

                break;
            case PLAYER_STATES.SPIN:

                SpinCamera(reverseSpin);

                break;
            default: break;
        }


        
        
        //if (cameraSpin) SpinCamera(reverseSpin);
        


    }

    private void OnGUI()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            RaycastHit hit;
            Event currEvent = Event.current;
            Vector2 mousePos = new Vector2(currEvent.mousePosition.x, playerCam.pixelHeight - currEvent.mousePosition.y);
            Vector3 startPosition = playerCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, playerCam.nearClipPlane));
            if (Physics.Raycast(startPosition, playerCam.transform.forward, out hit, Mathf.Infinity))
            {
                start_dig_pos = hit.point;

            }
        }

        if (Input.GetButton("Fire1"))
        {
            Event currEvent = Event.current;
            Vector2 mousePos = new Vector2(currEvent.mousePosition.x, playerCam.pixelHeight - currEvent.mousePosition.y);

            Vector3 startPosition = playerCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, playerCam.nearClipPlane));

            RaycastHit hit;
            if (Physics.Raycast(startPosition,playerCam.transform.forward, out hit, Mathf.Infinity)) {
                testCubePosition = hit.point;
                
            }
            //Find chunk for hit
            int _id = hit.transform ? hit.transform.GetComponent<DC_Chunk>().GetInstanceID() : 0;
            //int chunk_index = tickManager.findChunk(_id);

            if (dig_mode == DIG_MODE.FLATTEN && hit.point.y < (int)start_dig_pos.y) return;



            tickManager.pushEvent(new chunk_event(hit.point,_id) );
            testCube.transform.position = testCubePosition;
            testCube.transform.forward = hit.normal;
        }
    }

    private void SpinCamera(bool reverse) {
        Vector3 newRot = prevCameraTransform;
        newRot += new Vector3(0,reverse ? -90 : 90,0);
        Vector3 interRot = Vector3.Lerp(prevCameraTransform, newRot, easeFunction(spinT));

        transform.rotation = Quaternion.Euler(interRot.x,interRot.y,interRot.z);
        spinT += 1f * Time.deltaTime;
        if (spinT > 1)
        {
            //cameraSpin = false;
            playerState = PLAYER_STATES.IDLE;
            spinT = 0;
        }
    }

    private float easeFunction(float t)
    {
        return 1 - Mathf.Pow(1 - t, 8);

    }
}
