using Microsoft.VisualStudio.Extensibility.Documents;

namespace ResxGenerator.VSExtension.Infrastructure
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public static class OutputWindowExtensions
    {
        public static async Task WriteToOutputAsync(this OutputWindow? output, string message, bool newLine = true)
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

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}