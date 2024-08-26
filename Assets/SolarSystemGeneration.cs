using ProceduralNoiseProject;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;


public class SolarSystemGeneration : MonoBehaviour {

    [SerializeField]private GameObject planet;
    [SerializeField] private Event_Manager_Script event_manager;


    [SerializeField]private float seed;
    [SerializeField]private int density;

    [SerializeField] private int planetCount;

    [SerializeField]private int RegionRadius;
    [SerializeField]private int RegionHeight;

    List<GameObject> planets = new List<GameObject> { };

    public float solarsystemSpacing;

    public SolarSystemQuadTree quadTree; //doesnt show up for some reason, but is still there

    [SerializeField] private Material Sun_Halo;
    [SerializeField] private Color starColor;

    [SerializeField] private Mesh sunLoaded; //icosohedron
    [SerializeField] private Mesh sunUnloaded; //octahedron

    //TESTING ONLY
    [SerializeField] bool initialize = false;

    // Use this for initialization
    public void Initialize () {

        //Generates solar system
        GenerateSolarSystem();

        //Set the sun to an icosohedron mesh
        transform.GetComponent<MeshFilter>().mesh = sunLoaded;

        //Sets the event manager's planet list to said planets
        Assert.IsTrue(event_manager);
        event_manager.set_planetList(true, ref planets);

        //Sets necessary shader variables for ring HUD
        Sun_Halo.SetInt("_PlanetCount", planetCount);
        Sun_Halo.SetFloat("_OrbitRad", RegionRadius / planetCount);
        Sun_Halo.SetColor("_HaloColor", starColor);
    }



    /*public void Update()
    {
        if (initialize)
        {
            Initialize();
            initialize = false;
        }
    }*/
    public void Uninitialize() { //delete planets, should be object pool in future

        transform.GetComponent<MeshFilter>().mesh = sunUnloaded;
        //This needs to change so it doesnt have an infinite loop
        for (int i = 0; i < transform.childCount; ++i) {
            transform.GetChild(i).gameObject.SetActive(false);
        }

    }

    public void Start()
    {

        //Assign the star a color
        var hashValue = new Hash128();
        hashValue.Append(seed);
        hashValue.Append(transform.position.x);
        hashValue.Append(transform.position.y);
        hashValue.Append(transform.position.z);
        UInt64 hashCode = (UInt64)hashValue.GetHashCode();

        
        starColor = new Color((hashCode >> 3) % 256, (hashCode>> 6) % 256, (hashCode>> 9) % 256, (hashCode>> 12) % 256);
        starColor/= 256.0f;

        transform.GetComponent<Renderer>().material.SetColor("_Color",starColor);
        ////////////////////////
    }

    public void setEventManager(Event_Manager_Script e_m) { event_manager = e_m; }

    public void GenerateSolarSystem() {

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
                newPlanet.transform.parent = transform; //we want the planets to be children

                planets.Add(newPlanet);

            }
        }  
    }

    private void GenerateQuadTree() {
        //subdivide if necessary
        quadTree.subdivide();
    }
}
