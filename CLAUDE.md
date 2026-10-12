# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 주의사항 (출시된 라이브 게임)
- `PlayerSaveData` 필드 이름/타입 변경 금지 (기존 유저 세이브 깨짐). 추가만 허용
  - 세이브는 `SaveManager`가 `JsonUtility`로 직렬화해 `PlayerPrefs` 키 `"PlayerSaveData"`에 저장한다. 필드 이름이 곧 JSON 키다.
- `AdManager`, `IAPManager`, 개인정보 동의 로직(`PrivacyConsentUI`, `AdManager`의 PlayerPrefs 동의 키)은 명시적 요청 없이 수정 금지
- ScriptableObject 에셋 / Addressables 라벨 이름 변경 금지 (코드가 문자열 라벨로 로드함: `EnemyData_Normal`, `EnemyData_Elite`, `EnemyData_Boss`, `Augment`)
- 수정 전 변경 계획을 먼저 설명할 것

## 작업 후 검증
- 기능 작업이 끝나면 `live-safety-reviewer`와 `unity-tester` 서브에이전트로 검증한다.
  - `live-safety-reviewer`: 세이브 데이터, 광고/IAP/동의, Addressables 라벨 위반 검사 (읽기 전용)
  - `unity-tester`: Unity-MCP로 콘솔 에러 확인 (Unity Editor와 MCP 서버가 실행 중이어야 함)
- 두 에이전트는 보고만 한다. 지적 사항은 메인 작업에서 수정한 뒤 다시 검증한다.

## 개발 환경
- Unity **6000.3.5f2**, URP, 모바일(세로) 타깃. 빌드, 실행, 컴파일 확인은 Unity Editor에서 한다.
- `dotnet build Assembly-CSharp.csproj`는 **쓰지 말 것**. Unity 전처리 심볼이 빠져서 DOTween 모듈 등에서 거짓 컴파일 오류가 수백 개 난다. `.csproj`/`.sln`은 Unity가 생성하는 파일이고 gitignore 대상이다.
- 테스트 코드와 asmdef는 없다. 모든 스크립트가 `Assembly-CSharp`에 속한다.
- 에디터 전용 디버그 단축키(`SaveManager`): **F9** 세이브 초기화, **F8** 일반 스테이지 전부 클리어 처리.
- 스테이지 씬을 바로 Play해도 동작하지만, `LoadingScreenManager`나 `SaveManager` 같은 영속 매니저는 **Bootstrap 씬**에서 생성된다. 전체 흐름을 테스트하려면 Bootstrap에서 시작해야 한다.
- 커밋 메시지 형식: `<feat> 한국어 설명`

## 아키텍처

### 씬과 수명 주기
`Bootstrap → MainLobby → Stage_01..03` 순서로 진행된다. 무한 모드는 일반 스테이지 씬 중 하나를 무작위로 다시 쓴다(`StageDatabase.GetRandomNormalStage`).
- 영속 싱글톤(`DontDestroyOnLoad`): `SaveManager`, `AdManager`, `CharacterDatabase`, `LoadingScreenManager` 등
- 씬 로컬 싱글톤: `GameManager`, `RoomManager`, `UIManager`, `AugmentManager`, `DamageManager`, `CombatFeedbackManager`, `ObjectPoolManager` 등. 모두 `static Instance` 패턴이다.
- 씬 간 전달 데이터는 static 클래스 `GameSessionData`가 맡는다(선택 스테이지, 무한 모드 여부, 무한 모드 누적 골드).
- 씬 전환은 반드시 `LoadingScreenManager.LoadScene(name, waitForSceneReady)`로 한다. `waitForSceneReady=true`이면 로딩 화면이 `NotifySceneReady()` 호출까지 기다린다. 스테이지 씬에서는 적 데이터 로드가 끝난 뒤 `RoomManager`가 이 함수를 호출한다.

### 전투 루프 (스테이지 씬)
- `GameManager.currentState` (`GameState`)가 입력과 진행을 제어한다.
  - `Ready`: 드래그 발사 가능
  - `Playing`: 공 이동 중
  - `RoomClear` → `AugmentSelect` → `Ready`
  - `GameOver`, `Paused`
  - 상태 전환은 여러 클래스가 이 필드에 직접 값을 대입하는 방식이다.
- `PlayerBallController`: 드래그로 발사한다. 속도가 0.1 미만으로 떨어지면 `Ready`로 돌아간다. 충돌하면 `DamageManager.ApplyDamage`를 부르는데, 피해량은 공격력과 현재 속도로 계산한다.
- `RoomManager`가 진행의 중심이다.
  - 시작 순서: `EnemySpawner`의 Addressables 로드가 끝나면 → `StartRoom()`
  - 방 종류: 5의 배수 방은 Boss, 3의 배수 방은 Elite
  - 적이 0마리가 되면 `ClearRoom()`이 실행된다.
    - 일반 스테이지의 마지막 방이면 `ClearStage()`(골드 지급, 해금, 저장)
    - 그 외에는 `AugmentManager`가 증강 선택지를 띄운다. 엘리트 방이고 아직 증강 스킬이 없으면 스킬 증강을 띄운다.
    - 증강을 고르면 `StartNextRoom()`으로 넘어간다.
  - 사망 시 광고 부활은 한 번만 가능하다. 무한 모드 골드는 최종 사망하거나 로비로 돌아갈 때 `ClaimEndlessRunGold()`로 지급된다.
- `StageController`: 다음 스테이지, 재도전, 로비 복귀, 다음 스테이지 해금을 처리한다.

### 피드백 / 이펙트 파이프라인
`DamageManager` → `DamageTextManager`(데미지 숫자) + `ObjectPoolManager`(히트 이펙트) + `CombatFeedbackManager`
- `CombatFeedbackManager`는 한 프레임 안에 들어온 SFX와 카메라 흔들림 요청을 **우선순위 기준으로 하나로 병합**한다. 그래서 새 전투 피드백은 `SoundManager`나 `CameraShake`를 직접 호출하지 말고 `RequestSfx`/`RequestImpact`를 쓴다. 기존 코드에는 Instance가 null일 때 직접 호출하는 대체 경로가 남아 있다.
- `ObjectPoolManager`는 **프리팹 이름**을 키로 쓴다. 이름이 같은 프리팹끼리는 풀이 섞인다.

### 데이터 주도 설계
- 밸런스와 콘텐츠는 `Assets/Project/ScriptableObjects/`의 클래스를 바탕으로 만든 `Assets/ScriptableData/` 에셋으로 관리한다.
- 적 데이터와 증강은 Addressables 라벨로 일괄 로드하므로, 새 에셋에 라벨을 붙이면 자동으로 포함된다.
- 확장 지점:
  - 캐릭터 고유 스킬: `ISkill`/`PlayerSkill` (`Scripts/Managers/Player/`)
  - 증강 스킬: `AugmentSkillRuntime` (`Player/AugmentSkills/`)
  - 보스 패턴: `BossPattern` (`Scripts/Boss/`)
  - 상태이상: `StatusEffectData` + `StatusEffectHandler`
- 다국어는 Unity Localization(`Assets/Localization`)을 쓴다. 증강 이름과 설명은 `AugmentManager.GetAugmentDisplayName/Description`을 거친다.
