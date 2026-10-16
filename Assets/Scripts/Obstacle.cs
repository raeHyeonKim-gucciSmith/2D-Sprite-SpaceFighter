using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public GameObject bounceEffectPrefab;

    public float minSize = 0.5f;
    public float maxSize = 2.0f;
    public float minSpeed = 50f;
    public float maxSpeed = 150f;

    Rigidbody2D rb;

    void Start()
    {
        float randomSize = Random.Range(minSize, maxSize);
        transform.localScale = new Vector3(randomSize, randomSize, 1);

        float randomSpeed = Random.Range(minSpeed, maxSpeed);
        Vector2 randomDirection = Random.insideUnitCircle;

        rb = GetComponent<Rigidbody2D>();
        rb.AddForce(randomDirection * randomSpeed);
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 contactPoint = collision.GetContact(0).point;
        GameObject bounceEffect = Instantiate(bounceEffectPrefab, contactPoint, Quaternion.identity);

        // Destroy the effect after 1 second
        Destroy(bounceEffect, 1f);
    }
}