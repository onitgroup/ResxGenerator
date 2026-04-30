using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;

#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class OutputChannelProvider
    {
        private static OutputChannel? _instance;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static async Task<OutputChannel> GetOrCreateAsync(VisualStudioExtensibility extensibility, CancellationToken cancellationToken = default)
        {
            if (_instance is not null)
                return _instance;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_instance is not null)
                    return _instance;

                _instance = await extensibility.Views().Output.CreateOutputChannelAsync(Resources.OutputWindowDisplayName, cancellationToken);

                return _instance;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public static class OutputChannelExtensions
    {
        public static async Task WriteToOutputAsync(this OutputChannel? output, string message, bool newLine = true)
        {
            if (output is not null)
            {
                if (newLine)
                {
                    await output.Writer.WriteLineAsync(message);
                }
                else
                {
                    await output.Writer.WriteAsync(message);
                }
            }
        }
    }
}
