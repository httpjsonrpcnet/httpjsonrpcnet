param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package))
try {
    $names = $archive.Entries.FullName
    foreach ($required in @('lib/netstandard2.0/HttpJsonRpc.dll','README.md','HttpJsonRpc.nuspec')) {
        if ($names -notcontains $required) { throw "Missing package entry: $required" }
    }
    if ($names -match 'Tests|\.cs$|\.pfx$') { throw 'Unexpected source, test, or certificate file in package.' }
    $reader = [IO.StreamReader]::new($archive.GetEntry('HttpJsonRpc.nuspec').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.package.metadata.id -ne 'HttpJsonRpc') { throw 'Unexpected package ID.' }
    $deps = $manifest.package.metadata.dependencies.group.dependency
    foreach ($required in @('Microsoft.AspNetCore.Server.Kestrel','Microsoft.Bcl.AsyncInterfaces','System.ComponentModel.Annotations')) {
        if ($deps.id -notcontains $required) { throw "Missing runtime dependency: $required" }
    }
    Write-Output "Validated HttpJsonRpc $($manifest.package.metadata.version): netstandard2.0 assembly, README, and runtime dependencies."
} finally { $archive.Dispose() }
