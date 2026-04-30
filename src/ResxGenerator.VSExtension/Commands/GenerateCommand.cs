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
using ResxGenerator.VSExtension.Translators;
using System.Globalization;
using System.IO;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Commands
{
    [VisualStudioContribution]
    internal class GenerateCommand(AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider, IAnalyzerService analyzer, IConfigurationService configuration, IServiceProvider services) : Command
    {
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
        private readonly IAnalyzerService _analyzer = Requires.NotNull(analyzer, nameof(analyzer));
        private readonly IConfigurationService _configuration = Requires.NotNull(configuration, nameof(configuration));
        private readonly IServiceProvider _services = Requires.NotNull(services, nameof(services));
        private OutputChannel? _output;

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
            _output = await OutputChannelProvider.GetOrCreateAsync(Extensibility);
            await base.InitializeAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            try
            {
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

                if (!_configuration.TryGetNeutralCulture(projectSnapshot, out var neutralLanguage))
                {
                    neutralLanguage = CultureInfo.InvariantCulture; // Not sure if it's the best way;
                }

                await _output.WriteToOutputAsync($"Project: {projectSnapshot.Name}, TypeGuid: {projectSnapshot.TypeGuid}");
                await _output.WriteToOutputAsync($"Neutral language: {neutralLanguage}");

                var languages = config.Cultures;
                if (languages.Contains(neutralLanguage))
                {
                    await _output.WriteToOutputAsync("Attention target languages contain the neutral language of the project, it will be ignored");
                    languages = languages.Where(x => x != neutralLanguage);
                }

                if (languages.Any() == false)
                    throw new InvalidOperationException("No languages found in the config file, aborting.");

                await _output.WriteToOutputAsync($"Target languages: {string.Join(", ", config.Languages)}");

                var symbols = await _analyzer.GatherTargetSymbolsAsync(compilation);
                var resourceInfos = await _analyzer.FindStringsAsync(symbols, project.Solution, config.DefaultResourceName);

                await _output.WriteToOutputAsync($"Found {resourceInfos.Sum(x => x.Strings.Count())} strings across {resourceInfos.Count()} resource types.");

                ITranslator? translator = config.TranslatorService switch
                {
                    TranslatorService.ChatGPT => _services.GetRequiredService<ChatGPTTranslator>(),
                    TranslatorService.GoogleTranslate => _services.GetRequiredService<GoogleTranslateTranslator>(),
                    _ => null,
                };

                if (translator is null)
                {
                    await _output.WriteToOutputAsync($"No translator set.");
                }
                else
                {
                    await _output.WriteToOutputAsync($"Using translator {config.Translator}");
                }

                foreach (var resourceGroup in resourceInfos)
                {
                    var strings = resourceGroup.Strings.Select(x => x.Name).ToList();

                    await _output.WriteToOutputAsync($"\n=== Processing resource: {resourceGroup.Name} ({strings.Count()} strings) ===");

                    // Find where this resource type is located
                    var resxLocation = await _analyzer.GetResourceTypeDirectoryAsync(resourceGroup.Name, project);
                    if (resxLocation is null)
                    {
                        continue;
                    }

                    var (directory, targetProject) = resxLocation.Value;
                    var isInCurrentProject = targetProject.Id == project.Id;

                    if (!isInCurrentProject)
                    {
                        await _output.WriteToOutputAsync($"Note: This is in referenced project '{targetProject.Name}'");
                    }

                    foreach (var lang in languages)
                    {
                        // Create resx in the same directory as the resource type source file
                        var file = Path.Combine(directory, $"{resourceGroup.Name}.{lang}.resx");
                        var resxManager = ResxManager.Load(file);

                        List<ResxElement> resxElements;
                        if (translator is not null)
                        {
                            Dictionary<string, string?> translations;

                            if (config.OverwriteTranslations)
                            {
                                var existingStrings = resxManager
                                    .EnumerateElements()
                                    .Where(x => !string.IsNullOrEmpty(x.Value))
                                    .Select(x => x.Key)
                                    .ToList();

                                strings = strings
                                    .Where(x => existingStrings.Contains(x, StringComparer.InvariantCultureIgnoreCase) == false)
                                    .ToList();
                            }

                            translations = await translator.TranslateAsync(config.GetTranslatorConfig(), neutralLanguage, lang, strings);

                            foreach (var entry in translations.Where(x => string.IsNullOrEmpty(x.Value)))
                            {
                                await _output.WriteToOutputAsync($"Unable to translate value: \"{entry.Key}\" for locale {lang.Name} in {resourceGroup.Name}");
                            }

                            resxElements = strings.Select(x => new ResxElement(x, translations.GetValueOrDefault(x), null)).ToList();
                        }
                        else
                        {
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

                        resxManager.AddRange(resxElements, config.OverwriteTranslations);
                        resxManager.Save();

                        await _output.WriteToOutputAsync($"Updated: {file}");
                    }
                }

                await _output.WriteToOutputAsync("\n=== Command executed successfully ===");
            }
            catch (Exception e)
            {
                await Extensibility
                    .Shell()
                    .ShowPromptAsync(e.Message, PromptOptions.OK, cancellationToken);
            }
        }
    }
}