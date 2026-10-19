param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
# SPlusCC's line-comment parser requires CRLF; LF can silently swallow source.
$usp = Join-Path $root 'Televic Confero Control.usp'
$source = [IO.File]::ReadAllText($usp)
[IO.File]::WriteAllText($usp, ($source -replace '\r?\n', "`r`n"), [Text.Encoding]::ASCII)
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $root 'Televic Confero c#\Televic Confero\Televic Confero.csproj') /t:Rebuild /p:Configuration=Release /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'C# build failed' }
$release = Join-Path $root 'Televic Confero c#\Televic Confero\bin\Release'
Copy-Item (Join-Path $release 'Televic Confero.clz') $root -Force
$log = Join-Path $root 'compile.log'
& 'C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe' /rebuild (Join-Path $root 'Televic Confero Control.usp') /target series4 /usersplusfolder $root /out $log
if ($LASTEXITCODE -ne 0 -or !(Select-String -LiteralPath $log -SimpleMatch 'Total Error(s): 0')) { throw 'SIMPL+ build failed; see compile.log' }
$generated = Join-Path $root 'SPlsWork\Televic_Confero_Control.cs'
if (!(Test-Path -LiteralPath $generated) -or !(Select-String -LiteralPath $generated -SimpleMatch 'ConfigureTransport')) {
    throw 'SIMPL+ compiler omitted module code; check USP encoding and line endings'
}
if ($Test) {
    $testDir = Join-Path $root 'tests'
    & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo "/out:$testDir\Integration.exe" "/reference:$release\Televic Confero.dll" (Join-Path $testDir 'Integration.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    Copy-Item (Join-Path $release '*.dll') $testDir -Force
    & (Join-Path $testDir 'Integration.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Integration test failed' }
    & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo "/out:$testDir\ModuleIntegration.exe" "/reference:$release\Televic Confero.dll" "/reference:$release\SimplSharpNewtonsoft.dll" (Join-Path $testDir 'ModuleIntegration.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Module test build failed' }
    & (Join-Path $testDir 'ModuleIntegration.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Module integration test failed' }
}
