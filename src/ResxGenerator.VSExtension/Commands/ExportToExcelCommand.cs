using Microsoft;
using Microsoft.CodeAnalysis;
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
using System.IO;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Commands
{
    [VisualStudioContribution]
    internal class ExportToExcelCommand(AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider, IAnalyzerService analyzer, IConfigurationService configuration, IExcelManager excelManager) : Command
    {
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
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

                if (!_configuration.TryGet(projectSnapshot, out var config))
                {
                    _configuration.AddDefault(projectSnapshot);
                    throw new InvalidOperationException("No configuration file was found, a new one was created, please relaunch the command.");
                }

                var project = workspace.CurrentSolution.Projects
                    .Where(x => x.FilePath == projectSnapshot.Path)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("Unable to load current project.");

                var projectDir = Path.GetDirectoryName(project.FilePath)!;
                var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new InvalidOperationException("Unable to get the project compilation.");

                // Step 1: Gather symbols and find all strings in code
                await _output.WriteToOutputAsync("Analyzing source code...");
                var symbols = await _analyzer.GatherTargetSymbolsAsync(compilation);
                var resourceInfos = await _analyzer.FindStringsAsync(symbols, project.Solution, config.DefaultResourceName);

                await _output.WriteToOutputAsync($"Found {resourceInfos.Sum(x => x.Strings.Count())} strings across {resourceInfos.Count()} resource types.");

                // Step 2: Find all .resx files for each resource type
                await _output.WriteToOutputAsync("Scanning .resx files...");
                var allResxEntries = new List<ExcelModel>();

                foreach (var resourceInfo in resourceInfos)
                {
                    var location = await _analyzer.FindResourceTypeLocationAsync(resourceInfo.Name, project);
                    if (location is null)
                    {
                        continue;
                    }

                    var directory = Path.GetDirectoryName(location.Value.FilePath);

                    // Find all .resx files for this resource type
                    var files = Directory.GetFiles(directory, $"{resourceInfo.Name}.*.resx");
                    if (files.Length == 0)
                    {
                        await _output.WriteToOutputAsync($"No .resx files found for {resourceInfo.Name}");
                    }

                    // Group by key across all language files
                    var entriesByKey = new Dictionary<string, ExcelModel>(StringComparer.OrdinalIgnoreCase);

                    foreach (var file in files)
                    {
                        var resxManager = ResxManager.Load(file);

                        // Extract language from filename (e.g., "SharedResource.it-IT.resx" -> "it-IT")
                        var parts = Path.GetFileNameWithoutExtension(file).Split('.'); // "SharedResource.it-IT"
                        var language = parts.Length > 1
                            ? parts.Last()
                            : string.Empty;

                        if (string.IsNullOrEmpty(language))
                        {
                            await _output.WriteToOutputAsync($"Unable to parse the culture of file {file}");
                        }

                        foreach (var element in resxManager.EnumerateElements())
                        {
                            if (entriesByKey.TryGetValue(element.Key, out var model))
                            {
                                model.Languages[language] = (element.Value, element.Comment);
                                continue;
                            }

                            var codeEntry = resourceInfo.Strings
                                .Where(x => x.Name == element.Key)
                                .FirstOrDefault();

                            entriesByKey[element.Key] = new ExcelModel
                            {
                                ResourcePath = location.Value.FilePath,
                                Key = element.Key,
                                Occurrences = codeEntry?.Occurrences ?? 0
                            };
                        }
                    }

                    foreach (var entry in resourceInfo.Strings.Where(x => !entriesByKey.ContainsKey(x.Name)))
                    {
                        entriesByKey[entry.Name] = new ExcelModel
                        {
                            ResourcePath = location.Value.FilePath,
                            Key = entry.Name,
                            Occurrences = entry.Occurrences
                        };
                    }

                    allResxEntries.AddRange(entriesByKey.Values);
                }

                await _output.WriteToOutputAsync($"Total keys found: {allResxEntries.Count}");

                var defaultFileName = $"{projectSnapshot.Name}_Translations_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                var excelPath = await Extensibility
                    .Shell()
                    .ShowSaveAsFileDialogAsync(new Microsoft.VisualStudio.Extensibility.Shell.FileDialog.FileDialogOptions
                    {
                        Title = "Save exported excel file",
                        InitialDirectory = projectDir,
                        InitialFileName = defaultFileName,
                    }, cancellationToken);

                if (string.IsNullOrWhiteSpace(excelPath))
                {
                    await _output.WriteToOutputAsync("Export cancelled by user.");
                    return;
                }

                _excelManager.WriteFile(excelPath, allResxEntries);

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