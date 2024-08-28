using GJ.IO;
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
            if (args.Length == 0)
            {
                Console.WriteLine("SprExtractor 1.5 by Pioziomgames");
                Console.WriteLine("A program for quickly extracting all sprites as seperate images from a persona 3/4 spr file");
                Console.WriteLine("\nUsage:");
                Console.WriteLine("\nExtract sprites as seperate images:");
                Console.WriteLine("\tSprExtractor.exe pathToFile.spr (optional)Directory/to/extract/to");
                Console.WriteLine("\nExtract sprites bounds and base textures:");
                Console.WriteLine("\tSprExtractor.exe pathToFile.spr -b (optional)Directory/to/extract/to");
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
            bool bounds = false;
            string extractDir = string.Empty;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-b")
                    bounds = true;
                else
                    extractDir = args[i];
            }
            if (extractDir == string.Empty)
            {
                string? fileDir = Path.GetDirectoryName(args[0]);
                if (fileDir == null)
                    fileDir = Directory.GetCurrentDirectory();
                extractDir = Path.Combine(fileDir, Path.GetFileNameWithoutExtension(args[0]));
            }

            SprFile spr = new SprFile(args[0]);
            
            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);


            if (!bounds)
            {
                Color[][] PixelData = new Color[spr.Textures.Count][];
                //The textures are probably indexed so it's better to unindex them once now
                //instead of doing it 40 times later

                for (int i = 0; i < spr.Textures.Count; i++)
                    PixelData[i] = spr.Textures[i].GetPixelData();

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
            else
            {
                //Discard unused sprites
                List<SprSprite> usedSprites = spr.Sprites.Where(x => x.Size.X > 0 && x.Size.Y > 0).ToList();

                Parallel.For(0, spr.Textures.Count, i =>
                {
                    //Find sprites that use this texture
                    List<SprSprite> currentSprites = spr.Sprites.Where(x => x.TextureIndex == i).ToList();

                    //Get the name of the texture
                    string textureName = spr.Textures[i].Picture.Header.UserComment;

                    //Save the texture as png
                    Bitmap currentTexture = BitMapMethods.GetTmxBitmap(spr.Textures[i]);
                    currentTexture.Save(Path.Combine(extractDir, textureName + ".png"), ImageFormat.Png);

                    //Create a texture that will hold the bounds
                    int width = currentTexture.Width;
                    int height = currentTexture.Height;
                    Bitmap image = new(width, height, PixelFormat.Format32bppArgb);
                    BitmapData data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, image.PixelFormat);
                    byte[] pixels = new byte[width * height * 4];

                    void SetPixel(int x, int y, Color color)
                    {
                        int index = (y * width + x) * 4;
                        pixels[index] = color.B;
                        pixels[index + 1] = color.G;
                        pixels[index + 2] = color.R;
                        pixels[index + 3] = color.A;
                    }

                    //Draw sprite bounds
                    for (int j = 0; j < currentSprites.Count; j++)
                    {
                        int x = currentSprites[j].Position.X;
                        int y = currentSprites[j].Position.Y;
                        int xs = currentSprites[j].Size.X;
                        int ys = currentSprites[j].Size.Y;
                        //Top
                        for (int k = x; k < x + xs; k++)
                            SetPixel(k, y, Color.Red);
                        //Bottom
                        for (int k = x; k < x + xs; k++)
                            SetPixel(k, y + ys - 1, Color.Red);
                        //Left
                        for (int k = y; k < y + ys; k++)
                            SetPixel(x, k, Color.Red);
                        //Right
                        for (int k = y; k < y + ys; k++)
                            SetPixel(x + xs - 1, k, Color.Red);
                    }
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    image.UnlockBits(data);
                    //save the result as a png
                    image.Save(Path.Combine(extractDir, textureName + "_bounds.png"), ImageFormat.Png);
                });
            }
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

                //x1 += XOffset;
                //x2 += XOffset;
                //y1 += YOffset;
                //y2 += YOffset;

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