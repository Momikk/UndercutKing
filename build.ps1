param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$previousDalamudHome = $env:DALAMUD_HOME
try {
    if ([string]::IsNullOrWhiteSpace($env:DALAMUD_HOME)) {
        $candidates = @(
            (Join-Path $env:APPDATA 'XIVLauncherCN\addon\Hooks\dev'),
            (Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev')
        )
        $env:DALAMUD_HOME = $candidates | Where-Object {
            Test-Path -LiteralPath (Join-Path $_ 'Dalamud.dll')
        } | Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($env:DALAMUD_HOME)) {
        throw '未找到 Dalamud 开发程序集，请先运行 XIVLauncher，或指定 DALAMUD_HOME。'
    }
    $dalamudDll = Join-Path $env:DALAMUD_HOME 'Dalamud.dll'
    $apiLevel = [System.Reflection.AssemblyName]::GetAssemblyName($dalamudDll).Version.Major
    if ($apiLevel -ne 15) {
        throw "当前 Dalamud API 是 $apiLevel，此版本要求 API 15。"
    }
    & dotnet build (Join-Path $PSScriptRoot 'UndercutKing\UndercutKing.csproj') -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE。" }
}
finally {
    $env:DALAMUD_HOME = $previousDalamudHome
}
