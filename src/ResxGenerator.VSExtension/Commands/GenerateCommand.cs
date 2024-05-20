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
using ResxGenerator.VSExtension.Translators;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ResxGenerator.VSExtension.Commands
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    [VisualStudioContribution]
    internal class GenerateCommand(AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider, AnalyzerService analyzer, ConfigService config, IServiceProvider services) : Command
    {
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
        private readonly AnalyzerService _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
        private readonly ConfigService _config = Requires.NotNull(config, nameof(config));
        private readonly IServiceProvider _services = Requires.NotNull(services, nameof(services));
        private OutputWindow? _output;

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.GenerateCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.GenerateCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.GenerateFile, IconSettings.IconAndText),
            EnabledWhen = ActivationConstraint.And(
                ActivationConstraint.SolutionState(SolutionState.FullyLoaded)
            )
        };

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _output = await Utilities.GetOutputWindowAsync(Extensibility);
            await base.InitializeAsync(cancellationToken);
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
                    x => x.With(p => new { p.Name, p.Path, p.TypeGuid }),
                    cancellationToken)
                    ?? throw new InvalidOperationException("No active project found.");

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

                var nl = Utilities.GetProjectNeutralLanguage(project.FilePath);
                var neutralLanguage = string.IsNullOrEmpty(nl)
                    ? CultureInfo.InvariantCulture // Not sure if it's the best way
                    : new CultureInfo(nl);

                await _output.WriteToOutputAsync($"Project: {projectSnapshot.Name}, TypeGuid: {projectSnapshot.TypeGuid}");
                await _output.WriteToOutputAsync($"Neutral language: {nl}");

                var languages = config.Cultures;
                if (languages.Contains(neutralLanguage))
                {
                    await _output.WriteToOutputAsync("Attention target languages contain the neutral language of the project, it will be ignored");
                    languages = languages.Where(x => x != neutralLanguage);
                }

                if (languages.Any() == false)
                    throw new InvalidOperationException("No languages found in the config file, aborting.");

                await _output.WriteToOutputAsync($"Target languages: {string.Join(", ", config.Languages)}");

                var symbols = await _analyzer.GatherSymbolsAsync(compilation);
                var strings = await _analyzer.FindStringsAsync(symbols, project.Solution);

                await _output.WriteToOutputAsync($"Found {strings.Count()} strings.");

                ITranslator? translator = config.TranslatorService switch
                {
                    TranslatorService.ChatGPT => _services.GetRequiredService<ChatGPTTranslator>(),
                    TranslatorService.GoogleTranslate => _services.GetRequiredService<GoogleTranslateTranslator>(),
                    _ => null,
                };

                foreach (var lang in languages)
                {
                    List<ResxElement> resxElements;
                    if (translator is not null)
                    {
                        await _output.WriteToOutputAsync($"Translating with {config.Translator}");
                        var settings = await _config.GetTranslatorConfigAsync(projectSnapshot);
                        var translations = await translator.TranslateAsync(settings, neutralLanguage, lang, strings);

                        foreach (var entry in translations.Where(x => string.IsNullOrEmpty(x.Value)))
                        {
                            await _output.WriteToOutputAsync($"Unable to translate value: \"{entry.Key}\" for locale {lang.Name}");
                        }

                        resxElements = strings.Select(x => new ResxElement(x, translations.GetValueOrDefault(x), null)).ToList();
                    }
                    else
                    {
                        await _output.WriteToOutputAsync($"No translator used");
                        resxElements = strings.Select(x => new ResxElement(x, null, null)).ToList();
                    }

                    if (config.WriteKeyAsValue)
                    {
                        foreach (var element in resxElements.Where(x => string.IsNullOrEmpty(x.Value)))
                        {
                            element.Value = element.Key;
                        }
                    }

                    if (string.IsNullOrEmpty(config.ValidationComment) == false)
                    {
                        foreach (var element in resxElements)
                        {
                            element.Comment = config.ValidationComment;
                        }
                    }

                    var writer = new ResxWriter(Path.Combine(projectDir, $"{config.ResourceName}.{lang}.resx"));
                    writer.AddRange(resxElements, config.OverwriteTranslations);
                    writer.Save();
                }

                await _output.WriteToOutputAsync("Command executed.");
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