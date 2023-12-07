using System;
using System.IO;

namespace Pfim
{
    /// <summary>
    /// A DirectDraw Surface that is not compressed.  
    /// Thus what is in the input stream gets directly translated to the image buffer.
    /// </summary>
    public class UncompressedDds : Dds
    {
        private readonly uint? _bitsPerPixel;
        private readonly bool? _rgbSwapped;
        private ImageFormat _format;
        private MipMapOffset[] _mipMaps = new MipMapOffset[0];


        internal UncompressedDds(DdsHeader header, PfimConfig config, uint bitsPerPixel, bool rgbSwapped) : base(header, config)
        {
            _bitsPerPixel = bitsPerPixel;
            _rgbSwapped = rgbSwapped;
        }

        internal UncompressedDds(DdsHeader header, PfimConfig config) : base(header, config)
        {

        }

        public override int BitsPerPixel => ImageInfo().Depth;
        public override ImageFormat Format => _format;
        public override bool Compressed => false;
        public override MipMapOffset[] MipMaps => _mipMaps;

        public override void Decompress()
        {
        }

        protected override void Decode(Stream stream, PfimConfig config)
        {
            Data = DataDecode(stream, config);
        }

        /// <summary>Determine image info from header</summary>
        public DdsLoadInfo ImageInfo()
        {
            bool rgbSwapped = _rgbSwapped ?? Header.PixelFormat.RBitMask < Header.PixelFormat.GBitMask;

            switch (_bitsPerPixel ?? Header.PixelFormat.RGBBitCount)
            {
                case 8:
                    return new DdsLoadInfo(false, rgbSwapped, true, 1, 1, 8, EightBitImageFormat());
                case 16:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 2, 16, SixteenBitImageFormat());
                case 24:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 3, 24, ImageFormat.Rgb24);
                case 32:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 4, 32, ThirtyTwoBitImageFormat());
                default:
                    throw new Exception($"Unrecognized rgb bit count: {Header.PixelFormat.RGBBitCount}");
            }
        }

        private ImageFormat EightBitImageFormat()
        {
            var pf = Header.PixelFormat;

            if (pf.ABitMask == 0xFF && pf.RBitMask == 0 && pf.GBitMask == 0 && pf.BBitMask == 0)
                return ImageFormat.A8;

            throw new NotImplementedException("Unrecognized 8-bit image format");
        }

        private ImageFormat SixteenBitImageFormat()
        {
            var pf = Header.PixelFormat;

            if (pf.ABitMask == 0xF000 && pf.RBitMask == 0xF00 && pf.GBitMask == 0xF0 && pf.BBitMask == 0xF)
            {
                return ImageFormat.Rgba16;
            }

            if (pf.ABitMask == 0xFF00 && pf.RBitMask == 0xFF && pf.GBitMask == 0 && pf.BBitMask == 0)
            {
                return ImageFormat.Ra16;
            }

            if (pf.PixelFormatFlags.HasFlag(DdsPixelFormatFlags.AlphaPixels))
            {
                return ImageFormat.R5g5b5a1;
            }

            return pf.GBitMask == 0x7e0 ? ImageFormat.R5g6b5 : ImageFormat.R5g5b5;
        }

        private ImageFormat ThirtyTwoBitImageFormat()
        {
            var pf = Header.PixelFormat;

            // This is for DX10 formats
            if (pf.RBitMask == 0 && pf.GBitMask == 0 && pf.BBitMask == 0 && pf.ABitMask == 0)
                return ImageFormat.Rgba32;

            if (pf.RBitMask == 0xFF &&
                pf.GBitMask == 0xFF00 &&
                pf.BBitMask == 0xFF0000 &&
                pf.ABitMask == 0xFF000000)
                return ImageFormat.Rgba32;

            if (pf.RBitMask == 0xFF0000 &&
                pf.GBitMask == 0xFF00 &&
                pf.BBitMask == 0xFF &&
                pf.ABitMask == 0xFF000000)
                return ImageFormat.Rgba32;

            if (pf.RBitMask == 0xFFFF &&
                pf.GBitMask == 0xFFFF0000 &&
                pf.BBitMask == 0 &&
                pf.ABitMask == 0)
                return ImageFormat.Rg32;

            throw new NotImplementedException("Unrecognized 32-bit image format");
        }

        /// <summary>Calculates the number of bytes to hold image data</summary>
        private int CalcSize(DdsLoadInfo info)
        {
            int height = (int)Math.Max(info.DivSize, Header.Height);
            return Stride * height;
        }

        private int AllocateMipMaps(DdsLoadInfo info)
        {
            var len = CalcSize(info);

            if (Header.MipMapCount <= 1)
            {
                return len;
            }

            _mipMaps = new MipMapOffset[Header.MipMapCount - 1];
            var totalLen = len;

            for (int i = 0; i < Header.MipMapCount - 1; i++)
            {
                int width = (int)Math.Max(info.DivSize, (int)(Header.Width / Math.Pow(2, i + 1)));
                int height = (int)Math.Max(info.DivSize, Header.Height / Math.Pow(2, i + 1));
                int stride = Util.Stride(width, BitsPerPixel);
                len = stride * height;

                _mipMaps[i] = new MipMapOffset(width, height, stride, totalLen, len);
                totalLen += len;
            }

            return totalLen;
        }

        private static void SwapLevelRgb24(byte[] data, MipMapOffset mip)
        {
            for (int y = 0; y < mip.Height; y++)
            {
                var rowOffset = mip.DataOffset + y * mip.Stride;
                for (int x = 0; x < mip.Width; x++)
                {
                    var i = rowOffset + x * 3;
                    byte temp = data[i];
                    data[i] = data[i + 2];
                    data[i + 2] = temp;
                }
            }
        }

        /// <summary>Decode data into raw rgb format</summary>
        private byte[] DataDecode(Stream str, PfimConfig config)
        {
            var imageInfo = ImageInfo();
            _format = imageInfo.Format;

            DataLen = CalcSize(imageInfo);
            var totalLen = AllocateMipMaps(imageInfo);
            byte[] data = config.Allocator.Rent(totalLen);

            var stride = Util.Stride((int)Header.Width, BitsPerPixel);
            var width = (int)Header.Width;
            var len = DataLen;

            if (width * BytesPerPixel == stride)
            {
                Util.Fill(str, data, len, config.BufferSize);
            }
            else
            {
                Util.InnerFillUnaligned(str, data, len, width * BytesPerPixel, stride, config.BufferSize);
            }

            foreach (var mip in _mipMaps)
            {
                if (mip.Width * BytesPerPixel == mip.Stride)
                {
                    Util.Fill(str, data, mip.DataLen, config.BufferSize, mip.DataOffset);
                }
                else
                {
                    Util.InnerFillUnaligned(str, data, mip.DataLen, mip.Width * BytesPerPixel, mip.Stride, config.BufferSize, mip.DataOffset);
                }
            }

            // Swap the R and B channels
            if (imageInfo.Swap)
            {
                switch (imageInfo.Format)
                {
                    case ImageFormat.Rgb24:
                        SwapLevelRgb24(data, new MipMapOffset(width, (int)Header.Height, stride, 0, 0));
                        foreach (var mip in _mipMaps)
                        {
                            SwapLevelRgb24(data, mip);
                        }
                        break;
                    case ImageFormat.Rgba32:
                        for (int i = 0; i < totalLen; i += 4)
                        {
                            byte temp = data[i];
                            data[i] = data[i + 2];
                            data[i + 2] = temp;
                        }
                        break;
                    case ImageFormat.Rgba16:
                        for (int i = 0; i < totalLen; i += 2)
                        {
                            byte temp = (byte)(data[i] & 0xF);
                            data[i] = (byte)((data[i] & 0xF0) + (data[i + 1] & 0XF));
                            data[i + 1] = (byte)((data[i + 1] & 0xF0) + temp);

                        }
                        break;

                    case ImageFormat.Rg32:
                        for (int i = 0; i < totalLen; i += 4)
                        {
                            (data[i], data[i + 2]) = (data[i + 2], data[i]);
                            (data[i + 1], data[i + 3]) = (data[i + 3], data[i + 1]);
                        }
                        break;

                    default:
                        throw new Exception($"Do not know how to swap {imageInfo.Format}");
                }
            }

            return data;
        }
    }
}
