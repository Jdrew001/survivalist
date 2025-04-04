using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessTerrain
{
    public class DemoCamera : MonoBehaviour
    {
        [SerializeField]
        private float sensitivity;
        [SerializeField]
        private float moveSpeed;

        private Transform mainCamera;

        // Start is called before the first frame update
        void Start()
        {
            mainCamera = transform.GetChild(0);
        }

        // Update is called once per frame
        void Update()
        {
            //Move camera
            Vector3 deltaPosition = Vector3.zero;

            float currentSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed = moveSpeed * 2f;
            }
            else
            {
                currentSpeed = moveSpeed;
            }

            if (Input.GetKey(KeyCode.W))
            { 
                deltaPosition += transform.forward;
            }
            if (Input.GetKey(KeyCode.S))
            { 
                deltaPosition -= transform.forward;
            }
            if (Input.GetKey(KeyCode.A))
            { 
                deltaPosition -= transform.right;
            }
            if (Input.GetKey(KeyCode.D))
            { 
                deltaPosition += transform.right;
            }
            if (Input.GetKey(KeyCode.E))
            {
                deltaPosition += transform.up;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                deltaPosition -= transform.up;
            }

            transform.position += deltaPosition * currentSpeed;

            //Rotate camera
            float h = sensitivity * Input.GetAxis("Mouse X");
            float v = sensitivity * Input.GetAxis("Mouse Y");

            transform.Rotate(0f, h, 0f);
            mainCamera.Rotate(-v, 0f, 0f);
        }
    }
}