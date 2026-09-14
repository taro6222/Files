# 정보 창 미리보기 확장 검토 — Office / CAD(DXF·DWG) / 그 외 포맷

작성일: 2026-09-14
대상 커밋: d99a711 (v4.2.32)
검토 범위: 정보 창(Info Pane)의 "미리보기" 탭에서 표시되는 내용

## 1. 결론 요약

| 요청 항목 | 판정 | 필요한 작업 |
| --- | --- | --- |
| Office 설치 시 Office 파일 미리보기 | **이미 구현되어 있음** | 코드 추가 불필요. 다만 표시 품질·안정성 결함 4건 수정 권장 (2장) |
| 한/글(HWP), PDF, RTF, 메일, 3MF 등 | **이미 구현되어 있음** | 없음. 셸 미리보기 처리기가 등록되어 있으면 자동 동작 |
| DXF 미리보기 | **가능하나 자체 구현 필요** | ACadSharp + Win2D 기반 CAD 미리보기 추가 (3장) |
| DWG 미리보기 | **가능하나 자체 구현 필요 / 호환성 제한 있음** | 위와 동일. AC1014~AC1032 범위, 일부 엔티티 미지원 감수 |
| "가능한 경우 모든 파일" | **단계적으로 가능** | 4장 로드맵 참조. 즉시 채울 수 있는 공백 몇 건 존재(SVG 등) |

핵심은 두 가지입니다.

1. Office는 **새로 만들 것이 아니라 이미 있는 경로를 다듬는 일**입니다. Files는 이미 Windows 셸 미리보기 처리기(IPreviewHandler)를 범용으로 호출하고 있어서, Office가 설치되어 있으면 Word/Excel/PowerPoint/Visio 미리보기가 그대로 뜹니다.
2. DXF/DWG는 **Windows에 미리보기 처리기 자체가 없기 때문에** 셸 경유로는 영원히 불가능합니다. 앱 안에서 파싱·렌더링하는 수밖에 없습니다.

## 2. 현재 구조와 Office 미리보기

### 2.1 미리보기 선택 경로

`InfoPaneViewModel.GetBuiltInPreviewControlAsync()` 가 위에서부터 순서대로 판정합니다.

| 순서 | 조건 | 사용 컨트롤 |
| --- | --- | --- |
| 1 | 폴더 / 라이브러리 / 바로 가기 | FolderPreview, BasicPreview |
| 2 | 탐색 가능한 압축 파일 | BasicPreview(ArchivePreviewViewModel) |
| 3 | 오디오·비디오 | MediaPreview |
| 4 | `.md` | MarkdownPreview |
| 5 | 이미지(png/jpg/bmp/gif/tiff/ico/webp/jxr) | ImagePreview |
| 6 | `.txt` | TextPreview |
| 7 | ~~PDF~~ / ~~HTML~~ | **주석 처리되어 비활성** |
| 8 | RTF | RichTextPreview |
| 9 | 코드 확장자 사전(약 20개 언어) | CodePreview |
| 10 | **셸 미리보기 처리기가 등록된 모든 확장자** (폰트·실행 파일 제외) | **ShellPreview** |
| 11 | 그 외 500 KB 이하 | 텍스트로 시도 |
| 실패 | — | BasicPreview (Jumbo 아이콘 + 속성) |

10번이 Office를 포함한 범용 경로입니다. `ShellPreviewViewModel.FindPreviewHandlerFor()` 가 `AssocQueryString(ASSOCSTR_SHELLEXTENSION, "{8895b1c6-…}")` 로 확장자에 연결된 처리기 CLSID를 찾고, `PreviewHandler` 가 `CoCreateInstance(..., CLSCTX_LOCAL_SERVER, ...)` 로 prevhost 대리 프로세스에 띄운 뒤, 자식 HWND를 DirectComposition 표면으로 만들어 XAML에 합성합니다.

`CLSCTX_LOCAL_SERVER`만 사용하므로 **32비트 Office + 64비트 Files 조합에서도 비트 불일치 문제가 없습니다.**

### 2.2 대상 PC 실측 (desktop-3062kq9)

`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers` 등록 현황:

