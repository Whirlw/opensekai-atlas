Add-Type -AssemblyName System.Drawing, PresentationCore, WindowsBase, System.Xaml
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.cs) + @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Core') -Filter *.cs)
Add-Type -Path $sources.FullName -ReferencedAssemblies System.Drawing, PresentationCore, WindowsBase, System.Xaml, System.Core
