using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Translators
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class DeepLTranslator : ITranslator
    {
        private readonly ConfigService _config;
        private readonly VisualStudioExtensibility _extensibility;
        private readonly Task _initializationTask; // probably not needed
        private OutputWindow? _output;

        public DeepLTranslator(ConfigService config, VisualStudioExtensibility extensibility)
        {
            _config = config;
            _extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        public Task<Dictionary<string, string>> TranslateAsync(ITranslatorSettings settingsInterface, CultureInfo source, CultureInfo target, IEnumerable<string> values)
        {
            Requires.NotNull(settingsInterface, nameof(settingsInterface));
            throw new NotImplementedException();
        }

        public class Settings : ITranslatorSettings
        {
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}