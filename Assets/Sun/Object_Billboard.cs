using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Object_Billboard : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Event_Manager_Script event_manager;
    [SerializeField] private Camera _playerCamera;


    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(_playerCamera.transform.position);
    }

    public void setEventManager(Event_Manager_Script e_m) { 
        event_manager = e_m;
        _playerCamera = event_manager.get_playerCamera(); // since this is the only thing the script is doing, it doesnt matter if its here.
    }
}
