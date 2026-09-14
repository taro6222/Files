# 검색 성능 개선 검토 및 실행 계획

작성일: 2026-09-14
대상 커밋: d99a711 (v4.2.32)
대상 파일: `src/Files.App/Utils/Storage/Search/FolderSearch.cs`

이 문서는 이전에 작성했던 Everything 연동 검토 문서를 대체합니다. 결론은 **외부 프로그램 연동이나 관리자 권한 없이, 현재 순회 코드의 구조적 낭비를 제거하는 것만으로 큰 폭의 개선이 가능하다**는 것입니다.

## 1. 배경 — 외부 의존 방안을 배제한 이유

| 방안 | 판정 | 이유 |
| --- | --- | --- |
| Everything SDK 내장 | **무의미** | SDK(`Everything32/64.dll`)는 IPC 클라이언트 래퍼일 뿐 검색 엔진이 아님. `Everything.exe` 가 떠 있지 않으면 `EVERYTHING_ERROR_IPC` 만 반환 |
| Everything 본체 번들 | **비권장** | 클로즈드 소스 프리웨어로 재배포 조건이 공개되어 있지 않음. 더구나 Everything 자신도 NTFS 인덱싱에 관리자 권한/서비스를 요구하므로 **권한 문제가 전혀 해결되지 않음** |
| Everything 설치 시 감지 연동 | 보조 수단으로만 가능 | `voidtools/everything_sdk3` 는 MIT이고 named pipe(`\\.\PIPE\Everything IPC`) 프로토콜이라 재배포할 바이너리는 0개. 다만 사용자에게 별도 설치를 요구하므로 주력이 될 수 없음 |
| MFT + USN 자체 인덱서 | 최종형이나 후순위 | 볼륨 핸들 열기에 관리자/Backup Operators 권한 필요 → 승격 헬퍼 프로세스가 전제. 포터블 배포 정책과 충돌 |

**중요한 인식:** Everything 속도의 대부분은 MFT가 아니라 "미리 만들어 둔 메모리 상주 이름 인덱스"에서 나옵니다. MFT는 그 인덱스를 1초 만에 채우는 수단일 뿐입니다. 따라서 인덱스를 일반 디렉터리 순회로 채워도 **검색 응답 자체는 동일하게 즉시**입니다. 권한이 필요한 것은 "인덱스를 채우는 소스"뿐입니다.

## 2. 현재 코드의 문제 — 줄 단위 근거

`FolderSearch.SearchWithWin32Async()` 가 병목의 전부입니다.

### (1) 디렉터리마다 열거를 두 번 수행 — 가장 큰 단일 낭비

```csharp
// 416행 부근: 이름 매칭용
FindFirstFileEx($"{folder}\\*{QueryWithWildcard}", ...)

// 572행 부근: 하위 폴더 수집용 — 같은 디렉터리를 다시 엽니다
FindFirstFileEx($"{folder}\\*", ...)
```

와일드카드 필터링을 커널에 맡겨 얻는 이득보다, 모든 디렉터리를 두 번 전수 열거하는 손해가 훨씬 큽니다. `*` 로 한 번만 열거하면서 매칭과 하위 폴더 수집을 동시에 처리하면 **디렉터리 I/O가 그대로 절반**이 됩니다.

### (2) 완전 직렬 DFS — 디스크 큐 깊이가 항상 1

```csharp
// 619행
foreach (var subDir in subDirectories)
    await SearchWithWin32Async(subDir, hiddenOnly, maxItemCount - (uint)results.Count, results, token);
```

병렬성이 0입니다. NVMe/SSD에서 특히 손해가 큽니다. 바운디드 워커 풀 + BFS 큐로 전환 시 **4~8배**를 기대할 수 있습니다.

### (3) 디렉터리 하나당 `Task.Run` 4개 + `WithTimeoutAsync(5초)` 2개

디렉터리 10만 개면 태스크 40만 개, 타이머 20만 개입니다. 더 심각한 것은 **5초를 넘긴 가지가 조용히 버려져 결과가 누락**된다는 점입니다. 성능 문제이자 정확성 버그입니다.

### (4) 인덱서와 전수 순회를 매번 둘 다 수행 (400~412행)

```csharp
if (workingFolder) { await SearchAsync(storageFolder, results, token); hiddenOnlyFromWin32 = (results.Count != 0); }
if (!IsAQSQuery)  { await SearchWithWin32Async(folder, hiddenOnlyFromWin32, ...); }
```

Windows Search 인덱서가 결과를 반환한 경우에도 Win32 재귀는 그대로 돕니다.

### (5) 바로가기마다 결과 목록 선형 스캔 — O(n²) (527행)

```csharp
if (results.Any(r => string.Equals(r.ItemPath, itemPath, StringComparison.OrdinalIgnoreCase)))
    continue;
```

