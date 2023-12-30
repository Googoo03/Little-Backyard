using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;

public class SolarSystemQuadTree
{
    //THIS MAY NEED TO DERIVE FROM MONOBEHAVIOUR.......YES ADD THE U IN BEHAVIOUR

    // THIS SCRIPT CREATES A QUADTREE SO THAT EACH PLANET IS IN EXACTLY 1 LEAF
    // THE PLAYER CAN THEN SIMPLY TRAVERSE THE TREE AND CHECK INDIVIDUAL PLANETS
    public List<GameObject> planets = new List<GameObject> { };
    public Vector2 bounds;
    public float size; //we want to account for half values, so float then


    private List<SolarSystemQuadTree> childNode;
    private SolarSystemQuadTree parent;
    

    public SolarSystemQuadTree(Vector2 boundVector,float Size, List<GameObject> planetList, SolarSystemQuadTree parentNode) {
        bounds = boundVector;
        parent = parentNode;
        planets = planetList;
        size = Size;
        childNode = new List<SolarSystemQuadTree>() { };
    }

    public int getChildCount() { return childNode.Count; }

    public void AddChild(Vector2 boundVector,float size, List<GameObject> planetList, SolarSystemQuadTree parentNode)
    {
        SolarSystemQuadTree newChild = new SolarSystemQuadTree(boundVector, size, planetList, parentNode);
        childNode.Add(newChild); // are all parameters local?
    }


    //order of children
    //0 - northwest
    //1 - northeast
    //2 - southwest
    //3 - southeast
    public SolarSystemQuadTree getChild(int childIndex) {
        if (childNode.Count > 0)
        {
            return childNode[childIndex];
        }
        else { return null; } //WHY DOES THIS LOOK FUNKY??
    }

    public SolarSystemQuadTree getParent() {
        return this.parent;
    }

    public GameObject getPlanet(int planetIndex)
    {
        return planets[planetIndex];
    }

    public int getPlanetCount()
    {
        return planets.Count;
    }

    public void addPlanet(GameObject planet) {
        planets.Add(planet); 
    }




    //SOMETHING WRONG WITH MEMORY ALLOCATION. NOT BEING RELEASED. CHECK ALL "NEW" STATEMENTS AND RELEASE CORRECTLY



    public void subdivide() {
        List<GameObject> northWest = new List<GameObject>();
        List<GameObject> northEast = new List<GameObject>();
        List<GameObject> southWest = new List<GameObject>();
        List<GameObject> southEast = new List<GameObject>();

        if (planets.Count <= 1) return;

        for (int i = 0; i < getPlanetCount(); ++i) { //adds each planet in root to a designated quadrant
            //guaranteed that planet is in a quadrant

            bool eastSector = ( (bounds.x + size/2) - planets[i].transform.position.x < 0); //determines if to the north of root
            bool northSector = ( planets[i].transform.position.z - (bounds.y + size/2) < 0); //determines if to the east of root

            if (!northSector && !eastSector) { southWest.Add(planets[i]); }
            else if (!northSector && eastSector) { southEast.Add(planets[i]); }
            else if (northSector && !eastSector) { northWest.Add(planets[i]); }
            else if (northSector && eastSector) { northEast.Add(planets[i]); }
        }
        //boundVector, planetList, parentNode
        float newSize = size / 2;

        //THE BOUNDS VALUES MAY NEED TO BE SWITCHED AROUND IF X AND Z DIRECTIONS ARE DIFFERENT

        AddChild(bounds, newSize, northWest, this);//northwest
        childNode[childNode.Count - 1].subdivide(); //recursion until each leaf has exactly 1 planet or less

        AddChild(new Vector2(bounds.x + newSize, bounds.y), newSize, northEast, this);//northeast
        childNode[childNode.Count - 1].subdivide();

        AddChild(new Vector2(bounds.x, bounds.y + newSize), newSize, southWest, this);//southwest
        childNode[childNode.Count - 1].subdivide();

        AddChild(new Vector2(bounds.x + newSize, bounds.y + newSize), newSize, southEast, this);//southeast
        childNode[childNode.Count - 1].subdivide();


        
    }
}
