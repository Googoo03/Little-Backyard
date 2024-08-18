using ProceduralNoiseProject;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class SolarSystemGeneration : MonoBehaviour {

    [SerializeField]private GameObject planet;
    [SerializeField] private GameObject Event_Manager;


    [SerializeField]private float seed;
    [SerializeField]private int density;

    [SerializeField] private int planetCount;

    [SerializeField]private int RegionRadius;
    [SerializeField]private int RegionHeight;

    List<GameObject> planets = new List<GameObject> { };

    public float solarsystemSpacing;

    public SolarSystemQuadTree quadTree; //doesnt show up for some reason, but is still there

    [SerializeField] private Material Sun_Halo;

    // Use this for initialization
    public void Initialize () {

        //Generates solar system
        GenerateSolarSystem();


        //Sets the event manager's planet list to said planets
        Event_Manager.GetComponent<Event_Manager_Script>().set_planetList(true, ref planets);

        //Sets necessary shader variables for ring HUD
        Sun_Halo.SetInt("_PlanetCount", planetCount);
        Sun_Halo.SetFloat("_OrbitRad", RegionRadius / planetCount);
    }

    // Update is called once per frame

    public void GenerateSolarSystem() {
        //List<GameObject> planets = new List<GameObject> { };



        for (int x = 0; x < planetCount; x++)
        {

            var hashValue = new Hash128();
            hashValue.Append(transform.position.x + x); //Keep these two append functions separate
            hashValue.Append(seed);
            int hashCode = hashValue.GetHashCode();
            //fix so that a reasonable amount of planets spawn. maybe put a cap? 
            //ALSO ADD SO THAT THE PLANETS ARE SPUN AROUND A RANDOM THETA TO MAKE RANDOM PLACEMENT


            if (hashCode % density == 0){
                //ROTATE RANDOMLY ALONG CIRCULAR PATH
                float cosineVal = Mathf.Cos( (hashCode % 360)); //the 500 is so its within 2pi range 
                float sineVal = Mathf.Sin( (hashCode % 360));
                float fracX = ( (x+1) / (float)planetCount) * RegionRadius; //each planet is a set fraction distance away from the sun.

                Vector3 position = new Vector3(transform.position.x + (fracX * cosineVal), transform.position.y, transform.position.z + (fracX * sineVal));
                GameObject newPlanet = Instantiate(planet, position, Quaternion.identity);
                planets.Add(newPlanet);

            }
        }  
    }

    private void GenerateQuadTree() {
        //subdivide if necessary
        quadTree.subdivide();
    }
}
