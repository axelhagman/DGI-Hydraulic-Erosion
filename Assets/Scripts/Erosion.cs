using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Erosion : MonoBehaviour
{

    public int seed;
    public float inertia = 0.05f; 
    public float initialSpeed = 10;
    public float initialWaterVolume = 1;

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

        Debug.Log("In erosion code");
        
        for (int dropletIteration = 0;  dropletIteration < numIterations; dropletIteration ++)
        {

            // Create a water droplet at a random point on the map
            float waterVol = initialWaterVolume;
            float sediment = 0; // Initial ammount of sediment.
            Droplet droplet = new Droplet() { worldX = (prng.Next(0, mapSize - 1)), worldY = (prng.Next(0, mapSize - 1)) };

            droplet.dirX = 0;
            droplet.dirY = 0;
            droplet.speed = initialSpeed;


            // Loop through the lifetime of the water droplet, lifetime is described as discrete "steps"
            for (int lifetime = 0; lifetime < 30; lifetime++)
            {
                // Determine cell coordinates, cell is the mesh points but the droplet moves in fractions inside of the cell.
                int coordX = (int)droplet.worldX;
                int coordY = (int)droplet.worldY;

                // Determine the location of the droplet within the cell coordinates. (0, 0) is at NW node and (1,1) is at SE node
                float insideX = droplet.worldX - coordX;
                float insideY = droplet.worldY - coordY;
                droplet.coordX = coordX;
                droplet.coordY = coordY;
                droplet.insideX = insideX;
                droplet.insideY = insideY;

                // Calculate droplet height and direction of flow, probably using bilinear interpolation of surrounding heights
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


                // DEBUG DRAWING
                Vector3 worldPos = new Vector3(droplet.worldX, heightAndGradient.height, droplet.worldY);
                debugPositions.Add(worldPos);

            }
        }
    }

    HeightAndGradient CalcHeightAndGradient(float[] nodes, int mapSize, Droplet droplet)
    {
        float insideX = droplet.insideX;
        float insideY = droplet.insideY;

        // Get cell heights for the four corners surrounding the droplet
        int indexNodeNWCorner = droplet.coordY * mapSize + droplet.coordX;
        float heightNW = nodes[indexNodeNWCorner];
        float heightNE = nodes[indexNodeNWCorner + 1];
        float heightSW = nodes[indexNodeNWCorner + mapSize];
        float heightSE = nodes[indexNodeNWCorner + mapSize + 1];

        // Linear interpolation in X and Y direction
        float gradientX = (heightNE - heightNW) * (1 - insideY) + (heightSE - heightSW) * insideY;
        float gradientY = (heightSW - heightNW) * (1 - insideX) + (heightSE - heightSW) * insideX;

        // Bilinear interpolation to approximate the height within the cell
        float dropletHeight = heightNW * (1 - insideX) * (1 - insideY) + heightNE * insideX * (1 - insideY) + heightSW * (1 - insideX) * insideY + heightSE * insideX * insideY;

        return new HeightAndGradient() { height = heightSE, gradientX = gradientX, gradientY = gradientY };
    }

    struct HeightAndGradient
    {
        public float height;
        public float gradientX;
        public float gradientY;
    }

    struct Droplet
    {
        // World coordinates for droplet
        public float worldX;
        public float worldY;

        // Cell location and coordinates for droplet
        public int coordX;
        public int coordY;
        public float insideY;
        public float insideX;

        // Movement
        public float dirX;
        public float dirY;
        public float speed;
    }
}
