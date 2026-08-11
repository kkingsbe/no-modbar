$ErrorActionPreference = 'Stop'
$managed = 'D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed'

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

$asm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
try { $types = $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }

Write-Output "--- Camera state classes (methods, tolerant) ---"
foreach ($t in $types) {
    if ($t.FullName -match 'Camera(Base|Cockpit|Free|Chase)State$|CameraStateManager$') {
        Write-Output "=== $($t.FullName) ==="
        $ms = $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')
        foreach ($m in $ms) {
            try {
                $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
                Write-Output "  $($m.ReturnType.Name) $($m.Name)($ps)"
            } catch { Write-Output "  $($m.Name)(...)" }
        }
        $fs = $t.GetFields('Public,NonPublic,Instance,Static')
        foreach ($f in $fs) {
            try { Write-Output "  [f] $($f.FieldType.Name) $($f.Name)" } catch { Write-Output "  [f] $($f.Name)" }
        }
    }
}

Write-Output "`n--- any member named *look* (all types) ---"
foreach ($t in $types) {
    try {
        $hits = @()
        $hits += $t.GetFields('Public,NonPublic,Instance,Static') | Where-Object { $_.Name -match 'look' } | ForEach-Object { "f:$($_.Name)" }
        $hits += $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly') | Where-Object { $_.Name -match 'look' } | ForEach-Object { "m:$($_.Name)" }
        $hits += $t.GetProperties('Public,NonPublic,Instance,Static') | Where-Object { $_.Name -match 'look' } | ForEach-Object { "p:$($_.Name)" }
        if ($hits) { Write-Output "  $($t.FullName): $($hits -join ', ')" }
    } catch {}
}
