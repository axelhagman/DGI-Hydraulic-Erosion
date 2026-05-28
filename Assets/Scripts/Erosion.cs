using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Erosion : MonoBehaviour
{

    public int seed;
    public float inertia = 0.05f; 
    public float initialSpeed = 1;
    public float initialWaterVolume = 1;
    public float sedimentCapacityFactor = 4;
    public float minSedimentCapacity = 0.01f;
    public float depositSpeed = 0.3f;
    public float erodeSpeed = 0.3f;
    public float gravity = 4;
    public float evaporateSpeed = 0.01f;

    public List<Vector3> debugPositions = new List<Vector3>();

    System.Random prng;

    int currentSeed;

    void Initialize(int mapSize, bool resetSeed)
    {
        if (resetSeed || prng == null || currentSeed != seed)
        {
            prng = new System.Random(seed);
            currentSeed = seed;
        }
        debugPositions.Clear();
    }

    public void Erode(float[] map, int mapSize, int numIterations = 1, bool resetSeed = false)
    {
        Initialize(mapSize, resetSeed);
        
        for (int dropletIteration = 0;  dropletIteration < numIterations; dropletIteration ++)
        {

            // Create a water droplet at a random point on the map
            float waterVol = initialWaterVolume;
            float sediment = 0; // Initial ammount of sediment.
            Droplet droplet = new Droplet() { worldX = (prng.Next(0, mapSize - 1)), worldY = (prng.Next(0, mapSize - 1)) };

            droplet.dirX = 0;
            droplet.dirY = 0;
            droplet.speed = initialSpeed;
            float oldHeight = CalcHeightAndGradient(map, mapSize, droplet).height;


            // Loop through the lifetime of the water droplet, lifetime is described as discrete "steps"
            for (int lifetime = 0; lifetime < 30; lifetime++)
            {
                // Determine cell coordinates, cell is the mesh points but the droplet moves in fractions inside of the cell.
                int coordX = (int)droplet.worldX;
                int coordY = (int)droplet.worldY;
                droplet.index = coordY*mapSize + coordX;

                // Determine the location of the droplet within the cell coordinates. (0, 0) is at NW node and (1,1) is at SE node
                float insideX = droplet.worldX - coordX;
                float insideY = droplet.worldY - coordY;

                // Calculate droplet height and direction of flow, using bilinear interpolation of surrounding heights
                HeightAndGradient heightAndGradient = CalcHeightAndGradient(map, mapSize, droplet);
                // Update the droplet position
                droplet.dirX = (droplet.dirX * inertia - heightAndGradient.gradientX * (1 - inertia));
                droplet.dirY = (droplet.dirY * inertia - heightAndGradient.gradientY * (1 - inertia));
                // Normalize the direction
                float magnitude = Mathf.Sqrt(droplet.dirX * droplet.dirX + droplet.dirY * droplet.dirY);
                if (magnitude != 0)
                {
                    droplet.dirX /= magnitude;
                    droplet.dirY /= magnitude;
                }
                // Add normalized direction to position
                droplet.worldX += droplet.dirX;
                droplet.worldY += droplet.dirY;
                //Debug.Log("Dir X and Y: " + droplet.dirX + " -- " +  droplet.dirY);
                //Debug.Log("Coord X and Y: " + coordX + " -- " + coordY);
                // Stop simulating the droplet if it is not moving or has moved over the edge of the map
                if ((droplet.dirX == 0 && droplet.dirY == 0) || droplet.worldX < 0 || droplet.worldX >= mapSize - 1 || droplet.worldY < 0 || droplet.worldY >= mapSize - 1)
                {
                    break;
                }


                // Find the droplets new height and calculate the deltaHeight
                float newHeight = CalcHeightAndGradient(map, mapSize, droplet).height;
                float deltaHeight = (lifetime == 0) ? 0 : newHeight - oldHeight;
                // Debug.Log("NewHeight: " + newHeight + " Delta: " + deltaHeight);

                // Calculate the droplet sediment capacity (higher when moving fast down a slope and contains a lot of water)
                float sedimentCapacity = Mathf.Max(-deltaHeight * droplet.speed * waterVol * sedimentCapacityFactor, minSedimentCapacity);


                List<WeightedIndex> brushIndexes = CalcErodeRadiusAndWeight(droplet, mapSize, 3, 0.5f);
                // If: carrying more sediment than the capacity or if flowing up a slope 
                // { deposit a fraction of the sediment to the surrounding nodes (with bilinear interpolation) }
                // Else:
                // erode a fraction of the droplets remaining capacity from the current position, distributed over the radius of the droplet.
                // NOTE: do not erode more than the delta height to avoid digging holes behind the droplet and creating spikes.
                if (sediment > sedimentCapacity || deltaHeight > 0)
                {

                    float excessSediment = Mathf.Max(0, sediment - sedimentCapacity);
                    // Fill up to current height if moving uphill else deposit a fraction of current sediment
                    float ammountDeposit = (deltaHeight > 0) ? Mathf.Min(deltaHeight, sediment) : (sediment - sedimentCapacity) *depositSpeed;
                    // Debug.Log("DEPOSITING: " + ammountDeposit + " Sediment: " + sediment + " DeltaHeight: " + deltaHeight + " SedimentCapacity: " + sedimentCapacity);
                    // sediment -= ammountDeposit;

                    //for (int currentDepositIndex = 0; currentDepositIndex < brushIndexes.Count; currentDepositIndex++)
                    //{
                    //    int depositMapIndex = brushIndexes[currentDepositIndex].index;
                    //    float weightedDepositAmmount = ammountDeposit * brushIndexes[currentDepositIndex].weight;
                    //    map[depositMapIndex] += weightedDepositAmmount;

                    //}


                    map[droplet.index] += ammountDeposit * (1 - insideX) * (1 - insideY);
                    map[droplet.index + 1] += ammountDeposit * insideX * (1 - insideY);
                    map[droplet.index + mapSize] += ammountDeposit * (1 - insideX) * insideY;
                    map[droplet.index + mapSize + 1] += ammountDeposit * insideX * insideY;

                    sediment -= ammountDeposit;
                }
                else
                {
                   
                    float ammountErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);
                    // Debug.Log("ERODING: " + ammountErode + " Sediment: " + sediment + " DeltaHeight: " + deltaHeight + " SedimentCapacity: " + sedimentCapacity);

                    //map[droplet.index] += ammountErode * (1 - insideX) * (1 - insideY);
                    //map[droplet.index + 1] += ammountErode * insideX * (1 - insideY);
                    //map[droplet.index + mapSize] += ammountErode * (1 - insideX) * insideY;
                    //map[droplet.index + mapSize + 1] += ammountErode * insideX * insideY;

                    //sediment -= ammountErode * 4;

                    

                    for (int currentErodeIndex = 0; currentErodeIndex < brushIndexes.Count; currentErodeIndex++)
                    {
                        int erodeMapIndex = brushIndexes[currentErodeIndex].index;
                        float weightedErodeAmmount = ammountErode * brushIndexes[currentErodeIndex].weight;
                        float deltaSediment = (map[erodeMapIndex] < weightedErodeAmmount) ? map[erodeMapIndex] : weightedErodeAmmount;
                        map[erodeMapIndex] -= deltaSediment;
                        sediment += deltaSediment;
                    }
                    

                }

                // Update droplet speed based on deltaHeight
                // Evaporate a fraction of the droplet water
                droplet.speed = Mathf.Sqrt(droplet.speed * droplet.speed + deltaHeight * gravity);
                waterVol *= (1 - evaporateSpeed);
                oldHeight = newHeight;



                // DEBUG DRAWING
                //Vector3 worldPos = new Vector3(droplet.worldX, heightAndGradient.height, droplet.worldY);
                //debugPositions.Add(worldPos);

            }
        }
    }

    List<WeightedIndex> CalcErodeRadiusAndWeight(Droplet droplet, int mapSize, int radius, float falloff = 1f)
    {
        List<WeightedIndex> results = new List<WeightedIndex>();

        int centerX = droplet.index % mapSize;
        int centerY = droplet.index / mapSize;

        int radiusSqared = radius * radius;

        float totalWeight = 0f;

        for (int y = centerY - radius; y <= centerY + radius; y++) 
        {
            if (y<0 || y >= mapSize)
            {
                continue;
            }

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x<0 || x >= mapSize)
                {
                    continue;
                }

                int dx = x - centerX;
                int dy = y - centerY;

                float distanceSquared = dx * dx + dy * dy;

                if (distanceSquared <= radiusSqared)
                {
                    float distance = Mathf.Sqrt(distanceSquared);

                    float normalized = distance / radius;
                    float weight = 1f - normalized;

                    weight = Mathf.Pow(weight, falloff);

                    int index = y * mapSize + x;
                    // Debug.Log("Index: " + index + "  Weight: " + weight);
                    results.Add(new WeightedIndex(index, weight));

                    totalWeight += weight;
                }
            }
        }

        for (int i = 0; i < results.Count; i++)
        {
            WeightedIndex result = results[i];
            result.weight /= totalWeight;
            results[i] = result; 
        }

        return results;
    }

    HeightAndGradient CalcHeightAndGradient(float[] nodes, int mapSize, Droplet droplet)
    {
        // Determine cell coordinates, cell is the mesh points but the droplet moves in fractions inside of the cell.
        int coordX = (int)droplet.worldX;
        int coordY = (int)droplet.worldY;

        // Determine the location of the droplet within the cell coordinates. (0, 0) is at NW node and (1,1) is at SE node
        float insideX = droplet.worldX - coordX;
        float insideY = droplet.worldY - coordY;

        // Get cell heights for the four corners surrounding the droplet
        int indexNodeNWCorner = coordY * mapSize + coordX;
        float heightNW = nodes[indexNodeNWCorner];
        float heightNE = nodes[indexNodeNWCorner + 1];
        float heightSW = nodes[indexNodeNWCorner + mapSize];
        float heightSE = nodes[indexNodeNWCorner + mapSize + 1];

        // Linear interpolation in X and Y direction
        float gradientX = (heightNE - heightNW) * (1 - insideY) + (heightSE - heightSW) * insideY;
        float gradientY = (heightSW - heightNW) * (1 - insideX) + (heightSE - heightNE) * insideX;

        // Bilinear interpolation to approximate the height within the cell
        float dropletHeight = heightNW * (1 - insideX) * (1 - insideY) + heightNE * insideX * (1 - insideY) + heightSW * (1 - insideX) * insideY + heightSE * insideX * insideY;

        return new HeightAndGradient() { height = dropletHeight, gradientX = gradientX, gradientY = gradientY };
    }

    struct WeightedIndex
    {
        public int index;
        public float weight;

        public WeightedIndex(int index, float weight)
        {
            this.index = index;
            this.weight = weight;
        }
    }

    struct HeightAndGradient
    {
        public float height;
        public float gradientX;
        public float gradientY;
    }

    struct Droplet
    {
        public int index;

        // World coordinates for droplet
        public float worldX;
        public float worldY;

        // Movement
        public float dirX;
        public float dirY;
        public float speed;
    }
}