- Microsoft Word previewer `{84F66100-…}`, Excel `{00020827-…}`, PowerPoint `{65235197-…}`, Visio `{21E17C2F-…}`
- Microsoft PDF Previewer, Adobe PDF Preview Handler
- **HwpPreviewHandler `{CF8092A3-…}`** — `Hwp.Document.130\shellex\{8895b1c6-…}` 에 정상 연결됨
- Windows TXT / RTF / Font / Contact / Mail(MAPI·MIME·HTML) / Media Player / 3MF

`.docx → {84F66100-…}` 연결도 확인했습니다. 즉 **이 PC에서는 Office·HWP·PDF 미리보기가 현재 코드로 이미 동작해야 합니다.**

### 2.3 수정이 필요한 결함 4건

동작은 하지만 "제대로 보이지 않는" 원인이 되는 지점들입니다.

**(a) 포인터를 올리기 전까지 Office 미리보기가 그려지지 않음** — `ShellPreviewViewModel`

미리보기 자식 창은 `WS_EX_LAYERED | WS_EX_COMPOSITED` + DWM 클로킹 상태로 생성됩니다. Office 처리기는 이 조합에서 정상적으로 그리지 못해, 현재 코드는 `_isOfficePreview` 일 때 포인터가 미리보기 위로 들어오면 `GWL_EXSTYLE` 를 0으로 바꿔 임시로 푸는 우회를 씁니다. 결과적으로 마우스를 올리기 전에는 빈 영역으로 보입니다.

→ 개선: Office 처리기로 판정되면 처음부터 레이어드 스타일 없이 창을 만들고(비 Office 경로는 현행 유지), 로드 완료 시점에 한 번 언클로킹하도록 분리.

**(b) 다크 테마 미대응** — `PreviewHandler.SetBackground/SetForeground/SetFont` 가 **한 번도 호출되지 않음**

`IPreviewHandlerVisuals` 를 얻어 두고도 쓰지 않아, 다크 테마에서 흰 배경 문서가 그대로 튀어나옵니다.

→ 개선: `DoPreview()` 직전에 현재 테마의 배경/전경/기본 글꼴을 전달.

**(c) `FindPreviewHandlerFor()` 의 버퍼 길이 불일치 (잠재적 버퍼 오버런)**

```csharp
fixed (char* … pszOutput = new char[1024])
…
uint cchOutput = 2024;   // ← 버퍼는 1024자인데 2024자로 알려줌
```

또한 `AssocQueryString` 의 반환 HRESULT를 검사하지 않고 곧바로 `Guid.Parse` 합니다(실패 시 예외 → catch → null). CLSID 문자열은 38자라 실제로 넘칠 일은 드물지만, 네이티브 API에 잘못된 길이를 넘기는 것은 그대로 두면 안 되는 결함입니다.

→ 개선: `cchOutput = 1024` 로 정정, HRESULT 검사 후 `Guid.TryParse` 사용.

**(d) 항목 선택마다 같은 조회를 2회 수행**

판정 단계(`GetBuiltInPreviewControlAsync`)에서 한 번, `WM_CREATE` 에서 다시 한 번 `FindPreviewHandlerFor` 를 호출합니다. 확장자별 CLSID를 캐시하면 선택 변경이 잦을 때의 레지스트리 조회를 없앨 수 있습니다.

**(e) 실패 시 침묵** — `WM_CREATE` 의 `catch { UnloadPreview(); }` 가 원인을 삼킵니다. 최소한 로그와 "이 파일 형식의 미리보기를 표시할 수 없습니다" 안내로 대체 권장.

### 2.4 사용자 설정 제안

현재 셸 미리보기는 항상 켜져 있고 끌 수 있는 스위치가 없습니다(`IInfoPaneSettingsService` 에는 `IsInfoPaneEnabled`, 크기, 볼륨, 선택 탭만 존재). 외부 처리기는 서드파티 코드이므로 다음 두 개를 추가하는 편이 안전합니다.

- `UseShellPreviewHandlers` (기본 켜짐)
- `ShellPreviewHandlerBlocklist` (문제 확장자 제외용, 고급 설정)

## 3. DXF / DWG 미리보기

### 3.1 왜 지금은 안 되는가

대상 PC 실측 결과:

- `HKCR\.dwg\shellex` — **없음** (미리보기 처리기, 썸네일 처리기 모두 미등록)
- `HKCR\.dxf\shellex` — **없음**
- `.dwg` 의 ProgID는 `Icad.Drawing.14.0.P.x64` (IntelliCAD 계열)이며 이 ProgID에도 `shellex` 키가 없음

