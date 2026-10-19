param(
    [string]$TargetPath = (Join-Path $PSScriptRoot '..\Q-Sys plugin dev\Q-SYS-Plugin-televic-Confero\ConferoFakeAPI-TestKit')
)

$ErrorActionPreference = 'Stop'
$target = (Resolve-Path -LiteralPath $TargetPath).ProviderPath
if (-not (Test-Path -LiteralPath $target -PathType Container)) {
    throw "Fake API target is not a directory: $target"
}
$linkPath = Join-Path $PSScriptRoot 'ConferoFakeAPI-TestKit'
$existing = Get-Item -LiteralPath $linkPath -Force -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($existing.LinkType -eq 'Junction' -and $existing.Target -contains $target) {
        Write-Output "Fake API is already linked: $linkPath -> $target"
        return
    }
    throw "A different item already exists at $linkPath. It has been left untouched."
}
New-Item -ItemType Junction -Path $linkPath -Target $target | Out-Null
Write-Output "Fake API linked: $linkPath -> $target"
