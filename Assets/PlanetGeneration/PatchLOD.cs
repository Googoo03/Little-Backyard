using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatchLOD {

    // HAVE ALL PATCHES BE PART OF A TREE THAT THE PLANET KEEPS TRACK OF
    private List<PatchLOD> childNode;

    private Material planetMaterial;

    private GameObject patch;
    private PatchConfig patchConfig; //configuration data for the patch
    private PatchLOD parent;
    private Vector3 position;
    public int test;

    [SerializeField]private float distance;
    
    public PatchLOD(GameObject patch, PatchLOD parent) {
        this.patch = patch;
        patchConfig = patch.GetComponent<GeneratePlane>().patch;
        childNode = new List<PatchLOD>() { };
        //planetMaterial = Resources.Load("Planet_Shader", typeof(Material)) as Material;
        this.parent = parent;

        //the complicated math part should be refactored, it should be consolidated
        this.position = patch.GetComponent<GeneratePlane>().getPosition(patchConfig, 1f / (1 << (patchConfig.LODlevel))  );
        //needs gameObject plane, plane needs resolution, start position
    }


    public Vector3 getPosition() { return position; }

    public void AddChild(GameObject patchChild)
    {
        PatchLOD newNode = new PatchLOD(patchChild, this);
        childNode.Add(newNode);
    }

    public PatchLOD getChild(int childIndex)
    {
        if (childNode.Count > 0)
        {
            return childNode[childIndex];
        }
        else { return null; }
    }

    public PatchLOD getParent()
    {
        return this.parent;
    }

    public GameObject getPatch(int planetIndex)
    {
        return patch;
    }

    public void nextLOD() {

        patch.transform.GetComponent<MeshRenderer>().enabled = false;
        patch.transform.GetComponent<MeshCollider>().enabled = false;

        //PatchConfig patchConfigChild = new PatchConfig("NorthWest", patchConfig.uAxis, patchConfig.vAxis, patchConfig.LODlevel + 1);
        string[] names = {"NorthWest", "NorthEast", "SouthWest","SouthEast" };
        Vector2[] binaryOperator = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };

        for (int i = 0; i < names.Length; ++i)
        {
            GameObject patchChild = new GameObject(names[i] + "_Level_" + patchConfig.LODlevel + 1);

            //patchChild.AddComponent<GeneratePlane>();
            addMeshGenerationScript(patchChild);

            patchChild.transform.parent = patch.transform;
            patchChild.transform.localEulerAngles = Vector3.zero; //zero out local position and rotation
            patchChild.transform.localPosition = Vector3.zero;
            
            float powerof2Frac = 1f/ (1 << (patchConfig.LODlevel+1)); //maybe +1?

            Vector2 LODOffset = patchConfig.LODOffset;
            LODOffset += (new Vector2(powerof2Frac, powerof2Frac) * binaryOperator[i]);
            ////////////////////////////////////////////////////////////////

            //make new patch config
            float newDistanceThreshold;
            newDistanceThreshold = patchConfig.distanceThreshold / 2f;

            PatchConfig patchConfigChild = new PatchConfig(
                names[i], 
                patchConfig.uAxis, 
                patchConfig.vAxis, 
                patchConfig.LODlevel + 1, 
                LODOffset, 
                patchConfig.vertices,
                patchConfig.planetObject,
                newDistanceThreshold, patchConfig.radius
                );


            //have patchConfig child inherit everything from parent
            //addFlatShader(patchChild);
            patchChild.GetComponent<GeneratePlane>().Generate(patchConfigChild,powerof2Frac);
            patchChild.transform.GetComponent<Renderer>().material.SetTextureOffset("_HeightMap", -LODOffset * (1 << patchConfigChild.LODlevel) );

            AddChild(patchChild);
        }
    }

    private void addFlatShader(GameObject patchChild) {

        patchChild.GetComponent<Renderer>().material = planetMaterial;
    }

    public void addMeshGenerationScript(GameObject patchChild) {
        int planetType = patchConfig.planetObject.GetComponent<Sphere>().getPlanetType(); //is this the right object?
        switch (planetType) {
            case 0:
                patchChild.AddComponent<HotPlanetNoise>();
                break;
            case 1:
                patchChild.AddComponent<IcePlanetNoise>();
                break;
            case 2:
                patchChild.AddComponent<LifePlanetNoise>();
                break;
            case 3:
                patchChild.AddComponent<BarrenPlanetNoise>();
                break;
            case 4:
                patchChild.AddComponent<DesertPlanetNoise>();
                break;
            case 5:
                patchChild.AddComponent<GasPlanetNoise>();
                break;
            default:
                patchChild.AddComponent<BarrenPlanetNoise>();
                break;
        }
    }

    public void prevLOD(PatchLOD node) {
        //do 2DFS, first one deletes all leaf nodes and corresponding objects
        //2nd one turns back on the mesh renderer and collider of new leaf
        deleteLeafDFS(node);
        turnOnLeafMeshDFS(node);
    }

    public void turnOnLeafMeshDFS(PatchLOD node) {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                turnOnLeafMeshDFS(node.childNode[i]);

            }
        }
        else
        {
            //if no children, then turn on mesh renderer and mesh collider
            node.patch.GetComponent<MeshRenderer>().enabled = true;
            node.patch.GetComponent<MeshCollider>().enabled = true;
        }
    }

    public void deleteLeafDFS(PatchLOD node) {
        if (node.childNode.Count == 0) {
            node.parent.childNode.Remove(node);
            GameObject.Destroy(node.patch);
            return;
        }
        else {
            for (int i = node.childNode.Count-1; i >= 0; --i)
            { //traverses DFS 
                deleteLeafDFS(node.childNode[i]);
            }
        }
    }

    public void traverseAndGenerate(PatchLOD node) //used for finding the leaf nodes and then using nextLOD to actually generate
    {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                traverseAndGenerate(node.childNode[i]);

            }
        }
        else {
            node.nextLOD(); //if no children, then activates nextLOD functions of lowest level children
        }

    }

    public void LODbyDistance(PatchLOD node, GameObject player) //used for finding the leaf nodes and then using nextLOD to actually generate
    {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                LODbyDistance(node.childNode[i],player);

            }
        }
        else
        {
            //need to take into account that all patches have the same location, but are offset differently
            distance = Vector3.Distance(node.position, player.transform.position);
            if (distance < (node.patchConfig.distanceThreshold) &&  node.patchConfig.LODlevel < node.patchConfig.maxLOD)
            {
                node.nextLOD();
            }else if (distance > 4* (node.patchConfig.distanceThreshold+node.patchConfig.radius)) //if distance between player and patch is too large
                                                                        //then undo the LOD
            {
                node.prevLOD(node.parent);

            }
        }

    }
}

