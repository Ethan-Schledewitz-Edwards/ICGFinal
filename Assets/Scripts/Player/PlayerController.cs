using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(
	typeof(Rigidbody),
	typeof(BoxCollider),
	typeof(Player)
)]
public partial class PlayerController : MonoBehaviour
{
	// Constants
	const float k_HitEpsilon = 0.02f; // Min distance to wall
	const float k_GroundCheckDist = 0.01f;
	const float k_VaultMinHeight = 1.2f;
	const float k_StopEpsilon = 0.001f; // Stop when <= this speed
	const int k_MaxBumps = 8; // Max number of iterations per frame
	const int k_MaxConcurrentPlanes = 8; // Max number of planes to collide with at once

	// Components
	private Player player;
	private Rigidbody rb;
	private BoxCollider col;

	// Fields
	public bool IsCrouching { get; private set; }
	public bool IsSprinting { get; private set; }
	public bool IsGrounded { get; private set; }

	Vector2 inputDir;
	Vector3 velocity;
	Vector3 position;

	bool isCrouchPressed;
	bool isSprintPressed;
	bool isJumpPressed;

	int framesStuck = 0;
	bool justJumped;

	GameObject surfaceObject;

	#region Unity Callbacks

	void Awake()
	{
		player = GetComponent<Player>();
		rb = GetComponent<Rigidbody>();
		col = GetComponent<BoxCollider>();

		rb.isKinematic = true;
		rb.freezeRotation = true;

		col.layerOverridePriority = 100;

		LayerMask colliderMask = PlayerMovementData.s_collisionLayerMask | LayerMask.GetMask("Trigger");
		col.excludeLayers = ~colliderMask;
		col.includeLayers = colliderMask;

		UpdateCollider();

		position = transform.position;
	}

	void Start()
	{
		SetControlsSubscription(true);
	}


	private void OnDestroy()
	{
		SetControlsSubscription(false);
	}

	private void FixedUpdate()
	{
		UpdateCollider();

		if (InputManager.ControlMode != InputManager.ControlType.Player)
		{
			inputDir = Vector2.zero;
		}

		StandardMovement();
		rb.MovePosition(position);

		UpdateCollider();
		justJumped = false;
	}

	#endregion

	#region Input Methods

	public void SetControlsSubscription(bool isInputEnabled)
	{
		if (isInputEnabled)
			Subscribe();
		else
			Unsubscribe();
	}

	private void OnMoveInput(InputAction.CallbackContext context)
	{
		inputDir = context.ReadValue<Vector2>();
	}

	private void OnJumpInput(InputAction.CallbackContext context)
	{
		isJumpPressed = context.ReadValueAsButton();

		if (isJumpPressed && IsGrounded)
			Jump();
	}

	private void OnCrouchInput(InputAction.CallbackContext context)
	{
		isCrouchPressed = context.ReadValueAsButton();
	}

	private void OnSprintInput(InputAction.CallbackContext context)
	{
		isSprintPressed = context.ReadValueAsButton();

		TrySprint(isSprintPressed);
	}

	public void Subscribe()
	{
		InputManager.Controls.Player.Movement.performed += OnMoveInput;
		InputManager.Controls.Player.Jump.performed += OnJumpInput;

		InputManager.Controls.Player.Crouch.performed += OnCrouchInput;
		InputManager.Controls.Player.Crouch.canceled += OnCrouchInput;

		InputManager.Controls.Player.Sprint.performed += OnSprintInput;
		InputManager.Controls.Player.Sprint.canceled += OnSprintInput;
	}

	public void Unsubscribe()
	{
		InputManager.Controls.Player.Movement.performed -= OnMoveInput;
		InputManager.Controls.Player.Jump.performed -= OnJumpInput;

		InputManager.Controls.Player.Crouch.performed -= OnCrouchInput;
		InputManager.Controls.Player.Crouch.canceled -= OnCrouchInput;

		InputManager.Controls.Player.Sprint.performed -= OnSprintInput;
		InputManager.Controls.Player.Sprint.canceled -= OnSprintInput;

		inputDir = Vector2.zero;
	}

	#endregion

	#region Actions

	private void TryCrouch(bool isAttemptingCrouch)
	{
		if (isAttemptingCrouch)
		{
			IsCrouching = true;
			IsSprinting = false;

			if (!IsGrounded)
			{
				position += Vector3.up * ((PlayerMovementData.s_standingHeight - PlayerMovementData.s_crouchingHeight) / 1.5f);
			}
		}
		else
		{
			// Make sure there is room
			// todo: box cast in air so we can uncrouch close to ground
			IsCrouching = false;
			if (!IsGrounded)
			{
				position -= Vector3.up * ((PlayerMovementData.s_standingHeight - PlayerMovementData.s_crouchingHeight) / 1.5f);
			}

			bool hasRoom = !CheckHull();

			if (!hasRoom)
			{
				IsCrouching = true;
				if (!IsGrounded)
				{
					position += Vector3.up * ((PlayerMovementData.s_standingHeight - PlayerMovementData.s_crouchingHeight) / 1.5f);
				}
			}
		}
	}