Windows는 DWG/DXF용 미리보기 처리기를 기본 제공하지 않고, AutoCAD·DWG TrueView 계열도 썸네일만 등록하거나 그마저도 등록하지 않는 경우가 많습니다. 따라서 10번 경로(ShellPreview)에서는 CLSID를 찾지 못해 탈락하고, 11번(텍스트 시도)에서 ASCII DXF가 원시 텍스트로 잠깐 보이거나(500 KB 이하), 결국 BasicPreview의 아이콘으로 떨어집니다.

**즉 외부 프로그램 설치로 해결될 여지가 거의 없으며, 앱 내 구현이 유일한 현실적 경로입니다.**

### 3.2 구현 방안 비교

| 방안 | 라이선스 | DWG | DXF | 평가 |
| --- | --- | --- | --- | --- |
| **A. ACadSharp + Win2D 자체 렌더러** | MIT | AC1014~AC1032 읽기 | ASCII/바이너리 읽기 | **권장.** 외부 의존 없음, 포터블 배포 그대로 가능 |
| B. netDxf + Win2D | MIT | 불가 | 읽기 | DXF만 필요할 때의 경량 대안 |
| C. LibreDWG 연동 | **GPL-3.0** | 가능 | 가능 | **비권장.** 앱 전체에 GPL 전파, MIT/MPL 이중 라이선스와 충돌 |
| D. ODA File Converter 외부 호출로 DWG→DXF | 재배포 제한 | 간접 | — | 사용자가 별도 설치해야 하고 프로세스 기동 비용이 큼. 보조 수단으로만 |
| E. DWG 내장 미리보기 비트맵 추출 | — | 저해상도 | 없음 | 파일에 미리보기가 저장된 경우에만. 폴백으로 유용 |

ACadSharp 3.7.1은 MIT, net8/9/10 및 .NET Standard 2.0/2.1 지원, DWG는 AC1014·AC1015·AC1018·AC1021(읽기)·AC1024·AC1027·AC1032, DXF는 ASCII/바이너리 양쪽을 읽습니다. **Win2D(Microsoft.Graphics.Win2D 1.4.0)는 이미 `Files.App.csproj` 에 참조되어 있어 추가 의존성이 없습니다.**

### 3.3 권장 구현 설계

신규 파일(예상):

```
src/Files.App/ViewModels/UserControls/Previews/CadPreviewViewModel.cs
src/Files.App/UserControls/FilePreviews/CadPreview.xaml(.cs)
src/Files.App/Utils/Cad/CadDocumentLoader.cs      // ACadSharp 파싱 + 취소/가드
src/Files.App/Utils/Cad/CadWin2DRenderer.cs       // 엔티티 → Win2D 드로잉
src/Files.App/Utils/Cad/AutoCadColorIndex.cs      // ACI 256색 팔레트
```

1차 지원 엔티티: `LINE`, `LWPOLYLINE`, `POLYLINE`, `CIRCLE`, `ARC`, `ELLIPSE`, `POINT`, `SOLID`, `TEXT`, `MTEXT`, `INSERT`(블록 재귀, 깊이 제한), `DIMENSION`(치수 블록의 지오메트리만), `SPLINE`(폴리라인 근사), `HATCH`(경계선만 또는 단색 채움).

표시 규칙:

- 모델 공간 기준, 전체 엔티티 바운딩 박스로 fit-to-view. 팬/줌은 2단계 과제
- 색상은 레이어 ACI 색인 → RGB. 라이트 테마는 흰 배경·검은 선, 다크 테마는 반전(AutoCAD 모델 공간 관례와 동일)
- 선 굵기(lineweight)는 1차에서 무시하고 화면 배율 기준 1 px 고정
- 글꼴은 SHX 미지원 → 시스템 대체 글꼴

성능·안정성 가드 (기존 `Constants.PreviewPane` 스타일에 맞춰 상수화):

- 파일 크기 상한(예: DXF 50 MB / DWG 50 MB), 초과 시 BasicPreview로 폴백
- 엔티티 수 상한(예: 200,000) 초과 시 부분 렌더 + "일부만 표시" 표기
- 파싱·렌더는 백그라운드 스레드, 기존 `CreatePreviewLoadToken()` 취소 토큰과 연결(선택 변경 100 ms 디바운스는 이미 존재)
- 파싱 결과는 경로+수정시각 키로 소량 캐시

