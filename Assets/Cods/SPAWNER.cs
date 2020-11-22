using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SPAWNER : MonoBehaviour
{
    public Transform[] spawnpoint;
    public GameObject[] objeler;
    int rsp, rm;
    public float zaman;
    


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        zaman++;
        spawnmonster();
    }
    void spawnmonster()
    {
        if (zaman == 20)
        {
            rsp = Random.Range(0, spawnpoint.Length);
            rm = Random.Range(0, objeler.Length);
            Instantiate(objeler[rm], spawnpoint[rsp].position, Quaternion.identity);
            zaman = 0;
        }

    }
}
