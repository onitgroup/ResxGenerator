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
using ResxGenerator.VSExtension.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ResxGenerator.VSExtension
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    [VisualStudioContribution]
    internal class GenerateCommand : Command
    {
        private readonly TraceSource _logger;
        private OutputWindow? _output;
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider;
        public readonly AnalyzerService _analyzer;
        public readonly ConfigService _config;

        public GenerateCommand(TraceSource traceSource, AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider,
            AnalyzerService analyzer, ConfigService config)
        {
            _logger = Requires.NotNull(traceSource, nameof(traceSource));
            _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
            _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
            _config = Requires.NotNull(config, nameof(config));
        }

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.GenerateCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.GenerateCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
        };

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _output = await Utilities.GetOutputWindowAsync(Extensibility);
            await base.InitializeAsync(cancellationToken);
        }

        private async Task WriteToOutputAsync(string message)
        {
            if (_output is not null)
            {
                await _output.Writer.WriteLineAsync(message);
            }
        }

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
                var componentModel = await _componentModelProvider
                    .GetServiceAsync() as IComponentModel
                    ?? throw new InvalidOperationException("Unable to get the MEF service.");

                // Roslyn instance
                var workspace = componentModel.GetService<VisualStudioWorkspace>();

                var projectSnapshot = await context.GetActiveProjectAsync(
                    x => x
                        .With(p => new { p.Name, p.Path, p.Type, p.TypeGuid, p.Kind })
                        .With(p => p.Files
                            .Where(f => f.Extension == ".json")
                            .With(f => new { f.Path, f.IsHidden }))
                        .With(p => p.LaunchProfiles
                            .With(y => new { y.Name, y.Categories, y.DisplayName, y.Order })
                    ), cancellationToken)
                    ?? throw new InvalidOperationException("No active project found.");
                var a = projectSnapshot.Kind;
                var b = projectSnapshot.TypeGuid;

                if (Utilities.SupportedProjects.Contains(projectSnapshot.TypeGuid) == false)
                    throw new InvalidOperationException("The project type is not supported.");


                var project = workspace.CurrentSolution.Projects
                    .Where(x => x.FilePath == projectSnapshot.Path)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException("Unable to load current project.");

                var config = await _config.GetOrCreateAsync(projectSnapshot);

                var projectDir = Path.GetDirectoryName(project.FilePath)!;
                var compilation = await project.GetCompilationAsync(cancellationToken) ?? throw new InvalidOperationException("Unable to get the project compilation.");

                var nl = Utilities.GetProjectNeutralLanguage(project.FilePath);
                var neutralLanguage = string.IsNullOrEmpty(nl)
                    ? CultureInfo.InvariantCulture // Not sure if it's the best way
                    : new CultureInfo(nl);

                await WriteToOutputAsync($"Project: {projectSnapshot.Name}, TypeGuid: {projectSnapshot.TypeGuid}");
                await WriteToOutputAsync($"Neutral language: {nl}");

                var languages = config.Cultures;
                if (languages.Contains(neutralLanguage))
                {
                    await WriteToOutputAsync("Attention target languages contain the neutral language of the project, it will be ignored");
                    languages = languages.Where(x => x != neutralLanguage);
                }

                if (languages.Any() == false)
                    throw new InvalidOperationException("No languages found in the config file, aborting.");

                await WriteToOutputAsync($"Target languages: {string.Join(", ", config.Languages)}");

                var symbols = await _analyzer.GatherSymbolsAsync(compilation);
                var strings = await _analyzer.FindStringsAsync(symbols, project.Solution);

                var resxElements = config.WriteKeyAsValue
                    ? strings.Select(x => new ResxElement(x, x))
                    : strings.Select(x => new ResxElement(x, null));

                foreach (var lang in languages)
                {
                    var writer = new ResxWriter(Path.Combine(projectDir, $"{config.ResourceName}.{lang}.resx"));
                    writer.AddRange(resxElements);
                    writer.Save();
                }

                await WriteToOutputAsync("Done.");
            }
            catch (Exception e)
            {
                await Extensibility
                    .Shell()
                    .ShowPromptAsync(e.Message, PromptOptions.OK, cancellationToken);
            }
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}