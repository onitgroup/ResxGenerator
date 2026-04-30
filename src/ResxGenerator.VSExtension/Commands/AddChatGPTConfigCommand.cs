using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.ProjectSystem.Query;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Translators;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Commands
{
    [VisualStudioContribution]
    internal class AddChatGPTConfigCommand(IConfigurationService configuration) : Command
    {
        private readonly IConfigurationService _configuration = Requires.NotNull(configuration, nameof(configuration));
        private OutputChannel? _output;

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.AddChatGPTConfigCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.AddChatGPTConfigCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.AddBehavior, IconSettings.IconAndText),
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
                var projectSnapshot = await context.GetActiveProjectAsync(
                    x => x.With(p => new { p.Name, p.Path, p.TypeGuid }),
                    cancellationToken)
                    ?? throw new InvalidOperationException("No active project found.");

                if (!_configuration.TryGet(projectSnapshot, out var config))
                {
                    _configuration.AddDefault(projectSnapshot);
                    throw new InvalidOperationException("No configuration file was found, a new one was created, please relaunch the command.");
                }

                if (config.ChatGPT is not null)
                {
                    throw new InvalidOperationException("A configuration already exists, aborting.");
                }
                else
                {
                    config.Translator = TranslatorService.ChatGPT.GetDescription();
                    config.ChatGPT = new ChatGPTTranslator.Settings
                    {
                        Token = "<api-key>",
                        Model = "gpt-3.5-turbo",
                        Prompt = $"Translate the values of this JSON object from this locale {ChatGPTTranslator.SOURCE_PLACEHOLDER} to this locale {ChatGPTTranslator.TARGET_PLACEHOLDER} preserving its keys",
                    };
                }

                _configuration.Update(projectSnapshot, config);

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
}