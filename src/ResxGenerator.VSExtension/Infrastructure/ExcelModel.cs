using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Infrastructure
{
    /// <summary>
    /// Represents a complete resx entry with all language variants
    /// </summary>
    public class ExcelModel
    {
        public string ResourcePath { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public int Occurrences { get; set; }

        public Dictionary<string, (string? Value, string? Comment)> Languages { get; set; } = [];
    }
}