	private void TrySprint(bool isAttemptingSprint)
	{
		if (isAttemptingSprint)
		{
			if (!IsCrouching)
			{
				IsSprinting = true;
			}
		}
		else
		{
			IsSprinting = false;
		}
	}

	private void Jump()
	{
		velocity.y += PlayerMovementData.s_jumpForce;

		IsGrounded = false;
		justJumped = true;
	}

	#endregion

	#region Collision

	private bool CastHull(Vector3 position, Vector3 direction, float maxDist, out RaycastHit hitInfo)
	{
		direction.Normalize();

		float halfHeight = GetColliderHeight() / 2f;

		bool hit = Physics.BoxCast(position + Vector3.up * halfHeight,
			new Vector3(PlayerMovementData.s_horizontalSize / 2f, halfHeight, PlayerMovementData.s_horizontalSize / 2f),
			direction,
			out hitInfo,
			Quaternion.identity,
			maxDist + k_HitEpsilon,
			PlayerMovementData.s_collisionLayerMask,
			QueryTriggerInteraction.Ignore
		);

		// Back up a little
		if (hit)
		{
			float nDot = -Vector3.Dot(hitInfo.normal, direction);
			float backup = k_HitEpsilon / nDot;
			hitInfo.distance -= backup;
		}
		else
		{
			hitInfo.distance = maxDist;
		}
		if (hitInfo.distance < 0) hitInfo.distance = 0;

		return hit;
	}

	private bool CheckHull()
	{
		float halfHeight = GetColliderHeight() / 2f;
		return Physics.CheckBox(
			position + Vector3.up * halfHeight,
			new Vector3(PlayerMovementData.s_horizontalSize / 2f, halfHeight, PlayerMovementData.s_horizontalSize / 2f),
			Quaternion.identity,
			PlayerMovementData.s_collisionLayerMask,
			QueryTriggerInteraction.Ignore
		);
	}

	private bool StuckCheck()
	{
		float halfHeight = GetColliderHeight() / 2f;
		Collider[] colliders = Physics.OverlapBox(
			position + Vector3.up * halfHeight,
			new Vector3(
				PlayerMovementData.s_horizontalSize / 2f - k_HitEpsilon,
				halfHeight - k_HitEpsilon,
				PlayerMovementData.s_horizontalSize / 2f - k_HitEpsilon
			),
			Quaternion.identity,
			PlayerMovementData.s_collisionLayerMask,
			QueryTriggerInteraction.Ignore
		);

		if (colliders.Length > 0)
		{
			++framesStuck;

			Debug.LogWarning("Player stuck!");

			if (framesStuck > 5)
			{
				Debug.Log("Wow, you're REALLY stuck.");
				velocity = Vector3.zero;
				position += Vector3.up * 0.5f;
			}

			if (Physics.ComputePenetration(
				col,
				position,
				transform.rotation,
				colliders[0],
				colliders[0].transform.position,
				colliders[0].transform.rotation,
				out Vector3 dir,
				out float dist
			))
			{
				position += dir * (dist + k_HitEpsilon * 2.0f);
				velocity = Vector3.zero;
			}
			else
			{
				velocity = Vector3.zero;
				position += Vector3.up * 0.5f;
			}

			return true;
		}

		framesStuck = 0;

		return false;
	}

