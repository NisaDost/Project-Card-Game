using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class CardBehaviour : MonoBehaviour
{
    bool isMouseOver = false;
    public GameObject cardPanel;
    public Transform target;
    
    // Start is called before the first frame update
    void Start()
    {
        cardPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(target);
    }
    void OnMouseOver()
    {
        if (isMouseOver == false)
        {
            isMouseOver = true;
            Debug.Log("Mouse is over GameObject.");
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z);
        }
        //tam pikseli tutturursak kart z�pl�yor BUG
    }
    void OnMouseExit()
    {
        isMouseOver = false;
        Debug.Log("Mouse is no longer over GameObject.");
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.1f, transform.position.z);
    }

    void OnMouseDown()
    {
        cardPanel.SetActive(true);
    }
    public void ClosePanel()
    {
        cardPanel.SetActive(false);
    }
}
