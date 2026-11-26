#Requires -Version 7.2
[CmdletBinding()]
param(
    # Accept files or directories for a module-by-module migration.
    [string[]]$Path = @('Assets/Game', 'Assets/Tests')
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')

$issues = [Collections.Generic.SortedSet[string]]::new()
function Report($Token, $Message) {
    $line = $Token.GetLocation().GetLineSpan().StartLinePosition.Line + 1
    [void]$issues.Add("${file}:${line}: $Message")
}

$files = foreach ($entry in $Path) {
    Get-ChildItem -LiteralPath $entry -File -Recurse | Where-Object Extension -eq '.cs'
}
$options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default
$repoRoot = Split-Path -Parent $PSScriptRoot
foreach ($item in $files) {
    $file = [IO.Path]::GetRelativePath($PWD, $item.FullName)
    $relative = [IO.Path]::GetRelativePath($repoRoot, $item.FullName).Replace('\', '/')
    $checkSpacing = $relative.StartsWith('Assets/Game/Runtime/OpenSekai/App/UI/') -and
        $relative -ne 'Assets/Game/Runtime/OpenSekai/App/UI/LibraryUi.cs'
    $source = [IO.File]::ReadAllText($item.FullName)
    foreach ($symbols in @('UNITY_EDITOR,UNITY_STANDALONE,UNITY_INCLUDE_TESTS', 'UNITY_ANDROID')) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            $source, $options.WithPreprocessorSymbols([string[]]($symbols.Split(',') + 'UNITY_5_3_OR_NEWER' + 'UNITY_2022_2_OR_NEWER')))
        foreach ($node in $tree.GetRoot().DescendantNodes()) {
            if ($checkSpacing) {
                $target = if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax]) { $node.Left }
                    elseif (($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.PrefixUnaryExpressionSyntax] -or
                             $node -is [Microsoft.CodeAnalysis.CSharp.Syntax.PostfixUnaryExpressionSyntax]) -and
                            $node.OperatorToken.Text -in '++', '--') { $node.Operand }
                if ($null -ne $target -and $target.GetLastToken().ValueText -ceq 'characterSpacing') {
                    Report $target.GetLastToken() 'Set UI letter spacing through LibraryUi.UseSpacedCondensedFont or LibraryUi.SetLetterSpacing.'
                }
            }
            if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.AttributeListSyntax] -and
                $node.Parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax]) {
                $end = $node.CloseBracketToken
                $next = $end.GetNextToken()
                if ($node.Attributes.Count -gt 1 -or
                    $end.GetLocation().GetLineSpan().EndLinePosition.Line -eq
                    $next.GetLocation().GetLineSpan().StartLinePosition.Line) {
                    Report $end 'Place each declaration attribute on its own line.'
                }
            }

            if ($node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax]) { continue }
            $declaration = $node.Parent.Parent
            $modifiers = @($declaration.Modifiers | ForEach-Object Text)
            if ($modifiers -contains 'const') {
                $pattern = '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'
            } elseif ($declaration -is [Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax]) {
                $attributes = foreach ($list in $declaration.AttributeLists) {
                    foreach ($attribute in $list.Attributes) { $attribute.Name.ToString().Split('.')[-1] }
                }
                if ($attributes -match '^Serialize(Field|Reference)(Attribute)?$') {
                    $pattern = '^[a-z][a-zA-Z0-9]*$'
                } elseif (-not ($modifiers -match '^(public|internal|protected)$')) {
                    $pattern = '^_[a-z][a-zA-Z0-9]*$'
                } else { continue }
            } else { continue }
            if ($node.Identifier.ValueText -cnotmatch $pattern) {
                Report $node.Identifier "Identifier '$($node.Identifier.ValueText)' should match $pattern"
            }
        }
    }
}
$issues
Write-Host "$($files.Count) files, $($issues.Count) findings."
if ($issues.Count) { exit 1 }
