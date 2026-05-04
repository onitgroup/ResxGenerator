using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.Query;
using System.IO;

namespace ResxGenerator.VSExtension.Infrastructure
{
    /// <summary>
    /// Holds the resolved context for a command execution.
    /// </summary>
    public class ProjectContext
    {
        public required string Name { get; init; }

        public required string FilePath { get; init; }

        public required string Directory { get; init; }

        public required Config Config { get; init; }
    }

    public class RoslynContext
    {
        public required Project Project { get; init; }

        public required Compilation Compilation { get; init; }
    }

    /// <summary>
    /// Builder for resolving the common context needed by all commands.
    /// </summary>
    public class ContextBuilder(AsyncServiceProviderInjection<SComponentModel, SComponentModel> componentModelProvider, IConfigurationService configuration)
    {
        private readonly AsyncServiceProviderInjection<SComponentModel, SComponentModel> _componentModelProvider = Requires.NotNull(componentModelProvider, nameof(componentModelProvider));
        private readonly IConfigurationService _configuration = Requires.NotNull(configuration, nameof(configuration));

        /// <summary>
        /// Builds the command context, optionally including the project compilation.
        /// </summary>
        /// <param name="clientContext">The client context from the command execution.</param>
        /// <param name="includeCompilation">If true, also resolves the project compilation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The fully resolved command context.</returns>
        public async Task<ProjectContext> BuildProjectContextAsync(IClientContext clientContext, CancellationToken cancellationToken = default)
        {
            var projectSnapshot = await clientContext.GetActiveProjectAsync(x => x.With(p => new
            {
                p.Name,
                p.Path,
                p.TypeGuid
            }), cancellationToken) ?? throw new InvalidOperationException("No active project found.");

            var projectDirectory = Path.GetDirectoryName(projectSnapshot.Path) ?? throw new InvalidOperationException("Unable to determine the project directory.");

            if (!_configuration.TryGet(projectDirectory, out var config))
            {
                _configuration.AddDefault(projectDirectory);
                throw new InvalidOperationException("No configuration file was found, a new one was created, please relaunch the command.");
            }

            return new ProjectContext
            {
                Name = projectSnapshot.Name,
                FilePath = projectSnapshot.Path ?? throw new InvalidOperationException("Unable to determine the project path."),
                Directory = projectDirectory,
                Config = config,
            };
        }

        /// <summary>
        /// Builds the command context, optionally including the project compilation.
        /// </summary>
        /// <param name="clientContext">The client context from the command execution.</param>
        /// <param name="includeCompilation">If true, also resolves the project compilation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The fully resolved command context.</returns>
        public async Task<RoslynContext> BuildRoslynContextAsync(ProjectContext context, CancellationToken cancellationToken = default)
        {
            var componentModel = await _componentModelProvider.GetServiceAsync() as IComponentModel
                ?? throw new InvalidOperationException("Unable to get the MEF service.");

            var workspace = componentModel.GetService<VisualStudioWorkspace>();

            var project = workspace.CurrentSolution.Projects
                .Where(x => x.FilePath == context.FilePath)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Unable to load current project.");

            var projectDir = Path.GetDirectoryName(project.FilePath)
                ?? throw new InvalidOperationException("Unable to determine the project directory.");

            var compilation = await project.GetCompilationAsync(cancellationToken)
                ?? throw new InvalidOperationException("Unable to get the project compilation.");

            return new RoslynContext
            {
                Project = project,
                Compilation = compilation
            };
        }
    }
}