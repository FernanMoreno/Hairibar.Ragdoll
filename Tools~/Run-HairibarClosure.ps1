param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$UnityPath = [System.IO.Path]::GetFullPath($UnityPath)
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable not found: $UnityPath"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Unity project not found: $ProjectPath"
}

[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

$lockPath = Join-Path $OutputRoot '.hairibar-closure.lock'
try {
    $lockStream = [System.IO.File]::Open(
        $lockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch {
    throw "Another Hairibar closure process owns output directory: $OutputRoot"
}

try {

# A failed rerun must not leave a previous final manifest looking current.
$runArtifacts = @(
    'editmode-results.xml',
    'playmode-results.xml',
    'build-manifest.json',
    'windows-player-result.json',
    'profiler-results.json',
    'scene-results.json',
    'documentation-audit.json',
    'coverage-manifest-provisional.json',
    'coverage-manifest-validation.json',
    'coverage-manifest-final.json'
)
foreach ($artifactName in $runArtifacts) {
    $artifactPath = Join-Path $OutputRoot $artifactName
    if (Test-Path -LiteralPath $artifactPath -PathType Leaf) {
        Remove-Item -LiteralPath $artifactPath -Force
    }
}

$env:HAIRIBAR_CERTIFICATION_OUTPUT = $OutputRoot
$env:HAIRIBAR_CLOSURE_OUTPUT = $OutputRoot
$env:HAIRIBAR_EDITMODE_RESULTS = Join-Path $OutputRoot 'editmode-results.xml'
$env:HAIRIBAR_PLAYMODE_RESULTS = Join-Path $OutputRoot 'playmode-results.xml'
$env:HAIRIBAR_PROFILER_RESULTS = Join-Path $OutputRoot 'profiler-results.json'
$env:HAIRIBAR_SCENE_RESULTS = Join-Path $OutputRoot 'scene-results.json'
$env:HAIRIBAR_DOCUMENTATION_AUDIT = Join-Path $OutputRoot 'documentation-audit.json'

function Invoke-UnityStage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $logPath = Join-Path $OutputRoot ($Name + '.log')
    $allArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-logFile', $logPath
    ) + $Arguments

    & $UnityPath @allArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Hairibar closure stage '$Name' failed with exit code $LASTEXITCODE. Log: $logPath"
    }
}

$env:HAIRIBAR_CLOSURE_PHASE = 'Prepare'
Invoke-UnityStage -Name '01-prepare-assets' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure'
)

$env:HAIRIBAR_CLOSURE_PHASE = 'Build'
Invoke-UnityStage -Name '02-build-and-player' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure'
)

Invoke-UnityStage -Name '03-editmode' -Arguments @(
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testResults', $env:HAIRIBAR_EDITMODE_RESULTS
)

Invoke-UnityStage -Name '04-playmode' -Arguments @(
    '-runTests',
    '-testPlatform', 'PlayMode',
    '-testResults', $env:HAIRIBAR_PLAYMODE_RESULTS
)

$env:HAIRIBAR_CLOSURE_PHASE = 'Provisional'
Invoke-UnityStage -Name '05-provisional-manifest' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure',
    '-quit'
)

$env:HAIRIBAR_CLOSURE_PHASE = 'Validate'
Invoke-UnityStage -Name '06-independent-validation' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure',
    '-quit'
)

$env:HAIRIBAR_CLOSURE_PHASE = 'Finalize'
Invoke-UnityStage -Name '07-final-manifest' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure',
    '-quit'
)

$finalManifest = Join-Path $OutputRoot 'coverage-manifest-final.json'
if (-not (Test-Path -LiteralPath $finalManifest -PathType Leaf)) {
    throw "Closure completed without a final manifest: $finalManifest"
}

Write-Output "Hairibar closure completed: $finalManifest"
}
finally {
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }
    if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
        Remove-Item -LiteralPath $lockPath -Force
    }
}
