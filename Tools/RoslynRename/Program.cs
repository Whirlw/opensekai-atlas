using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace RoslynRename;

/// <summary>
/// Turns public fields into "private backing field + property" without breaking call sites.
///
/// The property keeps the old field name for one moment, so every existing reference keeps
/// compiling; Roslyn's <see cref="Renamer"/> then applies the PascalCase name everywhere,
/// which is what the hand-rolled syntax scan used to get wrong (nested types, generics,
/// nameof, XML docs).
///
/// Fields that cannot survive the change are skipped up front:
///   * passed by <c>ref</c>/<c>out</c> (a property is not a variable),
///   * a struct value whose members are assigned through the field (CS1612).
/// </summary>
internal static class Program
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp9);

    private sealed record Candidate(
        string File,
        SyntaxTree Tree,
        FieldDeclarationSyntax Field,
        INamedTypeSymbol Type,
        IFieldSymbol Symbol,
        string Name,
        string PropertyName,
        bool IsNested);

    private static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("usage: RoslynRename <unityManagedDir> <scriptAssembliesDir> <apply|dry> <path...> [--nested]");
            return 2;
        }

        var managed = args[0];
        var scriptAsm = args[1];
        var apply = string.Equals(args[2], "apply", StringComparison.OrdinalIgnoreCase);
        var withNested = args.Any(a => a == "--nested");
        var withUnityObjects = args.Any(a => a == "--unity-objects");
        var typeFilter = args.FirstOrDefault(a => a.StartsWith("--type=", StringComparison.Ordinal))?.Substring(7);
        var excluded = args.Where(a => a.StartsWith("--exclude=", StringComparison.Ordinal))
            .Select(a => a.Substring("--exclude=".Length))
            .ToHashSet(StringComparer.Ordinal);
        var roots = args.Skip(3).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

        var files = roots.SelectMany(r => Directory.Exists(r)
                ? Directory.EnumerateFiles(r, "*.cs", SearchOption.AllDirectories)
                : Enumerable.Repeat(r, 1))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"files: {files.Count}");

        var trees = files.ToDictionary(
            f => f,
            f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), ParseOptions, path: f),
            StringComparer.OrdinalIgnoreCase);

        var refs = new List<MetadataReference>();
        foreach (var dir in new[] { managed, scriptAsm })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var dll in Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories))
            {
                try { refs.Add(MetadataReference.CreateFromFile(dll)); } catch { /* native or broken */ }
            }
        }

        var compilation = CSharpCompilation.Create(
            "Assembly-CSharp",
            trees.Values,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var candidates = new List<Candidate>();
        var skipped = new List<string>();
        var nested = new List<string>();

        foreach (var tree in trees.Values)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol) continue;

                var isNested = typeDecl.Parent is TypeDeclarationSyntax;
                var isStruct = typeDecl is StructDeclarationSyntax;

                foreach (var field in typeDecl.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(SyntaxKind.PublicKeyword)) continue;
                    if (field.Modifiers.Any(SyntaxKind.ConstKeyword)) continue;
                    if (field.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;

                    foreach (var variable in field.Declaration.Variables)
                    {
                        var name = variable.Identifier.ValueText;
                        var propertyName = char.ToUpperInvariant(name[0]) + name.Substring(1);

                        if (isNested && !withNested) { nested.Add($"{typeSymbol.ToDisplayString()}.{name}"); continue; }

                        if (typeFilter != null && typeSymbol.Name != typeFilter) continue;
                        if (excluded.Contains(typeSymbol.Name)) { skipped.Add($"{typeSymbol.Name}.{name}: excluded"); continue; }

                        // A struct turns every member assignment of its constructor into a property
                        // setter, which is CS0188 ("this used before all fields are assigned").
                        if (isStruct) { skipped.Add($"{typeSymbol.Name}.{name}: struct"); continue; }

                        // Compiler-generated state machines (decompiled as _003C..._003Ed__NN) are not
                        // hand-written code: leave their fields alone.
                        if (typeSymbol.Name.Contains("_003C") || typeSymbol.Name.Contains("<>"))
                        {
                            skipped.Add($"{typeSymbol.Name}.{name}: generated");
                            continue;
                        }

                        // Serialized component fields stay public for now: renaming them means
                        // rewriting the YAML keys of every scene and prefab that carries the
                        // component, which is a separate, riskier pass.
                        if (!withUnityObjects && DerivesFromUnityObject(typeSymbol)) { skipped.Add($"{typeSymbol.Name}.{name}: serialized component"); continue; }

                        if (typeSymbol.GetMembers(propertyName).Any(m => m.Name == propertyName && m is IPropertySymbol))
                        {
                            skipped.Add($"{typeSymbol.Name}.{name}: {propertyName} already exists");
                            continue;
                        }

                        if (typeSymbol.GetMembers(propertyName).Any(m => m.Name == propertyName && m is not IFieldSymbol))
                        {
                            skipped.Add($"{typeSymbol.Name}.{name}: {propertyName} taken by another member");
                            continue;
                        }

                        if (typeSymbol.GetMembers(BackingName(name)).Any(m => m is IFieldSymbol))
                        {
                            skipped.Add($"{typeSymbol.Name}.{name}: backing field already present");
                            continue;
                        }

                        if (model.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
                        {
                            skipped.Add($"{typeSymbol.Name}.{name}: no symbol");
                            continue;
                        }

                        candidates.Add(new Candidate(tree.FilePath, tree, field, typeSymbol, fieldSymbol, name, propertyName, isNested));
                    }
                }
            }
        }

        var candidateSymbols = new HashSet<ISymbol>(candidates.Select(c => (ISymbol)c.Symbol), SymbolEqualityComparer.Default);
        var cannotBeVariable = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in trees.Values)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None))
                {
                    if (model.GetSymbolInfo(argument.Expression).Symbol is ISymbol s && candidateSymbols.Contains(s))
                    {
                        cannotBeVariable.Add(s);
                    }
                }

                if (node is AssignmentExpressionSyntax assignment)
                {
                    var target = assignment.Left;
                    while (target is MemberAccessExpressionSyntax memberAccess) target = memberAccess.Expression;

                    if (model.GetSymbolInfo(target).Symbol is IFieldSymbol f &&
                        candidateSymbols.Contains(f) &&
                        f.Type.IsValueType &&
                        f.Type.SpecialType != SpecialType.System_Int32 &&
                        f.Type.SpecialType != SpecialType.System_Single &&
                        f.Type.SpecialType != SpecialType.System_Boolean &&
                        f.Type.SpecialType != SpecialType.System_Double)
                    {
                        cannotBeVariable.Add(f);
                    }
                }
            }
        }

        var edits = new Dictionary<string, List<(int Start, int Length, string Text)>>(StringComparer.OrdinalIgnoreCase);
        var converted = new List<(string File, string TypeName, string OldName, string NewName)>();

        foreach (var candidate in candidates)
        {
            if (cannotBeVariable.Contains(candidate.Symbol))
            {
                skipped.Add($"{candidate.Type.Name}.{candidate.Name}: ref/out or struct mutation");
                continue;
            }

            var variable = candidate.Field.Declaration.Variables
                .First(v => v.Identifier.ValueText == candidate.Name);
            var isUnityObject = DerivesFromUnityObject(candidate.Type);
            var indent = IndentOf(candidate.Field);
            var typeText = candidate.Field.Declaration.Type.WithoutTrivia().ToString();
            var builder = new StringBuilder();

            builder.Append(indent);
            if (isUnityObject) builder.Append("[SerializeField] ");
            builder.Append("private ").Append(typeText).Append(' ').Append(BackingName(candidate.Name));
            if (variable.Initializer != null) builder.Append(" = ").Append(variable.Initializer.Value.WithoutTrivia());
            builder.Append(';').Append('\n');
            builder.Append(indent).Append("public ").Append(typeText).Append(' ').Append(candidate.Name).Append('\n');
            builder.Append(indent).Append('{').Append('\n');
            builder.Append(indent).Append("    get => _").Append(candidate.Name).Append(';').Append('\n');
            builder.Append(indent).Append("    set => _").Append(candidate.Name).Append(" = value;").Append('\n');
            builder.Append(indent).Append('}');

            AddEdit(edits, candidate.File, candidate.Field.Span.Start, candidate.Field.Span.Length, builder.ToString());
            converted.Add((candidate.File, candidate.Type.ToDisplayString(), candidate.Name, candidate.PropertyName));
        }

        Console.WriteLine($"converted fields: {converted.Count}");
        Console.WriteLine($"skipped: {skipped.Count}");
        foreach (var g in skipped.GroupBy(s => s.Substring(s.IndexOf(": ") + 2)).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  skip[{g.Key}]: {g.Count()}");
        }
        Console.WriteLine($"nested public fields left alone: {nested.Count}");

        foreach (var group in converted.GroupBy(c => c.TypeName).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  type {group.Key}: {group.Count()}");
        }

        if (!apply)
        {
            Console.WriteLine("dry run (pass 'apply' to write)");
            return 0;
        }

        WriteDeclarationEdits(edits);

        var renames = converted.Where(c => c.OldName != c.NewName).ToList();
        Console.WriteLine($"renaming {renames.Count} properties to PascalCase ...");
        Console.WriteLine($"renamed: {RenameProperties(files, refs, renames)}");
        return 0;
    }

    private static void WriteDeclarationEdits(Dictionary<string, List<(int Start, int Length, string Text)>> edits)
    {
        var touched = 0;
        foreach (var (file, list) in edits)
        {
            var text = File.ReadAllText(file);
            foreach (var e in list.OrderByDescending(x => x.Start))
            {
                text = text.Remove(e.Start, e.Length).Insert(e.Start, e.Text);
            }
            File.WriteAllText(file, text, new UTF8Encoding(false));
            touched++;
        }
        Console.WriteLine($"files written: {touched}");
    }

    private static int RenameProperties(
        List<string> files,
        List<MetadataReference> refs,
        List<(string File, string TypeName, string OldName, string NewName)> renames)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Game",
            "Game",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true),
            parseOptions: ParseOptions,
            metadataReferences: refs);

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var text = SourceText.From(File.ReadAllText(file), Encoding.UTF8);
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), Path.GetFileName(file), text, filePath: file);
        }

        var done = 0;
        foreach (var rename in renames)
        {
            var document = solution.Projects.Single().Documents.FirstOrDefault(d => d.FilePath == rename.File);
            if (document == null) continue;

            var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            var model = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            if (root == null || model == null) continue;

            var property = root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == rename.OldName &&
                                     model.GetDeclaredSymbol(p)?.ContainingType.ToDisplayString() == rename.TypeName);
            if (property == null) continue;

            var symbol = model.GetDeclaredSymbol(property);
            if (symbol == null) continue;

            try
            {
                solution = Renamer.RenameSymbolAsync(
                        solution, symbol, new SymbolRenameOptions(), rename.NewName, CancellationToken.None)
                    .GetAwaiter().GetResult();
                done++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  rename failed {rename.TypeName}.{rename.OldName}: {ex.Message}");
            }
        }

        foreach (var changed in solution.GetChanges(workspace.CurrentSolution).GetProjectChanges())
        {
            foreach (var docId in changed.GetChangedDocuments())
            {
                var doc = solution.GetDocument(docId);
                if (doc == null) continue;
                var text = doc.GetTextAsync().GetAwaiter().GetResult();
                File.WriteAllText(doc.FilePath!, text.ToString(), new UTF8Encoding(false));
            }
        }

        return done;
    }

    private static void AddEdit(
        Dictionary<string, List<(int Start, int Length, string Text)>> edits,
        string file,
        int start,
        int length,
        string text)
    {
        if (!edits.TryGetValue(file, out var list))
        {
            list = new List<(int, int, string)>();
            edits[file] = list;
        }
        list.Add((start, length, text));
    }

    private static string BackingName(string name) =>
        "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string IndentOf(SyntaxNode node)
    {
        var line = node.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .Select(t => t.ToFullString())
            .LastOrDefault();
        return line ?? "        ";
    }

    private static bool DerivesFromUnityObject(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            var ns = current.ContainingNamespace?.ToDisplayString();
            if (ns == "UnityEngine" && current.Name is "MonoBehaviour" or "ScriptableObject" or "StateMachineBehaviour") return true;
            if (ns == "UnityEngine.EventSystems" && current.Name == "UIBehaviour") return true;
            current = current.BaseType;
        }
        return false;
    }
}
