using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Helpers;
using ResxGenerator.VSExtension.Infrastructure;
using System.IO;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Services
{
    public interface IAnalyzerService
    {
        /// <summary>
        /// Tries to gathers the set of target symbols from the specified compilation.
        /// </summary>
        /// <param name="compilation">The compilation from which to collect symbols. Must not be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of symbols
        /// identified as targets within the compilation. The collection is empty if no target symbols are found.</returns>
        Task<IEnumerable<ISymbol>> GatherTargetSymbolsAsync(Compilation compilation);

        /// <summary>
        /// Finds all symbols's references and search for strings literals parameters
        /// Groups results by resource type (generic type argument of IStringLocalizer<T> or attribute ResourceType)
        /// </summary>
        /// <param name="symbols">The symbols to search</param>
        /// <param name="solution">The solution to search in</param>
        /// <returns>Dictionary mapping resource type names to their strings</returns>
        Task<IEnumerable<ResourceTypeInfo>> FindStringsAsync(IEnumerable<ISymbol> symbols, Solution solution, string defaultResourceType);

        /// <summary>
        /// Gets the directory path where resx files should be created for a given resource type
        /// </summary>
        /// <param name="resourceTypeName"></param>
        /// <param name="currentProject"></param>
        /// <returns></returns>
        Task<(string DirectoryPath, Project Project)?> GetResourceTypeDirectoryAsync(string resourceTypeName, Project currentProject);
    }

    public class AnalyzerService : DisposableObject, IAnalyzerService
    {
        private readonly VisualStudioExtensibility _extensibility;
        private OutputChannel? _output;
        private readonly Task _initializationTask; // probably not needed

        public AnalyzerService(VisualStudioExtensibility extensibility)
        {
            _extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await OutputChannelProvider.GetOrCreateAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        private static string? ParseText(ExpressionSyntax expression)
        {
            return expression.Kind() switch
            {
                // TrimOne " because GetText() returns the string with the surrounding ""
                SyntaxKind.StringLiteralExpression => expression.GetText().ToString().TrimOne('"'),
#if DEBUG
                SyntaxKind.TypeOfExpression => null,
                _ => throw new NotImplementedException(),
#else
                _ => null,
#endif
            };
        }

        /// <summary>
        /// Extracts the generic type argument from IStringLocalizer<T> or IHtmlLocalizer<T>
        /// </summary>
        /// <param name="node">The syntax node MUST containing the indexer call</param>
        /// <param name="semanticModel">The semantic model for symbol resolution</param>
        /// <returns>The name of the resource type (e.g., "SharedResource") or null if not found</returns>
        private static string? FindResourceType(BracketedArgumentListSyntax node, SemanticModel semanticModel)
        {
            // Find the member access or identifier that contains the indexer
            var memberAccess = node.AncestorsAndSelf()
                .OfType<ElementAccessExpressionSyntax>()
                .FirstOrDefault();

            if (memberAccess?.Expression is null)
            {
                return null;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
            var symbol = symbolInfo.Symbol;

            if (symbol is null)
            {
                return null;
            }

            // Get the type of the variable (e.g., IStringLocalizer<SharedResource>)
            var typeSymbol = symbol switch
            {
                ILocalSymbol localSymbol => localSymbol.Type,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                _ => null
            };

            // not generic and not handled
            if (typeSymbol is not INamedTypeSymbol namedType || !namedType.IsGenericType)
            {
                return null;
            }

            var typeArgument = namedType.TypeArguments.FirstOrDefault();
            return typeArgument?.Name;
        }

        /// <summary>
        /// Extracts the resource type from attribute arguments (e.g., ErrorMessageResourceType = typeof(Resources)), if present
        /// </summary>
        /// <param name="attributeSyntax">The attribute syntax node</param>
        /// <param name="semanticModel">The semantic model for symbol resolution</param>
        /// <returns>The name of the resource type or null if not found</returns>
        private static string? FindResourceType(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
        {
            if (attributeSyntax.ArgumentList is null)
            {
                return null;
            }

            // Look for type arguments like ErrorMessageResourceType = typeof(MyResource) or ResourceType = typeof(MyResource)
            var resourceTypeArg = attributeSyntax.ArgumentList.Arguments
                .Where(x => x.NameEquals?.Name.Identifier.Text == "ErrorMessageResourceType" ||
                            x.NameEquals?.Name.Identifier.Text == "ResourceType")
                .FirstOrDefault();

            if (resourceTypeArg?.Expression is not TypeOfExpressionSyntax typeOfExpression)
            {
                return null;
            }

            var typeInfo = semanticModel.GetTypeInfo(typeOfExpression.Type);
            return typeInfo.Type?.Name;
        }

        /// <summary>
        /// Finds the source file location for a given type name across the solution
        /// </summary>
        /// <param name="typeName">The type name to search for (e.g., "SharedResource")</param>
        /// <param name="project">The projects which contains the file</param>
        /// <returns>Tuple of (file path, project) or null if not found</returns>
        private async Task<(string FilePath, Project Project)?> FindResourceTypeLocationAsync(string typeName, Project project)
        {
            // Search in the current project
            var compilation = await project.GetCompilationAsync();
            if (compilation is not null)
            {
                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol is null)
                {
                    // Try searching by name in all types (in case of namespace differences)
                    var allTypes = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type);
                    typeSymbol = allTypes.OfType<INamedTypeSymbol>().FirstOrDefault(); // take the first
                }

                if (typeSymbol is not null && typeSymbol.Locations.FirstOrDefault() is Location location && location.IsInSource)
                {
                    return (location.SourceTree.FilePath, project);
                }
            }

            // If not found in current project, search in referenced projects
            var referencedProjects = project.ProjectReferences
                .Select(x => project.Solution.GetProject(x.ProjectId))
                .Where(x => x is not null)
                .Cast<Project>();

            foreach (var referencedProject in referencedProjects)
            {
                var refCompilation = await referencedProject.GetCompilationAsync();
                if (refCompilation is null)
                {
                    continue;
                }

                var typeSymbol = refCompilation.GetTypeByMetadataName(typeName);
                if (typeSymbol is null)
                {
                    var allTypes = refCompilation.GetSymbolsWithName(typeName, SymbolFilter.Type);
                    typeSymbol = allTypes.OfType<INamedTypeSymbol>().FirstOrDefault();
                }

                if (typeSymbol is not null && typeSymbol.Locations.FirstOrDefault() is Location location && location.IsInSource)
                {
                    return (location.SourceTree.FilePath, referencedProject);
                }
            }

            return null; // external assembly
        }

        // <inheritdoc />
        public async Task<IEnumerable<ISymbol>> GatherTargetSymbolsAsync(Compilation compilation)
        {
            List<ISymbol> symbols = [];

            INamedTypeSymbol? type;

            // localizers
            type = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer");
            if (type is not null) // not found or the symbol it's ambiguous
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(x => x.IsIndexer)); // should be 2, .this[string] and .this[string, params object[]]
            }
            else
            {
                await _output.WriteToOutputAsync("Symbol Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer not found.");
            }

            type = compilation.GetTypeByMetadataName("Microsoft.Extensions.Localization.IStringLocalizer");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(x => x.IsIndexer)); // should be 2, .this[string] and .this[string, params object[]]
            }
            else
            {
                await _output.WriteToOutputAsync("Symbol Microsoft.Extensions.Localization.IStringLocalizer not found.");
            }

            // Onit extensions
            // if the class name or assembly name changes this breaks
            type = compilation.GetTypeByMetadataName("Onit.Infrastructure.AspNetCore.HtmlLocalizerExtensions");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsExtensionMethod &&
                                x.Parameters.FirstOrDefault() is IParameterSymbol method &&
                                method.Type.MetadataName == "IHtmlLocalizer"));
            }

            type = compilation.GetTypeByMetadataName("Onit.Infrastructure.AspNetCore.StringLocalizerExtensions");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsExtensionMethod &&
                                x.Parameters.FirstOrDefault() is IParameterSymbol method &&
                                method.Type.MetadataName == "IStringLocalizer"));
            }

            // Onit duplicates extensions for common library
            type = compilation.GetTypeByMetadataName("LocalizerExtensions.HtmlLocalizerExtensions");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsExtensionMethod &&
                                x.Parameters.FirstOrDefault() is IParameterSymbol method &&
                                method.Type.MetadataName == "IHtmlLocalizer"));
            }

            type = compilation.GetTypeByMetadataName("LocalizerExtensions.StringLocalizerExtensions");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsExtensionMethod &&
                                x.Parameters.FirstOrDefault() is IParameterSymbol method &&
                                method.Type.MetadataName == "IStringLocalizer"));
            }

            // attributes
            type = compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.DisplayAttribute");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.MethodKind == MethodKind.Constructor));
            }
            else
            {
                await _output.WriteToOutputAsync("Symbol System.ComponentModel.DataAnnotations.DisplayAttribute not found.");
            }

            type = compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.RequiredAttribute");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.MethodKind == MethodKind.Constructor));
            }
            else
            {
                await _output.WriteToOutputAsync("Symbol System.ComponentModel.DataAnnotations.RequiredAttribute not found.");
            }

            type = compilation.GetTypeByMetadataName("System.ComponentModel.DescriptionAttribute");
            if (type is not null)
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(x => x.MethodKind == MethodKind.Constructor));
            }
            else
            {
                await _output.WriteToOutputAsync("Symbol System.ComponentModel.DescriptionAttribute not found.");
            }

            return symbols;
        }

        // <inheritdoc />
        public async Task<IEnumerable<ResourceTypeInfo>> FindStringsAsync(IEnumerable<ISymbol> symbols, Solution solution, string defaultResourceType)
        {
            // Dictionary: ResourceType -> List of strings
            var resultsByResource = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols)
            {
                var results = await SymbolFinder.FindReferencesAsync(symbol, solution);

                foreach (var result in results)
                {
                    foreach (var location in result.Locations)
                    {
                        int spanStart = location.Location.SourceSpan.Start;
                        var doc = location.Document;

                        var syntaxTree = await doc.GetSyntaxRootAsync();
                        if (syntaxTree is null) continue;

                        var semanticModel = await doc.GetSemanticModelAsync();
                        if (semanticModel is null) continue;

                        var reference = syntaxTree
                            .DescendantNodes()
                            .Where(x => x.GetLocation().SourceSpan.Start == spanStart)
                            .FirstOrDefault();

                        string? resourceType = null;
                        List<string?> stringsToAdd = new();

                        switch (reference)
                        {
                            case BracketedArgumentListSyntax indexerInvokation:
                                {
                                    // Extract resource type from the variable
                                    resourceType = FindResourceType(indexerInvokation, semanticModel);

                                    foreach (var argument in indexerInvokation.Arguments)
                                    {
                                        stringsToAdd.Add(ParseText(argument.Expression));
                                    }
                                }
                                break;

                            case AttributeSyntax attributeSyntax:
                                {
                                    // Try to get resource type from the attribute itself
                                    resourceType = FindResourceType(attributeSyntax, semanticModel);

                                    // If no resource type specified, use default

                                    if (resourceType is null)
                                    {
                                        resourceType ??= defaultResourceType;
                                    }

                                    foreach (var argument in attributeSyntax.ArgumentList?.Arguments ?? [])
                                    {
                                        stringsToAdd.Add(ParseText(argument.Expression));
                                    }
                                }
                                break;

                            case null:
                                await _output.WriteToOutputAsync($"Symbol reference is null {location}");
                                continue;
                            default:
                                continue;
                        }

                        if (!stringsToAdd.Where(x => x is not null).Any())
                        {
                            continue;
                        }

                        // Use default if we couldn't determine the resource type
                        if (resourceType is null)
                        {
                            resourceType ??= defaultResourceType;
                        }

                        // Add to the dictionary
                        if (!resultsByResource.ContainsKey(resourceType))
                        {
                            resultsByResource[resourceType] = [];
                        }

                        resultsByResource[resourceType].AddRange(stringsToAdd);

                        var line = location.Location.GetLineSpan().StartLinePosition.Line;
                        var docText = await doc.GetTextAsync();
                        await _output.WriteToOutputAsync($"[{resourceType}] {doc.FilePath} Line: {line} => {docText.Lines[line].ToString().Trim()}");
                    }
                }
            }

            // Clean up and return: remove nulls, duplicates, and sort
            List<ResourceTypeInfo> res = [];
            foreach (var source in resultsByResource)
            {
                var strings = source.Value
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x.Key)
                    .Select(x => new ResourceStringInfo
                    {
                        Name = x.Key,
                        Occurrences = x.Count()
                    })
                    .ToList();

                if (strings.Any())
                {
                    res.Add(new ResourceTypeInfo
                    {
                        Name = source.Key,
                        Strings = strings
                    });
                }
            }

            return res;
        }

        // <inheritdoc />
        public async Task<(string DirectoryPath, Project Project)?> GetResourceTypeDirectoryAsync(string resourceTypeName, Project currentProject)
        {
            var location = await FindResourceTypeLocationAsync(resourceTypeName, currentProject);

            if (location is null)
            {
                await _output.WriteToOutputAsync($"Resource type '{resourceTypeName}' not found in current project or in the referenced projects, skipping.");
                return null;
            }

            var directory = Path.GetDirectoryName(location.Value.FilePath);
            if (string.IsNullOrEmpty(directory))
            {
                await _output.WriteToOutputAsync($"Unable to determine directory for '{resourceTypeName}'. Skipping.");
                return null;
            }

            await _output.WriteToOutputAsync($"Resource type '{resourceTypeName}' found at: {location.Value.FilePath}");
            return (directory, location.Value.Project);
        }
    }

    public class ResourceTypeInfo
    {
        public string Name { get; set; } = string.Empty;

        public IEnumerable<ResourceStringInfo> Strings { get; set; } = [];
    }

    public class ResourceStringInfo
    {
        public string Name { get; set; } = string.Empty;

        public int Occurrences { get; set; }
    }
}