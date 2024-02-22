using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class regionTexture : MonoBehaviour {
    public string Region;
    public Vector2 textureStart;

    public Texture2D perlinMap;
    public Texture2D parentMap;
	// Use this for initialization
    /* //UNDO LATER, FOR TESTING PURPOSES. I HAVE NO IDEA WHY THIS IS HERE
	void Start () {
        parentMap = GameObject.Find("CubeSphere").GetComponent<Sphere>().patchMaterial.mainTexture as Texture2D;
        perlinMap = new Texture2D(32,32);
        Region = find();
        textureStart = regionFindOnTexture();


        float Xval = textureStart.x + (gameObject.transform.name[gameObject.transform.name.Length - 2] * (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount));
        float Yval = textureStart.y + (gameObject.transform.name[gameObject.transform.name.Length - 1] * (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount));

        for (float y = textureStart.y + (gameObject.transform.name[gameObject.transform.name.Length - 1] * (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount)); y < Yval + (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount); y++) { 
            for (float x = textureStart.x + (gameObject.transform.name[gameObject.transform.name.Length - 2] * (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount)); x < Xval + (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount); x++)
            {
                Color color = parentMap.GetPixel(Mathf.FloorToInt(x),Mathf.FloorToInt( y));
                perlinMap.SetPixel(Mathf.FloorToInt( x),Mathf.FloorToInt( y), color);
            }
        }
        perlinMap.Apply();
        gameObject.GetComponent<Renderer>().material.mainTexture = perlinMap;
        gameObject.GetComponent<Renderer>().material.mainTextureScale = new Vector2(1,1);
    }
    // for (float x = textureStart.x + (gameObject.transform.name[gameObject.transform.name.Length - 2]*(32/ GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount)); x < Xval+ (32 / GameObject.Find("CubeSphere").GetComponent<Sphere>().uPatchCount); x++) 
    // Update is called once per frame
    void Update () {
		
	}

    public string find() {
        string region = "";
        for (int i = 0; i < gameObject.transform.name.Length; i++) {
            if (gameObject.transform.name[i].ToString() == "_")
            {
                break;
            }
            else {
                region = region + gameObject.transform.name[i].ToString();
            }
        }
        return region;
    }
    public Vector2 regionFindOnTexture() {
        if (Region == "top")
        {
            return new Vector2(64, 32);
        }
        else if (Region == "bottom")
        {
            return new Vector2(0, 32);
        }
        else if (Region == "front")
        {
            return new Vector2(32, 32);
        }
        else if (Region == "back")
        {
            return new Vector2(96, 32);
        }
        else if (Region == "left")
        {
            return new Vector2(64, 64);
        }
        else {
            return new Vector2(64, 0);
        }
    }*/
}