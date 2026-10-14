using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wyspa.Localization
{
    /// <summary>
    /// Rdzeń lokalizacji bez zależności od Unity (testowalny poza silnikiem):
    /// parser tabel CSV (klucz, pl, en, de, ja, zh-Hans, uwagi), formatowanie {0}, {0:N0}, {0:0.0}
    /// i odmiana przez liczby {0:plural:forma1|forma2|forma3} wg reguł CLDR dla naszych 5 języków.
    /// </summary>
    public class LocCore
    {
        public static readonly string[] Languages = { "pl", "en", "de", "ja", "zh-Hans" };

        // klucz -> (język -> tekst)
        readonly Dictionary<string, Dictionary<string, string>> table = new Dictionary<string, Dictionary<string, string>>();

        public int Count => table.Count;

        public bool Has(string key) => key != null && table.ContainsKey(key);

        /// <summary>Wczytuje tabelę CSV. Kolumny języków rozpoznaje po nagłówku. Późniejsze tabele nadpisują wcześniejsze.</summary>
        public void AddCsv(string csv)
        {
            var rows = ParseCsv(csv);
            if (rows.Count == 0)
                return;
            var header = rows[0];
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
                    continue;
                if (!table.TryGetValue(row[0], out var entry))
                    table[row[0]] = entry = new Dictionary<string, string>();
                for (int c = 1; c < header.Count && c < row.Count; c++)
                    if (Array.IndexOf(Languages, header[c]) >= 0 && !string.IsNullOrEmpty(row[c]))
                        entry[header[c]] = row[c];
            }
        }

        /// <summary>Tekst dla języka (z zapasem: angielski, potem polski). Brak klucza: zwraca sam klucz.</summary>
        public string Raw(string key, string lang)
        {
            if (key == null || !table.TryGetValue(key, out var entry))
                return null;
            if (entry.TryGetValue(lang, out var s)) return s;
            if (entry.TryGetValue("en", out s)) return s;
            if (entry.TryGetValue("pl", out s)) return s;
            return null;
        }

        public string Get(string key, string lang, params object[] args)
        {
            var raw = Raw(key, lang);
            return raw == null ? key : Format(raw, lang, args);
        }

        // ------------------------------------------------------------------ formatowanie

        public static string Format(string template, string lang, object[] args)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
                return template;
            var culture = Culture(lang);
            var sb = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                char ch = template[i];
                if (ch != '{')
                {
                    sb.Append(ch);
                    i++;
                    continue;
                }
                int end = FindClose(template, i);
                if (end < 0)
                {
                    sb.Append(template, i, template.Length - i);
                    break;
                }
                sb.Append(Placeholder(template.Substring(i + 1, end - i - 1), lang, culture, args));
                i = end + 1;
            }
            return sb.ToString();
        }

        static int FindClose(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}' && --depth == 0) return i;
            }
            return -1;
        }

        static string Placeholder(string body, string lang, CultureInfo culture, object[] args)
        {
            int colon = body.IndexOf(':');
            string idxPart = colon < 0 ? body : body.Substring(0, colon);
            string fmt = colon < 0 ? null : body.Substring(colon + 1);
            if (!int.TryParse(idxPart.Trim(), out int idx) || args == null || idx < 0 || idx >= args.Length)
                return "{" + body + "}";
            object arg = args[idx];

            if (fmt != null && fmt.StartsWith("plural:", StringComparison.Ordinal))
            {
                var forms = fmt.Substring(7).Split('|');
                long n = ToLong(arg);
                int form = PluralForm(lang, n);
                return forms[Math.Min(form, forms.Length - 1)];
            }
            if (!string.IsNullOrEmpty(fmt) && arg is IFormattable f)
                return f.ToString(fmt, culture);
            return arg is IFormattable g ? g.ToString(null, culture) : (arg?.ToString() ?? "");
        }

        static long ToLong(object o)
        {
            switch (o)
            {
                case int i: return i;
                case long l: return l;
                case float f: return (long)Math.Floor(f);
                case double d: return (long)Math.Floor(d);
                default: return long.TryParse(o?.ToString(), out var v) ? v : 0;
            }
        }

        /// <summary>Indeks formy liczby mnogiej (CLDR, liczby całkowite). PL: 1 / 2-4 / 5+; EN, DE: 1 / reszta; JA, ZH: jedna forma.</summary>
        public static int PluralForm(string lang, long n)
        {
            n = Math.Abs(n);
            switch (lang)
            {
                case "pl":
                    if (n == 1) return 0;
                    long m10 = n % 10, m100 = n % 100;
                    return m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? 1 : 2;
                case "en":
                case "de":
                    return n == 1 ? 0 : 1;
                default:
                    return 0;
            }
        }

        public static CultureInfo Culture(string lang)
        {
            try { return CultureInfo.GetCultureInfo(lang); }
            catch (Exception) { return CultureInfo.InvariantCulture; }
        }

        // ------------------------------------------------------------------ CSV (RFC 4180: cudzysłowy, przecinki, nowe linie w polach)

        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text))
                return rows;
            if (text[0] == '﻿')
                text = text.Substring(1);
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
