using System.Collections.Generic;
using UnityEngine;

public class Plantation : MonoBehaviour
{
    [Header("Tomato Spawning")]
    public float spawnTime = 3f;
    [Range(0, 1)]
    public float spawnTimeDeviation = 0.1f;
    [Range(0, 1)]
    public float rottenProbability = 0.2f;

    [Header("Visual Prefabs")]
    public GameObject healthyTomatoPrefab;
    public GameObject rottenTomatoPrefab;

    [Header("Spawn Settings")]
    public float spawnRadius = 0.3f;

    private int healthy;
    public int HealthyTomatoes;

    private int rotten;
    public int RottenTomatoes;

    public bool collecting;

    private List<GameObject> healthyVisuals = new();
    private List<GameObject> rottenVisuals = new();

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    public void Scan()
    {
        HealthyTomatoes += healthy;
        RottenTomatoes += rotten;

        healthy = 0;
        rotten = 0;
    }

    public void Harvest(int quantity, bool removeRotten)
    {
        if (removeRotten)
        {
            RottenTomatoes -= quantity;
            RemoveVisualTomatoes(quantity, rottenVisuals);
        }
        else
        {
            HealthyTomatoes -= quantity;
            RemoveVisualTomatoes(quantity, healthyVisuals);
        }
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            float variation = spawnTime * spawnTimeDeviation;
            float waitTime = spawnTime + Random.Range(-variation, variation);
            waitTime *= SimulationManager.Instance.TickInterval;

            yield return new WaitForSeconds(waitTime);

            SpawnTomato();
        }
    }

    private void SpawnTomato()
    {
        bool isRotten = Random.value < rottenProbability;

        if (isRotten)
        {
            rotten++;
            SpawnVisualTomato(false);
        }
        else
        {
            healthy++;
            SpawnVisualTomato(true);
        }
    }

    private void SpawnVisualTomato(bool isHealthy)
    {
        GameObject prefab = isHealthy ? healthyTomatoPrefab : rottenTomatoPrefab;
        if (prefab == null) return;

        // Random small offset
        Vector2 rand = Random.insideUnitCircle * spawnRadius;
        Vector3 offset = new Vector3(rand.x, prefab.transform.position.y, rand.y);

        Vector3 pos = transform.position + offset;

        GameObject vis = Instantiate(prefab, pos, Quaternion.identity, transform);

        if (isHealthy) healthyVisuals.Add(vis);
        else rottenVisuals.Add(vis);
    }

    private void RemoveVisualTomatoes(int count, List<GameObject> list)
    {
        int toRemove = Mathf.Min(count, list.Count);
        for (int i = 0; i < toRemove; i++)
        {
            Destroy(list[0]);
            list.RemoveAt(0);
        }
    }
}
