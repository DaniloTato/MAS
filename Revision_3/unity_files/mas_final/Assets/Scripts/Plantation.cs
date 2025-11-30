using UnityEngine;

public class Plantation : MonoBehaviour
{
    public float spawnTime;
    public float spawnTimeRandomness;
    public float rottenProbability;

    private int healthy;
    public int HealthyTomatoes { get; private set; }

    private int rotten;
    public int RottenTomatoes { get; private set; }

    public bool collecting;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            float waitTime = spawnTime + Random.Range(0, spawnTimeRandomness);

            yield return new WaitForSeconds(waitTime);

            SpawnTomato();
        }
    }

    private void SpawnTomato()
    {
        bool isRotten = Random.value < rottenProbability;

        if (isRotten) rotten++; else healthy++;
    }

    public void Scan()
    {
        HealthyTomatoes += healthy;
        RottenTomatoes += rotten;
        healthy = 0;
        rotten = 0;
    }

    public void Harvest(int quantity, bool rotten)
    {
        if (rotten)
            RottenTomatoes -= quantity;
        else
            HealthyTomatoes -= quantity;
    }
}
