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

$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $ProjectPath 'Packages\manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Certification project has no Packages/manifest.json: $ProjectPath"
}
$projectManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$ragdollDependency = $projectManifest.dependencies.'com.hairibar.ragdoll'
if ([string]::IsNullOrWhiteSpace($ragdollDependency) -or
    -not $ragdollDependency.StartsWith('file:', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Certification project must reference com.hairibar.ragdoll through a local file: dependency.'
}
$dependencyPath = $ragdollDependency.Substring(5)
if (-not [System.IO.Path]::IsPathRooted($dependencyPath)) {
    $dependencyPath = Join-Path $ProjectPath $dependencyPath
}
$dependencyPath = [System.IO.Path]::GetFullPath($dependencyPath)
if (-not [string]::Equals($dependencyPath, $packageRoot,
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Certification project references a different package tree: $dependencyPath (expected $packageRoot)"
}
if (-not ($projectManifest.testables -contains 'com.hairibar.ragdoll')) {
    throw 'Certification project must list com.hairibar.ragdoll in testables.'
}
$assetsRoot = Join-Path $ProjectPath 'Assets'
if (Test-Path -LiteralPath $assetsRoot -PathType Container) {
    $foreignCode = Get-ChildItem -LiteralPath $assetsRoot -Recurse -File |
        Where-Object {
            $_.Extension -in @('.cs', '.asmdef', '.asmref', '.dll') -and
            $_.FullName -notlike (Join-Path $assetsRoot '__HairibarCertification\*') -and
            $_.FullName -notlike (Join-Path $assetsRoot 'Samples\*')
        }
    if ($foreignCode) {
        throw "Certification project is not clean; consumer code exists under Assets: $($foreignCode[0].FullName)"
    }
}
$runtimeSourceRoots = @(
    (Join-Path $packageRoot 'Core'),
    (Join-Path $packageRoot 'Animation\Runtime'),
    (Join-Path $packageRoot 'Animation\Editor')
)
$forbiddenSourceReferences = Get-ChildItem -Path $runtimeSourceRoots -Recurse -File -Filter '*.cs' |
    Select-String -Pattern '(?m)^\s*using\s+RootMotion(?:\.|\s*;)|\bRootMotion\.(?:Dynamics|FinalIK)\b'
if ($forbiddenSourceReferences) {
    throw "Package contains a forbidden PuppetMaster/Final IK code dependency: $($forbiddenSourceReferences[0].Path):$($forbiddenSourceReferences[0].LineNumber)"
}
$assemblyAndPackageFiles = @((Join-Path $packageRoot 'package.json')) +
    @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.asmdef' |
        Select-Object -ExpandProperty FullName)
$forbiddenAssemblyReferences = Select-String -Path $assemblyAndPackageFiles `
    -Pattern '(?i)RootMotion|Final\s*IK|PuppetMaster'
if ($forbiddenAssemblyReferences) {
    throw "Package declares a forbidden proprietary dependency: $($forbiddenAssemblyReferences[0].Path):$($forbiddenAssemblyReferences[0].LineNumber)"
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
    'run-context.json',
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
$env:HAIRIBAR_CLOSURE_RUN_ID = [System.Guid]::NewGuid().ToString('D')
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

    # Unity Hub installations on Windows may detach the Editor process when invoked
    # through PowerShell's call operator, leaving $LASTEXITCODE unset while the
    # actual batch process is still running. Own the process explicitly so each
    # durable stage waits for, and validates, the Editor instance it launched.
    $escapedArguments = @($allArguments | ForEach-Object {
        $argument = [string]$_
        if ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $argument
        }
    })
    $unityProcess = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $escapedArguments `
        -WindowStyle Hidden `
        -PassThru
    # WaitForExit observes only the Editor process. Start-Process -Wait also waits
    # for Unity's persistent licensing descendant on Windows and can deadlock the
    # certification coordinator after an otherwise successful batch stage.
    $unityProcess.WaitForExit()
    if ($unityProcess.ExitCode -ne 0) {
        throw "Hairibar closure stage '$Name' failed with exit code $($unityProcess.ExitCode). Log: $logPath"
    }
    $ownedCompilerDiagnostics = Select-String -LiteralPath $logPath -Pattern `
        '(?i)(com\.hairibar\.ragdoll|Hairibar[._-]Ragdoll).*(warning|error)\s+CS\d+|(warning|error)\s+CS\d+.*(com\.hairibar\.ragdoll|Hairibar[._-]Ragdoll)'
    if ($ownedCompilerDiagnostics) {
        throw "Hairibar closure stage '$Name' emitted owned compiler diagnostics. Log: $logPath"
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
$provisionalEvidence = Get-Content -LiteralPath `
    (Join-Path $OutputRoot 'coverage-manifest-provisional.json') -Raw |
    ConvertFrom-Json
if ($provisionalEvidence.producerProcessId -le 0) {
    throw 'Provisional manifest did not record its producer process.'
}

$env:HAIRIBAR_CLOSURE_PHASE = 'Validate'
Invoke-UnityStage -Name '06-independent-validation' -Arguments @(
    '-executeMethod',
    'Hairibar.Ragdoll.Animation.Editor.HairibarCertification.RunClosure',
    '-quit'
)
$validationEvidence = Get-Content -LiteralPath `
    (Join-Path $OutputRoot 'coverage-manifest-validation.json') -Raw |
    ConvertFrom-Json
if (-not $validationEvidence.succeeded -or
    $validationEvidence.validatorProcessId -le 0 -or
    $validationEvidence.validatorProcessId -eq $provisionalEvidence.producerProcessId) {
    throw 'Independent validation did not execute in a distinct Unity process.'
}

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
$finalEvidence = Get-Content -LiteralPath $finalManifest -Raw | ConvertFrom-Json
if ($finalEvidence.finalizerProcessId -le 0 -or
    $finalEvidence.finalizerProcessId -eq $provisionalEvidence.producerProcessId -or
    $finalEvidence.finalizerProcessId -eq $validationEvidence.validatorProcessId) {
    throw 'Final manifest did not execute in a third distinct Unity process.'
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
