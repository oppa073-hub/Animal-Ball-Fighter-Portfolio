using System.Collections.Generic;
using UnityEngine;
public enum SoundId
{
    EnemyHit,
    EnemyShoot,
    EnemyDeath,

    RobotMissilePrepare,
    RobotMissileImpact,
    RobotChargeStart,
    RobotDash,

    HeroLaserCharge,
    HeroLaserFire,
    HeroPoisonPrepare,
    HeroPoisonSpawn,

    PlayerCharge,
    PlayerLaunch,

    CatSkill,
    PigSkill,
    ChickSkill,

    LightningSkill,
    FireBurstSkill,
    HealSkill,

    UI_Click,
    AugmentSelect,
    RoomClear
}

public enum BgmId
{
    Lobby,
    Stage1_Country,
    Stage2_Town,
    Stage3_City,
    Boss
}

[System.Serializable]
public class SoundData
{
    public SoundId id;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;
}
[System.Serializable]
public class BgmData
{
    public BgmId id;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;
}
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource loopSfxSource;

    [SerializeField] private SoundData[] sfxList;
    [SerializeField] private BgmData[] bgmList;

    private Dictionary<BgmId, BgmData> bgmDictionary;

    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private const string BgmVolumeKey = "BGM_VOLUME";
    private const string SfxVolumeKey = "SFX_VOLUME";


    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;

    private Dictionary<SoundId, SoundData> sfxDictionary;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        sfxDictionary = new Dictionary<SoundId, SoundData>();
        bgmDictionary = new Dictionary<BgmId, BgmData>();
        bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

        if (sfxList != null)
        {
            for (int i = 0; i < sfxList.Length; i++)
            {
                if (sfxList[i].clip == null) continue;

                sfxDictionary[sfxList[i].id] = sfxList[i];
            }
        }

        if (bgmList != null)
        {
            for (int i = 0; i < bgmList.Length; i++)
            {
                if (bgmList[i].clip == null) continue;

                bgmDictionary[bgmList[i].id] = bgmList[i];
            }
        }
    }

    public void PlayBGM(BgmId id)
    {
        if (!bgmDictionary.TryGetValue(id, out BgmData bgm))
        {
            Debug.LogWarning($"[SoundManager] 등록되지 않은 BGM: {id}");
            return;
        }

        // 같은 곡이 이미 재생 중이면 다시 처음부터 재생하지 않음
        if (bgmSource.clip == bgm.clip && bgmSource.isPlaying) return;

        bgmSource.clip = bgm.clip;
        bgmSource.volume = bgm.volume * bgmVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PlaySFX(SoundId id)
    {
        if (!sfxDictionary.TryGetValue(id, out SoundData sound))
        {
            Debug.LogWarning($"[SoundManager] 등록되지 않은 사운드: {id}");
            return;
        }

        sfxSource.PlayOneShot(sound.clip, sound.volume * sfxVolume);
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);

        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);

        if (bgmSource.clip != null)
        {
            BgmData currentBgm = null;

            foreach (var bgm in bgmDictionary.Values)
            {
                if (bgm.clip == bgmSource.clip)
                {
                    currentBgm = bgm;
                    break;
                }
            }

            bgmSource.volume =
                currentBgm != null
                ? currentBgm.volume * bgmVolume
                : bgmVolume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);

        if (loopSfxSource != null && loopSfxSource.clip != null)
        {
            foreach (var sound in sfxDictionary.Values)
            {
                if (sound.clip == loopSfxSource.clip)
                {
                    loopSfxSource.volume = sound.volume * sfxVolume;
                    break;
                }
            }
        }
    }
    public void PlayLoopSFX(SoundId id)
    {
        if (!sfxDictionary.TryGetValue(id, out SoundData sound))
        {
            Debug.LogWarning($"[SoundManager] 등록되지 않은 사운드: {id}");
            return;
        }

        loopSfxSource.clip = sound.clip;
        loopSfxSource.volume = sound.volume * sfxVolume;
        loopSfxSource.loop = true;
        loopSfxSource.Play();
    }

    public void StopLoopSFX()
    {
        loopSfxSource.Stop();
        loopSfxSource.clip = null;
    }
    public void PlayChargeSFX(SoundId id)
    {
        if (!sfxDictionary.TryGetValue(id, out SoundData sound)) return;

        loopSfxSource.Stop();

        loopSfxSource.clip = sound.clip;
        loopSfxSource.volume = sound.volume * sfxVolume;
        loopSfxSource.loop = false; // 반복 X
        loopSfxSource.Play();
    }

    public void StopChargeSFX()
    {
        loopSfxSource.Stop();
        loopSfxSource.clip = null;
    }
}