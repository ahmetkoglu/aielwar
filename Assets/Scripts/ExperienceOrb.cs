using UnityEngine;

/// <summary>
/// XP pickup orb. Add this to an XP orb prefab with a trigger collider.
/// </summary>
public class ExperienceOrb : MonoBehaviour
{
    [SerializeField, Min(1)] private int xpAmount = 10;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float magnetRange = 5f;
    [SerializeField, Min(0f)] private float moveSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 20f;

    public int XpAmount => xpAmount;

    private Transform target;
    private float currentSpeed;

    private void Update()
    {
        AcquireTargetIfNeeded();
        MoveToTarget();
    }

    public void SetXpAmount(int amount)
    {
        xpAmount = Mathf.Max(1, amount);
    }

    private void AcquireTargetIfNeeded()
    {
        if (target != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= magnetRange)
        {
            target = player.transform;
        }
    }

    private void MoveToTarget()
    {
        if (target == null)
        {
            return;
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, acceleration * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target.position, currentSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        PlayerExperience playerExperience = other.GetComponentInParent<PlayerExperience>();
        if (playerExperience == null)
        {
            return;
        }

        playerExperience.AddExperience(xpAmount);
        Destroy(gameObject);
    }
}