	private void CollideAndSlide(ref Vector3 position, ref Vector3 velocity)
	{
		Vector3 startVelocity = velocity;
		Vector3 velocityBeforePlanes = startVelocity;

		// When we collide with multiple planes at once (crease)
		Vector3[] planes = new Vector3[k_MaxConcurrentPlanes];
		int planeCount = 0;

		float time = Time.fixedDeltaTime; // The amount of time remaining in the frame, decreases with each iteration
		int bumpCount;
		for (bumpCount = 0; bumpCount < k_MaxBumps; ++bumpCount)
		{
			float speed = velocity.magnitude;

			if (speed <= k_StopEpsilon)
			{
				velocity = Vector3.zero;
				break;
			}

			// Try to move in this direction
			Vector3 direction = velocity.normalized;
			float maxDist = speed * time;
			if (CastHull(position, direction, maxDist, out RaycastHit hit))
			{
				if (hit.distance > 0)
				{
					// Move to where it collided
					position += direction * hit.distance;

					// Decrease time based on how far it travelled
					float fraction = hit.distance / maxDist;

					if (fraction > 1)
					{
						Debug.LogWarning("Fraction too high");
						fraction = 1;
					}

					time -= fraction * time;

					planeCount = 0;
					velocityBeforePlanes = velocity;
				}

				if (planeCount >= k_MaxConcurrentPlanes)
				{
					Debug.LogWarning("Colliding with too many planes at once");
					velocity = Vector3.zero;
					break;
				}

				planes[planeCount] = hit.normal;
				++planeCount;

				// Clip velocity to each plane
				bool conflictingPlanes = false;
				for (int j = 0; j < planeCount; ++j)
				{
					velocity = Vector3.ProjectOnPlane(velocityBeforePlanes, planes[j]);

					if (planeCount == 1)
					{
						velocityBeforePlanes = velocity;
					}

					// Check if the velocity is against any other planes
					for (int k = 0; k < planeCount; ++k)
					{
						if (j != k) // No point in checking the same plane we just clipped to
						{
							if (Vector3.Dot(velocity, planes[k]) < 0) // Moving into the plane, BAD!
							{
								conflictingPlanes = true;
								break;
							}
						}
					}

					if (!conflictingPlanes) break; // Use the first good plane
				}

				// No good planes
				if (conflictingPlanes)
				{
					if (planeCount == 2)
					{
						// Cross product of two planes is the only direction to go
						Vector3 dir = Vector3.Cross(planes[0], planes[1]).normalized;

						// Go in that direction
						velocity = dir * Vector3.Dot(dir, velocity);
					}
					else
					{
						velocity = Vector3.zero;
						break;
					}
				}
			}
			else
			{
				// Move rigibody according to velocity
				position += direction * hit.distance;
				break;
			}

			// Stop tiny oscillations
			if (Vector3.Dot(velocity, startVelocity) < 0)
			{
				//Debug.Log("Oscillation");
				velocity = Vector3.zero;
				break;
			}

			if (time < 0)
			{
				Debug.Log("Outta time");
				break; // outta time
			}
		}

		if (bumpCount >= k_MaxBumps)
		{
			Debug.LogWarning("Bumps exceeded");
		}
	}
	#endregion

	#region Movement Methods

	private void Friction(float friction)
	{
		float speed = velocity.magnitude;

		float control = Mathf.Max(speed, PlayerMovementData.s_stopSpeed);

		float newSpeed = Mathf.Max(speed - (control * friction * Time.fixedDeltaTime), 0);

		if (speed != 0)
		{
			float mult = newSpeed / speed;
			velocity *= mult;
		}
	}

	private void Accelerate(Vector3 direction, float acceleration, float maxSpeed)
	{
		float add = acceleration * maxSpeed * Time.fixedDeltaTime;

		// Clamp added velocity in acceleration direction
		float speed = Vector3.Dot(direction, velocity);

		if (speed + add > maxSpeed)
		{
			add = Mathf.Max(maxSpeed - speed, 0);
		}

		velocity += add * direction;
	}

	private void StandardMovement()
	{

		Vector3 globalWishDir = player.Camera.RotateVectorYaw(inputDir.normalized);

		// Crouch / un-crouch
		if (isCrouchPressed)
		{
			if (!IsCrouching)
			{
				TryCrouch(true);
			}
		}
		else if (IsCrouching)
		{
			TryCrouch(false);
		}

		IsGrounded = GroundCheck(position, out surfaceObject);

		// Pick movement method
		if (IsGrounded)
		{
			GroundMove(globalWishDir);
		}
		else
		{
			AirMove(globalWishDir);
		}

		StuckCheck();
	}

	// Stop sprinting if the player runs out of stamina or stops moving
	void CheckSprinting()
	{
		bool isMoving = velocity.magnitude > PlayerMovementData.s_sprintStopSpeed;

		if (!isMoving)
		{
			TrySprint(false);
		}
		else if (!IsSprinting && isSprintPressed && isMoving)
		{
			TrySprint(true);
		}
	}

	void AnimationMovement()
	{
		IsGrounded = GroundCheck(position, out surfaceObject);
		StuckCheck();
	}

	float GetMoveSpeed()
	{
		float moveSpeed = IsCrouching ? PlayerMovementData.s_crouchingSpeed :
					(IsSprinting ? PlayerMovementData.s_sprintingSpeed : PlayerMovementData.s_walkingSpeed);

        return moveSpeed;
	}

