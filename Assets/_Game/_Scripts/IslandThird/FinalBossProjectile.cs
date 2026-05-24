using UnityEngine;

public class FinalBossProjectile : MonoBehaviour {
    public float speed = 12f;
    public float damage = 10f;
    public float lifetime = 6f;

    private BossHealthSystem _playerHealth;

    public void Init(BossHealthSystem playerHealth, float dmg) {
        _playerHealth = playerHealth;
        damage = dmg;
        Destroy(gameObject, lifetime);
    }

    void Update() {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other) {
        if (_playerHealth != null && other.gameObject == _playerHealth.gameObject) {
            _playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        // Huỷ khi chạm bất kỳ thứ gì khác (tường, đất)
        if (!other.isTrigger) {
            Destroy(gameObject);
        }
    }
}
