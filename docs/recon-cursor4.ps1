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

$failCount = 0
Write-Output "--- members (field/prop/method-return) of type CursorManager ---"
foreach ($t in $types) {
    try {
        foreach ($f in $t.GetFields('Public,NonPublic,Instance,Static')) {
            if ($f.FieldType.FullName -match 'CursorManager') {
                $static = if ($f.IsStatic) { 'static ' } else { '' }
                Write-Output "  FIELD $($t.FullName) :: $static$($f.Name)"
            }
        }
        foreach ($p in $t.GetProperties('Public,NonPublic,Instance,Static')) {
            if ($p.PropertyType.FullName -match 'CursorManager') {
                Write-Output "  PROP  $($t.FullName) :: $($p.Name)"
            }
        }
        foreach ($m in $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')) {
            if ($m.ReturnType.FullName -match 'CursorManager') {
                Write-Output "  RET   $($t.FullName) :: $($m.Name)()"
            }
        }
    } catch { $script:failCount++ }
}
Write-Output "  (types that failed member scan: $failCount)"

Write-Output "`n--- enableMouseLook anywhere (fields+props, all assemblies in Managed) ---"
foreach ($a in [System.AppDomain]::CurrentDomain.ReflectionOnlyGetAssemblies()) {
    try { $ts = $a.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $ts = $_.Exception.Types | Where-Object { $_ -ne $null } } catch { continue }
    foreach ($t in $ts) {
        try {
            foreach ($f in $t.GetFields('Public,NonPublic,Instance,Static')) {
                if ($f.Name -match 'enableMouseLook') { Write-Output "  FIELD [$($a.GetName().Name)] $($t.FullName) :: $($f.FieldType.Name) $($f.Name)" }
            }
            foreach ($p in $t.GetProperties('Public,NonPublic,Instance,Static')) {
                if ($p.Name -match 'enableMouseLook') { Write-Output "  PROP  [$($a.GetName().Name)] $($t.FullName) :: $($p.PropertyType.Name) $($p.Name)" }
            }
        } catch {}
    }
}
