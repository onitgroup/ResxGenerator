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
    internal class AddChatGPTConfigCommand(ContextBuilder contextBuilder, IConfigurationService configuration) : Command
    {
        private readonly ContextBuilder _contextBuilder = Requires.NotNull(contextBuilder, nameof(contextBuilder));
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
                await _output.WriteToOutputAsync("\n=== Adding ChatGPT Configuration ===");

                var prjCtx = await _contextBuilder.BuildProjectContextAsync(context);

                if (prjCtx.Config.ChatGPT is not null)
                {
                    throw new InvalidOperationException("A configuration already exists, aborting.");
                }
                else
                {
                    prjCtx.Config.Translator = TranslatorService.ChatGPT.GetDescription();
                    prjCtx.Config.ChatGPT = new ChatGPTTranslator.Settings
                    {
                        Token = "<api-key>",
                        Model = "gpt-3.5-turbo",
                        Prompt = $"Translate the values of this JSON object from this locale {ChatGPTTranslator.SOURCE_PLACEHOLDER} to this locale {ChatGPTTranslator.TARGET_PLACEHOLDER} preserving its keys",
                    };
                }

                _configuration.Update(prjCtx.Directory, prjCtx.Config);

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