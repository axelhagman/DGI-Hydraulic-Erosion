using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TerrainHandler : MonoBehaviour
{
    // Debug
    public bool showGizmos = true;

    public int mapSize = 255;
    public float scale = 20;
    public float elevationScale = 10;
    public Material material;

    public int erosionBrushRadius = 1;
    public int erosionIterations = 10000;

    public bool animateErosion = true;
    public int iterationsPerFrame = 10;
    int runAnimatedIterations = 0;

    float[] map;
    Mesh mesh;
    int mapSizeWithBorder;
    Erosion erosion;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;

    void Start()
    {
        GenerateHeightMap();
        Application.runInBackground = true;
        if (map == null)
        {
            map = UnityEngine.Object.FindAnyObjectByType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
        }
        erosion = UnityEngine.Object.FindAnyObjectByType<Erosion>();
    }

    // Update is called once per frame
    void Update()
    {
        if (animateErosion && (runAnimatedIterations < erosionIterations))
        {
            for (int i = 0; i < iterationsPerFrame; i++)
            {
                Erode();
            }
            runAnimatedIterations += iterationsPerFrame;
        }
    }

    public void GenerateHeightMap()
    {
        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;
        map = UnityEngine.Object.FindAnyObjectByType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
    }

    public void Erode()
    {
        if (animateErosion)
        {
            erosion.Erode(map, mapSize, 1);
        }
        else
        {
            if (map == null)
            {
                map = UnityEngine.Object.FindAnyObjectByType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
            }
            erosion = UnityEngine.Object.FindAnyObjectByType<Erosion>();
            erosion.Erode(map, mapSize, erosionIterations);

        }
        ContructMesh();

    }

    Vector3 MeshPointFromMapPoint(Vector3 mapPoint)
    {
        Vector2 percent = new Vector2(mapPoint.x / (mapSize - 1f), mapPoint.z / (mapSize - 1f));
        Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
        pos += Vector3.up * mapPoint.y * elevationScale;
        return pos;
    }

    private void OnDrawGizmos()
    {
        if (erosion != null && erosion.debugPositions != null && showGizmos) {
            Gizmos.color = Color.red;
            for (int i = 0; i < erosion.debugPositions.Count; i++)
            {
                var p1 = MeshPointFromMapPoint(erosion.debugPositions[i]);

                float p = i / (erosion.debugPositions.Count - 1f);
                float s = Mathf.Lerp(.2f, .05f, p);
                Gizmos.DrawSphere(p1, s);

                if (i < erosion.debugPositions.Count - 1)
                {
                    float h = .1f;
                    Gizmos.DrawLine(p1 + Vector3.up * h, MeshPointFromMapPoint(erosion.debugPositions[i + 1]) + Vector3.up * h);
                }
            }
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
}
