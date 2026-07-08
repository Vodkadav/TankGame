using Godot;

namespace TankGame.Infrastructure;

public static class TranslationLoader
{
    private const string CsvPath = "res://i18n/strings.csv";
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        var file = FileAccess.Open(CsvPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            // Exported builds (e.g. web) don't ship the raw CSV — Godot imports it to
            // per-locale .translation resources and only those are packed. Load them.
            LoadImportedTranslations();
            return;
        }

        var headerLine = file.GetLine();
        var headers = ParseCsvLine(headerLine);
        if (headers.Length < 2 || headers[0] != "keys")
        {
            GD.PushError(
                $"TranslationLoader: malformed header '{headerLine}' " +
                "(expected 'keys,<locale>,...').");
            return;
        }

        var translations = new Translation[headers.Length - 1];
        for (var i = 0; i < translations.Length; i++)
        {
            translations[i] = new Translation { Locale = headers[i + 1] };
        }

        while (!file.EofReached())
        {
            var line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            if (cells.Length != headers.Length)
            {
                GD.PushError(
                    $"TranslationLoader: row '{line}' has {cells.Length} " +
                    $"columns, expected {headers.Length}.");
                continue;
            }

            var key = cells[0];
            for (var i = 0; i < translations.Length; i++)
            {
                translations[i].AddMessage(key, cells[i + 1]);
            }
        }

        foreach (var translation in translations)
        {
            TranslationServer.AddTranslation(translation);
        }

        _loaded = true;
    }

    // The CSV header order is keys,en,es,dk; the importer emits one .translation per locale.
    private static void LoadImportedTranslations()
    {
        foreach (var locale in new[] { "en", "es", "dk" })
        {
            var translation = GD.Load<Translation>($"res://i18n/strings.{locale}.translation");
            if (translation is not null)
            {
                TranslationServer.AddTranslation(translation);
            }
        }

        _loaded = true;
    }

    private static string[] ParseCsvLine(string line)
    {
        var cells = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    cells.Add(current.ToString());
                    current.Clear();
                }
                else if (c == '"' && current.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        cells.Add(current.ToString());
        return cells.ToArray();
    }
}
