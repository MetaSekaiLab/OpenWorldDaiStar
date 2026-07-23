using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OpenWorldDaiStar.ProtocolGen;

internal static class Program
{
    private const string GeneratorVersion = "0.2.0";
    private static readonly HashSet<string> WireAttributes =
        ["MessagePackObject", "Key", "Union", "IgnoreMember"];
    private static readonly HashSet<string> NonWireAttributes =
        [
            "Token", "Address", "FieldOffset", "CompilerGenerated", "MemoryTable",
            "Nullable", "NullableContext", "Flags"
        ];

    public static int Main(string[] args)
    {
        try
        {
            var options = GeneratorOptions.Parse(args);
            var artifacts = Generate(options);
            if (options.Command == "generate")
            {
                Directory.CreateDirectory(options.OutputDirectory);
                File.WriteAllText(options.OutputFile, artifacts.Source, Utf8NoBom);
                File.WriteAllText(options.LockFile, artifacts.LockJson, Utf8NoBom);
                Console.WriteLine(
                    $"Generated {artifacts.TypeCount} protocol types from {artifacts.InputCount} stub files.");
                return 0;
            }

            VerifyFile(options.OutputFile, artifacts.Source);
            VerifyFile(options.LockFile, artifacts.LockJson);
            Console.WriteLine(
                $"Verified {artifacts.TypeCount} protocol types from {artifacts.InputCount} stub files.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static GeneratedArtifacts Generate(GeneratorOptions options)
    {
        var rootsJson = File.ReadAllText(options.RootsFile);
        var roots = ParseRoots(rootsJson);
        var declarations = IndexDeclarations(options.StubRoot);
        var selected = ResolveClosure(roots.Values, declarations);

        var source = RenderSource(selected.Values);
        var inputFiles = selected.Values
            .Select(item => item.Path)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => new LockInput(
                Path.GetRelativePath(options.StubRoot, path).Replace('\\', '/'),
                Sha256(File.ReadAllBytes(path))))
            .ToArray();
        var types = selected.Values
            .OrderBy(item => item.FullName, StringComparer.Ordinal)
            .Select(item => new LockType(item.FullName, ShapeHash(item.Declaration)))
            .ToArray();
        var lockData = new ProtocolLock(
            options.ClientVersion,
            GeneratorVersion,
            Sha256(Encoding.UTF8.GetBytes(rootsJson)),
            inputFiles,
            types,
            new LockOutput("Sirius.Shared.g.cs", Sha256(Encoding.UTF8.GetBytes(source))),
            new SerializerContract(
                ["GeneratedResolver", "StandardResolver"],
                "Lz4BlockArray",
                "stub/Sirius.Serializers/DefaultMessagePackSerializer.cs@0xA66ABC8"));
        var lockJson = JsonSerializer.Serialize(lockData, JsonOptions) + "\n";
        return new GeneratedArtifacts(source, lockJson, types.Length, inputFiles.Length);
    }

    private static Dictionary<string, string> ParseRoots(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var endpoint in document.RootElement.EnumerateObject())
        {
            foreach (var direction in endpoint.Value.EnumerateObject())
            {
                if (direction.Value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException($"Root {endpoint.Name}.{direction.Name} must be a type name.");
                result[$"{endpoint.Name}.{direction.Name}"] = direction.Value.GetString()!;
            }
        }
        return result;
    }

    private static Dictionary<string, StubDeclaration> IndexDeclarations(string stubRoot)
    {
        if (!Directory.Exists(stubRoot))
            throw new DirectoryNotFoundException($"Stub root does not exist: {stubRoot}");

        var sharedProject = Path.Combine(stubRoot, "Sirius.Shared");
        if (!Directory.Exists(sharedProject))
            throw new DirectoryNotFoundException($"Sirius.Shared stub project does not exist: {sharedProject}");

        var result = new Dictionary<string, StubDeclaration>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(sharedProject, "*.cs", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}MessagePack{Path.DirectorySeparatorChar}Formatters{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.EndsWith("GeneratedMessagePackResolver.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            var compilationUnit = tree.GetCompilationUnitRoot();
            foreach (var declaration in compilationUnit.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (declaration.Parent is not NamespaceDeclarationSyntax and
                    not FileScopedNamespaceDeclarationSyntax)
                {
                    continue;
                }

                var nameSpace = declaration.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault()?.Name.ToString() ?? string.Empty;
                var fullName = string.IsNullOrEmpty(nameSpace)
                    ? declaration.Identifier.ValueText
                    : $"{nameSpace}.{declaration.Identifier.ValueText}";
                var candidate = new StubDeclaration(
                    fullName,
                    nameSpace,
                    path,
                    declaration,
                    compilationUnit.Usings.ToArray(),
                    false);
                if (result.TryAdd(fullName, candidate))
                    continue;

                var existing = result[fullName];
                if (existing.Declaration.NormalizeWhitespace().ToFullString() !=
                    declaration.NormalizeWhitespace().ToFullString())
                {
                    result[fullName] = existing with { HasConflictingDefinition = true };
                }
            }
        }
        return result;
    }

    private static SortedDictionary<string, StubDeclaration> ResolveClosure(
        IEnumerable<string> roots,
        Dictionary<string, StubDeclaration> declarations)
    {
        var selected = new SortedDictionary<string, StubDeclaration>(StringComparer.Ordinal);
        var queue = new Queue<string>(roots.Distinct(StringComparer.Ordinal));
        while (queue.TryDequeue(out var fullName))
        {
            if (selected.ContainsKey(fullName))
                continue;
            if (!declarations.TryGetValue(fullName, out var item))
                throw new InvalidDataException($"Protocol root or dependency is missing from stub: {fullName}");
            ValidateDeclaration(item);
            selected.Add(fullName, item);

            foreach (var dependency in FindDependencies(item, declarations))
            {
                if (!selected.ContainsKey(dependency))
                    queue.Enqueue(dependency);
            }
        }
        return selected;
    }

    private static IEnumerable<string> FindDependencies(
        StubDeclaration item,
        Dictionary<string, StubDeclaration> declarations)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        if (item.Declaration.BaseList is not null)
            CollectTypeNames(item.Declaration.BaseList, identifiers);
        if (item.Declaration is TypeDeclarationSyntax typeDeclaration)
        {
            if (typeDeclaration.TypeParameterList is not null)
                CollectTypeNames(typeDeclaration.TypeParameterList, identifiers);
            foreach (var clause in typeDeclaration.ConstraintClauses)
                CollectTypeNames(clause, identifiers);
        }
        foreach (var property in Members(item.Declaration).OfType<PropertyDeclarationSyntax>())
            CollectTypeNames(property.Type, identifiers);
        foreach (var field in Members(item.Declaration).OfType<FieldDeclarationSyntax>())
            CollectTypeNames(field.Declaration.Type, identifiers);
        foreach (var method in Members(item.Declaration).OfType<MethodDeclarationSyntax>())
        {
            CollectTypeNames(method.ReturnType, identifiers);
            CollectTypeNames(method.ParameterList, identifiers);
            if (method.TypeParameterList is not null)
                CollectTypeNames(method.TypeParameterList, identifiers);
            foreach (var clause in method.ConstraintClauses)
                CollectTypeNames(clause, identifiers);
        }
        foreach (var name in identifiers.Order(StringComparer.Ordinal))
        {
            var resolved = ResolveTypeName(item, name, declarations);
            if (resolved is not null)
                yield return resolved;
        }

        foreach (var attribute in item.Declaration.AttributeLists.SelectMany(list => list.Attributes)
                     .Where(attribute => AttributeName(attribute) == "Union"))
        {
            var type = attribute.DescendantNodes().OfType<TypeOfExpressionSyntax>().Single().Type;
            var name = type.DescendantNodesAndSelf().OfType<SimpleNameSyntax>()
                .Last().Identifier.ValueText;
            var resolved = ResolveTypeName(item, name, declarations, preferImports: true)
                           ?? throw new InvalidDataException(
                               $"Union target {name} on {item.FullName} cannot be resolved.");
            yield return resolved;
        }
    }

    private static string? ResolveTypeName(
        StubDeclaration item,
        string name,
        IReadOnlyDictionary<string, StubDeclaration> declarations,
        bool preferImports = false)
    {
        string? ResolveFromCurrentNamespace()
        {
            var currentNamespace = item.Namespace;
            while (!string.IsNullOrEmpty(currentNamespace))
            {
                var candidate = $"{currentNamespace}.{name}";
                if (declarations.ContainsKey(candidate))
                    return candidate;
                var separator = currentNamespace.LastIndexOf('.');
                currentNamespace = separator < 0 ? string.Empty : currentNamespace[..separator];
            }
            return null;
        }

        string? ResolveFromImports()
        {
            string? resolved = null;
            foreach (var usingDirective in item.Usings)
            {
                if (usingDirective.Alias is not null || usingDirective.StaticKeyword != default)
                    continue;
                var candidate = $"{usingDirective.Name}.{name}";
                if (!declarations.ContainsKey(candidate))
                    continue;
                if (resolved is not null && resolved != candidate)
                    return null;
                resolved = candidate;
            }
            return resolved;
        }

        var scoped = preferImports
            ? ResolveFromImports() ?? ResolveFromCurrentNamespace()
            : ResolveFromCurrentNamespace() ?? ResolveFromImports();
        if (scoped is not null)
            return scoped;

        var candidates = declarations.Keys
            .Where(key => key.EndsWith($".{name}", StringComparison.Ordinal) || key == name)
            .Take(2)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static void CollectTypeNames(SyntaxNode node, HashSet<string> names)
    {
        foreach (var identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var name = identifier.Identifier.ValueText;
            if (!IsFrameworkType(name) && name is not "Key" and not "Union" and not "MessagePackObject")
                names.Add(name);
        }
        foreach (var generic in node.DescendantNodesAndSelf().OfType<GenericNameSyntax>())
        {
            var name = generic.Identifier.ValueText;
            if (!IsFrameworkType(name))
                names.Add(name);
        }
    }

    private static bool IsFrameworkType(string name) => name is
        "string" or "String" or "bool" or "Boolean" or "byte" or "sbyte" or
        "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
        "float" or "double" or "decimal" or "char" or "object" or "Object" or
        "DateTime" or "DateTimeOffset" or "TimeSpan" or "Guid" or "Nullable" or
        "Array" or "List" or "Dictionary" or "IEnumerable" or "IReadOnlyList" or "typeof";

    private static void ValidateDeclaration(StubDeclaration item)
    {
        if (item.HasConflictingDefinition)
            throw new InvalidDataException($"Conflicting stub declarations: {item.FullName}");

        var keyValues = Members(item.Declaration)
            .SelectMany(MemberAttributes)
            .Where(attribute => AttributeName(attribute) == "Key")
            .Select(attribute => attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString()
                                 ?? throw new InvalidDataException($"Missing Key value on {item.FullName}."))
            .ToArray();
        if (keyValues.Length != keyValues.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException($"Duplicate MessagePack Key on {item.FullName}.");

        foreach (var attribute in item.Declaration.AttributeLists.SelectMany(list => list.Attributes))
        {
            var name = AttributeName(attribute);
            if (NonWireAttributes.Contains(name))
                continue;
            if (!WireAttributes.Contains(name))
                throw new InvalidDataException($"Unsupported type attribute {name} on {item.FullName}.");
        }
    }

    private static string RenderSource(IEnumerable<StubDeclaration> declarations)
    {
        var builder = new StringBuilder();
        var items = declarations.OrderBy(item => item.FullName, StringComparer.Ordinal).ToArray();
        var declarationLookup = items.ToDictionary(item => item.FullName, StringComparer.Ordinal);
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable disable");
        builder.AppendLine("using MessagePack;");
        foreach (var item in items)
        {
            builder.AppendLine();
            builder.Append("namespace ").Append(item.Namespace).AppendLine();
            builder.AppendLine("{");
            foreach (var usingDirective in item.Usings
                         .Where(usingDirective => ShouldRenderUsing(usingDirective, items))
                         .OrderBy(value => value.ToString(), StringComparer.Ordinal))
            {
                builder.Append("    ").AppendLine(RenderUsing(usingDirective));
            }
            if (item.Usings.Any(usingDirective => ShouldRenderUsing(usingDirective, items)))
                builder.AppendLine();
            RenderDeclaration(builder, item, declarationLookup, "    ");
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static bool ShouldRenderUsing(
        UsingDirectiveSyntax usingDirective,
        IReadOnlyCollection<StubDeclaration> declarations)
    {
        var name = usingDirective.Name?.ToString();
        if (name is null or "Il2CppDummyDll" or "MessagePack" or "MasterMemory" ||
            name.StartsWith("MasterMemory.", StringComparison.Ordinal))
        {
            return false;
        }

        return name == "System" || name.StartsWith("System.", StringComparison.Ordinal) ||
               declarations.Any(item => item.Namespace == name ||
                                        item.Namespace.StartsWith(name + ".", StringComparison.Ordinal));
    }

    private static string RenderUsing(UsingDirectiveSyntax usingDirective)
    {
        var alias = usingDirective.Alias is null ? string.Empty : $"{usingDirective.Alias} ";
        var staticModifier = usingDirective.StaticKeyword == default ? string.Empty : "static ";
        return $"using {alias}{staticModifier}global::{usingDirective.Name};";
    }

    private static void RenderDeclaration(
        StringBuilder builder,
        StubDeclaration item,
        IReadOnlyDictionary<string, StubDeclaration> declarations,
        string indent)
    {
        RenderAttributes(builder, item.Declaration.AttributeLists, indent, item, declarations);
        var modifiers = item.Declaration.Modifiers
            .Where(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) ||
                               modifier.IsKind(SyntaxKind.SealedKeyword) ||
                               modifier.IsKind(SyntaxKind.AbstractKeyword))
            .Select(modifier => modifier.ValueText)
            .ToList();
        if (!modifiers.Contains("public", StringComparer.Ordinal))
            modifiers.Insert(0, "public");
        if (item.Declaration is ClassDeclarationSyntax or StructDeclarationSyntax)
            modifiers.Add("partial");
        var kind = item.Declaration switch
        {
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            EnumDeclarationSyntax => "enum",
            _ => throw new InvalidDataException($"Unsupported declaration kind: {item.FullName}")
        };
        builder.Append(indent).Append(string.Join(' ', modifiers)).Append(' ').Append(kind).Append(' ')
            .Append(item.Declaration.Identifier.ValueText);
        if (item.Declaration is TypeDeclarationSyntax genericType && genericType.TypeParameterList is not null)
            builder.Append(genericType.TypeParameterList.WithoutTrivia());
        if (item.Declaration is EnumDeclarationSyntax enumDeclaration && enumDeclaration.BaseList is not null)
            builder.Append(' ').Append(enumDeclaration.BaseList);
        else if (item.Declaration.BaseList is not null)
            builder.Append(' ').Append(item.Declaration.BaseList);
        if (item.Declaration is TypeDeclarationSyntax constrainedType)
        {
            foreach (var clause in constrainedType.ConstraintClauses)
                builder.Append(' ').Append(clause.WithoutTrivia());
        }
        builder.AppendLine();
        builder.Append(indent).AppendLine("{");

        if (item.Declaration is EnumDeclarationSyntax enumType)
        {
            foreach (var member in enumType.Members)
            {
                builder.Append(indent).Append("    ").Append(member.Identifier.ValueText);
                if (member.EqualsValue is not null)
                    builder.Append(" = ").Append(member.EqualsValue.Value);
                builder.AppendLine(",");
            }
        }
        else
        {
            foreach (var property in Members(item.Declaration).OfType<PropertyDeclarationSyntax>())
            {
                RenderAttributes(builder, property.AttributeLists, indent + "    ");
                builder.Append(indent).Append("    public ").Append(property.Type).Append(' ')
                    .Append(property.Identifier.ValueText).Append(" { ");
                var accessors = property.AccessorList?.Accessors
                    .Select(accessor =>
                    {
                        var accessorModifiers = string.Join(' ',
                            accessor.Modifiers.Select(modifier => modifier.ValueText));
                        return string.IsNullOrEmpty(accessorModifiers)
                            ? accessor.Keyword.ValueText + ";"
                            : accessorModifiers + " " + accessor.Keyword.ValueText + ";";
                    })
                    .ToArray() ?? [];
                builder.Append(string.Join(' ', accessors)).AppendLine(" }");
                builder.AppendLine();
            }
            foreach (var field in Members(item.Declaration).OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(SyntaxKind.PublicKeyword))
                    continue;
                RenderAttributes(builder, field.AttributeLists, indent + "    ");
                foreach (var variable in field.Declaration.Variables)
                    builder.Append(indent).Append("    public ").Append(field.Declaration.Type).Append(' ')
                        .Append(variable.Identifier.ValueText).AppendLine(";");
                builder.AppendLine();
            }
            foreach (var method in Members(item.Declaration).OfType<MethodDeclarationSyntax>())
            {
                if (item.Declaration is not InterfaceDeclarationSyntax &&
                    !method.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    continue;
                }
                RenderMethod(builder, method, item.Declaration is InterfaceDeclarationSyntax, indent + "    ");
                builder.AppendLine();
            }
        }
        builder.Append(indent).AppendLine("}");
    }

    private static void RenderMethod(
        StringBuilder builder,
        MethodDeclarationSyntax method,
        bool isInterface,
        string indent)
    {
        builder.Append(indent);
        if (!isInterface)
        {
            var modifiers = method.Modifiers
                .Where(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) ||
                                   modifier.IsKind(SyntaxKind.StaticKeyword))
                .Select(modifier => modifier.ValueText);
            builder.Append(string.Join(' ', modifiers)).Append(' ');
        }
        builder.Append(method.ReturnType.WithoutTrivia()).Append(' ')
            .Append(method.Identifier.ValueText);
        if (method.TypeParameterList is not null)
            builder.Append(method.TypeParameterList.WithoutTrivia());
        builder.Append(method.ParameterList.WithoutTrivia());
        foreach (var clause in method.ConstraintClauses)
            builder.Append(' ').Append(clause.WithoutTrivia());

        if (isInterface || method.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            builder.AppendLine(";");
            return;
        }

        builder.Append(method.ReturnType is PredefinedTypeSyntax predefined &&
                       predefined.Keyword.IsKind(SyntaxKind.VoidKeyword)
            ? " { }"
            : " => default;");
        builder.AppendLine();
    }

    private static void RenderAttributes(
        StringBuilder builder,
        SyntaxList<AttributeListSyntax> lists,
        string indent,
        StubDeclaration? item = null,
        IReadOnlyDictionary<string, StubDeclaration>? declarations = null)
    {
        foreach (var attribute in lists.SelectMany(list => list.Attributes))
        {
            if (!WireAttributes.Contains(AttributeName(attribute)))
                continue;
            var rendered = attribute;
            if (AttributeName(attribute) == "Union" && item is not null && declarations is not null)
            {
                var typeOf = attribute.DescendantNodes().OfType<TypeOfExpressionSyntax>().Single();
                var name = typeOf.Type.DescendantNodesAndSelf().OfType<SimpleNameSyntax>()
                    .Last().Identifier.ValueText;
                var resolved = ResolveTypeName(item, name, declarations, preferImports: true)
                               ?? throw new InvalidDataException(
                                   $"Union target {name} on {item.FullName} cannot be rendered.");
                rendered = attribute.ReplaceNode(
                    typeOf.Type,
                    SyntaxFactory.ParseTypeName($"global::{resolved}"));
            }
            builder.Append(indent).Append('[').Append(rendered.WithoutTrivia()).AppendLine("]");
        }
    }

    private static IEnumerable<AttributeSyntax> MemberAttributes(MemberDeclarationSyntax member) =>
        member.AttributeLists.SelectMany(list => list.Attributes);

    private static SyntaxList<MemberDeclarationSyntax> Members(BaseTypeDeclarationSyntax declaration) =>
        declaration switch
        {
            TypeDeclarationSyntax type => type.Members,
            EnumDeclarationSyntax => default,
            _ => throw new InvalidDataException(
                $"Unsupported declaration kind: {declaration.Identifier.ValueText}")
        };

    private static string AttributeName(AttributeSyntax attribute) =>
        attribute.Name.ToString().Split('.').Last().Replace("Attribute", string.Empty, StringComparison.Ordinal);

    private static string ShapeHash(BaseTypeDeclarationSyntax declaration)
    {
        var builder = new StringBuilder(declaration.Kind().ToString())
            .Append('|').Append(declaration.Identifier.ValueText)
            .Append('|').Append(declaration.BaseList?.WithoutTrivia());
        if (declaration is TypeDeclarationSyntax typeDeclaration)
        {
            builder.Append('|').Append(typeDeclaration.TypeParameterList?.WithoutTrivia());
            foreach (var clause in typeDeclaration.ConstraintClauses)
                builder.Append('|').Append(clause.WithoutTrivia());
        }
        foreach (var attribute in declaration.AttributeLists.SelectMany(list => list.Attributes)
                     .Where(attribute => WireAttributes.Contains(AttributeName(attribute))))
            builder.Append('|').Append(attribute.WithoutTrivia());
        foreach (var member in Members(declaration))
        {
            foreach (var attribute in MemberAttributes(member)
                         .Where(attribute => WireAttributes.Contains(AttributeName(attribute))))
                builder.Append('|').Append(attribute.WithoutTrivia());
            if (member is PropertyDeclarationSyntax property)
                builder.Append('|').Append(property.Type.WithoutTrivia()).Append(':').Append(property.Identifier.ValueText);
            if (member is FieldDeclarationSyntax field)
                builder.Append('|').Append(field.Declaration.Type.WithoutTrivia()).Append(':')
                    .Append(string.Join(',', field.Declaration.Variables.Select(variable => variable.Identifier.ValueText)));
            if (member is MethodDeclarationSyntax method)
            {
                builder.Append('|').Append(method.ReturnType.WithoutTrivia()).Append(':')
                    .Append(method.Identifier.ValueText)
                    .Append(method.TypeParameterList?.WithoutTrivia())
                    .Append(method.ParameterList.WithoutTrivia());
                foreach (var clause in method.ConstraintClauses)
                    builder.Append(':').Append(clause.WithoutTrivia());
            }
        }
        if (declaration is EnumDeclarationSyntax enumDeclaration)
        {
            foreach (var member in enumDeclaration.Members)
                builder.Append('|').Append(member.Identifier.ValueText).Append('=').Append(member.EqualsValue?.Value);
        }
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void VerifyFile(string path, string expected)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != expected)
            throw new InvalidDataException($"Generated protocol file is stale: {path}");
    }

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed record StubDeclaration(
        string FullName,
        string Namespace,
        string Path,
        BaseTypeDeclarationSyntax Declaration,
        UsingDirectiveSyntax[] Usings,
        bool HasConflictingDefinition);
    private sealed record GeneratedArtifacts(string Source, string LockJson, int TypeCount, int InputCount);
    private sealed record LockInput(string Path, string Sha256);
    private sealed record LockType(string Name, string ShapeSha256);
    private sealed record LockOutput(string Path, string Sha256);
    private sealed record SerializerContract(string[] ResolverOrder, string DefaultCompression, string Evidence);
    private sealed record ProtocolLock(
        string ClientVersion,
        string GeneratorVersion,
        string RootsSha256,
        LockInput[] Inputs,
        LockType[] Types,
        LockOutput Output,
        SerializerContract Serializer);

    private sealed record GeneratorOptions(
        string Command,
        string StubRoot,
        string ClientVersion,
        string RootsFile,
        string OutputDirectory)
    {
        public string OutputFile => Path.Combine(OutputDirectory, "Sirius.Shared.g.cs");
        public string LockFile => Path.Combine(OutputDirectory, "protocol.lock.json");

        public static GeneratorOptions Parse(string[] args)
        {
            if (args.Length == 0 || args[0] is not ("generate" or "verify"))
                throw new ArgumentException("Usage: ProtocolGen <generate|verify> --stub-root <path> [--client-version 2.31.0]");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Invalid argument: {args[index]}");
                values[args[index]] = args[index + 1];
            }
            if (!values.TryGetValue("--stub-root", out var stubRoot))
                throw new ArgumentException("--stub-root is required.");

            var toolRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../"));
            var repositoryRoot = Path.GetFullPath(Path.Combine(toolRoot, "../"));
            var resolvedStub = Path.GetFullPath(Path.IsPathRooted(stubRoot)
                ? stubRoot
                : Path.Combine(Directory.GetCurrentDirectory(), stubRoot));
            var clientVersion = values.GetValueOrDefault("--client-version", "2.31.0");
            return new GeneratorOptions(
                args[0],
                resolvedStub,
                clientVersion,
                Path.Combine(toolRoot, "protocol-roots.json"),
                Path.Combine(repositoryRoot, "OpenWorldDaiStar.Server", "Generated", "Protocol"));
        }
    }
}
