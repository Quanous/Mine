# HD — D-Day

바탕화면에 두고 쓰는 디데이 앱이에요. 설치 없이 `HD.exe` 하나만 있으면 돼요.

## 받는 법 (바탕화면으로 바로 받기)

PowerShell(윈도우 키 → `powershell` 검색)에 아래 한 줄을 붙여넣고 엔터:

```powershell
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $ProgressPreference='SilentlyContinue'; iwr https://github.com/Quanous/Mine/releases/latest/download/HD.exe -OutFile (Join-Path ([Environment]::GetFolderPath('Desktop')) 'HD.exe')
```

바탕화면에 `HD.exe`(아이콘 포함)가 생겨요. 더블클릭하면 실행돼요.
(브라우저로 받고 싶으면 [Releases](https://github.com/Quanous/Mine/releases/latest) 페이지에서 `HD.exe`를 받으세요.)

## 사용법

| 동작 | 방법 |
| --- | --- |
| 디데이 추가 | 빈 곳 우클릭 → **디데이 추가** (또는 오른쪽 위 `+`, `Ctrl+N`) |
| 수정 | 항목 우클릭 → **수정** (또는 더블클릭) |
| 사진 넣기/바꾸기 | 추가·수정 창에서 동그란 사진 칸 클릭 (파일을 끌어다 놓아도 돼요) / 항목 우클릭 → **사진 변경** |
| 삭제 | 항목 우클릭 → **삭제** |
| 항상 위에 표시 | 오른쪽 위 핀 아이콘 |
| 창 이동·크기 조절 | 제목줄 드래그 / 가장자리 드래그 |
| 최소화·닫기 | 오른쪽 위 `—`, `×` |

- 가장 가까운 디데이가 맨 위 큰 카드에 나와요. 사진을 안 넣으면 기본 아트가 배경이에요.
- 날짜 칸에서 마우스 휠이나 ↑↓ 키로 숫자를 바꿀 수 있어요.
- 데이터는 `%APPDATA%\HD` 에 저장돼요 (`data.json`, `images`). HD를 켰을 때만 창이 떠 있어요.

## 개발

- .NET Framework 4.8 + WPF (Windows 10/11 에 기본 내장이라 별도 설치 불필요)
- 고해상도(DPI) 대응 매니페스트, 사진은 고품질로 축소/회전 보정 후 저장
- `main` 에 푸시하면 GitHub Actions 가 `HD.exe` 를 빌드해서 Releases 에 올려요
- `HD.exe --demo --shots <폴더>` : 화면을 PNG 로 저장 (CI 에서 UI 확인용)
