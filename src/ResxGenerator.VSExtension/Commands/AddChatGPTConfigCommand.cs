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
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace ResxGenerator.VSExtension.Commands
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    [VisualStudioContribution]
    internal class AddChatGPTConfigCommand(ConfigService config) : Command
    {
        private readonly ConfigService _config = Requires.NotNull(config, nameof(config));
        private OutputWindow? _output;

        /// <inheritdoc />
        public override CommandConfiguration CommandConfiguration => new("%ResxGenerator.VSExtension.AddChatGPTConfigCommand.DisplayName%")
        {
            TooltipText = "%ResxGenerator.VSExtension.AddChatGPTConfigCommand.ToolTip%",
            Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
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
                var projectSnapshot = await context.GetActiveProjectAsync(
                    x => x.With(p => new { p.Name, p.Path, p.TypeGuid }),
                    cancellationToken)
                    ?? throw new InvalidOperationException("No active project found.");

                if (_config.Exists(projectSnapshot) == false)
                {
                    await _config.AddDefaultConfigFileAsync(projectSnapshot);
                }

                var config = await _config.GetAsync(projectSnapshot);

                if (config.ChatGPT is not null)
                {
                    throw new InvalidOperationException("A configuration already exists, aborting.");
                }
                else
                {
                    config.Translator = TranslatorService.ChatGPT.GetDescription();
                    config.ChatGPT = new ChatGPTTranslator.Settings
                    {
                        Token = "",
                        Model = "gpt-3.5-turbo",
                        Prompt = $"Translate every value of the following JSON object from this locale {ChatGPTTranslator.SOURCE_PLACEHOLDER} in this locale {ChatGPTTranslator.TARGET_PLACEHOLDER}, do not translate symbols",
                    };
                }

                await _config.UpdateAsync(projectSnapshot, config);

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