using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
	// Constants
	const float k_RunTiltMax = 10;
	const float k_RunTiltMultiplier = 0.3f;
	const float k_DuckSpeed = 6;
	const float k_StepLerpSpeed = 15;
	const float k_MaxStepLerpDist = 0.6f;
	const float k_SprintFOVMult = 1.1f;
	const float k_FOVChangeSpeed = 40;

	const float k_PitchMin = -85;
	const float k_PitchMax = 85;
	const float k_ForeheadSize = 0.1f; // Sinks the camera down from the top of the head

	const float k_bobDamping = 5;

	// Camera Settings
	static float sensitivitySetting = 0.1f;
	static float fovSetting = 70;


	Player player;
	PlayerController Controller => player.Controller;

	Camera childCam;
	InputAction lookAction;

	float pitch;
	float yaw;
	private float standProgress = 1;
	private float stepOffset = 0; // Offset camera to smooth out steps
	private float currentFOV;

	private Vector3 lastWorldPos;
	private Vector3 worldPos;
	private float lastRoll;
	private float roll;

	[Header("Camera Bobbing")]
	private const float k_defaultBobAmplitudeX = 0.06f;
	private const float k_defaultBobAmplitudeY = 0.06f;
	private const float k_sprintBobAmplitudeX = 0.1f;
	private const float k_sprintBobAmplitudeY = 0.16f;
	private const float k_defaultBobAmplitudeX_Viewmodel = 0.02f;
	private const float k_defaultBobAmplitudeY_Viewmodel = 0.04f;
	private const float k_sprintBobAmplitudeX_Viewmodel = 0.03f;
	private const float k_sprintBobAmplitudeY_Viewmodel = 0.06f;
	private const float k_defaultBobFrequency = 14f;
	private const float k_sprintBobFrequency = 14f;

	private float bobTimer;

	#region Functionality

	protected void Awake()
	{
		player = GetComponentInParent<Player>();
		childCam = GetComponentInChildren<Camera>();
	}

	protected void Start()
	{
		lastWorldPos = CalcCameraPos();
		worldPos = lastWorldPos;

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		SubscribeEvents();
	}

	protected void OnDestroy()
	{
		UnsubscribeEvents();
	}

	protected void Update()
	{
		if (lookAction != null && InputManager.ControlMode == InputManager.ControlType.Player)
		{
			// Look
			Vector2 mouseDelta = lookAction.ReadValue<Vector2>();
			yaw += mouseDelta.x * sensitivitySetting;
			pitch -= mouseDelta.y * sensitivitySetting;

			pitch = Mathf.Clamp(pitch, k_PitchMin, k_PitchMax);
		}

		// Interpolate between positions calculated in FixedUpdate
		float fract = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

		// Update roll
		float roll = Mathf.Lerp(lastRoll, this.roll, fract);

		// Update rig position and rotation
		transform.position = Vector3.Lerp(lastWorldPos, worldPos, fract);
		transform.localRotation = Quaternion.Euler(pitch, yaw, 0);

		// Check if the player is sprinting
		bool isSprinting = player.Controller.IsSprinting;

		// Thirdperson view
		if (!player)
		{
			transform.position -= transform.forward * 3;
		}

		UpdateFOV(isSprinting);
	}

	protected void FixedUpdate()
	{
		// Update crouching
		if (Controller.IsGrounded)
		{
			standProgress += Time.fixedDeltaTime * k_DuckSpeed * (Controller.IsCrouching ? -1 : 1);
			standProgress = Mathf.Clamp01(standProgress);
		}
		else
		{
			standProgress = Controller.IsCrouching ? 0 : 1;
		}

		// Update smooth stepping
		stepOffset -= 0.5f * stepOffset * Time.fixedDeltaTime * k_StepLerpSpeed;
		stepOffset = Mathf.Clamp(stepOffset, -k_MaxStepLerpDist, k_MaxStepLerpDist);

		// Update position
		lastWorldPos = worldPos;
		worldPos = CalcCameraPos();

		// we do this in 2 parts for more accurate integration
		stepOffset -= 0.5f * stepOffset * Time.fixedDeltaTime * k_StepLerpSpeed;

		// Update camera rig tilt
		lastRoll = roll;
		roll = Mathf.Clamp(
			-Vector3.Dot(Controller.GetVelocity(), transform.right) * k_RunTiltMultiplier,
			-k_RunTiltMax, k_RunTiltMax
		);
	}

	void Step(float stepHeight)
	{
		stepOffset -= stepHeight;
	}

	void UpdateFOV(bool isSprinting)
	{
		float sprintFOV = fovSetting * k_SprintFOVMult;

		float t = (currentFOV - fovSetting) / (sprintFOV - fovSetting);
		float rateOfChange = Time.deltaTime / (sprintFOV - fovSetting) * k_FOVChangeSpeed;

		if (isSprinting)
		{
			t += rateOfChange;
		}
		else
		{
			t -= rateOfChange;
		}

		t = Mathf.Clamp01(t);
		currentFOV = Mathf.Lerp(fovSetting, sprintFOV, t);

		childCam.fieldOfView = currentFOV;
	}

	private Vector3 CalcCameraPos()
	{
		// Calculate view display height
		float height;

		height = Mathf.Lerp(
			PlayerMovementData.s_crouchingHeight,
			PlayerMovementData.s_standingHeight,
			standProgress
		) - k_ForeheadSize;

		height += stepOffset;

		Vector3 pos = Controller.GetPosition() + new Vector3(0, height, 0);

		return pos;
	}

	#endregion

	#region Input Methods

	public void SubscribeEvents()
	{
		lookAction = InputManager.Controls.Player.Look;
	}

	public void UnsubscribeEvents()
	{
		lookAction = null;
	}

	#endregion

	#region Helper Methods

	public Vector3 RotateVectorYaw(Vector2 vector)
	{
		return RotateVectorYaw(new Vector3(vector.x, 0, vector.y));
	}

	public Vector3 RotateVectorYaw(Vector3 vector)
	{
		Vector3 newVector = new();

		float c = Mathf.Cos(Mathf.Deg2Rad * yaw);
		float s = Mathf.Sin(Mathf.Deg2Rad * yaw);

		newVector.x = c * vector.x + s * vector.z;
		newVector.y = vector.y;
		newVector.z = -s * vector.x + c * vector.z;

		return newVector;
	}

	public Vector3 GetHeadForward()
	{
		return Quaternion.Euler(pitch, yaw, roll) * Vector3.forward;
	}
	#endregion
}
