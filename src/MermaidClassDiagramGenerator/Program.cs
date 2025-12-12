using System.CommandLine;
using DiagramGenerator.ClassGraph;

var outputOption = new Option<FileInfo?>(
    aliases: new[] { "--output", "-o" },
    description: "Output file.",
    getDefaultValue: () => new FileInfo("output.md"));

var nsOption = new Option<IList<string>>(
    aliases: new[] { "--namespace", "-ns" },
    description: "Namespace filter.",
    getDefaultValue: () => new List<string>());

// CHANGED: Input is now a folder path
var inputPathOption = new Option<string>(
        aliases: new[] { "--path", "-p" },
        description: "Path to the folder containing .cs files.")
    { IsRequired = true };

var tnOption = new Option<IList<string>>(
    aliases: new[] { "--type-names", "-t" },
    description: "Specific classes to include.",
    getDefaultValue: () => new List<string>());

var ignoreDependencyOption = new Option<bool>(
    name: "--ignore-dependency",
    description: "If true, skip dependency arrows.");

var rootCommand = new RootCommand("Generate mermaid class-diagram from C# source code.");
rootCommand.AddOption(outputOption);
rootCommand.AddOption(nsOption);
rootCommand.AddOption(inputPathOption);
rootCommand.AddOption(tnOption);
rootCommand.AddOption(ignoreDependencyOption);

rootCommand.SetHandler((output, ns, inputPath, tns, ignoreDep) => { Execute(output!, ns, inputPath, tns, ignoreDep); },
    outputOption, nsOption, inputPathOption, tnOption, ignoreDependencyOption);

return await rootCommand.InvokeAsync(args);

static void Execute(FileInfo outputFile,
    IList<string> nsList,
    string inputPath,
    IList<string> tnList,
    bool ignoreDependency) {
  // 1. Gather all .cs files recursively
  var files = Directory.GetFiles(inputPath, "*.cs", SearchOption.AllDirectories)
      .Where(f => !f.Contains("obj") && !f.Contains("bin")); // Simple exclude

  // 2. Use the new SourceGraphBuilder
  IGraphBuilder builder = new SourceGraphBuilder();
  var graph = builder.Build(files, nsList, tnList, ignoreDependency);

  var generator = new MermaidGenerator();
  var text = generator.Generate(graph);

  File.WriteAllText(outputFile.FullName, text);
  Console.WriteLine($"Diagram generated at: {outputFile.FullName}");
}