	private void GroundMove(Vector3 moveDir)
	{
		velocity.y = 0;

		// Pick movement speed based on current player state
		float moveSpeed = GetMoveSpeed();

		CheckSprinting();

		Friction(PlayerMovementData.s_friction);
		Accelerate(moveDir, PlayerMovementData.s_acceleration, moveSpeed);

		// Clamp Speed
		float speed = velocity.magnitude;
		if (speed > moveSpeed)
		{
			float mult = moveSpeed / speed;
			velocity *= mult;
		}

		if (velocity.sqrMagnitude == 0)
		{
			return;
		}

		// Try to step up/down
		StepMove();
	}

	bool StepMove()
	{
		// Do the regular move
		Vector3 regPosition = position;
		Vector3 regVelocity = velocity;
		CollideAndSlide(ref regPosition, ref regVelocity);

		// Move down to ground
		CastHull(regPosition, Vector3.down, PlayerMovementData.s_stepHeight, out RaycastHit downHit1);
		Vector3 regPosition2 = regPosition + Vector3.down * downHit1.distance;

		bool regGrounded = GroundCheck(regPosition2, out GameObject _);

		// Only step down onto ground
		if (regGrounded)
		{
			regPosition = regPosition2;
		}

		// Move up and try another move, stepping over stuff
		CastHull(position, Vector3.up, PlayerMovementData.s_stepHeight, out RaycastHit upHit);
		Vector3 stepPosition = position + Vector3.up * upHit.distance;
		Vector3 stepVelocity = velocity;

		CollideAndSlide(ref stepPosition, ref stepVelocity);

		// Move back down
		CastHull(stepPosition, Vector3.down, PlayerMovementData.s_stepHeight + upHit.distance, out RaycastHit downHit);
		stepPosition += Vector3.down * downHit.distance;

		bool stepGrounded = GroundCheck(stepPosition, out GameObject stepSurface);

		// If we stepped onto air, just do the regular move
		if (!stepGrounded)
		{
			position = regPosition;
			velocity = regVelocity;
			return false;
		}

		// Otherwise, pick the move that goes the furthest
		if (Vector3.Distance(position, regPosition) >= Vector3.Distance(position, stepPosition))
		{
			position = regPosition;
			velocity = regVelocity;
			return false;
		}

		// Stepped
		Vector3 ogPosition = position;
		Vector3 ogVelocity = velocity;

		position = stepPosition;
		velocity = stepVelocity;
		surfaceObject = stepSurface;
		IsGrounded = stepGrounded;

		velocity.y = Mathf.Max(velocity.y, ogVelocity.y); // funny quake ramp jumps
		return true;
	}

	private void AirMove(Vector3 moveDir)
	{
		Accelerate(moveDir, PlayerMovementData.s_airAcceleration, PlayerMovementData.s_airSpeed);

		float yVel = velocity.y;
		velocity.y -= PlayerMovementData.s_gravity * Time.fixedDeltaTime / 2f;
		CollideAndSlide(ref position, ref velocity);
		velocity.y -= PlayerMovementData.s_gravity * Time.fixedDeltaTime / 2f;

		IsGrounded = GroundCheck(position, out surfaceObject);
	}
	#endregion

	#region Utility

	private void UpdateCollider()
	{
		float h = GetColliderHeight();
		col.size = new Vector3(PlayerMovementData.s_horizontalSize, h, PlayerMovementData.s_horizontalSize);
		col.center = new Vector3(0, h / 2.0f, 0);
	}

	public float GetColliderHeight()
	{
		return IsCrouching ? PlayerMovementData.s_crouchingHeight : PlayerMovementData.s_standingHeight;
	}

	public Vector3 GetPosition()
	{
		return position;
	}

	public Vector3 GetVelocity()
	{
		return velocity;
	}

	private bool GroundCheck(Vector3 position, out GameObject surfaceObject)
	{
		surfaceObject = null;
		if (justJumped) return false;

		if (CastHull(position, Vector3.down, k_GroundCheckDist, out RaycastHit hit))
		{
			if (hit.normal.y > PlayerMovementData.s_minWalkableNormalY)
			{
				surfaceObject = hit.collider.gameObject;
				return true;
			}
		}
		else
		{
			return false;
		}

		// If we're on a slope, check if any point on the player is on the ground
		// Source uses 4 box checks, but I'm really lazy so I'll just do a raycast.

		if (Physics.Raycast(position,
			Vector3.down,
			out RaycastHit hit2,
			k_GroundCheckDist * 2,
			PlayerMovementData.s_collisionLayerMask,
			QueryTriggerInteraction.Ignore
		))
		{
			if (hit2.normal.y > PlayerMovementData.s_minWalkableNormalY)
			{
				surfaceObject = hit2.collider.gameObject;
				return true;
			}
			//Debug.DrawRay(m_position, Vector3.down * k_groundCheckDist, Color.yellow);
		}

		return false;
	}

	#endregion
}