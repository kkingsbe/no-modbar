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

Write-Output "--- parameters named *mouselook* or typed CursorManager ---"
foreach ($t in $types) {
    try {
        foreach ($m in $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')) {
            foreach ($p in $m.GetParameters()) {
                if ($p.Name -match 'mouselook') { Write-Output "  PARAM-NAME $($t.FullName).$($m.Name)(... $($p.ParameterType.Name) $($p.Name) ...)" }
                if ($p.ParameterType.FullName -match 'CursorManager') { Write-Output "  PARAM-TYPE $($t.FullName).$($m.Name)(... CursorManager $($p.Name) ...)" }
            }
        }
        foreach ($c in $t.GetConstructors('Public,NonPublic,Instance,Static')) {
            foreach ($p in $c.GetParameters()) {
                if ($p.ParameterType.FullName -match 'CursorManager') { Write-Output "  CTOR-PARAM $($t.FullName).ctor(... CursorManager $($p.Name) ...)" }
                if ($p.Name -match 'mouselook') { Write-Output "  CTOR-NAME $($t.FullName).ctor(... $($p.Name) ...)" }
            }
        }
    } catch {}
}

Write-Output "`n--- static fields/methods on CursorManager (declared) ---"
foreach ($t in $types) {
    if ($t.FullName -eq 'CursorManager') {
        Write-Output "  IsClass=$($t.IsClass) IsAbstract=$($t.IsAbstract) IsSealed=$($t.IsSealed)"
        foreach ($c in $t.GetConstructors('Public,NonPublic,Instance,Static')) {
            $ps = ($c.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
            Write-Output "  ctor($ps) public=$($c.IsPublic)"
        }
    }
}

Write-Output "`n--- where is CursorManager type? assembly + referencing assemblies ---"
foreach ($t in $types) {
    if ($t.FullName -eq 'CursorManager') { Write-Output "  declared in: $($t.Assembly.GetName().Name)" }
}

Write-Output "`n--- methods taking/returning CursorFlags (likely callers of SetFlag pattern) ---"
foreach ($t in $types) {
    try {
        foreach ($m in $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly')) {
            if ($m.ReturnType.FullName -match 'CursorFlags') { Write-Output "  RET-FLAGS $($t.FullName).$($m.Name)()" }
            foreach ($p in $m.GetParameters()) {
                if ($p.ParameterType.FullName -match 'CursorFlags') { Write-Output "  PARAM-FLAGS $($t.FullName).$($m.Name)(CursorFlags $($p.Name))" }
            }
        }
    } catch {}
}
