using System;
using UnityEngine;

public enum RotationAxis
{
    X,
    Y,
    Z,
    x = X,
    y = Y,
    z = Z
}

/// <summary>
/// Fears to Fathom Style FPS Car Controller.
/// Polished arcade-sphere physics controller designed for first-person interior driving.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class F2F_CarController : MonoBehaviour
{
    #region Movement & Physics
    [Header("=== MOVEMENT & HANDLING ===")]
    [Tooltip("Engine acceleration force.")]
    public float acceleration = 12f;

    [Tooltip("Maximum forward/reverse speed (higher = faster top speed).")]
    public float maxSpeed = 20f;

    [Tooltip("Steering agility / turning speed.")]
    public float turnSpeed = 10f;

    [Tooltip("Braking deceleration multiplier.")]
    public float brakeStrength = 8f;
    #endregion

    #region Grounding & Slope Alignment
    [Header("=== GROUND DETECTION & ALIGNMENT ===")]
    [Tooltip("Raycast distance downwards to check for road/terrain.")]
    public float rayLength = 2.5f;

    [Tooltip("Layers recognized as drivable ground.")]
    public LayerMask groundLayer;

    [Tooltip("How smoothly the car aligns with road slopes (lower = slower/heavier).")]
    public float titleSmoothness = 1f;
    #endregion

    #region Interior Cabin Visuals (Steering Wheel & Gauges)
    [Header("=== INTERIOR: STEERING WHEEL ===")]
    public bool useSteerInput = true;
    public Transform steeringWheel;
    public RotationAxis steeringWheelAxis = RotationAxis.Z;
    public float maxSteeringAngle = 120f;
    public float steerSmoothness = 10f;

    [Header("=== INTERIOR: SPEEDOMETER GAUGE ===")]
    public bool useSpeedometer = true;
    public Transform SpeedNeedle;
    public RotationAxis speedNeedleAxis = RotationAxis.Z;
    public float speedMinAngle = 135f;
    public float speedMaxAngle = -35f;
    public float speedSmoothness = 4f;

    [Header("=== INTERIOR: RPM GAUGE (SIMULATED GEARS) ===")]
    public bool UseRPMMeter = true;
    public Transform RPMNeedle;
    public RotationAxis RPMneedleAxis = RotationAxis.Z;
    public float RPMMinAngle = 135f;
    public float RPMMaxAngle = -45f;
    public float RPMSmoothness = 10f;

    [Tooltip("Simulated idle RPM value.")]
    public float idleRPM = 850f;

    [Tooltip("Redline / maximum RPM.")]
    public float maxRPM = 7000f;

    [Tooltip("Number of transmission gears for realistic RPM rev & shift bounce.")]
    [Range(3, 7)] public int gearCount = 5;

    [Tooltip("Time in seconds for a gear shift clutch drop.")]
    public float shiftDuration = 0.25f;
    #endregion

    #region Retro Atmosphere & Lighting
    [Header("=== RETRO LIGHTING (OPTIONAL) ===")]
    [Tooltip("Headlight objects / spot lights.")]
    public GameObject[] headlights;
    public KeyCode toggleHeadlightsKey = KeyCode.F;

    [Tooltip("Dashboard backlight or interior dome light.")]
    public GameObject[] interiorLights;
    public KeyCode toggleInteriorLightKey = KeyCode.L;
    #endregion

    #region Audio SFX
    [Header("=== AUDIO SFX (OPTIONAL) ===")]
    [Tooltip("Audio source looping engine sound (pitch shifts with RPM).")]
    public AudioSource engineAudioSource;
    public float minEnginePitch = 0.8f;
    public float maxEnginePitch = 2.2f;

    [Tooltip("Click sound when toggling lights.")]
    public AudioSource switchAudioSource;
    public AudioClip lightSwitchSound;
    #endregion

    #region Runtime Variables
    [Header("=== RIGIDBODY ===")]
    public Rigidbody rb;

    private float moveInput;
    private float turnInput;
    private bool isBraking;
    private bool isGrounded;
    private float currentSpeed;
    private float currentSteerAngle = 0f;
    private float currentSpeedAngle;
    private float currentRPMAngle;
    private float currentCalculatedRPM;
    private int lastGear = 0;
    private float shiftTimer = 0f;

    private bool headlightsOn = false;
    private bool interiorLightsOn = false;

    // Public getters
    public float CurrentSpeedKmh => currentSpeed * 3.6f;
    public float CurrentRPM => currentCalculatedRPM;
    public bool IsGrounded => isGrounded;
    public bool HeadlightsOn => headlightsOn;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Lower center of mass to stabilize the sphere physics
        if (rb != null) rb.centerOfMass = new Vector3(0f, -1f, 0f);

        // Lock cursor for FPS experience
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize gauge needle angles
        if (useSpeedometer) currentSpeedAngle = speedMinAngle;
        if (UseRPMMeter)
        {
            currentRPMAngle = RPMMinAngle;
            currentCalculatedRPM = idleRPM;
        }

        // Apply initial lighting
        UpdateLightObjects(headlights, headlightsOn);
        UpdateLightObjects(interiorLights, interiorLightsOn);
    }

    private void Update()
    {
        // Smooth player input
        moveInput = Input.GetAxis("Vertical");
        turnInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);

        // Atmosphere controls
        HandleLightingInputs();

        // Dashboard & Interior Visuals
        if (useSteerInput) UpdateSteeringWheel();
        if (useSpeedometer || UseRPMMeter) UpdateNeedles();

        // Audio
        UpdateEngineAudio();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        currentSpeed = rb.linearVelocity.magnitude;

        // 1. Raycast ground check
        RaycastHit hit;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, rayLength, groundLayer);

        if (isGrounded)
        {
            // Movement force
            if (isBraking)
            {
                // Handbrake
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, brakeStrength * Time.fixedDeltaTime);
            }
            else if (Mathf.Abs(moveInput) > 0.1f)
            {
                rb.AddForce(transform.forward * moveInput * acceleration, ForceMode.Acceleration);
            }

            // Steering (only when in motion)
            if (currentSpeed > 0.3f)
            {
                float direction = Vector3.Dot(rb.linearVelocity, transform.forward) >= 0 ? 1f : -1f;
                float turn = turnInput * turnSpeed * direction * Time.fixedDeltaTime;
                transform.Rotate(0f, turn, 0f);
            }

            // Slope / Ground alignment
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            Vector3 euler = targetRotation.eulerAngles;
            euler.z = 0f; // Prevent roll flip
            Quaternion finalTarget = Quaternion.Euler(euler);
            transform.rotation = Quaternion.Slerp(transform.rotation, finalTarget, titleSmoothness * Time.fixedDeltaTime);
        }

        // Max speed clamp
        if (currentSpeed > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        // Anti-Slide / Coasting brake when no input
        if (Mathf.Abs(moveInput) <= 0.1f && currentSpeed < 2f && !isBraking)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
        }
    }
    #endregion

    #region Interior Visuals: Steering Wheel & Gauges
    private void UpdateSteeringWheel()
    {
        if (!useSteerInput || steeringWheel == null) return;

        float targetAngle = -turnInput * maxSteeringAngle;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.deltaTime * steerSmoothness);
        steeringWheel.localRotation = GetAxisRotation(steeringWheelAxis, currentSteerAngle);
    }

    private void UpdateNeedles()
    {
        float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(maxSpeed, 0.1f));

        // Speedometer Needle
        if (useSpeedometer && SpeedNeedle != null)
        {
            float targetSpeedAngle = Mathf.Lerp(speedMinAngle, speedMaxAngle, speedRatio);
            currentSpeedAngle = Mathf.Lerp(currentSpeedAngle, targetSpeedAngle, Time.deltaTime * speedSmoothness);
            SpeedNeedle.localRotation = GetAxisRotation(speedNeedleAxis, currentSpeedAngle);
        }

        // RPM Meter (Realistic Multi-Gear Transmission with Shift Timing)
        if (UseRPMMeter && RPMNeedle != null)
        {
            // Realistic gear speed bands (each gear takes longer and pulls harder)
            float[] gearLimits = new float[] { 0.18f, 0.38f, 0.60f, 0.82f, 1.0f };
            int activeGear = 0;
            for (int i = 0; i < gearLimits.Length; i++)
            {
                if (speedRatio <= gearLimits[i] || i == gearLimits.Length - 1)
                {
                    activeGear = i;
                    break;
                }
            }

            // Detect gear shift to trigger realistic clutch/RPM drop
            if (activeGear != lastGear && currentSpeed > 0.5f)
            {
                shiftTimer = shiftDuration;
                lastGear = activeGear;
            }

            if (shiftTimer > 0f)
            {
                shiftTimer -= Time.deltaTime;
            }

            // Calculate progress within current gear
            float prevLimit = activeGear == 0 ? 0f : gearLimits[activeGear - 1];
            float nextLimit = gearLimits[activeGear];
            float gearProgress = Mathf.Clamp01((speedRatio - prevLimit) / Mathf.Max(nextLimit - prevLimit, 0.01f));

            float throttle = Mathf.Abs(moveInput);
            float targetRPMRatio;

            if (currentSpeed < 0.1f && throttle < 0.1f)
            {
                targetRPMRatio = 0.02f; // Idle (~850 RPM)
            }
            else if (shiftTimer > 0f)
            {
                targetRPMRatio = 0.22f; // Clutch shift drop
            }
            else
            {
                // Engine builds revs from low gear range up to peak
                float baseRPM = Mathf.Lerp(0.25f, 0.85f, gearProgress);
                float throttlePunch = throttle * 0.15f;
                targetRPMRatio = Mathf.Clamp01(baseRPM + throttlePunch);

                // Engine braking drop when letting off throttle
                if (throttle < 0.1f && currentSpeed > 0.5f)
                {
                    targetRPMRatio = Mathf.Lerp(0.12f, 0.32f, gearProgress);
                }
            }

            currentCalculatedRPM = Mathf.Lerp(idleRPM, maxRPM, targetRPMRatio);

            float targetRPMAngle = Mathf.Lerp(RPMMinAngle, RPMMaxAngle, targetRPMRatio);
            currentRPMAngle = Mathf.Lerp(currentRPMAngle, targetRPMAngle, Time.deltaTime * RPMSmoothness);
            RPMNeedle.localRotation = GetAxisRotation(RPMneedleAxis, currentRPMAngle);
        }
    }
    #endregion

    #region Retro Lighting
    private void HandleLightingInputs()
    {
        if (Input.GetKeyDown(toggleHeadlightsKey))
        {
            headlightsOn = !headlightsOn;
            UpdateLightObjects(headlights, headlightsOn);
            PlaySwitchSound();
        }

        if (Input.GetKeyDown(toggleInteriorLightKey))
        {
            interiorLightsOn = !interiorLightsOn;
            UpdateLightObjects(interiorLights, interiorLightsOn);
            PlaySwitchSound();
        }
    }

    private void UpdateLightObjects(GameObject[] lightObjects, bool state)
    {
        if (lightObjects == null) return;
        foreach (var obj in lightObjects)
        {
            if (obj != null && obj.activeSelf != state)
            {
                obj.SetActive(state);
            }
        }
    }

    private void PlaySwitchSound()
    {
        if (switchAudioSource != null && lightSwitchSound != null)
        {
            switchAudioSource.PlayOneShot(lightSwitchSound);
        }
    }
    #endregion

    #region Audio Modulation
    private void UpdateEngineAudio()
    {
        if (engineAudioSource == null) return;

        float rpmRatio = Mathf.Clamp01((currentCalculatedRPM - idleRPM) / Mathf.Max(maxRPM - idleRPM, 1f));
        engineAudioSource.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, rpmRatio);
    }
    #endregion

    #region Helper Methods & Gizmos
    private Quaternion GetAxisRotation(RotationAxis axis, float angle)
    {
        switch (axis)
        {
            case RotationAxis.X: return Quaternion.Euler(angle, 0f, 0f);
            case RotationAxis.Y: return Quaternion.Euler(0f, angle, 0f);
            case RotationAxis.Z: return Quaternion.Euler(0f, 0f, angle);
            default: return Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize Raycast in Editor
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayLength);
        Gizmos.DrawWireSphere(transform.position + Vector3.down * rayLength, 0.1f);
    }
    #endregion
}




