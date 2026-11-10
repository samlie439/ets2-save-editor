using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ets2Save
{
    public sealed class SiiTextBlock
    {
        public string Type;
        public string Id;
        public Dictionary<string, string> Values = new Dictionary<string, string>();
    }

    /// <summary>
    /// Small line-oriented reader for textual SII files (info.sii, profile.sii).
    /// The game writes one field per line, which keeps this parser simple and safe.
    /// </summary>
    public static class SiiTextParser
    {
        public static Dictionary<string, SiiTextBlock> Parse(string text)
        {
            Dictionary<string, SiiTextBlock> blocks = new Dictionary<string, SiiTextBlock>();
            SiiTextBlock current = null;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line == "SiiNunit" || line == "{") continue;
                if (line == "}")
                {
                    if (current != null && !blocks.ContainsKey(current.Type)) blocks[current.Type] = current;
                    current = null;
                    continue;
                }
                if (line.EndsWith("{"))
                {
                    string header = line.Substring(0, line.Length - 1).Trim();
                    int sep = header.IndexOf(" : ", StringComparison.Ordinal);
                    SiiTextBlock block = new SiiTextBlock();
                    if (sep > 0)
                    {
                        block.Type = header.Substring(0, sep).Trim();
                        block.Id = header.Substring(sep + 3).Trim();
                    }
                    else
                    {
                        block.Type = header;
                        block.Id = "";
                    }
                    current = block;
                    continue;
                }
                if (current == null) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (!current.Values.ContainsKey(key)) current.Values[key] = Unescape(value);
            }
            return blocks;
        }

        public static string Unescape(string value)
        {
            if (value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"') return value;
            string inner = value.Substring(1, value.Length - 2);
            List<byte> bytes = new List<byte>(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '\\' && i + 1 < inner.Length)
                {
                    char n = inner[i + 1];
                    if (n == 'x' && i + 3 < inner.Length)
                    {
                        int code;
                        if (int.TryParse(inner.Substring(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            bytes.Add((byte)code);
                            i += 3;
                            continue;
                        }
                    }
                    if (n == 'n') { bytes.Add(10); i++; continue; }
                    if (n == 't') { bytes.Add(9); i++; continue; }
                    if (n == 'r') { bytes.Add(13); i++; continue; }
                    if (n == '"' || n == '\\') { bytes.Add((byte)n); i++; continue; }
                }
                byte[] enc = Encoding.UTF8.GetBytes(c.ToString());
                bytes.AddRange(enc);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static string Get(Dictionary<string, SiiTextBlock> blocks, string block, string key, string fallback)
        {
            SiiTextBlock b;
            if (blocks.TryGetValue(block, out b))
            {
                string v;
                if (b.Values.TryGetValue(key, out v)) return v;
            }
            return fallback;
        }

        public static long GetLong(Dictionary<string, SiiTextBlock> blocks, string block, string key, long fallback)
        {
            string v = Get(blocks, block, key, null);
            long result;
            if (v != null && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) return result;
            return fallback;
        }
    }
}
