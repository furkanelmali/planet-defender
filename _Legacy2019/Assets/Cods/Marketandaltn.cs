using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Marketandaltn : MonoBehaviour {
    public int altın;
    public int Toplamaltn;
    public static bool kontrolcü;
    public GameObject[] gzgnlr;
    public static bool[] gzgnbool;
    public GameObject[] gzbtns;
    public static bool satnalındı;
    public GameObject panel;
    public GameObject activebuton;
    public GameObject activedbuton;
    public GameObject satinalbuton;
    public Text fiyattext;
    public Text toplamaltnt;
    
    public int[] fiyat=new int[] { 0, 50, 100, 150, 200, 250, 500, 1000, 2000 };

    int gezegenİndis = 0;

    public bool gezegenaktifbool;
    public Text[] ffyt;






    // Start is called before the first frame update
     void Start()
    {
        
        satnalındı = false;
        planetActivate();
        toplamaltnt.text = Toplamaltn.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        altınarttr();       
        altntopla();

    }
    void altınarttr()
    {
        if (kontrolcü)
        {
            altın++;
            kontrolcü = false;
        }
    }
    public void activePlanetButton()
    { 
            PlayerPrefs.SetInt("activatePlanet", gezegenİndis);
    }

    void altntopla()
    {
        if(gezgen.cankntrl==true)
        {
            Toplamaltn = altın + Toplamaltn;   
            
        }


    }

    public void satınal()
    {
        Debug.Log("alabilirsin" + Toplamaltn);
        if (Toplamaltn >= fiyat[gezegenİndis])
        {

            Toplamaltn = Toplamaltn - fiyat[gezegenİndis];
            toplamaltnt.text = Toplamaltn.ToString();

            
            satnalındı = true;
            PlayerPrefs.SetInt("Gezegen"+gezegenİndis+"SatinAl", 1);
            activebuton.SetActive(true);
            satinalbuton.SetActive(false);

        }

    }

    public void gezegenSifirla()
    {
        for (int i = 1; i < 7; i++)
        {
            PlayerPrefs.SetInt("Gezegen" + i + "SatinAl", 0);
        }
    }
    
    public void planetActivate()
    {
        int planetIndis = Convert.ToInt32( PlayerPrefs.GetInt("activatePlanet"));
        for (int i = 0; i < 7; i++)
        {
            if (i == planetIndis)
            {
               gzgnlr[i].SetActive(true);
            }
            else
            {
               gzgnlr[i].SetActive(false);
            }
        }
    }

    public void ileri()
    {

        

        if (gezegenİndis == 6)
        {
            gezegenİndis = -1;
        }
        gezegenİndis++;


        for (int i = 0; i < 7; i++)
        {
            if (i==gezegenİndis)
            {
                gzgnlr[i].SetActive(true);
                fiyattext.text = "" +fiyat[gezegenİndis];
            }
            else
            {
                gzgnlr[i].SetActive(false);
            }
        }

        if (PlayerPrefs.GetInt("activatePlanet") == gezegenİndis)
        {
            activebuton.SetActive(false);
            activedbuton.SetActive(true);
        }
        else
        {
            activebuton.SetActive(true);
            activedbuton.SetActive(false);
        }

        if (PlayerPrefs.GetInt("Gezegen" + gezegenİndis + "SatinAl")==0 && gezegenİndis!=0)
        {
            activebuton.SetActive(false);
            satinalbuton.SetActive(true);
        }
        else
        {
            activebuton.SetActive(true);
            satinalbuton.SetActive(false);
        }



            panel.SetActive(true);
    }

    public void geri()
    {
       

        if (gezegenİndis == 0)
        {
            gezegenİndis = 7;
        }
        gezegenİndis--;
        for (int i = 6; i >=0; i--)
        {
            if (i == gezegenİndis)
            {
                gzgnlr[i].SetActive(true);
                fiyattext.text = "" + fiyat[gezegenİndis];
            }
            else
            {
                gzgnlr[i].SetActive(false);
            }
        }


        if (PlayerPrefs.GetInt("activatePlanet") == gezegenİndis)
        {
            activebuton.SetActive(false);
            activedbuton.SetActive(true);
        }
        else
        {
            activebuton.SetActive(true);
            activedbuton.SetActive(false);
        }

        if (PlayerPrefs.GetInt("Gezegen" + gezegenİndis + "SatinAl") == 0 && gezegenİndis != 0)
        {
            activebuton.SetActive(false);
            satinalbuton.SetActive(true);
        }
        else
        {
            activebuton.SetActive(true);
            satinalbuton.SetActive(false);
        }



        
        panel.SetActive(true);

    }
    
   
    }


