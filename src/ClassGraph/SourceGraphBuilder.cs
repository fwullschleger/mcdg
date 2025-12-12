using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagramGenerator.ClassGraph;

public class SourceGraphBuilder : IGraphBuilder
{
    public Graph Build(IEnumerable<string> files, IEnumerable<string> nsList, IEnumerable<string> typenameList, bool inheritanceOnly)
    {
        var graph = new Graph();

        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;

            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // Find all classes, interfaces, records, structs
            var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                // Filter by type name
                if (typenameList.Any() && !typenameList.Contains(typeDecl.Identifier.Text))
                    continue;

                // Filter by Namespace
                var ns = GetNamespace(typeDecl);
                if (nsList.Any() && !nsList.Contains(ns))
                    continue;

                var @class = BuildClassFromSyntax(typeDecl);
                graph.AddClass(@class);
            }
        }

        graph.RebuildRelation(inheritanceOnly);
        return graph;
    }

    private Class BuildClassFromSyntax(TypeDeclarationSyntax typeDecl)
    {
        var c = new Class(typeDecl.Identifier.Text)
        {
            IsInterface = typeDecl is InterfaceDeclarationSyntax
        };

        // Handle Inheritance (Base Class) and Interfaces
        if (typeDecl.BaseList != null)
        {
            foreach (var baseType in typeDecl.BaseList.Types)
            {
                var typeName = baseType.Type.ToString();
                
                // Simple heuristic: If it starts with 'I' and the second letter is uppercase, it's likely an interface.
                // Otherwise, we assume it's the Base Class (if one hasn't been set yet).
                if (c.BaseType == null && (!typeName.StartsWith("I") || (typeName.Length > 1 && char.IsLower(typeName[1]))))
                {
                    c.BaseType = typeName;
                }
                else
                {
                    c.ImplementedInterface.Add(typeName);
                }
            }
        }

        // Parse Properties
        foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (!IsVisible(prop.Modifiers)) continue;

            var p = new Property(prop.Identifier.Text, GetVisibility(prop.Modifiers));
            
            // 1. Set the exact text representation (e.g. "List<TimingDose>?")
            p.Type = prop.Type.ToString();

            // 2. Deep dive into the syntax tree to find dependencies (e.g. "TimingDose")
            ExtractTypeDependencies(prop.Type, p);

            c.AddProperty(p);
        }

        // Parse Methods
        foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            if (!IsVisible(method.Modifiers)) continue;

            var m = new Method(method.Identifier.Text, GetVisibility(method.Modifiers));
            m.Type = method.ReturnType.ToString();

            ExtractTypeDependencies(method.ReturnType, m);

            c.AddMethod(m);
        }

        return c;
    }

    /// <summary>
    /// Recursively unwraps Nullables and Arrays to find Generics or Class Identifiers.
    /// Fills the GenericType and TypeParams fields on the Member object.
    /// </summary>
    private void ExtractTypeDependencies(TypeSyntax typeSyntax, Member member)
    {
        // Case: "int?" or "List<T>?" -> Unwrap the "?"
        if (typeSyntax is NullableTypeSyntax nullable)
        {
            ExtractTypeDependencies(nullable.ElementType, member);
            return;
        }

        // Case: "TimingDose[]" -> Unwrap the "[]"
        if (typeSyntax is ArrayTypeSyntax array)
        {
            ExtractTypeDependencies(array.ElementType, member);
            return;
        }

        // Case: "List<TimingDose>" -> Handle Generic
        if (typeSyntax is GenericNameSyntax generic)
        {
            if (member is Property p) p.GenericType = generic.Identifier.Text;
            if (member is Method m) m.GenericType = generic.Identifier.Text;

            foreach (var arg in generic.TypeArgumentList.Arguments)
            {
                // Add "TimingDose" to the dependency list
                var typeName = arg.ToString();
                if (member is Property pp) pp.TypeParams.Add(typeName);
                if (member is Method mm) mm.TypeParams.Add(typeName);
                
                // Recursion needed if you have complex nested types like Dictionary<string, List<Inner>>
                // But for now, adding the string name is enough for the Graph builder to link it.
            }
            return;
        }

        // Case: "Medication" -> Simple Identifier
        // Explicitly adding this helps if the top level was nullable e.g. "Medication?"
        if (typeSyntax is IdentifierNameSyntax identifier)
        {
             var typeName = identifier.Identifier.Text;
             if (member is Property pp) pp.TypeParams.Add(typeName);
             if (member is Method mm) mm.TypeParams.Add(typeName);
        }
    }

    private string GetNamespace(SyntaxNode node)
    {
        var potentialNamespace = node.Parent;
        while (potentialNamespace != null && 
               !(potentialNamespace is NamespaceDeclarationSyntax) && 
               !(potentialNamespace is FileScopedNamespaceDeclarationSyntax))
        {
            potentialNamespace = potentialNamespace.Parent;
        }

        if (potentialNamespace is BaseNamespaceDeclarationSyntax ns)
        {
            return ns.Name.ToString();
        }
        return string.Empty;
    }

    private bool IsVisible(SyntaxTokenList modifiers)
    {
        // Default to internal/private if no modifier, but we usually want Public properties for diagrams
        return modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private Visibility GetVisibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return Visibility.Public;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return Visibility.Protected;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) return Visibility.Internal;
        return Visibility.Private;
    }
}