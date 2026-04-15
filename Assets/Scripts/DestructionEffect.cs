using UnityEngine;

public class DestructionEffect : MonoBehaviour
{
	[Header("Physics Settings")]
	[field: SerializeField] public Rigidbody[] m_fragments { get; private set; }
	[field: SerializeField] public Transform m_explosionPlane { get; private set; }
	[SerializeField] private float m_impulseForce = 10f;

	[Header("Light Settings")]
	[field: SerializeField] public Light[] m_areaLights { get; private set; }
	[field: SerializeField] public AnimationCurve m_dimmingCurve { get; private set; }
	[SerializeField] private float m_flashStrength = 2f;
	[SerializeField] private float m_flashDuration = 2f;

	private float m_timer = 0f;
	private bool m_isEffectActive = false;

	private void OnEnable()
	{
		TriggerEffect();
	}

	private void Update()
	{
		if (m_isEffectActive)
		{
			UpdateLightIntensity();
		}
	}

	public void TriggerEffect()
	{
		PushFragments();

		m_timer = 0f;
		m_isEffectActive = true;
	}

	private void PushFragments()
	{
		Vector3 worldDown = Vector3.down;
		Vector3 planeNormal = m_explosionPlane.up;
		Vector3 planePosition = m_explosionPlane.position;

		foreach (Rigidbody rb in m_fragments)
		{
			if (rb == null) continue;

			Vector3 directionToRb = rb.worldCenterOfMass - planePosition;
			float side = Vector3.Dot(directionToRb, planeNormal);

			Vector3 appliedDir = planeNormal * Mathf.Sign(side);

			rb.AddForce((worldDown + appliedDir) * m_impulseForce, ForceMode.Impulse);
		}
	}


	private void UpdateLightIntensity()
	{
		m_timer += Time.deltaTime;
		float normalizedTime = m_timer / m_flashDuration;

		// Evaluate the curve
		float intensityMultiplier = m_dimmingCurve.Evaluate(normalizedTime);

		foreach (Light light in m_areaLights)
		{
			if (light != null)
				light.intensity = m_flashStrength * intensityMultiplier;
		}

		if (normalizedTime >= 1f)
			m_isEffectActive = false;
	}
}