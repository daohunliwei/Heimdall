using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Heimdall.Infrastructure.AstAnalysis;

public class RoslynCSharpAnalyzer : IAstAnalyzer
{
    public string Language => "C#";

    public bool CanAnalyze(string fileExtension) =>
        fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);

    public async Task<AstFileResult> AnalyzeAsync(string filePath, string source, CancellationToken ct = default)
    {
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct);

        var symbols = new List<AstSymbol>();
        var callEdges = new List<AstCallEdge>();
        var chunks = new List<SourceChunk>();
        var designHints = new List<string>();

        var compilation = CSharpCompilation.Create("Analysis")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(tree);
        var semanticModel = compilation.GetSemanticModel(tree);

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = ExtractTypeSymbol(typeDecl, filePath, semanticModel);
            symbols.Add(symbol);

            chunks.Add(new SourceChunk(
                typeDecl.SpanStart > 0 ? source[..typeDecl.SpanStart].Count(c => c == '\n') + 1 : 1,
                source[..typeDecl.Span.End].Count(c => c == '\n') + 1,
                $"class {typeDecl.Identifier.Text}",
                typeDecl.ToFullString()
            ));

            // Design pattern detection from AST structure
            DetectPatterns(typeDecl, semanticModel, designHints);

            // Extract methods
            foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>()
                         .Where(m => m.Parent == typeDecl || (m.Parent is TypeDeclarationSyntax p && p == typeDecl)))
            {
                var methodSymbol = ExtractMethodSymbol(method, symbol.Name, filePath);
                symbols.Add(methodSymbol);

                chunks.Add(new SourceChunk(
                    source[..method.SpanStart].Count(c => c == '\n') + 1,
                    source[..method.Span.End].Count(c => c == '\n') + 1,
                    $"{symbol.Name}.{method.Identifier.Text}",
                    method.ToFullString()
                ));

                // Build call edges from method invocations
                ExtractCallEdges(method, symbol.Name, filePath, semanticModel, callEdges, source);
            }
        }

        return new AstFileResult(filePath, "C#", symbols, callEdges, chunks, designHints);
    }

    private static AstSymbol ExtractTypeSymbol(TypeDeclarationSyntax type, string filePath, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(type);
        var baseTypes = type.BaseList?.Types.Select(t => t.Type.ToString()).ToArray() ?? Array.Empty<string>();
        var modifiers = type.Modifiers.Select(m => m.Text).ToArray();

        return new AstSymbol(
            type.Identifier.Text, type.Keyword.Text,
            type.Identifier.Text, filePath,
            type.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            type.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
            null, modifiers, baseTypes, null);
    }

    private static AstSymbol ExtractMethodSymbol(MethodDeclarationSyntax method, string parentClass, string filePath)
    {
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p =>
            $"{p.Type} {p.Identifier}"));
        var returnType = method.ReturnType.ToString();
        var modifiers = method.Modifiers.Select(m => m.Text).ToArray();

        return new AstSymbol(
            method.Identifier.Text, "method",
            $"{returnType} {method.Identifier.Text}({parameters})", filePath,
            method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            method.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
            parentClass, modifiers, null, null);
    }

    private static void ExtractCallEdges(MethodDeclarationSyntax method, string callerClass,
        string filePath, SemanticModel semanticModel, List<AstCallEdge> edges, string source)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var targetSymbol = symbolInfo.Symbol;
            if (targetSymbol == null) continue;

            var callType = targetSymbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor_call",
                IMethodSymbol m when IsInterfaceMember(m) => "interface_call",
                IMethodSymbol m when m.IsVirtual => "virtual_call",
                _ => "direct_call"
            };

            edges.Add(new AstCallEdge(
                $"{callerClass}.{method.Identifier.Text}",
                filePath,
                targetSymbol.ToDisplayString(),
                targetSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? filePath,
                callType,
                0.98));
        }
    }

    private static bool IsInterfaceMember(IMethodSymbol method) =>
        method.ContainingType?.TypeKind == TypeKind.Interface;

    private static void DetectPatterns(TypeDeclarationSyntax type, SemanticModel semanticModel, List<string> hints)
    {
        var symbol = semanticModel.GetDeclaredSymbol(type);
        if (symbol == null) return;

        // Factory pattern: method returning interface/base type
        var factoryMethods = type.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.ReturnType is IdentifierNameSyntax or GenericNameSyntax &&
                        !string.Equals(m.ReturnType.ToString(), "void", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (type.Identifier.Text.Contains("Factory", StringComparison.OrdinalIgnoreCase) && factoryMethods.Count > 0)
            hints.Add($"Factory: {type.Identifier.Text} creates {factoryMethods.First().ReturnType}");

        // Strategy pattern: interface + multiple implementations + DI injection
        if (symbol.Interfaces.Length >= 1 && type.Identifier.Text.Contains("Strategy", StringComparison.OrdinalIgnoreCase))
            hints.Add($"Strategy: {type.Identifier.Text} implements {string.Join(", ", symbol.Interfaces.Select(i => i.Name))}");

        // Observer pattern: event keyword in type
        if (type.Members.OfType<EventDeclarationSyntax>().Any() || type.Members.OfType<EventFieldDeclarationSyntax>().Any())
            hints.Add($"Observer: {type.Identifier.Text} declares events");

        // Singleton pattern: static instance + private constructor
        var hasPrivateCtor = type.Members.OfType<ConstructorDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)));
        var hasStaticInstance = type.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
                      f.Declaration.Variables.Any(v => v.Identifier.Text is "Instance" or "_instance"));
        if (hasPrivateCtor && hasStaticInstance)
            hints.Add($"Singleton: {type.Identifier.Text}");
    }
}
