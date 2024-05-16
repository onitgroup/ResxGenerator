using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using System.Collections.Frozen;
using System.Xml.Linq;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class Utilities
    {
        // https://github.com/JamesW75/visual-studio-project-type-guid
        //public static FrozenSet<Guid> SupportedProjects = new Guid[] {
        //    Guid.Parse("9A19103F-16F7-4668-BE54-9A1E7A4F7556"), // C# (.Net Core)
        //    Guid.Parse("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"), // C#
        //}.ToFrozenSet();

        /// <summary>
        /// Gets the project neutral language by reading the csproj xml
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public static string? GetProjectNeutralLanguage(string? projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath)) return null;
            var csproj = XDocument.Load(projectFilePath);
            return csproj.Root?
                .Descendants("NeutralLanguage")
                .Select(x => x.Value)
                .FirstOrDefault();
        }

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

        public static async Task<OutputWindow> GetOutputWindowAsync(VisualStudioExtensibility extensibility)
        {
            return await extensibility
                .Views()
                .Output
                .GetChannelAsync(nameof(ExtensionEntrypoint), nameof(Resources.OutputWindowDisplayName), default);
        }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW

    }
}