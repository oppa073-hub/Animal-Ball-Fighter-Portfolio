using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 출석 보상(7일 주기)과 오늘의 미션(매일 3개). 진행도는 세이브(PlayerSaveData)에 들어가 클라우드 세이브로도 이어진다.
// 날짜는 기기 현지 날짜 기준 (자정에 새로 고침).
public static class DailyRewardService
{
    public class MissionDefinition
    {
        public string id;
        public string titleKey;
        public string titleFallback;  //{0} = 목표 수
        public int target;
        public int reward;
    }

    public static readonly int[] AttendanceRewards = { 50, 60, 80, 100, 120, 150, 300 };
    public const int MissionsPerDay = 3;

    public static readonly MissionDefinition[] MissionPool =
    {
        new MissionDefinition { id = "kill_enemies", titleKey = "mission_kill_enemies", titleFallback = "몬스터 {0}마리 처치", target = 40, reward = 60 },
        new MissionDefinition { id = "clear_rooms", titleKey = "mission_clear_rooms", titleFallback = "방 {0}개 클리어", target = 8, reward = 60 },
        new MissionDefinition { id = "defeat_boss", titleKey = "mission_defeat_boss", titleFallback = "보스 {0}마리 처치", target = 1, reward = 80 },
        new MissionDefinition { id = "bank_shot", titleKey = "mission_bank_shot", titleFallback = "뱅크샷 {0}번 성공", target = 3, reward = 60 },
        new MissionDefinition { id = "perfect_bounce", titleKey = "mission_perfect_bounce", titleFallback = "퍼펙트 반사 {0}번 성공", target = 5, reward = 60 },
        new MissionDefinition { id = "combo", titleKey = "mission_combo", titleFallback = "{0}콤보 달성", target = 10, reward = 60 },
        new MissionDefinition { id = "use_skill", titleKey = "mission_use_skill", titleFallback = "고유 스킬 {0}번 사용", target = 5, reward = 50 },
    };

    public static event Action Changed;  //진행/수령으로 바뀜 (빨간 점 갱신 등)

    private static PlayerSaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Data : null;

    public static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);  //기기 언어와 관계없이 그레고리력 (태국어 등 불기 문화권 대비)

    // ── 출석 ──

    public static bool CanClaimAttendance => Data != null && Data.lastAttendanceDate != Today;

    // 오늘 받을(받은) 칸 번호 0~6
    public static int AttendanceDayIndex
    {
        get
        {
            if (Data == null) return 0;
            int claimed = Mathf.Max(0, Data.attendanceCount);
            return CanClaimAttendance ? claimed % AttendanceRewards.Length : (claimed - 1 + AttendanceRewards.Length) % AttendanceRewards.Length;
        }
    }

    public static int ClaimAttendance()
    {
        if (!CanClaimAttendance) return 0;

        int reward = AttendanceRewards[AttendanceDayIndex];
        Data.attendanceCount++;
        Data.lastAttendanceDate = Today;
        SaveManager.Instance.AddGold(reward);  //AddGold 안에서 저장
        SaveManager.Instance.Save();

        GameAnalytics.Record("daily_attendance", ("day", Data.attendanceCount));
        Changed?.Invoke();
        return reward;
    }

    // ── 미션 ──

    public static List<DailyMissionProgressData> GetTodayMissions()
    {
        if (Data == null) return new List<DailyMissionProgressData>();

        EnsureToday();
        return Data.dailyMissions;
    }

    public static MissionDefinition GetDefinition(string missionId)
    {
        foreach (MissionDefinition definition in MissionPool)
        {
            if (definition.id == missionId) return definition;
        }

        return null;
    }

    public static bool HasClaimableMission
    {
        get
        {
            foreach (DailyMissionProgressData mission in GetTodayMissions())
            {
                MissionDefinition definition = GetDefinition(mission.missionId);
                if (definition != null && !mission.claimed && mission.progress >= definition.target) return true;
            }

            return false;
        }
    }

    public static bool HasAnythingToClaim => CanClaimAttendance || HasClaimableMission;

    public static void AddProgress(string missionId, int amount = 1)
    {
        if (Data == null || amount <= 0) return;

        EnsureToday();

        foreach (DailyMissionProgressData mission in Data.dailyMissions)
        {
            if (mission.missionId != missionId || mission.claimed) continue;

            MissionDefinition definition = GetDefinition(missionId);
            if (definition == null) continue;

            int before = mission.progress;
            mission.progress = Mathf.Min(definition.target, mission.progress + amount);

            if (before < definition.target && mission.progress >= definition.target) Changed?.Invoke();  //방금 완료
        }
        //저장은 방 클리어/판 종료 등 다른 저장 시점에 함께 (적 처치마다 저장하지 않음)
    }

    public static void SetProgressAtLeast(string missionId, int value)  //최고 콤보처럼 '도달' 미션
    {
        if (Data == null) return;

        EnsureToday();

        foreach (DailyMissionProgressData mission in Data.dailyMissions)
        {
            if (mission.missionId != missionId || mission.claimed) continue;

            MissionDefinition definition = GetDefinition(missionId);
            if (definition == null) continue;

            int before = mission.progress;
            mission.progress = Mathf.Clamp(Mathf.Max(mission.progress, value), 0, definition.target);
            if (before < definition.target && mission.progress >= definition.target) Changed?.Invoke();
        }
    }

    public static int ClaimMission(DailyMissionProgressData mission)
    {
        MissionDefinition definition = mission != null ? GetDefinition(mission.missionId) : null;
        if (definition == null || mission.claimed || mission.progress < definition.target) return 0;

        mission.claimed = true;
        SaveManager.Instance.AddGold(definition.reward);
        SaveManager.Instance.Save();

        GameAnalytics.Record("daily_mission_claimed", ("mission_id", mission.missionId));
        Changed?.Invoke();
        return definition.reward;
    }

    private static void EnsureToday()
    {
        if (Data.dailyMissionDate == Today && Data.dailyMissions.Count == MissionsPerDay) return;

        Data.dailyMissionDate = Today;
        Data.dailyMissions.Clear();

        // 날짜로 정해지는 순서 (같은 날에는 다시 열어도 같은 미션)
        List<MissionDefinition> pool = new List<MissionDefinition>(MissionPool);
        System.Random random = new System.Random(int.Parse(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) ^ 0x5A17);  //string.GetHashCode는 실행마다 다를 수 있어 숫자 날짜로
        for (int i = 0; i < MissionsPerDay && pool.Count > 0; i++)
        {
            int index = random.Next(pool.Count);
            Data.dailyMissions.Add(new DailyMissionProgressData { missionId = pool[index].id, progress = 0, claimed = false });
            pool.RemoveAt(index);
        }

        SaveManager.Instance.Save();
    }
}
