using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.Query;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Resx;
using ResxGenerator.VSExtension.Services;
using System.Globalization;
using System.IO;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Commands
{
    [VisualStudioContribution]
    internal class ExportToExcelCommand(AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider, IAnalyzerService analyzer, ConfigService config) : Command
    {
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
        private readonly IAnalyzerService _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
        private readonly ConfigService _config = Requires.NotNull(config, nameof(config));
        private OutputChannel? _output;

        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.ExportToExcelCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.ExportToExcelCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.ExcelWorksheetView, IconSettings.IconAndText),
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
                await _output.WriteToOutputAsync("=== Starting Excel Export ===");

                var componentModel = await _componentModelProvider.GetServiceAsync() as IComponentModel
                    ?? throw new InvalidOperationException("Unable to get the MEF service.");

                var workspace = componentModel.GetService<VisualStudioWorkspace>();

                var projectSnapshot = await context.GetActiveProjectAsync(x => x.With(p => new
                {
                    p.Name,
                    p.Path,
                    p.TypeGuid
                }), cancellationToken) ?? throw new InvalidOperationException("No active project found.");

                if (_config.Exists(projectSnapshot) == false)
                {
                    await _config.AddDefaultConfigFileAsync(projectSnapshot);
                    throw new InvalidOperationException("No configuration file was found, a new one was created.");
                }

                var config = await _config.GetAsync(projectSnapshot);

                var project = workspace.CurrentSolution.Projects
                    .Where(x => x.FilePath == projectSnapshot.Path)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("Unable to load current project.");

                var projectDir = Path.GetDirectoryName(project.FilePath)!;
                var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new InvalidOperationException("Unable to get the project compilation.");

                // Step 1: Gather symbols and find all strings in code
                await _output.WriteToOutputAsync("Analyzing source code...");
                var symbols = await _analyzer.GatherTargetSymbolsAsync(compilation);
                var stringsByResource = await _analyzer.FindStringsAsync(symbols, project.Solution, config.ResourceName);

                await _output.WriteToOutputAsync($"Found {stringsByResource.Sum(x => x.Value.Count)} strings across {stringsByResource.Count} resource types.");

                // Step 2: Find all .resx files for each resource type
                await _output.WriteToOutputAsync("Scanning .resx files...");
                var allResxEntries = new List<ResxEntry>();

                foreach (var (resourceType, _) in stringsByResource)
                {
                    var resxLocation = await _analyzer.GetResourceTypeDirectoryAsync(resourceType, project);
                    if (resxLocation is null) continue;

                    var (resxDirectory, _) = resxLocation.Value;

                    // Find all .resx files for this resource type
                    var resxFiles = Directory.GetFiles(resxDirectory, $"{resourceType}.*.resx");

                    if (resxFiles.Length == 0)
                    {
                        await _output.WriteToOutputAsync($"No .resx files found for {resourceType}");
                        continue;
                    }

                    // Group by key across all language files
                    var entriesByKey = new Dictionary<string, ResxEntry>(StringComparer.OrdinalIgnoreCase);

                    foreach (var resxFile in resxFiles)
                    {
                        var resxManager = new ResxManager(resxFile);
                        var entries = resxManager.EnumerateElements();

                        // Extract language from filename (e.g., "SharedResource.it-IT.resx" -> "it-IT")
                        var fileName = Path.GetFileNameWithoutExtension(resxFile); // "SharedResource.it-IT"
                        var parts = fileName.Split('.');
                        var language = parts.Length > 1 ? parts[^1] : "default";

                        await _output.WriteToOutputAsync($"  Reading {Path.GetFileName(resxFile)} ({entries.Count} entries)");

                        foreach (var (key, value, comment) in entries)
                        {
                            if (!entriesByKey.ContainsKey(key))
                            {
                                entriesByKey[key] = new ResxEntry
                                {
                                    ResourcePath = Path.GetRelativePath(projectDir, resxDirectory),
                                    Key = key,
                                    Occurrences = occurrenceCounts.GetValueOrDefault((resourceType, key), 0)
                                };
                            }

                            entriesByKey[key].Languages[language] = (value, comment);
                        }
                    }

                    allResxEntries.AddRange(entriesByKey.Values);
                }

                await _output.WriteToOutputAsync($"Total unique keys found: {allResxEntries.Count}");

                // Step 3: Generate Excel file
                await _output.WriteToOutputAsync("Generating Excel file...");

                var excelPath = Path.Combine(projectDir, $"{projectSnapshot.Name}_Translations_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                await GenerateExcelFileAsync(allResxEntries, config.Cultures.ToList(), excelPath);

                await _output.WriteToOutputAsync($"Excel file created: {excelPath}");
                await _output.WriteToOutputAsync("=== Excel Export Complete ===");

                await Extensibility
                    .Shell()
                    .ShowPromptAsync($"Excel export completed successfully!\n\n{excelPath}", PromptOptions.OK, cancellationToken);
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