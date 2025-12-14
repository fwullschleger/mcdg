namespace DiagramGenerator.ClassGraph;

public class MermaidGenerator : IDiagramGenerator {
  private static string MDFrame =
      @"```mermaid
classDiagram

{0}
{1}
```
";

  private static string ClassFrame =
@"class {0} {{
{1}{2}
}}";

  public string Generate(Graph graph) {
    string allClass = string.Empty;
    foreach (var @class in graph.Classes) {
      var classString = GenerateClass(@class);
      allClass += classString + "\r\n";
    }

    string allRelation = string.Empty;
    foreach (var relation in graph.Relations) {
      var relationString = GenerateRelation(relation);
      allRelation += relationString + "\r\n";
    }

    return string.Format(MDFrame, allClass, allRelation);
  }

  private string GenerateClass(Class @class) {
    var content = string.Empty;

    // Add type annotation (<<interface>>, <<record>>, etc.) as first line inside class block
    var typeAnnotation = GetTypeAnnotation(@class.Kind);
    if (!string.IsNullOrEmpty(typeAnnotation)) {
      content += $"  {typeAnnotation}\r\n";
    }

    // Add properties
    foreach (var property in @class.Properties) {
      content += GenerateClassProperty(@class.Name, property, @class.Kind) + "\r\n";
    }

    // Add methods
    foreach (var method in @class.Methods) {
      content += GenerateClassMethod(@class.Name, method, @class.Kind) + "\r\n";
    }

    return string.Format(ClassFrame, @class.Name, content, string.Empty);
  }

  private string GetTypeAnnotation(TypeKind kind) {
    return kind switch {
      TypeKind.Interface => "<<interface>>",
      TypeKind.Record => "<<record>>",
      TypeKind.Struct => "<<struct>>",
      TypeKind.RecordStruct => "<<record struct>>",
      _ => string.Empty
    };
  }

  private string GenerateClassProperty(string className, Property property, TypeKind typeKind) {
    // Pass the raw Type string (e.g. "List<TimingDose>?")
    var typeString = GetTypeString(property.Type);
    var visibilityNotion = GetVisibilityNotion(property.MemberVisibility);

    // For cleaner Mermaid output, don't prefix with className - just indent
    return $"  {visibilityNotion}{typeString} {property.Name}";
  }

  private string GenerateClassMethod(string className, Method method, TypeKind typeKind) {
    // Pass the raw Type string
    var typeString = GetTypeString(method.Type);
    var visibilityNotion = GetVisibilityNotion(method.MemberVisibility);

    // For cleaner Mermaid output, don't prefix with className - just indent
    return $"  {visibilityNotion}{method.Name}() {typeString}";
  }

  /// <summary>
  /// Converts C# Type string to Mermaid safe string (swapping < > for ~)
  /// </summary>
  private string GetTypeString(string? type) {
    if (string.IsNullOrEmpty(type)) return "void";

    // Mermaid uses ~ for generics, e.g., List~string~
    // It renders "?" correctly as is.
    return type.Replace("<", "~").Replace(">", "~");
  }

  private string GenerateRelation(ClassRelation relation) {
    // For implementation, use the correct Mermaid syntax with "implements" label
    // Interface (To) should be on the left, implementing class (From) on the right
    if (relation.Type == RelationType.Implementation) {
      return $"{relation.To.Name} <|.. {relation.From.Name} : implements";
    }

    var relationNotion = GetRelationNotion(relation.Type);
    return $"{relation.To.Name} {relationNotion} {relation.From.Name}";
  }

  private string GetRelationNotion(RelationType type) {
    switch (type) {
      case RelationType.Inheritance:
        return "<|--";
      case RelationType.Implementation:
        return "..|>"; // Not used directly anymore, handled in GenerateRelation
      case RelationType.Dependency:
        return "<--"; // Defines the arrow direction for dependency
      default:
        return string.Empty;
    }
  }

  private string GetVisibilityNotion(Visibility visibility) {
    switch (visibility) {
      case Visibility.Private: return "-";
      case Visibility.Protected: return "#";
      case Visibility.Public: return "+";
      case Visibility.Internal: return "~";
      default: return string.Empty;
    }
  }
}