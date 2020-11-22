using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class metecontrol : MonoBehaviour{

    Rigidbody rb;
    public GameObject target;
    float mspeed;
    bool kontrolcu;
    Vector3 directiontarget;
    public ParticleSystem patlama;
    gezgen newgezgen = new gezgen();
    // Start is called before the first frame update
    void Start()
    {

        patlama = GetComponent<ParticleSystem>();
        target = GameObject.Find ("point");
        rb = GetComponent<Rigidbody>();
        mspeed = Random.Range(0.5f, 1f);

        kontrolcu = false;
        




    }

    // Update is called once per frame
    void Update()
    {       
     movemonster();
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == "Gezegen")
        {
            gezgen.cankntrl = true;
            kontrolcu=true;
            Destroy(gameObject);
           
        }
    }

    public void OnMouseDown()
    {

        kontrolcu = true;
        Debug.Log("working");
        Skor.skrarttırma = true;
        Destroy(gameObject);
        

    }

    void movemonster()
    {
        if(target != null)
        {                     
           directiontarget = (target.transform.position - transform.position);
           rb.velocity = new Vector2(directiontarget.x * mspeed,
                                   directiontarget.y * mspeed);
        }       
    }

    void patlamaefekt()
    {
        if (kontrolcu)
        {
            if (!patlama.isPlaying)
            {
                patlama.Play();
            }
        }
        else
        {
            if (patlama.isPlaying)
            {
                patlama.Stop();
            }
        }
       
    }
}
