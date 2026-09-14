using UnityEngine;

public class LobbyCharacterDisplay : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private GameObject currentCharacter;

    private void Start()
    {
        Refresh();
        SoundManager.Instance?.PlayBGM(BgmId.Lobby);
    }

    public void Refresh()
    {
        CharacterId selectedId = SaveManager.Instance.Data.selectedCharacterId;

        CharacterData data = CharacterDatabase.Instance.GetCharacter(selectedId);

        if (data == null)
        {
            Debug.LogError($"[LobbyCharacterDisplay] {selectedId} 데이터를 찾을 수 없습니다.");

            return;
        }

        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        currentCharacter = Instantiate(
            data.characterPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            spawnPoint
        );

        SetupLobbyAnimation(data);
    }

    private void SetupLobbyAnimation(CharacterData data)
    {
        if (currentCharacter == null) return;
        if (data.lobbyAnimatorController == null) return;

        Animator lobbyAnimator = currentCharacter.GetComponent<Animator>();

        if (lobbyAnimator == null)
        {
            lobbyAnimator = currentCharacter.AddComponent<Animator>();
        }

        lobbyAnimator.runtimeAnimatorController = data.lobbyAnimatorController;

        lobbyAnimator.applyRootMotion = false;
        lobbyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        lobbyAnimator.Rebind();
        lobbyAnimator.Update(0f);
    }
}