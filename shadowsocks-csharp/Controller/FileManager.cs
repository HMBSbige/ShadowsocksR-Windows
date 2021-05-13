using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Shadowsocks.Controller
{
    public static class FileManager
    {
        public static bool ByteArrayToFile(string fileName, byte[] content)
        {
            try
            {
                var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                fileStream.Write(content, 0, content.Length);
                fileStream.Close();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Exception caught in process: {ex}");
            }
            return false;
        }

        public static void DecompressFile(string fileName, byte[] content)
        {
            var destinationFile = File.Create(fileName);

            // Because the uncompressed size of the file is unknown, 
            // we are using an arbitrary buffer size.
            var buffer = new byte[4096];

            using (var input = new GZipStream(new MemoryStream(content), CompressionMode.Decompress, false))
            {
                while (true)
                {
                    var n = input.Read(buffer, 0, buffer.Length);
                    if (n == 0)
                    {
                        break;
                    }
                    destinationFile.Write(buffer, 0, n);
                }
            }
            destinationFile.Close();
        }

        public static byte[] DeflateCompress(byte[] content, int index, int count, out int size)
        {
            size = 0;
            try
            {
                var memStream = new MemoryStream();
                using (var ds = new DeflateStream(memStream, CompressionMode.Compress))
                {
                    ds.Write(content, index, count);
                }
                var buffer = memStream.ToArray();
                size = buffer.Length;
                return buffer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Exception caught in process: {ex}");
            }
            return null;
        }

        public static byte[] DeflateDecompress(byte[] content, int index, int count, out int size)
        {
            size = 0;
            try
            {
                var buffer = new byte[16384];
                var ds = new DeflateStream(new MemoryStream(content, index, count), CompressionMode.Decompress);
                while (true)
                {
                    var readSize = ds.Read(buffer, size, buffer.Length - size);
                    if (readSize == 0)
                    {
                        break;
                    }
                    size += readSize;
                    var newBuffer = new byte[buffer.Length * 2];
                    buffer.CopyTo(newBuffer, 0);
                    buffer = newBuffer;
                }
                return buffer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Exception caught in process: {ex}");
            }
            return null;
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public static async Task<bool> ZipCompressToFile(string path)
        {
            try
            {
                var filename = Path.GetFileName(path);
                var zipFilePath = $@"{Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))}.zip";
                using (var zipFileToOpen = new FileStream(zipFilePath, FileMode.Create))
                {
                    using var archive = new ZipArchive(zipFileToOpen, ZipArchiveMode.Create);
                    var readMeEntry = archive.CreateEntry(filename);
                    using var zipStream = readMeEntry.Open();
                    using var stream = File.Open(path, FileMode.Open);
                    var bytes = new byte[(int)stream.Length];
                    var totalBytesRead = 0;
                    while (totalBytesRead < bytes.Length)
                    {
                        totalBytesRead += await stream.ReadAsync(bytes, totalBytesRead, bytes.Length - totalBytesRead);
                    }

                    await zipStream.WriteAsync(bytes, 0, bytes.Length);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string NonExclusiveReadAllText(string path)
        {
            return NonExclusiveReadAllText(path, Encoding.UTF8);
        }

        public static string NonExclusiveReadAllText(string path, Encoding encoding)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, encoding);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Logging.Error(ex);
                throw;
            }
        }
    }
}
