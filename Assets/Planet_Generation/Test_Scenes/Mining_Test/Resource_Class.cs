using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource_Class : MonoBehaviour
{
    // Start is called before the first frame update
    private float health;
    private Tuple<Item, int> resource;
    [SerializeField] private ParticleSystem niblets;
    [SerializeField] private ParticleSystem dust;

    //BASIC CONSTRUCTOR WITH NO RESOURCE NAME. SHOULD ADD EXTRAS IN THE FUTURE
    public Resource_Class() {
        health = 10;
        Item item = new Item();
        resource = new Tuple<Item, int>(item,5);
    }

    public float getHealth() {
        return health;
    }

    private void OnDisable()
    {
        Instantiate(niblets, transform.position, transform.rotation);
        Instantiate(dust, transform.position, transform.rotation);
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
        }
    }
}
