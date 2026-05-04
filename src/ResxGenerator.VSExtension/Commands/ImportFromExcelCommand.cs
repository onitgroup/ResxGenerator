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
    internal class ImportFromExcelCommand(ContextBuilder contextBuilder, IAnalyzerService analyzer, IConfigurationService configuration, IExcelManager excelManager) : Command
    {
        private readonly ContextBuilder _contextBuilder = Requires.NotNull(contextBuilder, nameof(contextBuilder));
        private readonly IAnalyzerService _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
        private readonly IExcelManager _excelManager = Requires.NotNull(excelManager, nameof(excelManager));
        private OutputChannel? _output;

        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.ImportFromExcelCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.ImportFromExcelCommand.ToolTip%",
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
                await _output.WriteToOutputAsync("\n=== Starting Excel Import ===");

                var prjCtx = await _contextBuilder.BuildProjectContextAsync(context);
                var roslynCtx = await _contextBuilder.BuildRoslynContextAsync(prjCtx);

                var excelPath = await Extensibility.Shell()
                    .ShowOpenFileDialogAsync(new FileDialogOptions
                    {
                        Title = "Import Translations from Excel",
                        Filters = new DialogFilters(new DialogFilter("Excel Files", "*.xls", "*.xlsx")),
                    }, cancellationToken);

                if (string.IsNullOrEmpty(excelPath))
                {
                    await _output.WriteToOutputAsync("Import cancelled by user.");
                    return;
                }

                await _output.WriteToOutputAsync($"Reading Excel file {excelPath}");

                List<ExcelModel> entries;
                using (var fileStream = File.OpenRead(excelPath))
                {
                    entries = _excelManager.Read(fileStream);
                }

                await _output.WriteToOutputAsync($"Found {entries.Count} entries");

                foreach (var group in entries.GroupBy(x => x.FullyQualifiedName))
                {
                    var typeName = group.Key.Split('.').Last();

                    var location = await _analyzer.GetResourceTypeDirectoryAsync(group.Key, roslynCtx.Project);
                    if (location is null)
                    {
                        if (typeName == prjCtx.Config.DefaultResourceName)
                        {
                            location = (prjCtx.Directory, roslynCtx.Project);
                        }
                        else
                        {
                            await _output.WriteToOutputAsync($"Skipping '{group.Key}' - type not found in current or referenced projects");
                            continue;
                        }
                    }

                    var (directory, targetProject) = location.Value;

                    var cultures = group
                        .SelectMany(e => e.Cultures.Keys)
                        .Distinct()
                        .ToList();

                    foreach (var culture in cultures)
                    {
                        var fileName = Config.BuildResxFileName(typeName, culture);
                        var resxFile = Path.Combine(directory, fileName);
                        var resxManager = ResxManager.OpenOrCreate(resxFile);
                        var resxElements = new List<ResxElement>();

                        foreach (var entry in group)
                        {
                            if (entry.Cultures.TryGetValue(culture, out var langData))
                            {
                                resxElements.Add(new ResxElement(
                                    entry.Key,
                                    langData.Value,
                                    langData.Comment
                                ));
                            }
                        }

                        resxManager.AddRange(resxElements, overwriteValues: true);
                        resxManager.Save();

                        await _output.WriteToOutputAsync($" - Wrote file {fileName} ({resxElements.Count} entries)");
                    }
                }

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
