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
    internal class GenerateCommand(ContextBuilder contextBuilder, IAnalyzerService analyzer, IConfigurationService configuration, IServiceProvider services) : Command
    {
        private readonly ContextBuilder _contextBuilder = Requires.NotNull(contextBuilder, nameof(contextBuilder));
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
                await _output.WriteToOutputAsync("\n=== Starting resx generation ===");

                var prjCtx = await _contextBuilder.BuildProjectContextAsync(context);
                var roslynCtx = await _contextBuilder.BuildRoslynContextAsync(prjCtx);

                if (!_configuration.TryGetNeutralCulture(prjCtx.FilePath, out var neutralLanguage))
                {
                    neutralLanguage = CultureInfo.InvariantCulture; // Not sure if it's the best way;
                }

                await _output.WriteToOutputAsync($"Project: {prjCtx.Name}");
                await _output.WriteToOutputAsync($"Neutral language: {neutralLanguage}");

                var cultures = prjCtx.Config.Cultures;
                if (cultures.Contains(neutralLanguage))
                {
                    await _output.WriteToOutputAsync("Attention target languages contain the neutral language of the project, it will be ignored");
                    cultures = cultures.Where(x => x != neutralLanguage);
                }

                if (cultures.Any() == false)
                    throw new InvalidOperationException("No languages found in the config file, aborting.");

                await _output.WriteToOutputAsync($"Target languages: {string.Join(", ", prjCtx.Config.Languages)}");

                var symbols = await _analyzer.GatherTargetSymbolsAsync(roslynCtx.Compilation);
                var resources = await _analyzer.FindStringsAsync(symbols, roslynCtx.Project, prjCtx.Config.DefaultResourceName);

                await _output.WriteToOutputAsync($"Found {resources.Sum(x => x.Strings.Count())} strings across {resources.Count()} resource types.");

                ITranslator? translator = prjCtx.Config.TranslatorService switch
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
                    await _output.WriteToOutputAsync($"Using translator {prjCtx.Config.Translator}");
                }

                foreach (var resource in resources)
                {
                    var strings = resource.Strings.Select(x => x.Name).ToList();
                    string resourceName;
                    string directory;
                    bool isInCurrentProject;

                    if (resource.Type is null)
                    {
                        // Fallback per stringhe non attribuite a nessun tipo
                        resourceName = prjCtx.Config.DefaultResourceName;
                        directory = prjCtx.Directory;
                        isInCurrentProject = true;

                        await _output.WriteToOutputAsync($"Processing unlocated strings as {resourceName} ({strings.Count()} strings)");
                    }
                    else
                    {
                        // Logica originale
                        var location = await _analyzer.GetResourceTypeDirectoryAsync(resource.Type, roslynCtx.Project);
                        if (location is null)
                        {
                            continue;
                        }

                        resourceName = resource.Type.Name;
                        directory = location.Value.DirectoryPath;
                        isInCurrentProject = location.Value.Project.Id == roslynCtx.Project.Id;

                        await _output.WriteToOutputAsync($"Processing resource: {resourceName} ({strings.Count()} strings)");

                        if (!isInCurrentProject)
                        {
                            await _output.WriteToOutputAsync($"Note: This is in referenced project '{location.Value.Project.Name}'");
                        }
                    }

                    foreach (var culture in cultures)
                    {
                        // Create resx in the same directory as the resource type source file
                        var fileName = Config.BuildResxFileName(resourceName, culture.Name);
                        var path = Path.Combine(directory, fileName);
                        var resxManager = ResxManager.OpenOrCreate(path);

                        List<ResxElement> resxElements;
                        if (translator is not null)
                        {
                            Dictionary<string, string?> translations;

                            if (prjCtx.Config.OverwriteTranslations)
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

                            using var progressReporter = await Extensibility.Shell()
                                .StartProgressReportingAsync("Translating resources", cancellationToken);

                            translations = await translator.TranslateAsync(prjCtx.Config.GetTranslatorConfig(), neutralLanguage, culture, strings, progressReporter);

                            foreach (var entry in translations.Where(x => string.IsNullOrEmpty(x.Value)))
                            {
                                await _output.WriteToOutputAsync($"Unable to translate value: \"{entry.Key}\" for locale {culture.Name} in {resourceName}");
                            }

                            resxElements = strings.Select(x => new ResxElement(x, translations.GetValueOrDefault(x), $"Generated by {prjCtx.Config.Translator}")).ToList();
                        }
                        else
                        {
                            resxElements = strings.Select(x => new ResxElement(x, null, null)).ToList();
                        }

                        if (prjCtx.Config.WriteKeyAsValue)
                        {
                            foreach (var element in resxElements.Where(x => string.IsNullOrEmpty(x.Value)))
                            {
                                element.Value = element.Key;
                            }
                        }

                        if (!string.IsNullOrEmpty(prjCtx.Config.ValidationComment))
                        {
                            foreach (var element in resxElements)
                            {
                                element.Comment = prjCtx.Config.ValidationComment;
                            }
                        }

                        resxManager.AddRange(resxElements, prjCtx.Config.OverwriteTranslations);
                        resxManager.Save();

                        await _output.WriteToOutputAsync($" - Updated: {fileName}");
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