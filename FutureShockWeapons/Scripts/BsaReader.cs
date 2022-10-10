using DaggerfallConnect;
using DaggerfallConnect.Utility;
using System;
using System.IO;
using System.Text;

namespace FutureShock
{
    /// <summary>
    /// This reads BSA files in the format used by Arena and Future Shock.
    /// Daggerfall's BSA format is slightly different which is why this class's existence is necessary.
    /// </summary>
    public sealed class BsaReader : IDisposable
    {
        const ushort bsaIndexSize = 18; // name[12], u1, size, u2
        const ushort fileNameLength = 12;
        private readonly FileProxy bsaFile;
        public ushort IndexCount { get; private set; }
        public Tuple<string, ushort>[] IndexLookup;
        public BinaryReader Reader { get; private set; }

        public BsaReader(string path)
        {
            bsaFile = new FileProxy(path, FileUsage.UseMemory, true);
            Reader = bsaFile.GetReader();
            if (Reader == null)
                return;
            IndexCount = Reader.ReadUInt16();
            var indexSize = (uint)(IndexCount * bsaIndexSize);
            var indexOffset = (uint)(bsaFile.Buffer.Length - indexSize);
            // Read index
            Reader.BaseStream.Seek(indexOffset, SeekOrigin.Begin);
            IndexLookup = new Tuple<string, ushort>[IndexCount];
            var lookupIndex = 0;
            while (Reader.BaseStream.Position < indexOffset + indexSize)
            {
                var fileName = Encoding.UTF8.GetString(Reader.ReadBytes(fileNameLength)).Trim(new char[] { '\0' });
                Reader.ReadUInt16(); // Unused word
                var size = Reader.ReadUInt16(); // File size
                Reader.ReadUInt16(); // Unused word
                IndexLookup[lookupIndex++] = new Tuple<string, ushort>(fileName, size);
            }

            // Read data
            Reader.BaseStream.Seek(sizeof(ushort), SeekOrigin.Begin); // Seek past initial index count
        }

        public void Dispose()
        {
            var reader = bsaFile.GetReader();
            if (reader != null)
                reader.Close();
        }

        public string GetFileName(ushort index) => IndexLookup[index].Item1;
        public ushort GetFileLength(ushort index) => IndexLookup[index].Item2;
    }
}