주의 사항:

- `IsAotCompatible=true` 인 프로젝트이므로 ACadSharp 추가 후 **트리밍/AOT 분석 경고를 반드시 확인**해야 합니다. 리플렉션 경고가 나오면 `Files.App.Server` 쪽 분리나 `DynamicDependency` 로 국소 처리.
- 패키지는 `Directory.Packages.props` 에 중앙 버전으로 추가(`ManagePackageVersionsCentrally`).
- MIT 라이선스 고지를 `docs/licenses/` 와 포터블 패키징 스크립트(`Add-PortableNotices.ps1`)의 제3자 고지에 추가해야 합니다.

작업량 추정: DXF만 4~6일, DWG 포함 8~12일(엔티티 커버리지 확대와 실물 도면 회귀 검증 포함).

## 4. "가능한 모든 파일" 확장 로드맵

### 4.1 지금 이미 되는 것 (추가 작업 없음)

이미지, 오디오·비디오, 텍스트, 마크다운, 코드 20여 종, 압축, 폴더·바로 가기, 그리고 **셸 미리보기 처리기가 등록된 모든 포맷**(Office, HWP, PDF, RTF, Visio, 메일, 연락처, 3MF, 폰트 등).

### 4.2 적은 작업으로 메울 수 있는 공백

| 대상 | 현재 동작 | 조치 |
| --- | --- | --- |
| **SVG** | `IsHtmlFile()` 에 포함되지만 HtmlPreview 분기가 **주석 처리**되어 있어 결국 원시 텍스트로 표시 | `ImagePreview` 에서 `SvgImageSource` 로 처리. 1일 미만 |
| **PDF / HTML** | 분기가 주석 처리된 채 방치. 현재는 셸 처리기에 의존 | 죽은 코드 정리 또는 복구 여부 결정 |
| HEIC·AVIF·RAW | 미지원 | Windows 코덱 경유로 `IsImagePreviewFile` 확장 |
| JSON·YAML·TOML·INI·CSV·LOG | `.txt` 가 아니면 코드 사전에도 없어 폴백 경로로 감 | `CodePreviewViewModel` 확장자 사전에 추가(수 시간) |
| EPUB | 미지원 | zip+XHTML 파싱, 중간 난이도 |
| STL·OBJ | 미지원 | Win2D 와이어프레임 또는 Windows 3D 뷰어 위임 |
| STEP·IGES | 미지원 | 오픈소스 대안 빈약, 우선순위 하 |

### 4.3 권장 순서

- **Phase 1 (1~2일)** — 2.3의 결함 (a)~(e) 수정 + SVG 처리 + 코드 확장자 사전 확대. Office 체감 품질이 여기서 대부분 해결됩니다.
- **Phase 2 (4~6일)** — DXF 미리보기(ACadSharp + Win2D), 설정 스위치 2종 추가.
- **Phase 3 (4~6일)** — DWG 지원 확대, 엔티티 커버리지 보강, 내장 미리보기 비트맵 폴백.
- **Phase 4** — 이미지 코덱 확장, EPUB, 3D 포맷.

## 5. 검증 방법

- `msbuild -restore src/Files.App/Files.App.csproj -p:Configuration=Debug -p:Platform=x64 -v:quiet -clp:ErrorsOnly`
- 회귀 대상: docx/xlsx/pptx/hwp/pdf 각 1개, 라이트·다크 테마 각각, 마우스를 올리지 않은 상태의 최초 렌더링
- CAD: AC1015·AC1018·AC1024·AC1032 DWG 각 1개와 ASCII·바이너리 DXF 각 1개, 블록 중첩·치수·해치 포함 도면 1개
- 성능: 선택을 빠르게 연속 변경했을 때 prevhost 프로세스가 누적되지 않는지, `UnloadPreview()` 후 COM 참조가 해제되는지

## 6. 남은 판단 사항

1. DWG를 1차 범위에 포함할지(호환성 한계를 사용자에게 어떻게 고지할지)
2. 셸 미리보기 처리기 on/off 설정을 노출할지, 항상 켜둘지
3. ACadSharp 도입에 따른 포터블 패키지 크기 증가 허용 범위
