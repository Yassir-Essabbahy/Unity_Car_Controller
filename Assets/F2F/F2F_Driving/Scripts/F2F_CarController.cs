using System;
using UnityEngine;


public enum RotationAxis // allows us to choose which axis we want to rotate ON
{
    x, 
    y, 
    z
}


public class F2F_CarController : MonoBehaviour
{

    [Header("Movement")]
    public float acceleration = 10f;
    public float maxSpeed = 6f;
    public float turnSpeed = 8f;

    [Header("Grounding")]
    public float rayLength = 2.5f;
    public LayerMask groundLayer;


    [Header("Steering Wheel Visuals")]
    public bool useSteerInput = true;
    public Transform steeringWheel;
    public RotationAxis steeringWheelAxis = RotationAxis.z;
    public float maxSteeringAngle = 120f;
    public float steerSmoothness = 10f;
    private float currentSteerAngle = 0f;

    [Header("Speedometer Needle")]
    public bool useSpeedometer = true;
    public Transform SpeedNeedle;
    public RotationAxis speedNeedleAxis = RotationAxis.z;
    public float speedMinAngle = 135f;
    public float speedMaxAngle = -35f;
    public float speedSmoothness = 3f;
    private float currentSpeedAngle;

    [Header("RPM Meter")]
    public bool UseRPMMeter = true;
    public Transform RPMNeedle;
    public RotationAxis RPMneedleAxis = RotationAxis.z;
    public float RPMMinAngle = 135f;
    public float RPMMaxAngle = -45f;
    public float RPMSmoothness = 15f;
    private float currentRPMAngle;



    public Rigidbody rb;
    private float moveInput;
    private float turnInput;
    private bool isGrounded;

    public float titleSmoothness = 1f; // lower = slower, higher = snappier


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // move center of mass down a little to prevent weird movement 
        rb.centerOfMass = new Vector3(0f, -1f, 0);

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initial value for our current angles
        if (useSpeedometer) currentSpeedAngle = speedMinAngle;
        if (UseRPMMeter) currentRPMAngle = RPMMinAngle;



    }

    private void Update()
    {

        moveInput = Input.GetAxis("Vertical"); // W/S to move
        turnInput = Input.GetAxis("Horizontal"); // A/D for direction


        if(useSteerInput) UpdateSteeringWheel();
        if (useSpeedometer || UseRPMMeter) UpdateNeedles();


    }


    private void FixedUpdate()
    {

        // Raycast to ground 
        RaycastHit hit;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, rayLength, groundLayer);


        if (isGrounded)
        {

            // Movement 
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                // we add force in the direction the car is facing 
                rb.AddForce(transform.forward * moveInput * acceleration, ForceMode.Acceleration);

            }

            // Steering ( only when moving ) 
            if (rb.linearVelocity.magnitude > 0.5f)
            {
                // if moving backward, reverse the steering 
                float direction = Vector3.Dot(rb.linearVelocity, transform.forward) > 0 ? 1 : -1;
                float turn = turnInput * turnSpeed * direction * Time.fixedDeltaTime;
                transform.Rotate(0, turn, 0);
            }


            // Calculate the rotation we Want to be at
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;


            // Force Z to 0 so we don't roll sideways
            Vector3 euler = targetRotation.eulerAngles;
            euler.z = 0;
            Quaternion finalTarget = Quaternion.Euler(euler);

            // this make the car lean in the hills or bumps slower and smoother
            transform.rotation = Quaternion.Slerp(transform.rotation, finalTarget, titleSmoothness * Time.fixedDeltaTime);


        }

        // Max speed clamp, Stop the car from passing the max Speed value 
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }



        // Anti-Slide ( Fake breaks ) 
        if (moveInput == 0 && rb.linearVelocity.magnitude < 2f)
        {

            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);

        }


    }



    private void UpdateSteeringWheel()
    {

        if (!useSteerInput || steeringWheel == null) return;

        // calculate target angle based on the input 
        float targetAngle = -turnInput * maxSteeringAngle;

        // smoothly rotate current angle towards the final angle target 
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.deltaTime * steerSmoothness);

        // apply the rotation on the actual wheel Gameobject
        steeringWheel.localRotation = GetAxisRotation(steeringWheelAxis, currentSteerAngle);

    }


    private void UpdateNeedles()
    {


        float currentSpeed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);

        // speedometer
        if (useSpeedometer && SpeedNeedle != null)
        {

            // Map speed ration ( 0 to 1 ) between min and max angle
            float targetSpeedAngle = Mathf.Lerp(speedMinAngle, speedMaxAngle, speedRatio);

            // smooth needle movement 
            currentSpeedAngle = Mathf.Lerp(currentSpeedAngle, targetSpeedAngle, Time.deltaTime * speedSmoothness);

            // apply the rotation to the actual speed needle
            SpeedNeedle.localRotation = GetAxisRotation(speedNeedleAxis, currentSpeedAngle);


        }

        // rpm meter
        if(UseRPMMeter && RPMNeedle != null)
        {

            float targetRPMRatio = Mathf.Clamp01((speedRatio * 0.7f) * (MathF.Abs(moveInput) * 0.3f));

            float targetRPMAngle = Mathf.Lerp(RPMMinAngle, RPMMaxAngle, targetRPMRatio);

            // smooth needle movement
            currentRPMAngle = Mathf.Lerp(currentRPMAngle, targetRPMAngle, Time.deltaTime *  RPMSmoothness);

            RPMNeedle.localRotation = GetAxisRotation(RPMneedleAxis, currentRPMAngle);


        }



    }



    // converts a single float angle into a quaternion localRotation on the choosen axis
    private Quaternion GetAxisRotation(RotationAxis axis, float angle)
    {

        switch (axis)
        {
            case RotationAxis.x: return Quaternion.Euler(angle, 0f, 0f);
            case RotationAxis.y: return Quaternion.Euler(0f, angle, 0f);
            case RotationAxis.z: return Quaternion.Euler(0f, 0f, angle);
            default: return Quaternion.Euler(0f, 0f, angle);
        }

    }




}
