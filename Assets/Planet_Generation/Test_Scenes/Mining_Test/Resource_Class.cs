using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource_Class : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private float health;
    //private Tuple<Item, int> resource;
    [SerializeField] private Item resource;
    [SerializeField] private int resource_quantity;
    [SerializeField] private ParticleSystem niblets;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private Color color;

    [SerializeField] private FirstPersonController player;

    //BASIC CONSTRUCTOR WITH NO RESOURCE NAME. SHOULD ADD EXTRAS IN THE FUTURE
    public Resource_Class() {
        health = 10;
        //Item item = new Item();
        //resource = new Tuple<Item, int>(item,5);
    }

    public float getHealth() {
        return health;
    }

    private void OnDisable()
    {
        ParticleSystem _newniblets = Instantiate(niblets, transform.position, transform.rotation);
        var main = _newniblets.main;
        main.startColor = color;

        //Instantiate(dust, transform.position, transform.rotation);
        ParticleSystem _newdust = Instantiate(dust, transform.position, transform.rotation);
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
            Tuple<Item, int> new_item = new Tuple<Item, int>(resource, resource_quantity);
            if (player.getInventory().add_item(new_item)) Debug.Log("Added " + resource.getName());
        }
    }
}
