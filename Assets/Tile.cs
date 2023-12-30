using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour {
    public Texture2D _texture;
    public Texture2D savedTexture;
	// Use this for initialization
	void Start () {
        _texture = new Texture2D(128, 128);
        int seed = 500;

        for (int x = 0; x < _texture.width; x++)
        {
            for (int y = 0; y < _texture.height; y++)
            {
                float noise = SimplexNoise.SeamlessNoise(((float)x) / ((float)_texture.width),((float)y) / ((float)_texture.height),5.0f,10.0f, (float)seed);
                _texture.SetPixel(x, y, new Color(noise, noise, noise));
                savedTexture.SetPixel(x, y, new Color(noise, noise, noise));
            }
        }
        _texture.Apply();
        savedTexture.Apply();
        gameObject.GetComponent<Renderer>().material.mainTexture = _texture;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
