using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Altıncontrol : MonoBehaviour
{
    float donhız = 50f;
    Rigidbody rb;
    public GameObject target;
    float mspeed;
    

    Vector3 directiontarget;
    // Start is called before the first frame update
    void Start()
    {
       
        target = GameObject.Find("point");
        rb = GetComponent<Rigidbody>();
        mspeed = Random.Range(1f, 2f);
    }

    // Update is called once per frame
    void Update()
    {

        movemonster();
        Vector3 rot = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(rot.x, rot.y + Time.deltaTime * donhız, rot.z);
    }
    void movemonster()
    {
        if (target != null)
        {



            directiontarget = (target.transform.position - transform.position);
            rb.velocity = new Vector2(directiontarget.x * mspeed,
                                    directiontarget.y * mspeed);
        }



    }
    public void OnMouseDown()
    {
        Destroy(gameObject);
    }
    void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == "Gezegen")
        {
           // Debug.Log("hoppaa");
            Destroy(gameObject);
           
        }
    }
}
