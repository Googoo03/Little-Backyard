using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource_Class : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private float health;
    //private Tuple<Item, int> resource;
    [SerializeField] private Resource_Preset preset;
    [SerializeField] private GameObject niblets; //need to be set internally
    [SerializeField] private GameObject dust;

    [SerializeField] private Event_Manager_Script event_manager;
    [SerializeField] private Object_Pool_Manager object_pool_manager;

    //BASIC CONSTRUCTOR WITH NO RESOURCE NAME. SHOULD ADD EXTRAS IN THE FUTURE
    public Resource_Class() {
        health = 10;
        

        //Item item = new Item();
        //resource = new Tuple<Item, int>(item,5);
    }

    public void Awake()
    {
        niblets = Resources.Load("FX/ParticleSystems/Object_Destroy", typeof(GameObject)) as GameObject;
        dust = Resources.Load("FX/ParticleSystems/Dust", typeof(GameObject)) as GameObject;
        event_manager = GameObject.FindGameObjectWithTag("Event_Manager").GetComponent<Event_Manager_Script>();
        object_pool_manager = GameObject.FindGameObjectWithTag("Object_Manager").GetComponent<Object_Pool_Manager>();
    }

    public float getHealth() {
        return health;
    }

    public void setResourcePreset(Resource_Preset pre) {
        preset = pre;
    }

    private void OnDisable()
    {
        if (health > 0) return;
        ParticleSystem _newniblets = Instantiate(niblets, transform.position, transform.rotation).GetComponent<ParticleSystem>();
        var main = _newniblets.main;
        Color color = preset.GetItem().getColor();
        main.startColor = color;

        //Instantiate(dust, transform.position, transform.rotation);
        ParticleSystem _newdust = Instantiate(dust, transform.position, transform.rotation).GetComponent<ParticleSystem>();
        var dustmain = _newdust.main;
        dustmain.startColor = color;

        Gradient grad = new Gradient();
        grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) }, 
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
        var dustCol = _newdust.colorOverLifetime;
        dustCol.color = grad;
    }

    public void dealDamage(float damage) {
        health -= damage;
        deathCheck();
    }

    public void deathCheck() {
        if (health < 0)
        {
            //disable object
            transform.gameObject.SetActive(false);

            //create particle effect to represent objects going to player. Should be same color

            //separate object pool for particles.
            //Instantiate(niblets, transform.position, transform.rotation);

            //add objects to player inventory

            //If no more slots, drop items on the ground (somehow)


            //somehow talk to object pool manager to release this guy
            Tuple<Item, int> new_item = new Tuple<Item, int>(preset.GetItem(), preset.GetQuantity());
            event_manager.getPlayerObjectScript().getInventory().add_item(new_item);

            List<GameObject> releaseList = new List<GameObject>() { transform.gameObject};

            object_pool_manager.releasePoolObjs(ref releaseList);
        }
    }
}
