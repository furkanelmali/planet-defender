using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public class GameEngine : MonoBehaviour {

    public GameObject mainmenu;
    public GameObject pausemenu;
    public GameObject settingmenu;
    public GameObject marketmenu;
    public GameObject restartmenu;
    public GameObject gamemenu;
    public GameObject gezegen;
    public GameObject activedbuton;




    Marketandaltn marketaltin = new Marketandaltn();
    public static bool sttngcontrol;

    
    void Start () {
        Time.timeScale = 1;
        
    }
	
	void Update () {
        

	}
    public void playbtn()
    {
        oyun();
    }
    public void settingsbtn()
    {
        Time.timeScale = 0;
        mainmenu.SetActive(false);
        settingmenu.SetActive(true);
        sttngcontrol = true;
    }
    public void settingsbtn2()
    {
        Time.timeScale = 0;
        pausemenu.SetActive(false);
        settingmenu.SetActive(true);
        sttngcontrol = false;
    }
    public void marketbtn()
    {

        Time.timeScale = 0;
        mainmenu.SetActive(false);
        marketmenu.SetActive(true);
        activedbuton.SetActive(true);      
    }
    public void pausebtn()
    {
        Time.timeScale = 0;
        gamemenu.SetActive(false);
        pausemenu.SetActive(true);

    }
    
    public void backbtn()
    {

        if (marketmenu.active)
        {
            mainmenu.SetActive(true);
            marketmenu.SetActive(false);
           
        }
        else if (pausemenu.active)
        {
            Time.timeScale = 1;
            gamemenu.SetActive(true);
            pausemenu.SetActive(false);
        }
        else if (settingmenu.active)
        {
            if (sttngcontrol == true)
            {
                mainmenu.SetActive(true);
                settingmenu.SetActive(false);
            }
            if (sttngcontrol == false)
            {
                pausemenu.SetActive(true);
                settingmenu.SetActive(false);
            }
            
        }


    }


    

    public void homebtn()
    {
        if (restartmenu.active)
        {
            restartmenu.SetActive(false);
            mainmenu.SetActive(true);
        }
        if (pausemenu.active)
        {
            pausemenu.SetActive(false);
            mainmenu.SetActive(true);
        }
    }
    public void rstrt()
    {
        Time.timeScale = 1;
        gamemenu.SetActive(true);
        pausemenu.SetActive(false);
        Skor.skrsfrlama = true;
         
    }
    public void rstrt2()
    {
        Time.timeScale = 1;
        gamemenu.SetActive(true);
        restartmenu.SetActive(false);
        Skor.skrsfrlama = true;
    }
    void oyun()
    {
        Time.timeScale = 1;
        mainmenu.SetActive(false);
        gamemenu.SetActive(true);
    }


   
}