`.lnk` 가 많은 트리에서 결과가 쌓일수록 급격히 느려집니다.

### (6) 기본 결과 상한 없음

`MaxItemCount = 0 // 0: no limit`.

### 정정 사항

이전 문서에 "결과 항목마다 아이콘/썸네일을 로드한다"고 기재했으나 **부정확**했습니다. 683행 주석대로 아이콘은 `MaxItemCount > 0` 인 **검색창 제안**에서만 로드하며, 검색 페이지(`MaxItemCount == 0`)에서는 로드하지 않습니다.

## 3. 실행 계획

| 단계 | 내용 | 권한 | 외부 의존 | 작업량 | 기대 효과 |
| --- | --- | --- | --- | --- | --- |
| **A (진행 중)** | (1)(2)(3)(5) 수정 — 단일 열거 패스, 병렬 BFS, 타임아웃 래핑 제거, 중복 검사 HashSet | 불필요 | 없음 | 1~2일 | 수 배 + 결과 누락 버그 해소 |
| B | (4)(6) — 인덱서 성공 시 Win32 중복 제거, 기본 결과 상한 + "더 보기" | 불필요 | 없음 | 1일 | 인덱싱 경로 중복 제거 |
| C | `FindNextFile` → `GetFileInformationByHandleEx(FileIdBothDirectoryInfo)` 로 교체 (시스템 콜당 다수 엔트리 일괄 수신) | 불필요 | 없음 | 1~2일 | 추가 2~4배 |
| D | `ISearchQueryHelper` + OLE DB(`Search.CollatorDSO`) 로 `SystemIndex` 직접 질의 | 불필요 | 없음 | 2~3일 | 인덱싱된 범위 수십 ms |
| E | 자체 경량 이름 인덱스 + `ReadDirectoryChangesW` 실시간 갱신 | 불필요 | 없음 | 5~8일 | 전 범위 즉시 응답 |
| F | 폴더 탐색 시 열거 결과를 인덱스에 적립(비용 0 인덱싱) | 불필요 | 없음 | E에 포함 | E의 초기 구축 부담 완화 |
| G | 승격 헬퍼 + MFT/USN 소스 | **필요** | 없음 | 12~18일 | 초기 구축 1초대 |

E 단계는 인덱스 자료구조와 "인덱스를 채우는 소스"를 분리해 설계하고, G는 소스만 교체하는 형태로 얹습니다.

## 4. A단계 설계 (이번 작업 범위)

`SearchWithWin32Async()` 를 다음과 같이 재작성합니다.

- 디렉터리당 `FindFirstFileEx($"{folder}\\*", FindExInfoBasic, FIND_FIRST_EX_LARGE_FETCH)` **1회**. 순회 중 각 엔트리에 대해
  - 디렉터리면 BFS 큐에 투입
  - 파일/디렉터리 이름이 질의에 매칭되면 결과에 추가
- 매칭은 인프로세스에서 수행. 기존 `QueryWithWildcard` 의 `*질의*` 의미(부분 문자열, 대소문자 무시)를 그대로 보존
- BFS 큐를 바운디드 워커(`Environment.ProcessorCount` 기준, 상한 8)로 소비. 디렉터리 열거만 병렬화하고 결과 컬렉션 변형은 잠금으로 직렬화
- 디렉터리 단위 `Task.Run` / `WithTimeoutAsync` 제거. 취소는 검색 전체에 대한 `CancellationToken` 하나로 통일
- 바로가기 중복 검사를 `HashSet<string>(StringComparer.OrdinalIgnoreCase)` 로 전환
- 접근 거부 디렉터리는 조용히 건너뛰되 로그에 남김
- `SearchTick` 발화 주기(32건, 이후 300건마다)는 기존 동작 유지

동작 호환성에서 유지할 것: 숨김/시스템/점 파일 필터 규칙, `hiddenOnly` 모드 의미, 바로가기 `ParseLinkAsync` 지연 채움, `maxItemCount` 조기 종료.

## 5. 검증

```powershell
msbuild -restore src/Files.App/Files.App.csproj -p:Configuration=Debug -p:Platform=x64 -v:quiet -clp:ErrorsOnly
```

기능 회귀 항목:

- 비인덱싱 드라이브 루트에서 흔한 질의(예: `.pdf`) — 결과 건수가 수정 전과 동일한지
- 깊은 트리(5단계 이상)에서 5초 이상 걸리던 가지의 결과가 이제 누락되지 않는지
- 숨김 항목 표시 on/off, 보호된 시스템 파일 표시 on/off 조합
- 바로가기가 많은 폴더에서 중복 없이 1회만 나오는지
- 검색 도중 다른 폴더로 이동 시 취소가 즉시 걸리는지
