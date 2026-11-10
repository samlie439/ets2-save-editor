using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Ets2Save
{
    /// <summary>Reads SCS "ScsC" containers (AES-256-CBC + zlib).</summary>
    public static class Scsc
    {
        public const uint SigScsc = 0x43736353; // ScsC
        public const uint SigPlain = 0x4E696953; // SiiN
        public const uint SigBsii = 0x49495342; // BSII

        public const int HeaderSize = 56;

        static readonly byte[] Key = new byte[]
        {
            0x2A, 0x5F, 0xCB, 0x17, 0x91, 0xD2, 0x2F, 0xB6,
            0x02, 0x45, 0xB3, 0xD8, 0x36, 0x9E, 0xD0, 0xB2,
            0xC2, 0x73, 0x71, 0x56, 0x3F, 0xBF, 0x1F, 0x3C,
            0x9E, 0xDF, 0x6B, 0x11, 0x82, 0x5A, 0x5D, 0x0A
        };

        public static string DetectFormat(byte[] raw)
        {
            if (raw == null || raw.Length < 4) return "unknown";
            uint sig = BitConverter.ToUInt32(raw, 0);
            if (sig == SigScsc) return "scsc";
            if (sig == SigBsii) return "bsii";
            if (sig == SigPlain) return "text";
            return "unknown";
        }

        /// <summary>Returns the plain payload (BSII or text) and the container kind.</summary>
        public static byte[] LoadPayload(string path, out string container)
        {
            byte[] raw = File.ReadAllBytes(path);
            container = DetectFormat(raw);
            if (container == "scsc") return Decrypt(raw);
            if (container == "bsii" || container == "text") return raw;
            if (raw.Length > 4 && raw[0] == (byte)'S' && raw[1] == (byte)'i' && raw[2] == (byte)'i')
                return raw;
            throw new InvalidDataException("无法识别的文件格式");
        }

        public static byte[] Decrypt(byte[] raw)
        {
            if (raw.Length < HeaderSize) throw new InvalidDataException("文件太小");
            uint sig = BitConverter.ToUInt32(raw, 0);
            if (sig != SigScsc) throw new InvalidDataException("不是 ScsC 容器");
            byte[] iv = new byte[16];
            Buffer.BlockCopy(raw, 36, iv, 0, 16);
            int payloadLen = raw.Length - HeaderSize;

            byte[] decrypted;
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform dec = aes.CreateDecryptor())
                {
                    decrypted = dec.TransformFinalBlock(raw, HeaderSize, payloadLen);
                }
            }
            return Inflate(decrypted);
        }

        static byte[] Inflate(byte[] zlib)
        {
            // zlib: 2 byte header, deflate data, 4 byte adler32
            using (MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (DeflateStream inflater = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                inflater.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
