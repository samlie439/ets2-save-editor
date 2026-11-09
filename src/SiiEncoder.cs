using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ets2Save
{
    /// <summary>Re-encodes blocks that were modified, leaving every other byte untouched.</summary>
    public static class SiiEncoder
    {
        const string CharTable = "0123456789abcdefghijklmnopqrstuvwxyz_";

        public static byte[] Rebuild(SiiFile file)
        {
            if (file.Blocks.Count == 0) return file.Data;
            int prefixEnd = file.Blocks[0].Start;
            int tailStart = file.TailStart;
            MemoryStream ms = new MemoryStream(file.Data.Length + 4096);

            ms.Write(file.Data, 0, prefixEnd);
            for (int i = 0; i < file.Blocks.Count; i++)
            {
                SiiBlock block = file.Blocks[i];
                if (!block.Dirty) ms.Write(file.Data, block.Start, block.End - block.Start);
                else WriteBlock(ms, file, block);
            }
            ms.Write(file.Data, tailStart, file.Data.Length - tailStart);
            return ms.ToArray();
        }

        public static void WriteBlock(Stream s, SiiFile file, SiiBlock block)
        {
            WriteU32(s, block.Structure.Id);
            if (block.IdentLength == 0 && block.IdentParts == null)
                WriteId(s, block.Ident, file.Version);
            else if (block.IdentParts != null)
                WriteIdParts(s, block.IdentLength, block.IdentParts);
            else
                WriteId(s, block.Ident, file.Version);

            for (int i = 0; i < block.Values.Count; i++)
            {
                SiiValue v = block.Values[i];
                if (!v.Dirty && v.RawEnd > v.RawStart)
                {
                    s.Write(file.Data, v.RawStart, v.RawEnd - v.RawStart);
                }
                else
                {
                    try
                    {
                        WriteValue(s, v, file.Version);
                    }
                    catch (Exception ex)
                    {
                        throw new SiiException(string.Format("编码 {0}.{1} 失败: {2}",
                            block.Structure.Name, block.Structure.Fields[i].Name, ex.Message));
                    }
                }
            }
        }

        public static void WriteValue(Stream s, SiiValue v, int version)
        {
            switch (v.Type)
            {
                case SiiType.Int32:
                case SiiType.UInt32:
                case SiiType.UInt32B:
                    WriteU32(s, unchecked((uint)Convert.ToInt64(v.Data)));
                    return;
                case SiiType.Int16:
                case SiiType.UInt16:
                    WriteU16(s, unchecked((ushort)Convert.ToInt64(v.Data)));
                    return;
                case SiiType.Int64:
                case SiiType.UInt64:
                    WriteU64(s, unchecked((ulong)Convert.ToInt64(v.Data)));
                    return;
                case SiiType.Single:
                    WriteF32(s, Convert.ToSingle(v.Data));
                    return;
                case SiiType.Bool:
                    s.WriteByte((bool)v.Data ? (byte)1 : (byte)0);
                    return;
                case SiiType.String:
                    {
                        byte[] bytes = v.Raw;
                        if (bytes == null) bytes = Encoding.UTF8.GetBytes((string)v.Data);
                        WriteU32(s, (uint)bytes.Length);
                        s.Write(bytes, 0, bytes.Length);
                        return;
                    }
                case SiiType.EncodedString:
                    WriteU64(s, EncodeString((string)v.Data));
                    return;
                case SiiType.Ordinal:
                    WriteU32(s, v.OrdinalId);
                    return;
                case SiiType.Id:
                case SiiType.Id2:
                case SiiType.Id3:
                    WriteId(s, (string)v.Data, version);
                    return;
                case SiiType.Vec2:
                case SiiType.Vec3:
                case SiiType.Vec4:
                case SiiType.Vec8:
                    {
                        float[] vec = (float[])v.Data;
                        for (int i = 0; i < vec.Length; i++) WriteF32(s, vec[i]);
                        return;
                    }
                case SiiType.Vec3I:
                    {
                        long[] vec = (long[])v.Data;
                        for (int i = 0; i < vec.Length; i++) WriteU32(s, unchecked((uint)vec[i]));
                        return;
                    }
            }

            // arrays
            int count = v.Count;
            WriteU32(s, (uint)count);
            switch (v.Type)
            {
                case SiiType.StringArray:
                    {
                        string[] items = (string[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                        {
                            byte[] bytes = Encoding.UTF8.GetBytes(items[i]);
                            WriteU32(s, (uint)bytes.Length);
                            s.Write(bytes, 0, bytes.Length);
                        }
                        return;
                    }
                case SiiType.EncodedStringArray:
                    {
                        string[] items = (string[])v.Data;
                        for (int i = 0; i < items.Length; i++) WriteU64(s, EncodeString(items[i]));
                        return;
                    }
                case SiiType.Int32Array:
                case SiiType.UInt32Array:
                    {
                        long[] items = (long[])v.Data;
                        for (int i = 0; i < items.Length; i++) WriteU32(s, unchecked((uint)items[i]));
                        return;
                    }
                case SiiType.Int16Array:
                case SiiType.UInt16Array:
                    {
                        long[] items = (long[])v.Data;
                        for (int i = 0; i < items.Length; i++) WriteU16(s, unchecked((ushort)items[i]));
                        return;
                    }
                case SiiType.Int64Array:
                case SiiType.UInt64Array:
                    {
                        Array items = (Array)v.Data;
                        for (int i = 0; i < items.Length; i++) WriteU64(s, Convert.ToUInt64(items.GetValue(i)));
                        return;
                    }
                case SiiType.BoolArray:
                    {
                        bool[] items = (bool[])v.Data;
                        for (int i = 0; i < items.Length; i++) s.WriteByte(items[i] ? (byte)1 : (byte)0);
                        return;
                    }
                case SiiType.SingleArray:
                    {
                        float[] items = (float[])v.Data;
                        for (int i = 0; i < items.Length; i++) WriteF32(s, items[i]);
                        return;
                    }
                case SiiType.IdArray:
                case SiiType.Id2Array:
                case SiiType.Id3Array:
                    {
                        string[] items = (string[])v.Data;
                        for (int i = 0; i < items.Length; i++) WriteId(s, items[i], version);
                        return;
                    }
                case SiiType.Vec2Array:
                case SiiType.Vec3Array:
                case SiiType.Vec4Array:
                case SiiType.Vec8Array:
                    {
                        float[][] items = (float[][])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            for (int k = 0; k < items[i].Length; k++) WriteF32(s, items[i][k]);
                        return;
                    }
                case SiiType.Vec3IArray:
                    {
                        long[][] items = (long[][])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            for (int k = 0; k < items[i].Length; k++) WriteU32(s, unchecked((uint)items[i][k]));
                        return;
                    }
            }
            throw new SiiException(string.Format("无法重新编码字段类型 0x{0:x2}", v.Type));
        }

        // ------------------------------------------------------------------
        public static void WriteId(Stream s, string text, int version)
        {
            if (string.IsNullOrEmpty(text) || text == "null")
            {
                s.WriteByte(0);
                return;
            }
            if (text.StartsWith("_nameless", StringComparison.Ordinal))
            {
                ulong value = 0;
                string rest = text.Substring("_nameless".Length);
                if (rest.StartsWith(".")) rest = rest.Substring(1);
                foreach (string part in rest.Split('.'))
                {
                    value = (value << 16) | Convert.ToUInt64(part, 16);
                }
                s.WriteByte(0xFF);
                WriteU64(s, value);
                return;
            }
            string[] parts = text.Split('.');
            s.WriteByte((byte)parts.Length);
            for (int i = 0; i < parts.Length; i++) WriteU64(s, EncodeString(parts[i]));
        }

        public static void WriteIdParts(Stream s, int length, ulong[] parts)
        {
            s.WriteByte((byte)length);
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++) WriteU64(s, parts[i]);
        }

        public static ulong EncodeString(string text)
        {
            ulong result = 0;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                int idx = CharTable.IndexOf(text[i]);
                if (idx < 0) throw new SiiException("ID 中含有非法字符: " + text);
                result = result * 38 + (ulong)(idx + 1);
            }
            return result;
        }

        public static void WriteU16(Stream s, ushort value)
        {
            s.WriteByte((byte)value);
            s.WriteByte((byte)(value >> 8));
        }

        public static void WriteU32(Stream s, uint value)
        {
            s.WriteByte((byte)value);
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 24));
        }

        public static void WriteU64(Stream s, ulong value)
        {
            for (int i = 0; i < 8; i++) s.WriteByte((byte)(value >> (8 * i)));
        }

        public static void WriteF32(Stream s, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            s.Write(b, 0, 4);
        }
    }
}
