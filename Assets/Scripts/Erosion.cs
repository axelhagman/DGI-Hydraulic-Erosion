using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Erosion : MonoBehaviour
{
    public int mapSize = 255;
    public float scale = 20;
    public float elevationScale = 10;
    public Material material;

    public int erosionBrushRadius = 0;

    float[] map;
    Mesh mesh;
    int mapSizeWithBorder;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    public void GenerateHeightMap()
    {
        Debug.Log("In generateHeightmap()");
        mapSizeWithBorder = mapSize;
        map = UnityEngine.Object.FindAnyObjectByType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
    }

    public void Erode()
    {
        // Create a water droplet at a random point on the map

        // Loop through the lifetime of the water droplet, lifetime is described as discrete "steps"
        for (int lifetime = 0; lifetime < 30; lifetime++)
        {
            // Calculate droplet height and direction of flow, probably using bilinear interpolation of surrounding heights

            // Update the droplet position

            // Stop simulating the droplet if it is not moving or has moved over the edge of the map

            // Find the droplets new height and calculate the deltaHeight

            // Calculate the droplet sediment capacity (higher when moving fast down a slope and contains a lot of water)

            // If: carrying more sediment than the capacity or if flowing up a slope 
            // { deposit a fraction of the sediment to the surrounding nodes (with bilinear interpolation) }
            // Else:
            // erode a fraction of the droplets remaining capacity from the current position, distributed over the radius of the droplet.
            // NOTE: do not erode more than the delta height to avoid digging holes behind the droplet and creating spikes.

            // Update droplet speed based on deltaHeight
            // Evaporate a fraction of the droplet water

        }

    }
    // Mesh Construction code supplied by Sebastian League, source at https://github.com/SebLague/Hydraulic-Erosion
    public void ContructMesh()
    {
        Vector3[] verts = new Vector3[mapSize * mapSize];
        int[] triangles = new int[(mapSize - 1) * (mapSize - 1) * 6];
        int t = 0;

        for (int i = 0; i < mapSize * mapSize; i++)
        {
            int x = i % mapSize;
            int y = i / mapSize;
            int borderedMapIndex = (y + erosionBrushRadius) * mapSizeWithBorder + x + erosionBrushRadius;
            int meshMapIndex = y * mapSize + x;

            Vector2 percent = new Vector2(x / (mapSize - 1f), y / (mapSize - 1f));
            Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;

            float normalizedHeight = map[borderedMapIndex];
            pos += Vector3.up * normalizedHeight * elevationScale;
            verts[meshMapIndex] = pos;

            // Construct triangles
            if (x != mapSize - 1 && y != mapSize - 1)
            {
                t = (y * (mapSize - 1) + x) * 3 * 2;

                triangles[t + 0] = meshMapIndex + mapSize;
                triangles[t + 1] = meshMapIndex + mapSize + 1;
                triangles[t + 2] = meshMapIndex;

                triangles[t + 3] = meshMapIndex + mapSize + 1;
                triangles[t + 4] = meshMapIndex + 1;
                triangles[t + 5] = meshMapIndex;
                t += 6;
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh();
        }
        else
        {
            mesh.Clear();
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        AssignMeshComponents();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;

        material.SetFloat("_MaxHeight", elevationScale);
    }

    // Mesh Construction code supplied by Sebastian League, source at https://github.com/SebLague/Hydraulic-Erosion
    void AssignMeshComponents()
    {
        Debug.Log("In AssignMeshComponents()");
        // Find/creator mesh holder object in children
        string meshHolderName = "Mesh Holder";
        Transform meshHolder = transform.Find(meshHolderName);
        if (meshHolder == null)
        {
            meshHolder = new GameObject(meshHolderName).transform;
            meshHolder.transform.parent = transform;
            meshHolder.transform.localPosition = Vector3.zero;
            meshHolder.transform.localRotation = Quaternion.identity;
        }

        // Ensure mesh renderer and filter components are assigned
        if (!meshHolder.gameObject.GetComponent<MeshFilter>())
        {
            meshHolder.gameObject.AddComponent<MeshFilter>();
        }
        if (!meshHolder.GetComponent<MeshRenderer>())
        {
            meshHolder.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
