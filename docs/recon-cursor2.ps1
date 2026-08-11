$ErrorActionPreference = 'Stop'
$managed = 'D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed'
$dll = Join-Path $managed 'Assembly-CSharp.dll'

$onResolve = [System.ResolveEventHandler]{
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name + '.dll'
    $candidate = Join-Path $managed $name
    if (Test-Path $candidate) {
        return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate)
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($onResolve)

$asm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($dll)
try { $types = $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }

function Dump-Type($t) {
    Write-Output "=== TYPE: $($t.FullName) (base: $($t.BaseType)) ==="
    try {
        foreach ($f in $t.GetFields('Public,NonPublic,Instance,Static')) { Write-Output "  field: $($f.FieldType.Name) $($f.Name)" }
    } catch { Write-Output "  [fields failed]" }
    try {
        foreach ($m in $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')) {
            $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
            Write-Output "  method: $($m.ReturnType.Name) $($m.Name)($ps)"
        }
    } catch { Write-Output "  [methods failed]" }
}

foreach ($t in $types) {
    if ($t.FullName -match 'CursorManager') { Dump-Type $t }
}

Write-Output "`n--- types with any member matching 'mouselook|mouse_x|MouseX' (case-insens) ---"
foreach ($t in $types) {
    try {
        $names = @()
        $names += $t.GetFields('Public,NonPublic,Instance,Static') | ForEach-Object { $_.Name }
        $names += $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly') | ForEach-Object { $_.Name }
        $names += $t.GetProperties('Public,NonPublic,Instance,Static') | ForEach-Object { $_.Name }
        $hits = $names | Where-Object { $_ -match 'mouselook|mousex|mousey|mousedelta' }
        if ($hits) { Write-Output "$($t.FullName): $($hits -join ', ')" }
    } catch {}
}

Write-Output "`n--- camera state types ---"
foreach ($t in $types) {
    if ($t.FullName -match 'CameraCockpitState|CameraChaseState|CameraFreeState|CameraStateManager|CameraBaseState') { Dump-Type $t }
}
