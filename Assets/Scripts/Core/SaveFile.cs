using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VoxelWilds.Core
{
    public static class SaveFile
    {
        private const string Header = "VOXEL-WILDS 4";
        public const int MaximumBytes = 64 * 1024 * 1024;
        public static string Read(string path, out bool recovered)
        {
            recovered = false;
            try { return ReadOne(path); }
            catch (Exception error) when (error is IOException || error is FormatException || error is UnauthorizedAccessException)
            {
                string backup = ReadOne(path + ".bak"); recovered = true; return backup;
            }
        }
        public static string ReadOne(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumBytes) throw new IOException("World file is missing or too large.");
            string value = File.ReadAllText(path, Encoding.UTF8);
            int first = value.IndexOf('\n'), second = first < 0 ? -1 : value.IndexOf('\n', first + 1);
            if (first < 0 || second < 0 || value.Substring(0, first) != Header) throw new FormatException("Unsupported world format.");
            string payload = value.Substring(second + 1);
            if (value.Substring(first + 1, second - first - 1) != Digest(payload)) throw new FormatException("World checksum failed.");
            return payload;
        }
        public static void Write(string path, string payload)
        {
            if (payload == null || Encoding.UTF8.GetByteCount(payload) > MaximumBytes - 100) throw new IOException("World is too large to save safely.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(Header + "\n" + Digest(payload) + "\n" + payload);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    bool valid;
                    try { ReadOne(path); valid = true; } catch (Exception error) when (error is IOException || error is FormatException) { valid = false; }
                    if (valid) File.Replace(temporary, path, path + ".bak");
                    else
                    {
                        File.Move(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
                        File.Move(temporary, path);
                    }
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static string Digest(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}
