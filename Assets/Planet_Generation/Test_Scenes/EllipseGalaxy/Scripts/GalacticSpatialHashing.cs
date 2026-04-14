using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GalacticSpatialHashing
{
    // Start is called before the first frame update
    private Dictionary<Vector3, List<Matrix4x4>> galaxyHash;
    private int hashGridSize;
    public bool generatedHash = false;

    public GalacticSpatialHashing(int hashgridSize_)
    {
        galaxyHash = new();
        hashGridSize = hashgridSize_;
    }

    public void GenerateGalaxyHash(Matrix4x4[] matrices)
    {
        galaxyHash.Clear();
        foreach (Matrix4x4 star in matrices)
        {
            Vector3 starPos = star.GetColumn(3);
            Vector3 key = new Vector3(Mathf.Floor(starPos.x / hashGridSize), Mathf.Floor(starPos.y / hashGridSize), Mathf.Floor(starPos.z / hashGridSize));

            if (!galaxyHash.TryGetValue(key, out var list))
            {
                list = new List<Matrix4x4>();
                galaxyHash[key] = list;
            }

            galaxyHash[key].Add(star);
        }
    }

    public List<Matrix4x4> GetStarPositionsInSector(Vector3 floatingOriginSectorPos)
    {
        Vector3 key = -(floatingOriginSectorPos + Vector3.one);
        if (!galaxyHash.ContainsKey(key)) return null;

        return galaxyHash[key];
    }

    public Matrix4x4 FindClosestStar(Vector3 floatingOriginPos)
    {
        //convert to sector
        Vector3 floatingOriginSectorPos = new Vector3(Mathf.FloorToInt(floatingOriginPos.x / hashGridSize),
        Mathf.FloorToInt(floatingOriginPos.y / hashGridSize),
        Mathf.FloorToInt(floatingOriginPos.z / hashGridSize));

        //Original key
        Vector3 key = floatingOriginSectorPos;

        List<Matrix4x4> starPositions = new();

        //Get the star positions of all 3x3x3 sectors
        Vector3 sectorPosition;
        float minDistance = float.PositiveInfinity;
        Matrix4x4 minStar = Matrix4x4.identity;
        for (int i = -1; i < 2; i++)
        {
            for (int j = -1; j < 2; j++)
            {
                for (int k = -1; k < 2; k++)
                {
                    sectorPosition = key + new Vector3(i, j, k);
                    List<Matrix4x4> sectorStarPositions = GetStarPositionsInSector(sectorPosition);

                    if (sectorStarPositions == null) continue;
                    Debug.Log("Stars found in sector");
                    foreach (Matrix4x4 star in sectorStarPositions)
                    {
                        Vector3 pos = star.GetColumn(3);
                        float distance = (-floatingOriginPos - pos).sqrMagnitude;
                        if (distance < minDistance)
                        {
                            //Debug.Log("New Min Star Found at distance: " + distance);
                            minStar = star;
                            minDistance = distance;
                        }
                    }
                }
            }
        }
        return minStar;
    }
}
