using System.Collections.Generic;
using UnityEngine;

public class CharacterDatabase : MonoBehaviour
{
    public static CharacterDatabase Instance { get; private set; }

    [SerializeField] private CharacterData[] characters;

    private Dictionary<CharacterId, CharacterData> characterDict = new Dictionary<CharacterId, CharacterData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDictionary()
    {
        characterDict.Clear();

        for (int i = 0; i < characters.Length; i++)
        {
            CharacterData data = characters[i];

            if (data == null) continue;

            if (characterDict.ContainsKey(data.characterId))
            {
                Debug.LogError($"[CharacterDatabase] CharacterId 중복: {data.characterId}");
                continue;
            }

            characterDict.Add(data.characterId, data);
        }
    }

    public CharacterData GetCharacter(CharacterId id)
    {
        if (characterDict.TryGetValue(id, out CharacterData data)) return data;

        Debug.LogError($"[CharacterDatabase] {id} 캐릭터 데이터를 찾을 수 없습니다.");

        return null;
    }
}