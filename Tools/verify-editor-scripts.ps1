#requires -Version 7.3
# Compare the live Editor, its DLL/PDB pairs, and the actual inputs to each project assembly.
[CmdletBinding()]
param([switch]$Repair)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'Assets'
$pathComparer = if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }
$definitions = Get-ChildItem (Join-Path $assets Game), (Join-Path $assets Tests) -Recurse -Filter 'OpenSekai.*.asmdef'
if (!$definitions) { throw "No OpenSekai assembly definitions found under $assets." }

function Invoke-Editor([string]$code) {
    $reply = & unity command script eval --project-path $root --result-only $code | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$reply.success) { throw "Editor evaluation failed: $($reply | ConvertTo-Json -Depth 5)" }
    return $reply.result
}

function Get-StaleInputs {
    $modules = Invoke-Editor @'
if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorUtility.scriptCompilationFailed)
    throw new InvalidOperationException("Compilation pending or failed.");
return string.Join("\n", AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name.StartsWith("OpenSekai."))
    .Select(a => a.GetName().Name + "=" + a.ManifestModule.ModuleVersionId));
'@
    $loaded = ConvertFrom-StringData $modules
    foreach ($definition in $definitions) {
        $name = (Get-Content -LiteralPath $definition.FullName -Raw | ConvertFrom-Json).name
        $dll = Join-Path $root "Library/ScriptAssemblies/$name.dll"
        $pe = [Reflection.PortableExecutable.PEReader]::new([IO.File]::OpenRead($dll))
        $symbols = $null
        try {
            $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $id = $metadata.GetGuid($metadata.GetModuleDefinition().Mvid)
            if ($loaded[$name] -ne $id.ToString()) { throw "Editor has not loaded $dll ($id)." }
            $pdbPath = $null
            $openFile = [Func[string,IO.Stream]]{ param($path) [IO.File]::OpenRead($path) }
            if (!$pe.TryOpenAssociatedPortablePdb($dll, $openFile, [ref]$symbols, [ref]$pdbPath)) {
                throw "Missing or mismatched portable PDB for $name."
            }
            $reader = $symbols.GetMetadataReader()
            $inputs = [Collections.Generic.Dictionary[string,string]]::new($pathComparer)
            foreach ($handle in $reader.Documents) {
                $doc = $reader.GetDocument($handle)
                $path = $reader.GetString($doc.Name)
                # Roslyn also emits documents for generated code under Library/Bee.
                if (![IO.Path]::GetRelativePath($root, $path).Replace('\','/').StartsWith('Assets/')) { continue }
                if ($reader.GetGuid($doc.HashAlgorithm) -ne [guid]'8829d00f-11b8-4213-878b-770e8597ac16') {
                    throw "Unsupported source hash algorithm for $path."
                }
                $inputs[$path] = [Convert]::ToHexString($reader.GetBlobBytes($doc.Hash))
            }
            foreach ($source in Get-ChildItem -LiteralPath $definition.DirectoryName -Recurse -File -Filter '*.cs') {
                $hash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
                if ($inputs[$source.FullName] -ne $hash) { $source.FullName }
                [void]$inputs.Remove($source.FullName)
            }
            # Remaining documents belonged to sources that were moved or removed.
            $inputs.Keys
        } finally {
            if ($symbols) { $symbols.Dispose() }
            $pe.Dispose()
        }
    }
}

$stale = @(Get-StaleInputs | Sort-Object -Unique -CaseSensitive:(!$IsWindows))
if ($stale.Count -and $Repair) {
    # Force the individual imports that Refresh() alone can miss.
    $inputList = Join-Path $root "Temp/verify-scripts-$([guid]::NewGuid()).txt"
    $imports = foreach ($file in $stale) {
        if (Test-Path -LiteralPath $file -PathType Leaf) { [IO.Path]::GetRelativePath($root, $file).Replace('\','/') }
    }
    [IO.File]::WriteAllLines($inputList, [string[]]@($imports))
    try {
        $repairCode = @"
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before recompiling.");
var paths = System.IO.File.ReadAllLines(@"$($inputList.Replace('"','""'))");
UnityEditor.AssetDatabase.Refresh();
foreach (var path in paths)
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
return true;
"@
        Write-Host "Reimporting stale script inputs:`n$($stale -join [Environment]::NewLine)"
        Invoke-Editor $repairCode | Out-Null
    } finally {
        [IO.File]::Delete($inputList)
    }
    # Compilation and the following domain reload are asynchronous. Only the loaded result counts.
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        Start-Sleep -Seconds 2
        try {
            $stale = @(Get-StaleInputs | Sort-Object -Unique -CaseSensitive:(!$IsWindows))
        } catch {
            if ([DateTime]::UtcNow -ge $deadline) { throw }
        }
    } while ($stale.Count -and [DateTime]::UtcNow -lt $deadline)
}
if ($stale.Count) { throw "Editor assemblies have stale C# inputs (use -Repair):`n$($stale -join [Environment]::NewLine)" }
Write-Host "Verified source hashes and loaded DLL/PDB identities for $($definitions.Count) OpenSekai assemblies."
