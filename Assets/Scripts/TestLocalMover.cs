using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestLocalMover : MonoBehaviour
{
    public float Speed = 5f;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.position += new Vector3(0f, 0f, 1f) * Speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.position += new Vector3(0f, 0f, -1f) * Speed * Time.deltaTime;
        }
    }
}
