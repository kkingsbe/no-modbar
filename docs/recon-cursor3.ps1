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

Write-Output "--- CursorFlags enum ---"
foreach ($t in $types) {
    if ($t.FullName -match 'CursorFlags' -and $t.IsEnum) {
        foreach ($f in $t.GetFields('Public,Static')) { Write-Output "  $($f.Name) = $($f.GetRawConstantValue())" }
    }
}

Write-Output "`n--- fields of type CursorManager (owners) ---"
foreach ($t in $types) {
    try {
        foreach ($f in $t.GetFields('Public,NonPublic,Instance,Static')) {
            if ($f.FieldType.Name -eq 'CursorManager') {
                $static = if ($f.IsStatic) { 'static ' } else { '' }
                Write-Output "  $($t.FullName) :: $static$($f.Name)"
            }
        }
    } catch {}
}

Write-Output "`n--- methods returning CursorManager ---"
foreach ($t in $types) {
    try {
        foreach ($m in $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')) {
            if ($m.ReturnType.Name -eq 'CursorManager') {
                $static = if ($m.IsStatic) { 'static ' } else { '' }
                Write-Output "  $($t.FullName) :: $static$($m.Name)()"
            }
        }
    } catch {}
}

Write-Output "`n--- enableMouseLook declared on ---"
foreach ($t in $types) {
    try {
        foreach ($f in $t.GetFields('Public,NonPublic,Instance,Static')) {
            if ($f.Name -eq 'enableMouseLook') { Write-Output "  FIELD  $($t.FullName) :: $($f.FieldType.Name) $($f.Name)" }
        }
        foreach ($p in $t.GetProperties('Public,NonPublic,Instance,Static')) {
            if ($p.Name -eq 'enableMouseLook') { Write-Output "  PROP   $($t.FullName) :: $($p.PropertyType.Name) $($p.Name)" }
        }
    } catch {}
}

Write-Output "`n--- types matching 'Cockpit|MouseInput|FlightInput' ---"
foreach ($t in $types) {
    if ($t.FullName -match 'Cockpit|MouseInput|FlightInput') { Write-Output "  $($t.FullName) (base: $($t.BaseType))" }
}
