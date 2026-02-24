using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

[RequireComponent(typeof(PlayerController))]
public class Player : MonoBehaviour
{
	[field: Header("Components")]
	[field: SerializeField]
	public PlayerCamera Camera { get; private set; }
	public PlayerController Controller { get; private set; }

	#region Initialization Methods

	protected void Awake()
	{
		Assert.IsNotNull(Camera);

		Controller = GetComponent<PlayerController>();
	}
	#endregion

}