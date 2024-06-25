using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GJ.IO
{
    public abstract class Texture
    {
        public Texture()
        {

        }
        public Texture(string Path)
        {
            using (BinaryReader reader = new(File.OpenRead(Path)))
                Read(reader);
        }
        public Texture(byte[] Data)
        {
            Read(new BinaryReader(new MemoryStream(Data)));
        }
        public Texture(BinaryReader reader)
        {
            Read(reader);
        }
        public byte[] Save()
        {
            MemoryStream ms = new();
            using (BinaryWriter bw = new(ms))
                Save(bw);
            return ms.ToArray();
        }
        public void Save(string Path)
        {
            using (BinaryWriter writer = new(File.Create(Path)))
            {
                Write(writer);
                writer.Flush();
                writer.Close();
            }
        }
        public void Save(BinaryWriter writer)
        {
            Write(writer);
        }
        public abstract int GetWidth();
        public abstract int GetHeight();
        public abstract int GetMipMapCount();
        public abstract PixelFormat GetPixelFormat();
        public abstract Color[] GetPalette();
        public abstract Color[] GetPixelData();
        public abstract byte[] GetIndexData();
        public Color[] GetPixelDataFromIndexData()
        {
            Color[] pal = GetPalette();
            if (pal.Length == 0)
                return GetPixelData();
            byte[] ind = GetIndexData();

            Color[] full = new Color[ind.Length];
            for (int i = 0; i < ind.Length; i++)
                full[i] = pal[ind[i]];
            return full;
        }
        internal abstract void Read(BinaryReader reader);
        internal abstract void Write(BinaryWriter writer);
    }
}
