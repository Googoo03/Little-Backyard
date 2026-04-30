using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FloraLSystem;

public class Features_Manager : MonoBehaviour
{
    //This class will have the l system namespace
    //upon provokation, it will generate a set of features (trees, rocks, etc)
    //from a set seed

    //When finished, the resulting meshes will exist in the manager, and can be pulled at will by other managers

    //Will the features manager need to generate more than once?
    //Let's say it does, any additional meshes generated will be sent to the object calling it.
    //We'll separate functions accordingly

    public Flora_L_System flora_L_System;

    //the seed will propagate from the planet, from the solar system manager
    int seed;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GenerateFeatures()
    {
        GenerateFlora();
    }

    public void GenerateFlora() { }
}
