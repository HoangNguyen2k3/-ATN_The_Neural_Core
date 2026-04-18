using Lean.Pool;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleReturnToPool : MonoBehaviour {
    private void OnParticleSystemStopped() {
        LeanPool.Despawn(gameObject);
    }
}