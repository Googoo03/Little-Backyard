using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudCreator : MonoBehaviour {
    // Use this for initialization
    public float seed;
    public float scale;
    float radius = 1f;
    // Longitude |||
    int nbLong = 24;
    // Latitude ---
    int nbLat = 16;
    public MeshFilter filter;
    public Mesh mesh;

    public Texture2D cloudTex;

    void Start () {
        cloudTex = new Texture2D(nbLong, nbLat);
        filter = gameObject.GetComponent<MeshFilter>();
        mesh = filter.mesh;
        mesh.Clear();
        //cloudTex.alphaIsTransparency = true;
        #region Vertices
        Vector3[] vertices = new Vector3[(nbLong+1) * nbLat + 2];
        float _pi = Mathf.PI;
        float _2pi = _pi * 2f;
 
        vertices[0] = Vector3.up * radius;
        for( int lat = 0; lat < nbLat; lat++ )
        {
	        float a1 = _pi * (float)(lat+1) / (nbLat+1);
	        float sin1 = Mathf.Sin(a1);
	        float cos1 = Mathf.Cos(a1);
 
	        for( int lon = 0; lon <= nbLong; lon++ )
	        {
		        float a2 = _2pi * (float)(lon == nbLong ? 0 : lon) / nbLong;
		        float sin2 = Mathf.Sin(a2);
		        float cos2 = Mathf.Cos(a2);
 
		        vertices[ lon + lat * (nbLong + 1) + 1] = new Vector3( sin1 * cos2, cos1, sin1 * sin2 ) * radius;
                float height = Perlin3d(vertices[lon + lat * (nbLong + 1) + 1].x, vertices[lon + lat * (nbLong + 1) + 1].y, vertices[lon + lat * (nbLong + 1) + 1].z);

                        cloudTex.SetPixel(lon, lat, new Color(height, height, height,height));
            }
        }
        vertices[vertices.Length-1] = Vector3.up * -radius;
        #endregion
 
        #region Normales		
        Vector3[] normales = new Vector3[vertices.Length];
        for( int n = 0; n < vertices.Length; n++ )
	        normales[n] = vertices[n].normalized;
        #endregion
 
        #region UVs
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = Vector2.up;
        uvs[uvs.Length-1] = Vector2.zero;
        for( int lat = 0; lat < nbLat; lat++ )
	        for( int lon = 0; lon <= nbLong; lon++ )
		        uvs[lon + lat * (nbLong + 1) + 1] = new Vector2( (float)lon / nbLong, 1f - (float)(lat+1) / (nbLat+1) );
        #endregion
 
        #region Triangles
        int nbFaces = vertices.Length;
        int nbTriangles = nbFaces * 2;
        int nbIndexes = nbTriangles * 3;
        int[] triangles = new int[ nbIndexes ];
 
        //Top Cap
        int i = 0;
        for( int lon = 0; lon < nbLong; lon++ )
        {
	        triangles[i++] = lon+2;
	        triangles[i++] = lon+1;
	        triangles[i++] = 0;
        }
 
        //Middle
        for( int lat = 0; lat < nbLat - 1; lat++ )
        {
	        for( int lon = 0; lon < nbLong; lon++ )
	        {
		        int current = lon + lat * (nbLong + 1) + 1;
		        int next = current + nbLong + 1;
 
		        triangles[i++] = current;
		        triangles[i++] = current + 1;
		        triangles[i++] = next + 1;
 
		        triangles[i++] = current;
		        triangles[i++] = next + 1;
		        triangles[i++] = next;
	        }
        }
 
        //Bottom Cap
        for( int lon = 0; lon < nbLong; lon++ )
        {
	        triangles[i++] = vertices.Length - 1;
	        triangles[i++] = vertices.Length - (lon+2) - 1;
	        triangles[i++] = vertices.Length - (lon+1) - 1;
        }
        #endregion
 
        mesh.vertices = vertices;
        mesh.normals = normales;
        mesh.uv = uvs;
        mesh.triangles = triangles;
 
        mesh.RecalculateBounds();
        ;
        cloudTex.Apply();
        gameObject.GetComponent<Renderer>().material.mainTexture = cloudTex;

        
	}
	
	// Update is called once per frame
	void Update () {
        seed += 1*Time.deltaTime;
        for (int x = 0; x < (nbLat); x++) {
                for (int y = 0;y < (nbLong); y++)
            {
                float xx = ((mesh.vertices[y + x * (nbLong + 1) + 1].x - nbLong) / scale);
                float yy = ((mesh.vertices[y + x * (nbLong + 1) + 1].y - nbLong) / scale);
                float zz = ((mesh.vertices[y + x * (nbLong + 1) + 1].z - nbLong) / scale);
                float newHeight = Perlin3d(xx+seed,yy+seed,zz+seed);
                cloudTex.SetPixel(x, y, new Color(newHeight, newHeight, newHeight, newHeight));
        }
        }
        cloudTex.Apply();
	}
    public static float Perlin3d(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;

        return ABC / 6f;
    }
}
