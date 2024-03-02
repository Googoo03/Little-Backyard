using ProceduralNoiseProject;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class SolarSystemGeneration : MonoBehaviour {

    [SerializeField]private GameObject planet;


    [SerializeField]private float seed;
    [SerializeField]private int density;

    [SerializeField] private int planetCount;

    [SerializeField]private int RegionWidth;
    [SerializeField]private int RegionHeight;

    public float solarsystemSpacing;

    public SolarSystemQuadTree quadTree; //doesnt show up for some reason, but is still there

    // Use this for initialization
    void Start () {
        Vector2 quadTreeBounds = new Vector2(transform.position.x - RegionWidth-10, transform.position.z - RegionHeight-10);
        List<GameObject> empty = new List<GameObject>();
        quadTree = new SolarSystemQuadTree(quadTreeBounds, RegionWidth * 2, empty, null);
        GenerateSolarSystem();
        GenerateQuadTree();

        transform.GetChild(0).GetComponent<ShipControls>().planetQuadTree = quadTree;
    }

    // Update is called once per frame

    public void GenerateSolarSystem() {
        List<GameObject> planets = new List<GameObject> { };



        for (int x = 0; x < planetCount; x++)
        {

            var hashValue = new Hash128();
            hashValue.Append(transform.position.x + x); //Keep these two append functions separate
            hashValue.Append(seed);
            int hashCode = hashValue.GetHashCode();
            //fix so that a reasonable amount of planets spawn. maybe put a cap? 
            //ALSO ADD SO THAT THE PLANETS ARE SPUN AROUND A RANDOM THETA TO MAKE RANDOM PLACEMENT


            if (hashCode % density == 0){
                float cosineVal = Mathf.Cos( (hashCode % 360)); //the 500 is so its within 2pi range 
                float sineVal = Mathf.Sin( (hashCode % 360));
                float fracX = ( (x+1) / (float)planetCount) * RegionWidth; //each planet is a set fraction distance away from the sun.

                Vector3 position = new Vector3(transform.position.x + (fracX * cosineVal), transform.position.y, transform.position.z + (fracX * sineVal));
                GameObject newPlanet = Instantiate(planet, position, Quaternion.identity);
                planets.Add(newPlanet);

                //ROTATE RANDOMLY ALONG CIRCULAR PATH

                //add planets to root of quadtree
                quadTree.addPlanet(newPlanet);
                }
        }  
    }

    private void GenerateQuadTree() {
        //subdivide if necessary
        quadTree.subdivide();
    }
}
