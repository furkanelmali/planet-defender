using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gezegenOlay : MonoBehaviour
{
    public int HealtyPlanet;
    public AudioSource patlamaBekle;
    // Start is called before the first frame update
    void Start()
    {
        HealtyPlanet = 100;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider collision)
    {

        /* if (HealtyPlanet<=100)
         {
             HealtyPlanet--;
         }*/
        if (collision.gameObject.tag == "Altın")
        {
            Marketandaltn.kontrolcü = true;
        }

        if (collision.gameObject.tag == "Meteor")
        {
            HealtyPlanet--;
            patlamaEfect();
        }

    }

    void patlamaEfect()
    {
        patlamaBekle.Play();
    }
}
