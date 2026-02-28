# UHK Keymap Autochanger

Windows에서 활성 프로그램(프로세스)에 따라 Ultimate Hacking Keyboard(UHK) 키맵을 자동 전환하는 트레이 앱입니다.

English documentation: [README.md](README.md)

## 지원 범위

- OS: Windows
- 디바이스: UHK80 (오른쪽 통신 인터페이스 기준)
- 동작 방식: UHK HID `SwitchKeymap (0x11)` 직접 전송

## 주요 기능

- 프로세스별 키맵 매핑 (`Code.exe -> DEV` 등)
- 매핑되지 않은 앱에서는 기본 키맵으로 즉시 복귀
- 중복 전송 방지 (이미 같은 키맵이면 재전송하지 않음)
- 트레이 메뉴 제공
- `Start with Windows` 지원 (HKCU Run 등록)
- 설정 파일 JSON 저장

## 설치 방법

### 배포된 EXE 실행

`UhkKeymapAutochanger.exe`를 실행하면 됩니다.

`self-contained` 빌드라 .NET 런타임 별도 설치가 필요 없습니다.

### 시작 프로그램 등록

트레이 메뉴에서 `Start with Windows`를 켜면 부팅 시 자동 실행됩니다.

## 사용 방법

1. UHK Agent에서 키맵(약어)을 먼저 생성
2. `UhkKeymapAutochanger.exe` 실행
3. 트레이 아이콘 우클릭 후 `Open Settings`
4. 기본 키맵 + 프로세스 규칙 저장

트레이 메뉴:
- `Open Settings`
- `Start/Stop Switching`
- `Start with Windows`
- `Exit`

## 설정 파일

- 경로: `%AppData%\UhkKeymapAutochanger\config.json`
- 최초 실행 시 자동 생성

스키마:

```json
{
  "defaultKeymap": "DEF",
  "pollIntervalMs": 250,
  "startWithWindows": true,
  "pauseWhenUhkAgentRunning": true,
  "rules": [
    { "processName": "Code.exe", "keymap": "DEV" },
    { "processName": "chrome.exe", "keymap": "WEB" }
  ]
}
```

필드 설명:
- `defaultKeymap`: 기본(폴백) 키맵 약어
- `pollIntervalMs`: 활성 창 폴링 주기 (100~1000)
- `startWithWindows`: 윈도우 시작 시 자동 실행
- `pauseWhenUhkAgentRunning`: `true`면 UHK Agent 실행 중 자동 전환 일시 정지
- `rules`: 프로세스명 -> 키맵 매핑 목록

설정 파일이 손상되면 `*.invalid.json` 백업을 남기고 기본값으로 재생성합니다.

## UHK Agent 동작 관련

- 기본값은 `pauseWhenUhkAgentRunning=true`입니다.
- UHK Agent 실행 중 HID 충돌 가능성을 줄이기 위한 안전 설정입니다.
- 이 옵션을 끄면 Agent와 동시에 전환을 시도할 수 있지만 충돌 가능성은 높아집니다.

## 디버그 로그

디버그 모드 실행:

```powershell
UhkKeymapAutochanger.exe --debug
```

로그 경로:
- `%LocalAppData%\UhkKeymapAutochanger\debug.log`

## 개발 빌드

사전 요구사항:
- .NET 8 SDK

```powershell
dotnet restore
dotnet build UhkKeymapAutochanger.sln -c Release
dotnet test tests\UhkKeymapAutochanger.Tests\UhkKeymapAutochanger.Tests.csproj -c Release
```

## 단일 EXE 배포 빌드

```powershell
dotnet publish .\src\UhkKeymapAutochanger\UhkKeymapAutochanger.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

출력 경로:
- `src\UhkKeymapAutochanger\bin\Release\net8.0-windows\win-x64\publish\UhkKeymapAutochanger.exe`

## Keymap + Layer 업데이트 (2026-03)

- 프로세스 규칙이 이제 `keymap + layer` 조합을 지원합니다.
- 미매칭 프로세스 fallback은 `defaultKeymap + base`입니다.
- 레이어 전환은 HID `ExecMacroCommand (0x14)` 경로로 `toggleLayer <layer>`를 사용합니다.

설정 예시:

```json
{
  "defaultKeymap": "DEF",
  "pollIntervalMs": 250,
  "startWithWindows": true,
  "pauseWhenUhkAgentRunning": true,
  "rules": [
    { "processName": "Code.exe", "keymap": "DEV", "layer": "fn" },
    { "processName": "chrome.exe", "keymap": "WEB", "layer": "base" }
  ]
}
```

`layer` 허용값:
- `base, fn, mod, mouse, fn2, fn3, fn4, fn5, alt, shift, super, ctrl`

호환성:
- 기존 `rules[].keymap`만 있는 설정도 동작합니다.
- `rules[].layer`가 없거나 비어 있으면 `base`로 자동 처리됩니다.
