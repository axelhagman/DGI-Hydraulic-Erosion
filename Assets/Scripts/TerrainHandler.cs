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

    [Header("Heightmap Import/Export")]
    public bool useCustomHeightmap = false;
    public Texture2D importedHeightmap;
    public bool flipY = true;
    public string generatedFileName;

    [Header("RMSE Comparison Config")]
    public Texture2D realTerrainCompare;
    public Texture2D generatedTerrainCompare;

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
            GenerateHeightMap();
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
        if (useCustomHeightmap && importedHeightmap != null)
        {
            LoadHeightmapFromTexture(importedHeightmap);
        } else
        {
            map = UnityEngine.Object.FindAnyObjectByType<HeightMapGenerator>().GenerateHeightMap(mapSizeWithBorder);
        }
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
                GenerateHeightMap();
            }
            erosion = UnityEngine.Object.FindAnyObjectByType<Erosion>();
            erosion.Erode(map, mapSize, erosionIterations);

        }
        ConstructMesh();

    }

    public void SaveHeightmapPNG()
    {
        if (generatedFileName != null)
        {
            Texture2D texture = GenerateTextureFromMap();

            byte[] pngData = texture.EncodeToPNG();

            System.IO.File.WriteAllBytes(Application.dataPath + "/Heightmaps/GeneratedMaps/" + generatedFileName + ".png", pngData);

            Debug.Log("Saved heightmap to: " + Application.dataPath + "/Heightmaps/GeneratedMaps/" + generatedFileName + ".png");
        } else
        {
            Debug.LogError("No Filename for Generated Heightmap!");
        }
        
    }

    public void LoadHeightmapFromTexture(Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogError("No texture assigned.");
            return;
        }

        mapSize = texture.width;
        Debug.Log("Map Size: " + mapSize);

        mapSizeWithBorder = mapSize + erosionBrushRadius * 2;

        map = new float[mapSizeWithBorder * mapSizeWithBorder];

        Color[] pixels = texture.GetPixels();

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int textureIndex;

                if (flipY)
                {
                    textureIndex = (mapSize - 1 - y) * mapSize + x;
                }
                else
                {
                    textureIndex = y * mapSize + x;
                }

                float height = pixels[textureIndex].grayscale;

                int borderedIndex =
                    (y + erosionBrushRadius) * mapSizeWithBorder
                    + (x + erosionBrushRadius);

                map[borderedIndex] = height;
            }
        }

        ConstructMesh();
    }

    public Texture2D GenerateTextureFromMap()
    {
        Texture2D texture = new Texture2D(mapSize, mapSize);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[mapSize * mapSize];

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int borderedIndex =
                    (y + erosionBrushRadius) * mapSizeWithBorder
                    + (x + erosionBrushRadius);

                float value = Mathf.Clamp01(map[borderedIndex]);

                int textureIndex;

                if (flipY)
                {
                    textureIndex = (mapSize - 1 - y) * mapSize + x;
                }
                else
                {
                    textureIndex = y * mapSize + x;
                }

                pixels[textureIndex] = new Color(value, value, value, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    public float ComputeSlopeFieldRMSE(Texture2D a, Texture2D b)
    {
        if (a == null || b == null)
        {
            Debug.LogError("One or both textures are null.");
            return -1f;
        }

        if (a.width != b.width || a.height != b.height)
        {
            Debug.LogError("Texture sizes must match for slope-field comparison.");
            return -1f;
        }

        int width = a.width;
        int height = a.height;

        Color[] pixelsA = a.GetPixels();
        Color[] pixelsB = b.GetPixels();

        double sumSquaredError = 0.0;
        int count = 0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // --- gradient for texture A ---
                float dax =
                    GetHeight(pixelsA, width, x + 1, y) -
                    GetHeight(pixelsA, width, x - 1, y);

                float day =
                    GetHeight(pixelsA, width, x, y + 1) -
                    GetHeight(pixelsA, width, x, y - 1);

                // --- gradient for texture B ---
                float dbx =
                    GetHeight(pixelsB, width, x + 1, y) -
                    GetHeight(pixelsB, width, x - 1, y);

                float dby =
                    GetHeight(pixelsB, width, x, y + 1) -
                    GetHeight(pixelsB, width, x, y - 1);

                // --- vector difference ---
                float dx = dax - dbx;
                float dy = day - dby;

                sumSquaredError += dx * dx + dy * dy;
                count++;
            }
        }

        float mse = (float)(sumSquaredError / count);
        return Mathf.Sqrt(mse);
    }

    public void RunRMSE()
    {
        float result = ComputeSlopeFieldRMSE(realTerrainCompare, generatedTerrainCompare);
        Debug.Log("Slope Field RMSE: " + result);
    }

    float GetHeight(Color[] pixels, int width, int x, int y)
    {
        return pixels[y * width + x].grayscale;
    }

    //Vector3 MeshPointFromMapPoint(Vector3 mapPoint)
    //{
    //    Vector2 percent = new Vector2(mapPoint.x / (mapSize - 1f), mapPoint.z / (mapSize - 1f));
    //    Vector3 pos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * scale;
    //    pos += Vector3.up * mapPoint.y * elevationScale;
    //    return pos;
    //}

    //private void OnDrawGizmos()
    //{
    //    if (erosion != null && erosion.debugPositions != null && showGizmos) {
    //        Gizmos.color = Color.red;
    //        for (int i = 0; i < erosion.debugPositions.Count; i++)
    //        {
    //            var p1 = MeshPointFromMapPoint(erosion.debugPositions[i]);

    //            float p = i / (erosion.debugPositions.Count - 1f);
    //            float s = Mathf.Lerp(.2f, .05f, p);
    //            Gizmos.DrawSphere(p1, s);

    //            if (i < erosion.debugPositions.Count - 1)
    //            {
    //                float h = .1f;
    //                Gizmos.DrawLine(p1 + Vector3.up * h, MeshPointFromMapPoint(erosion.debugPositions[i + 1]) + Vector3.up * h);
    //            }
    //        }
    //    }
    //}

    // Mesh Construction code supplied by Sebastian League, source at https://github.com/SebLague/Hydraulic-Erosion
    public void ConstructMesh()
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
