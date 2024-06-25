using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TmxLib;

namespace SprExtractor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            args = new string[] { @"D:\Downloads\sprTest\i_battle_icon01.spr" };
            if (args.Length == 0)
            {
                Console.WriteLine("SprExtractor by Pioziomgames");
                Console.WriteLine("A program for quickly extracting all sprites as seperate images from a persona 3/4 spr file");
                Console.WriteLine("\nUsage:");
                Console.WriteLine("\tSprExtractor.exe pathToFile.spr (optional)Directory/to/extract/to");
                Console.WriteLine("\n\nPress any key to exit");
                Console.ReadKey();
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"Error: file {args[0]} doesn't exist!");
                Console.WriteLine("\n\nPress any key to exit");
                Console.ReadKey();
                return;
            }
            string extractDir;
            if (args.Length > 1)
                extractDir = args[1];
            else
            {
                string? fileDir = Path.GetDirectoryName(args[0]);
                if (fileDir == null)
                    fileDir = Directory.GetCurrentDirectory();
                extractDir = Path.Combine(fileDir, Path.GetFileNameWithoutExtension(args[0]));
            }

            SprFile spr = new SprFile(args[0]);
            Color[][] PixelData = new Color[spr.Textures.Count][];
            //The textures are probably indexed so it's better to unindex them once now
            //instead of doing it 40 times later
            for (int i = 0; i < spr.Textures.Count; i++)
                PixelData[i] = spr.Textures[i].GetPixelData();

            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);

            int digits = spr.Sprites.Count.ToString().Length; //Needed for making a nice looking file name

            Parallel.For(0, spr.Sprites.Count, i => //Use parallel processing because why not go faster if you can
            {
                if (spr.Sprites[i].Size.X > 0 && spr.Sprites[i].Size.Y > 0)
                {
                    //Calculate the texture holding just the current sprite data
                    Color[] sprite = new Color[spr.Sprites[i].Size.X * spr.Sprites[i].Size.Y];
                    int ogWidth = spr.Textures[spr.Sprites[i].TextureIndex].GetWidth();
                    for (int y = 0; y < spr.Sprites[i].Size.Y; y++)
                    {
                        for (int x = 0; x < spr.Sprites[i].Size.X; x++)
                        {
                            int originalIndex = (spr.Sprites[i].Position.Y + y) * ogWidth + (spr.Sprites[i].Position.X + x);
                            int spriteIndex = y * spr.Sprites[i].Size.X + x;
                            sprite[spriteIndex] = PixelData[spr.Sprites[i].TextureIndex][originalIndex];
                        }
                    }
                    //Transfer the raw pixel data into a bitmap object
                    Bitmap image = new(spr.Sprites[i].Size.X, spr.Sprites[i].Size.Y, PixelFormat.Format32bppArgb);
                    BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, image.PixelFormat);
                    byte[] pixels = new byte[sprite.Length * 4];
                    for (int j = 0; j < sprite.Length; j++)
                    {
                        Color color = sprite[j];
                        int offset = j * 4;
                        pixels[offset] = color.B;
                        pixels[offset + 1] = color.G;
                        pixels[offset + 2] = color.R;
                        pixels[offset + 3] = color.A;
                    }
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    image.UnlockBits(data);
                    //Save it as a png
                    image.Save(Path.Combine(extractDir, $"Sprite" + i.ToString("D" + digits) + ".png"), ImageFormat.Png);
                }
                else //Dummy sprite
                    File.Create(Path.Combine(extractDir, $"Sprite" + i.ToString("D" + digits) + ".dummy"));
            });
        }
    }

    public class SprSprite
    {
        public Point Position;
        public Point Size;
        public int TextureIndex;
        public Color[] Colors;
        public SprSprite() { }
        public SprSprite(Point position, Point size, int textureIndex, Color[] colors)
        {
            Position = position;
            Size = size;
            TextureIndex = textureIndex;
            Colors = colors;
        }
    }
    public class SprFile
    {
        public const uint Identifier = 0x30525053;
        public List<TmxFile> Textures;
        public List<SprSprite> Sprites;
        public SprFile(string Path)
        {
            using (BinaryReader reader = new(File.OpenRead(Path)))
                Read(reader);
        }
        public SprFile(byte[] Data)
        {
            Read(new BinaryReader(new MemoryStream(Data)));
        }
        public SprFile(BinaryReader reader)
        {
            Read(reader);
        }
        internal void Read(BinaryReader reader)
        {
            long fileStart = reader.BaseStream.Position;
            reader.BaseStream.Position += 8;
            uint identifier = reader.ReadUInt32();
            if (identifier != Identifier)
                throw new Exception("Not a proper spr file!");
            reader.BaseStream.Position += 8;
            ushort TextureCount = reader.ReadUInt16();
            ushort SpriteCount = reader.ReadUInt16();
            uint TextureOffset = reader.ReadUInt32();
            uint SpriteOffset = reader.ReadUInt32();
            long largestOff = 0;
            reader.BaseStream.Position = fileStart + TextureOffset;
            Textures = new List<TmxFile>();
            for (int i = 0; i < TextureCount; i++)
            {
                reader.BaseStream.Position += 4;
                uint offset = reader.ReadUInt32();
                long curOff = reader.BaseStream.Position;
                reader.BaseStream.Position = offset;
                Textures.Add(new TmxFile(reader));
                largestOff = reader.BaseStream.Position;
                reader.BaseStream.Position = curOff;
            }
            reader.BaseStream.Position = fileStart + SpriteOffset;
            Sprites = new List<SprSprite>();
            for (int i = 0; i < SpriteCount; i++)
            {
                reader.BaseStream.Position += 4;
                uint offset = reader.ReadUInt32();
                long curOff = reader.BaseStream.Position;
                reader.BaseStream.Position = offset + 20;

                int TextureId = reader.ReadInt32();
                reader.BaseStream.Position += 44;
                int XOffset = reader.ReadInt32();
                int YOffset = reader.ReadInt32();
                reader.BaseStream.Position += 8;
                int x1 = reader.ReadInt32();
                int y1 = reader.ReadInt32();
                int x2 = reader.ReadInt32();
                int y2 = reader.ReadInt32();
                Color[] colors = new Color[4];
                colors[0] = Color.FromArgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                colors[1] = Color.FromArgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                colors[2] = Color.FromArgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                colors[3] = Color.FromArgb(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());

                x1 += XOffset;
                x2 += XOffset;
                y1 += YOffset;
                y2 += YOffset;

                int width = x2 - x1;
                int height = y2 - y1;
                //Some unused sprites have incorrect bounds
                if (width > Textures[TextureId].GetWidth())
                    width -= width - Textures[TextureId].GetWidth();
                if (height > Textures[TextureId].GetHeight())
                    height -= height - Textures[TextureId].GetHeight();

                Sprites.Add(new SprSprite(new Point(x1, y1), new Point(width, height), TextureId, colors));
                if (reader.BaseStream.Position + 28 > largestOff)
                    largestOff = reader.BaseStream.Position + 28;
                reader.BaseStream.Position = curOff;
            }
            reader.BaseStream.Position = largestOff;
        }
    }
}