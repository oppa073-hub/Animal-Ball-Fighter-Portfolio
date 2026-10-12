using System;
using System.Collections.Generic;
using UnityEngine;

// 범위 피해 공용 처리 (전설 증강 폭발, 폭탄병, 폭발 통, 돼지 전용 증강 충격파 등). 연출은 CombatJuice가 OnExplosion을 받아 그린다.
public static class AreaDamage
{
    public static event Action<Vector3, float, Color> OnExplosion;  //위치, 반경, 색

    private static readonly Collider[] buffer = new Collider[32];
    private static int enemyMask = -1;

    public static readonly Color FireColor = new Color(1f, 0.55f, 0.2f);

    public static void Explode(Vector3 position, float radius, float damage, Color color, EnemyController ignore = null)
    {
        OnExplosion?.Invoke(position, radius, color);

        if (enemyMask < 0) enemyMask = LayerMask.GetMask("Enemy");

        int count = Physics.OverlapSphereNonAlloc(position, radius, buffer, enemyMask, QueryTriggerInteraction.Ignore);

        // 피해로 적이 죽으면 다른 폭발(연쇄 폭발/폭탄병)이 다시 이 함수를 부르므로(재진입) 대상을 먼저 따로 모아 둔다
        List<EnemyController> targets = new List<EnemyController>(count);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = buffer[i].GetComponentInParent<EnemyController>();
            if (enemy == null || enemy == ignore || enemy.IsDead || targets.Contains(enemy)) continue;  //콜라이더가 여러 개여도 한 번만
            targets.Add(enemy);
        }

        foreach (EnemyController enemy in targets)
        {
            if (enemy == null || enemy.IsDead) continue;
            DamageManager.Instance?.ApplyFixedDamage(enemy.gameObject, damage, DamageTextType.Normal, true, false);
        }
    }
}
