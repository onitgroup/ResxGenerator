using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using ResxGenerator.VSExtension.Commands;
using ResxGenerator.VSExtension.Services;
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
                MenuChild.Command<GenerateCommand>(),
            ],
        };

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // must be scoped
            serviceCollection.AddScoped<ConfigService>();
            serviceCollection.AddScoped<AnalyzerService>();
            serviceCollection.AddScoped<ChatGPTTranslator>();
            serviceCollection.AddScoped<DeepLTranslator>();
            serviceCollection.AddScoped<GoogleTranslateTranslator>();
        }
    }
}