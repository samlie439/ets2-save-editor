using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ets2Save
{
    /// <summary>Decoded representation of a binary SII ("BSII") file.</summary>
    public sealed class SiiFile
    {
        public const uint Signature = 0x49495342; // BSII

        public int Version;
        public byte[] Data;
        public int TailStart;
        public Dictionary<uint, SiiStructure> Structures = new Dictionary<uint, SiiStructure>();
        public List<SiiBlock> Blocks = new List<SiiBlock>();

        public SiiBlock FindBlock(string name)
        {
            for (int i = 0; i < Blocks.Count; i++)
                if (Blocks[i].Structure.Name == name) return Blocks[i];
            return null;
        }

        public List<SiiBlock> FindBlocks(string name)
        {
            List<SiiBlock> result = new List<SiiBlock>();
            for (int i = 0; i < Blocks.Count; i++)
                if (Blocks[i].Structure.Name == name) result.Add(Blocks[i]);
            return result;
        }

        // ------------------------------------------------------------------
        // Patching (all values are fixed size, so no re-layout is required)
        // ------------------------------------------------------------------

        public void SetInt(SiiBlock block, string field, long value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) throw new ArgumentException("字段不存在: " + field);
            switch (v.Type)
            {
                case SiiType.Int32: WriteInt32(v.Offset, checked((int)value)); break;
                case SiiType.UInt32:
                case SiiType.UInt32B: WriteUInt32(v.Offset, checked((uint)value)); break;
                case SiiType.Int16: WriteInt16(v.Offset, checked((short)value)); break;
                case SiiType.UInt16: WriteUInt16(v.Offset, checked((ushort)value)); break;
                case SiiType.Int64: WriteInt64(v.Offset, value); break;
                case SiiType.UInt64: WriteUInt64(v.Offset, checked((ulong)value)); break;
                default: throw new ArgumentException("字段不是整数: " + field);
            }
            if (v.Type == SiiType.UInt64) v.Data = (ulong)value;
            else v.Data = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetFloat(SiiBlock block, string field, float value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) throw new ArgumentException("字段不存在: " + field);
            if (v.Type != SiiType.Single) throw new ArgumentException("字段不是浮点数: " + field);
            WriteSingle(v.Offset, value);
            v.Data = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetString(SiiBlock block, string field, string value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) throw new ArgumentException("字段不存在: " + field);
            if (v.Type == SiiType.EncodedString || v.Type == SiiType.Id || v.Type == SiiType.Id2 || v.Type == SiiType.Id3)
            {
                v.Data = value;
                v.Dirty = true;
                block.Dirty = true;
                return;
            }
            if (v.Type != SiiType.String) throw new ArgumentException("字段不是字符串: " + field);
            v.Data = value;
            v.Raw = Encoding.UTF8.GetBytes(value);
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetVec8(SiiBlock block, string field, float[] values)
        {
            SiiValue v = block.GetValue(field);
            if (v == null || v.Type != SiiType.Vec8) throw new ArgumentException("字段不是 vec8: " + field);
            if (values.Length < 7) throw new ArgumentException("vec8 需要 7 个分量");
            float hidden = BitConverter.ToSingle(Data, v.Offset + 12);
            int bias = (int)hidden;
            float storedA = values[0] - (((bias & 0xFFF) - 2048) << 9);
            float storedC = values[2] - ((((bias >> 12) & 0xFFF) - 2048) << 9);
            WriteSingle(v.Offset, storedA);
            WriteSingle(v.Offset + 4, values[1]);
            WriteSingle(v.Offset + 8, storedC);
            WriteSingle(v.Offset + 12, hidden);
            for (int i = 0; i < 4; i++) WriteSingle(v.Offset + 16 + i * 4, values[3 + i]);
            v.Data = new float[] { values[0], values[1], values[2], values[3], values[4], values[5], values[6] };
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetIdArray(SiiBlock block, string field, string[] items)
        {
            SiiValue v = block.GetValue(field);
            if (v == null || v.Type != SiiType.IdArray) throw new ArgumentException("字段不是 ID 数组: " + field);
            v.Data = items;
            v.Count = items.Length;
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetBool(SiiBlock block, string field, bool value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) throw new ArgumentException("字段不存在: " + field);
            if (v.Type != SiiType.Bool) throw new ArgumentException("字段不是布尔值: " + field);
            Data[v.Offset] = value ? (byte)1 : (byte)0;
            v.Data = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        public void SetFloatArrayItem(SiiBlock block, string field, int index, float value)
        {
            SiiValue v = block.GetValue(field);
            if (v == null) throw new ArgumentException("字段不存在: " + field);
            if (v.Type != SiiType.SingleArray) throw new ArgumentException("字段不是浮点数组: " + field);
            if (index < 0 || index >= v.Count) throw new IndexOutOfRangeException(field);
            WriteSingle(v.Offset + index * 4, value);
            float[] arr = (float[])v.Data;
            arr[index] = value;
            v.Dirty = true;
            block.Dirty = true;
        }

        public byte[] Rebuild()
        {
            return SiiEncoder.Rebuild(this);
        }

        public SiiBlock NewBlock(uint structureId)
        {
            SiiStructure st;
            if (!Structures.TryGetValue(structureId, out st))
                throw new SiiException("未知结构: " + structureId);
            SiiBlock block = new SiiBlock();
            block.Structure = st;
            block.Ident = "null";
            block.Dirty = true;
            for (int i = 0; i < st.Fields.Count; i++)
            {
                SiiValue v = new SiiValue();
                v.Type = st.Fields[i].Type;
                v.Dirty = true;
                v.Data = DefaultFor(v.Type);
                block.Values.Add(v);
            }
            Blocks.Add(block);
            return block;
        }

        static object DefaultFor(uint type)
        {
            switch (type)
            {
                case SiiType.String: return "";
                case SiiType.EncodedString: return "";
                case SiiType.Single: return 0f;
                case SiiType.Bool: return false;
                case SiiType.StringArray: return new string[0];
                case SiiType.EncodedStringArray: return new string[0];
                case SiiType.SingleArray: return new float[0];
                case SiiType.Int32Array:
                case SiiType.UInt32Array:
                case SiiType.Int16Array:
                case SiiType.UInt16Array:
                case SiiType.Int64Array: return new long[0];
                case SiiType.UInt64Array: return new ulong[0];
                case SiiType.BoolArray: return new bool[0];
                case SiiType.IdArray:
                case SiiType.Id2Array:
                case SiiType.Id3Array: return new string[0];
                case SiiType.Vec2: return new float[2];
                case SiiType.Vec3: return new float[3];
                case SiiType.Vec4: return new float[4];
                case SiiType.Vec8: return new float[7];
                case SiiType.Vec3I: return new long[3];
                case SiiType.Vec2Array: return new float[0][];
                case SiiType.Vec3Array: return new float[0][];
                case SiiType.Vec4Array: return new float[0][];
                case SiiType.Vec8Array: return new float[0][];
                case SiiType.Vec3IArray: return new long[0][];
                default: return 0L;
            }
        }

        public void WriteInt32(int offset, int value)
        {
            Data[offset] = (byte)value; Data[offset + 1] = (byte)(value >> 8);
            Data[offset + 2] = (byte)(value >> 16); Data[offset + 3] = (byte)(value >> 24);
        }

        public void WriteUInt32(int offset, uint value)
        {
            Data[offset] = (byte)value; Data[offset + 1] = (byte)(value >> 8);
            Data[offset + 2] = (byte)(value >> 16); Data[offset + 3] = (byte)(value >> 24);
        }

        public void WriteInt16(int offset, short value)
        {
            Data[offset] = (byte)value; Data[offset + 1] = (byte)(value >> 8);
        }

        public void WriteUInt16(int offset, ushort value)
        {
            Data[offset] = (byte)value; Data[offset + 1] = (byte)(value >> 8);
        }

        public void WriteInt64(int offset, long value)
        {
            for (int i = 0; i < 8; i++) Data[offset + i] = (byte)(value >> (8 * i));
        }

        public void WriteUInt64(int offset, ulong value)
        {
            for (int i = 0; i < 8; i++) Data[offset + i] = (byte)(value >> (8 * i));
        }

        public void WriteSingle(int offset, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            Data[offset] = b[0]; Data[offset + 1] = b[1];
            Data[offset + 2] = b[2]; Data[offset + 3] = b[3];
        }

        // ------------------------------------------------------------------
        // Text rendering
        // ------------------------------------------------------------------

        public string ToText()
        {
            StringBuilder sb = new StringBuilder(Data.Length * 2);
            sb.Append("SiiNunit\n{\n");
            for (int i = 0; i < Blocks.Count; i++)
            {
                SiiBlock block = Blocks[i];
                sb.Append(block.Structure.Name).Append(" : ").Append(block.Ident).Append(" {\n");
                for (int j = 0; j < block.Values.Count; j++)
                    SiiTextWriter.AppendValue(sb, block.Structure.Fields[j].Name, block.Values[j], " ");
                sb.Append("}\n\n");
            }
            sb.Append('}');
            return sb.ToString();
        }
    }

    public static class SiiType
    {
        public const uint String = 0x01;
        public const uint StringArray = 0x02;
        public const uint EncodedString = 0x03;
        public const uint EncodedStringArray = 0x04;
        public const uint Single = 0x05;
        public const uint SingleArray = 0x06;
        public const uint Vec2 = 0x07;
        public const uint Vec2Array = 0x08;
        public const uint Vec3 = 0x09;
        public const uint Vec3Array = 0x0A;
        public const uint Vec3I = 0x11;
        public const uint Vec3IArray = 0x12;
        public const uint Vec4 = 0x17;
        public const uint Vec4Array = 0x18;
        public const uint Vec8 = 0x19;
        public const uint Vec8Array = 0x1A;
        public const uint Int32 = 0x25;
        public const uint Int32Array = 0x26;
        public const uint UInt32 = 0x27;
        public const uint UInt32Array = 0x28;
        public const uint Int16 = 0x29;
        public const uint Int16Array = 0x2A;
        public const uint UInt16 = 0x2B;
        public const uint UInt16Array = 0x2C;
        public const uint UInt32B = 0x2F;
        public const uint Int64 = 0x31;
        public const uint Int64Array = 0x32;
        public const uint UInt64 = 0x33;
        public const uint UInt64Array = 0x34;
        public const uint Bool = 0x35;
        public const uint BoolArray = 0x36;
        public const uint Ordinal = 0x37;
        public const uint Id = 0x39;
        public const uint IdArray = 0x3A;
        public const uint Id2 = 0x3B;
        public const uint Id2Array = 0x3C;
        public const uint Id3 = 0x3D;
        public const uint Id3Array = 0x3E;
    }

    public sealed class SiiField
    {
        public string Name;
        public uint Type;
        public Dictionary<uint, string> Ordinals;
    }

    public sealed class SiiStructure
    {
        public uint Id;
        public string Name;
        public List<SiiField> Fields = new List<SiiField>();
    }

    public sealed class SiiValue
    {
        public uint Type;
        public object Data;     // long / float / bool / string / arrays
        public byte[] Raw;      // original bytes for string values
        public int Offset;      // payload offset inside SiiFile.Data
        public int Size;        // payload size in bytes
        public int ElemSize;    // 0 when elements are variable length
        public int Count;
        public int RawStart;    // range of the original encoding (arrays include the count)
        public int RawEnd;
        public bool Dirty;      // encode from Data instead of copying RawStart..RawEnd
        public uint OrdinalId;  // original ordinal index for 0x37 values
        public int IdLength;    // original length byte for ID values
        public ulong[] IdParts; // original 64bit parts for ID values
    }

    public sealed class SiiBlock
    {
        public SiiStructure Structure;
        public string Ident;
        public int IdentLength;
        public ulong[] IdentParts;
        public List<SiiValue> Values = new List<SiiValue>();
        public int Start;          // offset of the block type field in SiiFile.Data
        public int End;            // offset just past the block
        public int IdentStart;     // raw ident bytes range
        public int IdentEnd;
        public bool Dirty;         // needs re-encoding when rebuilding

        public SiiValue GetValue(string name)
        {
            for (int i = 0; i < Structure.Fields.Count; i++)
                if (Structure.Fields[i].Name == name) return Values[i];
            return null;
        }

        public object Get(string name)
        {
            SiiValue v = GetValue(name);
            return v == null ? null : v.Data;
        }
    }

    /// <summary>Parses a BSII byte buffer into <see cref="SiiFile"/>.</summary>
    public static class SiiParser
    {
        sealed class Reader
        {
            public byte[] D;
            public int P;
            public Reader(byte[] data) { D = data; P = 0; }
            public byte U8() { return D[P++]; }
            public ushort U16() { ushort v = BitConverter.ToUInt16(D, P); P += 2; return v; }
            public int I32() { int v = BitConverter.ToInt32(D, P); P += 4; return v; }
            public uint U32() { uint v = BitConverter.ToUInt32(D, P); P += 4; return v; }
            public ulong U64() { ulong v = BitConverter.ToUInt64(D, P); P += 8; return v; }
            public float F32() { float v = BitConverter.ToSingle(D, P); P += 4; return v; }
            public string Str()
            {
                int n = (int)U32();
                string s = Encoding.UTF8.GetString(D, P, n);
                P += n;
                return s;
            }
            public byte[] StrBytes()
            {
                int n = (int)U32();
                byte[] b = new byte[n];
                Buffer.BlockCopy(D, P, b, 0, n);
                P += n;
                return b;
            }
        }

        const string CharTable = "0123456789abcdefghijklmnopqrstuvwxyz_";

        public static SiiFile Parse(byte[] data)
        {
            Reader r = new Reader(data);
            uint sig = r.U32();
            if (sig != SiiFile.Signature) throw new SiiException("不是 BSII 二进制存档");
            int version = (int)r.U32();
            if (version < 1 || version > 3) throw new SiiException("不支持的 BSII 版本: " + version);

            SiiFile file = new SiiFile();
            file.Version = version;
            file.Data = data;

            while (r.P + 4 <= data.Length)
            {
                int blockStart = r.P;
                uint blockType = r.U32();
                if (blockType == 0)
                {
                    bool valid = r.U8() != 0;
                    if (!valid) break;
                    uint sid = r.U32();
                    string name = r.Str();
                    SiiStructure st = new SiiStructure();
                    st.Id = sid;
                    st.Name = name;
                    while (true)
                    {
                        uint ftype = r.U32();
                        if (ftype == 0) break;
                        SiiField f = new SiiField();
                        f.Name = r.Str();
                        f.Type = ftype;
                        if (ftype == SiiType.Ordinal)
                            f.Ordinals = ReadOrdinalList(r);
                        st.Fields.Add(f);
                    }
                    file.Structures[sid] = st;
                }
                else
                {
                    SiiStructure st;
                    if (!file.Structures.TryGetValue(blockType, out st))
                        throw new SiiException("数据块引用了未知结构: " + blockType);
                    SiiBlock block = new SiiBlock();
                    block.Structure = st;
                    block.Start = blockStart;
                    block.IdentStart = r.P;
                    block.Ident = ReadIdText(r, version, out block.IdentLength, out block.IdentParts);
                    block.IdentEnd = r.P;
                    for (int i = 0; i < st.Fields.Count; i++)
                        block.Values.Add(ReadValue(r, st.Fields[i], version));
                    block.End = r.P;
                    file.Blocks.Add(block);
                }
            }
            if (r.P != data.Length)
                throw new SiiException(string.Format("解析在 {0}/{1} 字节处结束，格式未完全识别", r.P, data.Length));
            file.TailStart = file.Blocks.Count > 0 ? file.Blocks[file.Blocks.Count - 1].End : data.Length;
            return file;
        }

        static Dictionary<uint, string> ReadOrdinalList(Reader r)
        {
            Dictionary<uint, string> table = new Dictionary<uint, string>();
            int count = (int)r.U32();
            for (int i = 0; i < count; i++)
            {
                uint ordinal = r.U32();
                table[ordinal] = r.Str();
            }
            return table;
        }

        static string ReadIdText(Reader r, int version)
        {
            int length;
            ulong[] parts;
            return ReadIdText(r, version, out length, out parts);
        }

        static string ReadIdText(Reader r, int version, out int length, out ulong[] parts)
        {
            length = r.U8();
            int count = length == 0xFF ? 1 : length;
            parts = new ulong[count];
            for (int i = 0; i < count; i++) parts[i] = r.U64();
            return FormatId(length, parts, version);
        }

        public static string FormatId(int length, ulong[] parts, int version)
        {
            if (length == 0) return "null";
            if (length == 0xFF)
            {
                ulong value = parts[0];
                if (version < 2 && (value >> 32) == 0)
                    return string.Format("_nameless.{0:x4}.{1:x4}", (int)((value >> 16) & 0xFFFF), (int)(value & 0xFFFF));
                if (value == 0) return "_nameless.0";
                StringBuilder sb = new StringBuilder();
                ulong temp = value;
                while (temp != 0)
                {
                    int word = (int)(temp & 0xFFFF);
                    if ((temp & ~0xFFFFUL) != 0)
                        sb.Insert(0, "." + word.ToString("x4"));
                    else
                        sb.Insert(0, "." + word.ToString("x"));
                    temp >>= 16;
                }
                return "_nameless" + sb.ToString();
            }
            StringBuilder id = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) id.Append('.');
                id.Append(DecodeString(parts[i]));
            }
            return id.ToString();
        }

        public static string DecodeString(ulong number)
        {
            number &= ~(1UL << 63);
            StringBuilder sb = new StringBuilder();
            while (number != 0)
            {
                int digit = (int)(number % 38);
                if (digit == 0) break;
                sb.Append(CharTable[digit - 1]);
                number /= 38;
            }
            return sb.ToString();
        }

        static SiiValue ReadValue(Reader r, SiiField field, int version)
        {
            int start = r.P;
            SiiValue v = ReadValueCore(r, field, version);
            v.RawStart = start;
            v.RawEnd = r.P;
            return v;
        }

        static SiiValue ReadValueCore(Reader r, SiiField field, int version)
        {
            SiiValue v = new SiiValue();
            v.Type = field.Type;
            v.Offset = r.P;
            uint t = field.Type;

            switch (t)
            {
                case SiiType.Int32:
                    v.Data = (long)r.I32(); v.Size = 4; return v;
                case SiiType.UInt32:
                case SiiType.UInt32B:
                    v.Data = (long)r.U32(); v.Size = 4; return v;
                case SiiType.Int16:
                    v.Data = (long)BitConverter.ToInt16(r.D, r.P); r.P += 2; v.Size = 2; return v;
                case SiiType.UInt16:
                    v.Data = (long)r.U16(); v.Size = 2; return v;
                case SiiType.Int64:
                    v.Data = BitConverter.ToInt64(r.D, r.P); r.P += 8; v.Size = 8; return v;
                case SiiType.UInt64:
                    v.Data = r.U64(); v.Size = 8; return v;
                case SiiType.Single:
                    v.Data = r.F32(); v.Size = 4; return v;
                case SiiType.Bool:
                    v.Data = r.U8() != 0; v.Size = 1; return v;
                case SiiType.String:
                    {
                        int start = r.P;
                        byte[] raw = r.StrBytes();
                        v.Raw = raw;
                        v.Data = Encoding.UTF8.GetString(raw);
                        v.Offset = start;
                        v.Size = raw.Length;
                        return v;
                    }
                case SiiType.EncodedString:
                    v.Data = DecodeString(r.U64()); v.Size = 8; return v;
                case SiiType.Ordinal:
                    {
                        uint ordinal = r.U32();
                        string s;
                        if (field.Ordinals == null || !field.Ordinals.TryGetValue(ordinal, out s)) s = "";
                        v.Data = s; v.Size = 4; v.OrdinalId = ordinal; return v;
                    }
                case SiiType.Id:
                case SiiType.Id2:
                case SiiType.Id3:
                    {
                        int start = r.P;
                        int idLen;
                        ulong[] idParts;
                        v.Data = ReadIdText(r, version, out idLen, out idParts);
                        v.IdLength = idLen;
                        v.IdParts = idParts;
                        v.Size = r.P - start;
                        return v;
                    }
                case SiiType.Vec2:
                    v.Data = new float[] { r.F32(), r.F32() }; v.Size = 8; return v;
                case SiiType.Vec3:
                    v.Data = new float[] { r.F32(), r.F32(), r.F32() }; v.Size = 12; return v;
                case SiiType.Vec3I:
                    v.Data = new long[] { r.I32(), r.I32(), r.I32() }; v.Size = 12; return v;
                case SiiType.Vec4:
                    v.Data = new float[] { r.F32(), r.F32(), r.F32(), r.F32() }; v.Size = 16; return v;
                case SiiType.Vec8:
                    {
                        int start = r.P;
                        v.Data = ReadVec8(r, version);
                        v.Size = r.P - start;
                        return v;
                    }
            }

            // ---------------- arrays ----------------
            int count = (int)r.U32();
            v.Count = count;
            v.Offset = r.P;
            switch (t)
            {
                case SiiType.StringArray:
                    {
                        string[] items = new string[count];
                        for (int i = 0; i < count; i++) items[i] = r.Str();
                        v.Data = items; v.Size = r.P - v.Offset; v.ElemSize = 0; return v;
                    }
                case SiiType.EncodedStringArray:
                    {
                        string[] items = new string[count];
                        for (int i = 0; i < count; i++) items[i] = DecodeString(r.U64());
                        v.Data = items; v.Size = 8 * count; v.ElemSize = 8; return v;
                    }
                case SiiType.Int32Array:
                    {
                        long[] items = new long[count];
                        for (int i = 0; i < count; i++) items[i] = r.I32();
                        v.Data = items; v.Size = 4 * count; v.ElemSize = 4; return v;
                    }
                case SiiType.UInt32Array:
                    {
                        long[] items = new long[count];
                        for (int i = 0; i < count; i++) items[i] = r.U32();
                        v.Data = items; v.Size = 4 * count; v.ElemSize = 4; return v;
                    }
                case SiiType.Int16Array:
                    {
                        long[] items = new long[count];
                        for (int i = 0; i < count; i++) { items[i] = BitConverter.ToInt16(r.D, r.P); r.P += 2; }
                        v.Data = items; v.Size = 2 * count; v.ElemSize = 2; return v;
                    }
                case SiiType.UInt16Array:
                    {
                        long[] items = new long[count];
                        for (int i = 0; i < count; i++) { items[i] = r.U16(); }
                        v.Data = items; v.Size = 2 * count; v.ElemSize = 2; return v;
                    }
                case SiiType.Int64Array:
                    {
                        long[] items = new long[count];
                        for (int i = 0; i < count; i++) { items[i] = BitConverter.ToInt64(r.D, r.P); r.P += 8; }
                        v.Data = items; v.Size = 8 * count; v.ElemSize = 8; return v;
                    }
                case SiiType.UInt64Array:
                    {
                        ulong[] items = new ulong[count];
                        for (int i = 0; i < count; i++) items[i] = r.U64();
                        v.Data = items; v.Size = 8 * count; v.ElemSize = 8; return v;
                    }
                case SiiType.BoolArray:
                    {
                        bool[] items = new bool[count];
                        for (int i = 0; i < count; i++) items[i] = r.U8() != 0;
                        v.Data = items; v.Size = count; v.ElemSize = 1; return v;
                    }
                case SiiType.SingleArray:
                    {
                        float[] items = new float[count];
                        for (int i = 0; i < count; i++) items[i] = r.F32();
                        v.Data = items; v.Size = 4 * count; v.ElemSize = 4; return v;
                    }
                case SiiType.Vec2Array:
                    {
                        float[][] items = new float[count][];
                        for (int i = 0; i < count; i++) items[i] = new float[] { r.F32(), r.F32() };
                        v.Data = items; v.Size = 8 * count; v.ElemSize = 8; return v;
                    }
                case SiiType.Vec3Array:
                    {
                        float[][] items = new float[count][];
                        for (int i = 0; i < count; i++) items[i] = new float[] { r.F32(), r.F32(), r.F32() };
                        v.Data = items; v.Size = 12 * count; v.ElemSize = 12; return v;
                    }
                case SiiType.Vec3IArray:
                    {
                        long[][] items = new long[count][];
                        for (int i = 0; i < count; i++) items[i] = new long[] { r.I32(), r.I32(), r.I32() };
                        v.Data = items; v.Size = 12 * count; v.ElemSize = 12; return v;
                    }
                case SiiType.Vec4Array:
                    {
                        float[][] items = new float[count][];
                        for (int i = 0; i < count; i++) items[i] = new float[] { r.F32(), r.F32(), r.F32(), r.F32() };
                        v.Data = items; v.Size = 16 * count; v.ElemSize = 16; return v;
                    }
                case SiiType.Vec8Array:
                    {
                        int start = r.P;
                        float[][] items = new float[count][];
                        for (int i = 0; i < count; i++) items[i] = ReadVec8(r, version);
                        v.Data = items; v.Size = r.P - v.Offset; v.ElemSize = (r.P - start) / Math.Max(1, count); return v;
                    }
                case SiiType.IdArray:
                case SiiType.Id2Array:
                case SiiType.Id3Array:
                    {
                        string[] items = new string[count];
                        for (int i = 0; i < count; i++) items[i] = ReadIdText(r, version);
                        v.Data = items; v.Size = r.P - v.Offset; v.ElemSize = 0; return v;
                    }
            }
            throw new SiiException(string.Format("不支持的字段类型 0x{0:x2} (偏移 {1})", t, v.Offset));
        }

        static float[] ReadVec8(Reader r, int version)
        {
            if (version == 1)
            {
                return new float[] { r.F32(), r.F32(), r.F32(), r.F32(), r.F32(), r.F32(), r.F32() };
            }
            float a = r.F32();
            float b = r.F32();
            float c = r.F32();
            float d = r.F32();
            float e = r.F32();
            float f = r.F32();
            float g = r.F32();
            float h = r.F32();
            int bias = (int)d;
            a += ((bias & 0xFFF) - 2048) << 9;
            c += (((bias >> 12) & 0xFFF) - 2048) << 9;
            return new float[] { a, b, c, e, f, g, h };
        }
    }

    public static class SiiTextWriter
    {
        public static void AppendValue(StringBuilder sb, string name, SiiValue v, string indent)
        {
            switch (v.Type)
            {
                case SiiType.Int64:
                case SiiType.UInt32B:
                case SiiType.Int32:
                case SiiType.UInt32:
                case SiiType.Int16:
                case SiiType.UInt16:
                    sb.Append(indent).Append(name).Append(": ").Append(((long)v.Data).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    return;
                case SiiType.UInt64:
                    sb.Append(indent).Append(name).Append(": ").Append(((ulong)v.Data).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    return;
                case SiiType.Single:
                    sb.Append(indent).Append(name).Append(": ").Append(FormatFloat((float)v.Data)).Append('\n');
                    return;
                case SiiType.Bool:
                    sb.Append(indent).Append(name).Append(": ").Append((bool)v.Data ? "true" : "false").Append('\n');
                    return;
                case SiiType.String:
                    sb.Append(indent).Append(name).Append(": ").Append(QuoteString(v)).Append('\n');
                    return;
                case SiiType.EncodedString:
                case SiiType.Ordinal:
                    sb.Append(indent).Append(name).Append(": ").Append((string)v.Data).Append('\n');
                    return;
                case SiiType.Id:
                case SiiType.Id2:
                case SiiType.Id3:
                    sb.Append(indent).Append(name).Append(": ").Append((string)v.Data).Append('\n');
                    return;
            }

            if (v.Type == SiiType.Vec2 || v.Type == SiiType.Vec3 || v.Type == SiiType.Vec4)
            {
                float[] f = (float[])v.Data;
                sb.Append(indent).Append(name).Append(": ");
                if (f.Length == 2) sb.Append('(').Append(FormatFloat(f[0])).Append(", ").Append(FormatFloat(f[1])).Append(')');
                else if (f.Length == 3) sb.Append('(').Append(FormatFloat(f[0])).Append(", ").Append(FormatFloat(f[1])).Append(", ").Append(FormatFloat(f[2])).Append(')');
                else sb.Append('(').Append(FormatFloat(f[0])).Append("; ").Append(FormatFloat(f[1])).Append(", ").Append(FormatFloat(f[2])).Append(", ").Append(FormatFloat(f[3])).Append(')');
                sb.Append('\n');
                return;
            }
            if (v.Type == SiiType.Vec3I)
            {
                long[] f = (long[])v.Data;
                sb.Append(indent).Append(name).Append(": (").Append(f[0]).Append(", ").Append(f[1]).Append(", ").Append(f[2]).Append(")\n");
                return;
            }
            if (v.Type == SiiType.Vec8)
            {
                float[] f = (float[])v.Data;
                sb.Append(indent).Append(name).Append(": (")
                  .Append(FormatFloat(f[0])).Append(", ").Append(FormatFloat(f[1])).Append(", ").Append(FormatFloat(f[2])).Append(") (")
                  .Append(FormatFloat(f[3])).Append("; ").Append(FormatFloat(f[4])).Append(", ").Append(FormatFloat(f[5])).Append(", ").Append(FormatFloat(f[6])).Append(")\n");
                return;
            }

            // arrays
            AppendArray(sb, name, v, indent);
        }

        static void AppendArray(StringBuilder sb, string name, SiiValue v, string indent)
        {
            sb.Append(indent).Append(name).Append(": ").Append(v.Count).Append('\n');
            switch (v.Type)
            {
                case SiiType.StringArray:
                    {
                        string[] items = (string[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(QuoteString(items[i])).Append('\n');
                        return;
                    }
                case SiiType.EncodedStringArray:
                case SiiType.IdArray:
                case SiiType.Id2Array:
                case SiiType.Id3Array:
                    {
                        string[] items = (string[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(items[i]).Append('\n');
                        return;
                    }
                case SiiType.Int32Array:
                case SiiType.UInt32Array:
                case SiiType.Int16Array:
                case SiiType.UInt16Array:
                case SiiType.Int64Array:
                    {
                        long[] items = (long[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(items[i]).Append('\n');
                        return;
                    }
                case SiiType.UInt64Array:
                    {
                        ulong[] items = (ulong[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(items[i]).Append('\n');
                        return;
                    }
                case SiiType.BoolArray:
                    {
                        bool[] items = (bool[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(items[i] ? "true" : "false").Append('\n');
                        return;
                    }
                case SiiType.SingleArray:
                    {
                        float[] items = (float[])v.Data;
                        for (int i = 0; i < items.Length; i++)
                            sb.Append(indent).Append(name).Append('[').Append(i).Append("]: ").Append(FormatFloat(items[i])).Append('\n');
                        return;
                    }
            }
            // vectors
            Array items2 = (Array)v.Data;
            for (int i = 0; i < v.Count; i++)
            {
                Array vec = (Array)items2.GetValue(i);
                sb.Append(indent).Append(name).Append('[').Append(i).Append("]: (");
                for (int k = 0; k < vec.Length; k++)
                {
                    if (k > 0) sb.Append(k == 1 && vec.Length == 4 ? "; " : ", ");
                    sb.Append(FormatFloat(Convert.ToSingle(vec.GetValue(k))));
                }
                sb.Append(")\n");
            }
        }

        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value != Math.Truncate(value) || Math.Abs(value) >= 1e7f)
            {
                byte[] b = BitConverter.GetBytes(value);
                return "&" + b[3].ToString("x2") + b[2].ToString("x2") + b[1].ToString("x2") + b[0].ToString("x2");
            }
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        public static string QuoteString(SiiValue v)
        {
            return QuoteBytes(v.Raw ?? new byte[0]);
        }

        public static string QuoteString(string text)
        {
            return QuoteBytes(Encoding.UTF8.GetBytes(text));
        }

        static string QuoteBytes(byte[] raw)
        {
            bool simple = raw.Length > 0;
            bool allDigits = true;
            for (int i = 0; i < raw.Length; i++)
            {
                byte b = raw[i];
                bool ok = (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'z') ||
                          (b >= (byte)'A' && b <= (byte)'Z') || b == (byte)'_';
                if (!ok) { simple = false; }
                if (b < (byte)'0' || b > (byte)'9') allDigits = false;
            }
            if (simple && !allDigits) return Encoding.ASCII.GetString(raw);

            StringBuilder sb = new StringBuilder(raw.Length + 2);
            sb.Append('"');
            for (int i = 0; i < raw.Length; i++)
            {
                byte b = raw[i];
                if (b == (byte)'"') sb.Append("\\\"");
                else if (b == (byte)'\\') sb.Append("\\\\");
                else if (b < 0x20 || b > 0x7E) sb.Append("\\x").Append(b.ToString("x2"));
                else sb.Append((char)b);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }

    public sealed class SiiException : Exception
    {
        public SiiException(string message) : base(message) { }
    }
}
