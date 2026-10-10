param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.1.2',
    [string]$PublishDirectory = ''
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not a valid release version."
}

$projectDirectory = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectDirectory 'PhosphorPet.csproj'
$installerScript = Join-Path $PSScriptRoot 'PhosphorPet.iss'
if (-not $PublishDirectory) {
    $PublishDirectory = Join-Path $projectDirectory 'publish\win-x64'
}
$publishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)
$outputDirectory = Join-Path $projectDirectory 'dist'
$installerPath = Join-Path $outputDirectory 'PhosphorPet-Setup.exe'
$isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
if ($isccCommand) {
    $isccPath = $isccCommand.Source
} else {
    $knownPaths = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $isccPath = $knownPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $isccPath) {
    throw 'Inno Setup 6 is required to compile the installer. Install it, then run this script again.'
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
& dotnet publish $projectFile `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    "-p:Version=$Version" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

& $isccPath '/Qp' "/DAppVersion=$Version" "/DPublishDirectory=$publishDirectory" "/O$outputDirectory" $installerScript
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath)) {
    throw "Inno Setup failed to create $installerPath (exit code $LASTEXITCODE)"
}

Write-Output "Installer created: $installerPath"
