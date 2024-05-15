using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;
using ResxGenerator.VSExtension.Services;
using System.Resources;

namespace ResxGenerator.VSExtension
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// </summary>
    [VisualStudioContribution]
    internal class ResxGeneratorExtension : Extension
    {
        /// <inheritdoc />
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = true,
        };

        protected override ResourceManager? ResourceManager => Resources.ResourceManager;

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // must be scoped
            serviceCollection.AddScoped<ConfigService>();
            serviceCollection.AddScoped<AnalyzerService>();
        }
    }
}