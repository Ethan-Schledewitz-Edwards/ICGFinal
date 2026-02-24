using UnityEngine;

public static class PlayerMovementData
{
	// Ground Movement
	public static float s_walkingSpeed = 6.25f;
	public static float s_sprintingSpeed = 8.5f;
	public static float s_crouchingSpeed = 3.55f;
	public static float s_friction = 3.5f;
	public static float s_acceleration = 6.7f;
	public static float s_stopSpeed = 6; // Sharper friction when stopping
	public static float s_sprintStopSpeed = 1; // Stop sprint when speed <=

	public static float s_stepHeight = 0.7f;

	// Normals with Y greater than this are walkable
	const float k_maxWalkableAngle = 55;
	public static float s_minWalkableNormalY = Mathf.Cos(Mathf.Deg2Rad * k_maxWalkableAngle);

	public static float s_jumpForce = 6;


	// Air Movement Values
	public static float s_airSpeed = 1;
	public static float s_airAcceleration = 20;
	public static float s_gravity = 15;

    // Collision Values
    public static LayerMask s_collisionLayerMask = LayerMask.GetMask("Default");
	public static float s_horizontalSize = 0.5f;
	public static float s_standingHeight = 2;
	public static float s_crouchingHeight = 1.2f;
}
