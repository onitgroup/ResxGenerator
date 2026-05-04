using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.Shell.FileDialog;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.Query;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Resx;
using System.IO;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Commands
{
    [VisualStudioContribution]
    internal class ExportToExcelCommand(ContextBuilder contextBuilder, IAnalyzerService analyzer, IConfigurationService configuration, IExcelManager excelManager) : Command
    {
        private readonly ContextBuilder _contextBuilder = Requires.NotNull(contextBuilder, nameof(contextBuilder));
        private readonly IAnalyzerService _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
        private readonly IConfigurationService _configuration = Requires.NotNull(configuration, nameof(configuration));
        private readonly IExcelManager _excelManager = Requires.NotNull(excelManager, nameof(excelManager));
        private OutputChannel? _output;

        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.ExportToExcelCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.ExportToExcelCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.OfficeExcel2013, IconSettings.IconAndText),
            EnabledWhen = ActivationConstraint.SolutionState(SolutionState.FullyLoaded)
        };

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _output = await OutputChannelProvider.GetOrCreateAsync(Extensibility);
            await base.InitializeAsync(cancellationToken);
        }

        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                await _output.WriteToOutputAsync("\n=== Starting Excel Export ===");

                var prjCtx = await _contextBuilder.BuildProjectContextAsync(context);
                var roslynCtx = await _contextBuilder.BuildRoslynContextAsync(prjCtx);

                await _output.WriteToOutputAsync("Analyzing source code...");
                var symbols = await _analyzer.GatherTargetSymbolsAsync(roslynCtx.Compilation);
                var resources = await _analyzer.FindStringsAsync(symbols, roslynCtx.Project.Solution, prjCtx.Config.DefaultResourceName);

                await _output.WriteToOutputAsync($"Found {resources.Sum(x => x.Strings.Count())} strings across {resources.Count()} resource types.");

                await _output.WriteToOutputAsync("Scanning .resx files...");
                var allResxEntries = new List<ExcelModel>();

                foreach (var resource in resources)
                {
                    string fullyQualifiedName;
                    string directory;

                    if (resource.Type is null)
                    {
                        // Fallback per stringhe non attribuite a nessun tipo
                        fullyQualifiedName = prjCtx.Config.DefaultResourceName;
                        directory = prjCtx.Directory;
                    }
                    else
                    {
                        var location = await _analyzer.FindResourceTypeLocationAsync(resource.Type, roslynCtx.Project);
                        if (location is null)
                        {
                            continue;
                        }

                        fullyQualifiedName = resource.Type.ToFullyQualifiedName();
                        directory = Path.GetDirectoryName(location.Value.FilePath)!;
                    }

                    // Find all .resx files for this resource
                    var resourceName = resource.Type?.Name ?? prjCtx.Config.DefaultResourceName;
                    var files = Directory.GetFiles(directory, Config.BuildCatchAllResxFileName(resourceName));
                    if (files.Length == 0)
                    {
                        await _output.WriteToOutputAsync($"No .resx files found for {resourceName}");
                    }

                    var entriesByKey = new Dictionary<string, ExcelModel>(StringComparer.OrdinalIgnoreCase);
                    var occurrencesMap = resource.Strings.ToDictionary(x => x.Name, x => x.Occurrences);

                    // update strings in the resx files
                    foreach (var file in files)
                    {
                        var resxManager = ResxManager.OpenOrCreate(file);

                        if (!Config.TryParseCultureFromResxFileName(file, out var culture))
                        {
                            await _output.WriteToOutputAsync($"Unable to parse the culture of file {file}");
                            continue;
                        }

                        foreach (var element in resxManager.EnumerateElements())
                        {
                            if (entriesByKey.TryGetValue(element.Key, out var model))
                            {
                                model.Cultures[culture] = (element.Value, element.Comment);
                                continue;
                            }

                            entriesByKey[element.Key] = new ExcelModel
                            {
                                FullyQualifiedName = fullyQualifiedName,
                                Key = element.Key,
                                Occurrences = occurrencesMap.GetValueOrDefault(element.Key),
                                Cultures = { [culture] = (element.Value, element.Comment) }
                            };
                        }
                    }

                    // insert strings that are not in the resx files
                    foreach (var entry in resource.Strings.Where(x => !entriesByKey.ContainsKey(x.Name)))
                    {
                        entriesByKey[entry.Name] = new ExcelModel
                        {
                            FullyQualifiedName = fullyQualifiedName,
                            Key = entry.Name,
                            Occurrences = entry.Occurrences
                        };
                    }

                    allResxEntries.AddRange(entriesByKey.Values);
                }

                await _output.WriteToOutputAsync($"Total keys found: {allResxEntries.Count}");

                var defaultFileName = $"{prjCtx.Name}_Translations_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                var excelPath = await Extensibility.Shell()
                    .ShowSaveAsFileDialogAsync(new FileDialogOptions
                    {
                        Title = "Save exported excel file",
                        InitialDirectory = prjCtx.Directory,
                        InitialFileName = defaultFileName,
                    }, cancellationToken);

                if (string.IsNullOrWhiteSpace(excelPath))
                {
                    await _output.WriteToOutputAsync("Export cancelled by user.");
                    return;
                }

                using (var stream = File.Open(excelPath, FileMode.OpenOrCreate))
                {
                    _excelManager.Write(stream, allResxEntries);
                }

                await _output.WriteToOutputAsync($"Excel file created: {excelPath}");
                await _output.WriteToOutputAsync("\n=== Command executed successfully ===");
            }
            catch (Exception e)
            {
                await Extensibility
                    .Shell()
                    .ShowPromptAsync($"Error during export: {e.Message}", PromptOptions.OK, cancellationToken);
            }
        }
    }
}