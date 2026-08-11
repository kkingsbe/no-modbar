$ErrorActionPreference = 'Stop'
$managed = 'D:\SteamLibrary\steamapps\common\Nuclear Option\NuclearOption_Data\Managed'
$dll = Join-Path $managed 'Assembly-CSharp.dll'

# Resolve reflection-only dependencies from the game's Managed folder
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

Write-Output "TOTAL TYPES: $($types.Count)"

$patterns = 'enableMouseLook','SetLockState','lockState','LockCursor','MouseLook','FreeLook','Cursor'
foreach ($t in $types) {
    try {
        $members = @()
        $members += $t.GetFields('Public,NonPublic,Instance,Static') | ForEach-Object { "field $($_.Name)" }
        $members += $t.GetMethods('Public,NonPublic,Instance,Static,DeclaredOnly') | ForEach-Object { "method $($_.Name)" }
        $members += $t.GetProperties('Public,NonPublic,Instance,Static') | ForEach-Object { "prop $($_.Name)" }
        foreach ($m in $members) {
            foreach ($p in $patterns) {
                if ($m -match [regex]::Escape($p)) {
                    Write-Output "$($t.FullName) :: $m"
                    break
                }
            }
        }
    } catch {}
}
