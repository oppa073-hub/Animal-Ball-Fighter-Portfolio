using System.Collections;
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private Vector3 moveDirection;
    private Coroutine lifeCoroutine;

    public void Initialize(Vector3 direction, float damage, float speed, float lifeTime) //발사체 초기화
    {
        this.moveDirection = direction;
        this.damage = damage;
        this.speed = speed;

        if (lifeCoroutine != null)
            StopCoroutine(lifeCoroutine);

        lifeCoroutine = StartCoroutine(ReturnAfterTime(lifeTime));
    }

    private void Update()  
    {
        if (GameManager.Instance.currentState != GameState.Playing)
        {
            Return();
            return;
        }

        transform.position += moveDirection * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance.currentState != GameState.Playing) return;

        if (other.CompareTag("Player"))
        {
            DamageManager.Instance.ApplyFixedDamage(other.gameObject, damage, DamageTextType.Normal);
            Return();
        }

        if (other.CompareTag("Wall"))
        {
            Return();
        }
    }

    private IEnumerator ReturnAfterTime(float lifeTime)  //발사체 수령 후 반환
    {
        yield return new WaitForSeconds(lifeTime);
        Return();
    }

    private void Return()  //발사체 반환
    {
        GetComponent<PooledObject>().ReturnToPool();
    }
}