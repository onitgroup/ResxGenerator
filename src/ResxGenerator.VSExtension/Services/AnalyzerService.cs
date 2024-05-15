using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Helpers;
using ResxGenerator.VSExtension.Infrastructure;

namespace ResxGenerator.VSExtension.Services
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class AnalyzerService : DisposableObject
    {
        private readonly VisualStudioExtensibility _extensibility;
        private OutputWindow? _output;
        private readonly Task _initializationTask; // probably not needed

        public AnalyzerService(VisualStudioExtensibility extensibility)
        {
            this._extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        private async Task WriteToOutputAsync(string message)
        {
            if (_output is not null)
            {
                await _output.Writer.WriteLineAsync(message);
            }
        }

        private static string? GetTextFromArgument(ExpressionSyntax expression)
        {
            // TrimOne " beacause GetText() returns the string with the surrounding ""
            return expression.Kind() switch
            {
                SyntaxKind.StringLiteralExpression => expression.GetText().ToString().TrimOne('"'),
                _ => null,
            };
        }

        public async Task<IEnumerable<ISymbol>> GatherSymbolsAsync(Compilation compilation)
        {
            var symbols = new List<ISymbol>();

            INamedTypeSymbol? type;

            // localizers
            type = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer");
            if (type is not null) // not found. or the symbol it's ambiguous
            {
                symbols.AddRange(type
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(x => x.IsIndexer)); // should be 2, .this[string] and .this[string, params object[]]
            }
            else
            {
                await WriteToOutputAsync("Symbol Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer not found.");
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
                await WriteToOutputAsync("Symbol Microsoft.Extensions.Localization.IStringLocalizer not found.");
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
            else
            {
                await WriteToOutputAsync("Symbol Onit.Infrastructure.AspNetCore.HtmlLocalizerExtensions not found.");
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
            else
            {
                await WriteToOutputAsync("Symbol Onit.Infrastructure.AspNetCore.StringLocalizerExtensions not found.");
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
                await WriteToOutputAsync("Symbol System.ComponentModel.DataAnnotations.DisplayAttribute not found.");
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
                await WriteToOutputAsync("Symbol System.ComponentModel.DataAnnotations.RequiredAttribute not found.");
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
                await WriteToOutputAsync("Symbol System.ComponentModel.DescriptionAttribute not found.");
            }

            return symbols;
        }

        /// <summary>
        /// Finds all symbols's references and search for strings literals parameters
        /// </summary>
        /// <param name="symbols">The symbols to search</param>
        /// <param name="solution">The solution to search in</param>
        /// <returns></returns>
        public async Task<IEnumerable<string>> FindStringsAsync(IEnumerable<ISymbol> symbols, Solution solution)
        {
            var res = new List<string?>();

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

                        var reference = syntaxTree
                            .DescendantNodes()
                            // check if this where can be more robust
                            .Where(x => x.GetLocation().SourceSpan.Start == spanStart)
                            .FirstOrDefault();

                        switch (reference)
                        {
                            case BracketedArgumentListSyntax indexerInvokation:
                                {
                                    foreach (var argument in indexerInvokation.Arguments)
                                    {
                                        res.Add(GetTextFromArgument(argument.Expression));
                                    }
                                }
                                break;

                            case AttributeSyntax attributeSyntax:
                                {
                                    foreach (var argument in attributeSyntax.ArgumentList?.Arguments ?? [])
                                    {
                                        res.Add(GetTextFromArgument(argument.Expression));
                                    }
                                }
                                break;

                            case null: // should NEVER happen
                                await WriteToOutputAsync($"Symbol reference is null {location}");
                                continue;
                            default:
                                continue;
                        }

                        var line = location.Location.GetLineSpan().StartLinePosition.Line;
                        var text = await doc.GetTextAsync();
                        await WriteToOutputAsync($"{doc.FilePath} Line: {line} => {text.Lines[line].ToString().Trim()}");
                    }
                }
            }

            return res
                .Where(x => string.IsNullOrWhiteSpace(x) == false)
                .Select(x => x!)
                .OrderBy(x => x)
                .Distinct();
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}