# 배포 라이선스와 소스 제공

파일별 MIT 또는 MPL-2.0 표기가 우선합니다. MPL 적용 파일 및 그 수정 내용은 MPL 2.0으로 제공하며, 실행 파일 수령자는 `SOURCE-CODE.txt`에 기재된 정확한 리비전의 소스를 무료로 받을 수 있습니다. 이 문서는 원본 코드나 외부 구성요소의 라이선스를 변경하지 않습니다.

## 함께 배포하는 고지

- `LICENSE`: 적용 범위와 수정 배포본 안내
- `LICENSE-MIT`, `LICENSE-MPL`: 원문과 기존 저작권 표시
- `THIRD-PARTY-NOTICES.md`: 원본의 제3자 고지
- `licenses/`: 7-Zip 26.00 고지, LGPL 2.1·3.0 및 GPL 3.0 원문
- `SOURCE-CODE.txt`: 앱의 정확한 소스 및 외부 구성요소 소스 주소

## 외부 구성요소 소스

| 구성요소 | 배포 버전 | 소스 |
| --- | --- | --- |
| 7-Zip DLL | 26.00 | [26.00 태그](https://github.com/ip7z/7zip/tree/26.00) |
| SevenZipSharp | 1.0.3 | [NuGet 패키지에 기록된 리비전](https://github.com/files-community/SevenZipSharp/tree/9725bda53e1ee018b39a51c268a90c4905915926) |

`portable-2026.09.11` 릴리즈에는 위 두 구성요소의 소스 ZIP도 제공합니다. 7-Zip에는 LGPL 외에 BSD 고지와 unRAR 제한이 적용되므로 `7-Zip-26.00.txt`를 함께 보관하세요.

SevenZipSharp는 NativeAOT 실행 파일에 포함됩니다. 변경한 라이브러리를 사용하려면 해당 소스를 받아 수정한 패키지를 빌드하고, 앱의 `nuget.config`와 `Directory.Packages.props`에서 그 패키지를 사용하도록 설정한 다음 README의 포터블 빌드 명령으로 앱을 다시 빌드할 수 있습니다. 앱의 소스 및 빌드 설정은 공개되어 있습니다. 적용 라이선스가 허용하는 라이브러리 수정과 수정 사항 디버깅을 위한 역공학을 추가로 제한하지 않습니다.

## 고지 파일 준비

저장소 루트의 PowerShell에서 빌드 결과에 다음 스크립트를 실행합니다. `SourceRevision`에는 실제 실행 파일을 빌드한 소스의 전체 커밋 해시를 지정해야 합니다.

```powershell
./.github/scripts/Add-PortableNotices.ps1 -BuildDirectory ./build -SourceRevision <전체-커밋-해시> -ReleaseTag <릴리즈-태그>
```

그다음 `Data` 및 디버그 심볼을 제외해 압축합니다. 위 고지와 소스 안내를 ZIP에서도 유지하세요. 라이브러리 버전을 변경할 때에는 해당 고지·소스 주소와 릴리즈의 소스 첨부 파일도 갱신해야 합니다.

이 자료는 확인된 고지 누락을 보완합니다. 모든 전이 의존성·상표·재배포 조건에 대한 독립적인 법률 검토가 완료됐다는 의미는 아닙니다.
