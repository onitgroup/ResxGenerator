using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using ResxGenerator.VSExtension.Commands;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Translators;
using System.Resources;

namespace ResxGenerator.VSExtension
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        /// <inheritdoc />
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = true,
        };

        protected override ResourceManager? ResourceManager => Resources.ResourceManager;

        [VisualStudioContribution]
        public static MenuConfiguration MyMenu => new("%ResxGenerator.VSExtension.MyMenu.DisplayName%")
        {
            Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
            Children = [
                MenuChild.Command<GenerateCommand>(),
                MenuChild.Separator,
                MenuChild.Command<ExportToExcelCommand>(),
                MenuChild.Command<ImportFromExcelCommand>(),
                MenuChild.Separator,
                MenuChild.Command<AddChatGPTConfigCommand>(),
            ],
        };

        /// <inheritdoc />
        protected override Task OnInitializedAsync(VisualStudioExtensibility extensibility, CancellationToken cancellationToken)
        {
            return base.OnInitializedAsync(extensibility, cancellationToken);
        }
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // must be scoped
            serviceCollection.AddScoped<ContextBuilder>();
            serviceCollection.AddScoped<IConfigurationService, ConfigurationService>();
            serviceCollection.AddScoped<IAnalyzerService, AnalyzerService>();
            serviceCollection.AddScoped<IExcelManager, ExcelManager>();

            // this service providers doesn't support keyed services
            serviceCollection.AddScoped<ChatGPTTranslator>();
            serviceCollection.AddScoped<GoogleTranslateTranslator>();
        }
    }
}