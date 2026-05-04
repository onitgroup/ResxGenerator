using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public interface IExcelManager
    {
        public void Write(Stream stream, IEnumerable<ExcelModel> rows);

        public List<ExcelModel> Read(Stream stream);
    }

    internal class ExcelManager : IExcelManager
    {
        private const string TRANSLATIONS_SHEET_NAME = "Translations";

        private static Regex CultureRegex()
        {
            return new Regex(@"([A-Z-]+)\s?-\s?Value", RegexOptions.IgnoreCase);
        }

        private static SheetData InitializeWorkbook(SpreadsheetDocument spreadsheet, string sheetName)
        {
            var workbookPart = spreadsheet.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = sheetName
            };
            sheets.Append(sheet);

            return worksheetPart.Worksheet.AppendChild(new SheetData());
        }

        private static Cell CreateCell(string? value)
        {
            return new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty))
            };
        }

        private static Cell CreateCell(int value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value)
            };
        }

        public void Write(Stream stream, IEnumerable<ExcelModel> rows)
        {
            using var spreadsheet = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
            var sheetData = InitializeWorkbook(spreadsheet, TRANSLATIONS_SHEET_NAME);

            // Get all unique languages
            var languages = rows
                .SelectMany(x => x.Cultures.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // headers
            var headerRow = new Row
            {
                RowIndex = 1
            };
            headerRow.Append(CreateCell("Resource Path"));
            headerRow.Append(CreateCell("Key"));
            headerRow.Append(CreateCell("Occurrences"));

            foreach (var lang in languages)
            {
                headerRow.Append(CreateCell($"{lang} - Value"));
                headerRow.Append(CreateCell($"{lang} - Comment"));
            }

            sheetData.Append(headerRow);

            // data
            uint rowIndex = 2;
            foreach (var entry in rows)
            {
                var dataRow = new Row
                {
                    RowIndex = rowIndex
                };

                dataRow.Append(CreateCell(entry.FullyQualifiedName));
                dataRow.Append(CreateCell(entry.Key));
                dataRow.Append(CreateCell(entry.Occurrences));

                foreach (var lang in languages)
                {
                    if (entry.Cultures.TryGetValue(lang, out var langData))
                    {
                        dataRow.Append(CreateCell(langData.Value));
                        dataRow.Append(CreateCell(langData.Comment));
                    }
                    else
                    {
                        dataRow.Append(CreateCell(string.Empty));
                        dataRow.Append(CreateCell(string.Empty));
                    }
                }

                sheetData.Append(dataRow);
                rowIndex++;
            }

            spreadsheet.Save();
        }

        public List<ExcelModel> Read(Stream stream)
        {
            List<Row> rows;
            IReadOnlyDictionary<int, string> sharedStrings;

            using (var spreadsheet = SpreadsheetDocument.Open(stream, isEditable: false)) // read-only
            {
                var workbookPart = spreadsheet.WorkbookPart
                    ?? throw new InvalidOperationException("Workbook part not found");

                // loaded here before closing the stream
                sharedStrings = LoadSharedStringTable(workbookPart);

                var sheet = workbookPart.Workbook?.Descendants<Sheet>()
                    .Where(x => x.Name == TRANSLATIONS_SHEET_NAME)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException($"Sheet '{TRANSLATIONS_SHEET_NAME}' not found");

                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var sheetData = worksheetPart.Worksheet?.Elements<SheetData>()
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException($"Sheet '{TRANSLATIONS_SHEET_NAME}' not found");

                rows = sheetData.Elements<Row>().ToList();
            }

            if (rows.Count < 2)
            {
                return [];
            }

            List<ExcelModel> res = [];

            // header processing to identify language columns
            var headerCells = rows.First().Elements<Cell>().ToList();
            var culturesColumns = new List<(string Culture, int ValueIndex, int CommentIndex)>();
            var cultureRegex = CultureRegex();

            for (int i = 3; i < headerCells.Count; i += 2) // start after ResourcePath, Key, Occurrences
            {
                var text = GetStringValue(headerCells[i], sharedStrings);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var match = cultureRegex.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                try
                {
                    var culture = match.Groups[1].Value;
                    _ = new CultureInfo(culture);
                    culturesColumns.Add((culture, i, i + 1));
                }
                catch (CultureNotFoundException)
                {
                    // invalid culture, skip
                    continue;
                }
            }

            // data rows
            foreach (var row in rows.Skip(1))
            {
                var cells = row.Elements<Cell>().ToList();
                if (cells.Count < 3 || cells.All(x => string.IsNullOrWhiteSpace(x.CellValue?.Text)))
                {
                    continue;
                }

                var languages = new Dictionary<string, (string? Value, string? Comment)>();

                foreach (var (culture, valueIdx, commentIdx) in culturesColumns)
                {
                    var value = valueIdx < cells.Count
                        ? GetStringValue(cells[valueIdx], sharedStrings)
                        : null;

                    var comment = commentIdx < cells.Count
                        ? GetStringValue(cells[commentIdx], sharedStrings)
                        : null;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        languages[culture] = (value, comment);
                    }
                }

                res.Add(new ExcelModel
                {
                    FullyQualifiedName = GetStringValue(cells[0], sharedStrings) ?? throw new InvalidDataException($"Unable to parse the fully qualified name in row {row.RowIndex}"),
                    Key = GetStringValue(cells[1], sharedStrings) ?? throw new InvalidDataException($"Unable to parse the key in row {row.RowIndex}"),
                    Occurrences = 0, // not relevant when reading
                    Cultures = languages
                });
            }

            return res;
        }

        private static IReadOnlyDictionary<int, string> LoadSharedStringTable(WorkbookPart workbookPart)
        {
            var result = new Dictionary<int, string>();

            var stringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            if (stringTablePart?.SharedStringTable is null)
                return result;

            int index = 0;
            foreach (var item in stringTablePart.SharedStringTable.Elements<SharedStringItem>())
            {
                result[index++] = item.Text?.Text ?? string.Empty;
            }

            return result;
        }

        private static string? GetStringValue(Cell cell, IReadOnlyDictionary<int, string> sharedStrings)
        {
            if (cell.DataType is null)
            {
                return cell.CellValue?.Text;
            }
            else if (cell.DataType.Value == CellValues.SharedString)
            {
                return int.TryParse(cell.CellValue?.Text, out int index)
                    ? sharedStrings.GetValueOrDefault(index)
                    : null;
            }
            else if (cell.DataType.Value == CellValues.InlineString)
            {
                return cell.InlineString?.Text?.Text;
            }
            else if (cell.DataType.Value == CellValues.String)
            {
                return cell.CellValue?.Text;
            }
            else
            {
                return cell.CellValue?.Text;
            }
        }
    }

    /// <summary>
    /// Represents a complete resx entry with all language variants
    /// </summary>
    public record ExcelModel
    {
        public required string FullyQualifiedName { get; init; }

        public required string Key { get; init; }

        public int Occurrences { get; init; }

        public Dictionary<string, (string? Value, string? Comment)> Cultures { get; init; } = [];
    }
}