using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gezgen : MonoBehaviour
{
    float donhız = 5f;
    public static bool cankntrl;


    



    


    // Use this for initialization
    void Start()
    {
       
    
        
        

        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 rot = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(rot.x, rot.y + Time.deltaTime * donhız, rot.z);
    }

    

   /* void can0()
    {
        if (can == 0)
        {


        }
        

    }*/

    void carpma()
    {
        if (cankntrl == true)
        {
            
            cankntrl = false;

            Debug.Log("bakburdaöldün");
        }

    }

}

