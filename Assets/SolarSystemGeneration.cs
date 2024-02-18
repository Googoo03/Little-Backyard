using ProceduralNoiseProject;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class SolarSystemGeneration : MonoBehaviour {

    public GameObject planet;
    public float seed;
    public float scale;

    public int density;

    public int RegionWidth;
    public int RegionHeight;

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
        for (int x = Mathf.FloorToInt(transform.localScale.x + 5); x < RegionWidth; x++)
        {


            //fix so that a reasonable amount of planets spawn. maybe put a cap? 
            //ALSO ADD SO THAT THE PLANETS ARE SPUN AROUND A RANDOM THETA TO MAKE RANDOM PLACEMENT
            var hashValue = new Hash128();
            hashValue.Append(transform.position.x + x); //Keep these two append functions separate
            hashValue.Append(seed);
            int hashCode = hashValue.GetHashCode();

            if (hashCode % density == 0){
                float cosineVal = Mathf.Cos( (hashCode % 360)); //the 500 is so its within 2pi range 
                float sineVal = Mathf.Sin( (hashCode % 360));
                Vector3 position = new Vector3(transform.position.x + (x * solarsystemSpacing * cosineVal), transform.position.y, transform.position.z + (x * solarsystemSpacing * sineVal));
                GameObject newPlanet = Instantiate(planet, position, Quaternion.identity);
                planets.Add(newPlanet);

                //ROTATE RANDOMLY ALONG CIRCULAR PATH

                //add planets to root of quadtree
                quadTree.addPlanet(newPlanet);
                }
        }
        //transform.GetChild(0).GetComponent<ShipControls>().planets = planets;
        
    }

    private void GenerateQuadTree() {
        //subdivide if necessary
        quadTree.subdivide();
    }
}
