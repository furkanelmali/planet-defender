using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Skor : MonoBehaviour {
    public int skorcount;
    public string skkrr;
    public Text skor;
    public Text sonskr;
    public static bool skrarttırma;
    public static bool skrsfrlama;



    public Text high;





    // Use this for initialization
    void Start() {


        skrarttırma = false;
        high.text = PlayerPrefs.GetInt("High Score", 0).ToString();
        
        

    }

    // Update is called once per frame
    void Update() {
        skrarttır();
        skor.text = "" + (int)skorcount;
        skrsfrlamaa();
        sonskr.text = "" + (int)skorcount;
    }

    public void skrarttır()
    {
        if (skrarttırma)
        {
            skorcount++;
            Debug.Log(skorcount);
            skrarttırma = false;

        }

        if (skorcount > PlayerPrefs.GetInt("High Score", 0))
        {

            PlayerPrefs.SetInt("High Score", skorcount);
            high.text = skorcount.ToString();


        }



    }
    public void skrsfrlamaa()
    {
        if (skrsfrlama)
        {
            skorcount = 0;

        }


    }

}
