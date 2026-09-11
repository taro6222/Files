# Copyright (c) 2026 changhoon
# Licensed under the MIT License.

param(
	[Parameter(Mandatory)]
	[string]$BuildDirectory,
	[Parameter(Mandatory)]
	[ValidatePattern('^[0-9a-fA-F]{40}$')]
	[string]$SourceRevision,
	[Parameter(Mandatory)]
	[ValidatePattern('^[A-Za-z0-9._-]+$')]
	[string]$ReleaseTag
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destinationRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $destinationRoot 'Files.exe'))) {
	throw 'BuildDirectory must contain Files.exe.'
}
$resolvedRevision = git -C $repositoryRoot rev-parse --verify "$SourceRevision^{commit}"
if ($LASTEXITCODE -ne 0 -or $resolvedRevision -ne $SourceRevision) {
	throw 'SourceRevision must identify an existing source commit.'
}
foreach ($name in @('LICENSE', 'LICENSE-MIT', 'LICENSE-MPL')) {
	Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination (Join-Path $destinationRoot $name) -Force
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot '.github/NOTICE.md') -Destination (Join-Path $destinationRoot 'THIRD-PARTY-NOTICES.md') -Force
$licensesDirectory = Join-Path $destinationRoot 'licenses'
[IO.Directory]::CreateDirectory($licensesDirectory) | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs/licenses') -Filter '*.txt' -File | ForEach-Object {
	Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licensesDirectory $_.Name) -Force
}
$sourceNotice = @"
Files Portable — source code and licensing

This is an independently maintained modified distribution of Files.
Upstream: https://github.com/files-community/Files
Maintained source: https://github.com/taro6222/Files
Source revision used for the app executable: $SourceRevision
Browse: https://github.com/taro6222/Files/tree/$SourceRevision
Download: https://github.com/taro6222/Files/archive/$SourceRevision.zip

MPL-2.0-covered source files and modifications are available under MPL 2.0.
MIT-covered files retain their MIT terms. See LICENSE, LICENSE-MIT and
LICENSE-MPL. Recipients can obtain the corresponding source free of charge
at the URLs above. Copyright and license notices in source files remain
applicable. No additional restriction is imposed on rights under MPL/LGPL.

Third-party notices: THIRD-PARTY-NOTICES.md and licenses/
7-Zip 26.00 source: https://github.com/ip7z/7zip/tree/26.00
SevenZipSharp 1.0.3 source:
https://github.com/files-community/SevenZipSharp/tree/9725bda53e1ee018b39a51c268a90c4905915926
These component source archives are also provided as assets of release:
https://github.com/taro6222/Files/releases/tag/$ReleaseTag

The application source includes its project files and build instructions.
SevenZipSharp is compiled into the NativeAOT executable. To use a modified
version, build its source as a NuGet package, select that package in
nuget.config/Directory.Packages.props and rebuild the application using
the portable publish instructions in the repository README. Debugging
such library modifications through reverse engineering is not prohibited
by any additional terms of this distribution.
"@
[IO.File]::WriteAllText((Join-Path $destinationRoot 'SOURCE-CODE.txt'), ($sourceNotice -replace "`r?`n", "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
Write-Output 'Portable license notices and source information copied.'
