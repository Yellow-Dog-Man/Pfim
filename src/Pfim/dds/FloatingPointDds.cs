using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Pfim
{
    public class FloatingPointDds : Dds
    {
        private ImageFormat _format;
        private int _bitsPerPixel;
        private MipMapOffset[] _mipMaps = new MipMapOffset[0];

        public override int BitsPerPixel => _bitsPerPixel;

        public override ImageFormat Format => _format;

        public override bool Compressed => false;

        public override MipMapOffset[] MipMaps => _mipMaps;

        public FloatingPointDds(ImageFormat format, DdsHeader header, PfimConfig config) : base(header, config)
        {
            _format = format;

            switch(format)
            {
                case ImageFormat.R_FP16:
                    _bitsPerPixel = 16;
                    break;

                case ImageFormat.R_FP32:
                    _bitsPerPixel = 32;
                    break;

                case ImageFormat.Rgba_FP16:
                    _bitsPerPixel = 16 * 4;
                    break;

                case ImageFormat.Rgba_FP32:
                    _bitsPerPixel = 32 * 4;
                    break;

                default:
                    throw new ArgumentException("Invalid ImageFormat for floating point DDS: " + format);
            }
        }

        public override void Decompress()
        {
        }

        /// <summary>Calculates the number of bytes to hold image data</summary>
        private int CalcSize() => Stride * Height;

        private int AllocateMipMaps()
        {
            var len = CalcSize();

            if (Header.MipMapCount <= 1)
            {
                return len;
            }

            _mipMaps = new MipMapOffset[Header.MipMapCount - 1];
            var totalLen = len;

            for (int i = 0; i < Header.MipMapCount - 1; i++)
            {
                int width = (int)Math.Max(1, (int)(Header.Width / Math.Pow(2, i + 1)));
                int height = (int)Math.Max(1, Header.Height / Math.Pow(2, i + 1));
                int stride = Util.Stride(width, BitsPerPixel);
                len = stride * height;

                _mipMaps[i] = new MipMapOffset(width, height, stride, totalLen, len);
                totalLen += len;
            }

            return totalLen;
        }

        protected override void Decode(Stream str, PfimConfig config)
        {
            DataLen = CalcSize();
            var totalLen = AllocateMipMaps();
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

            Data = data;
        }
    }
}
