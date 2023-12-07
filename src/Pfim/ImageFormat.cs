namespace Pfim
{
    /// <summary>Describes how pixel data is arranged</summary>
    public enum ImageFormat
    {
        /// <summary>Alpha only, single byte</summary>
        A8,

        /// <summary>Red and alpha only, contained in two bytes</summary>
        Ra16,

        /// <summary>Red, green, and blue are the same values contained in a single byte</summary>
        Rgb8,

        /// <summary>Red, green, and blue are contained in a two bytes</summary>
        R5g5b5,

        R5g6b5,

        R5g5b5a1,

        Rgba16,

        /// <summary>Red, green, and blue channels are 8 bits apiece</summary>
        Rgb24,

        /// <summary>
        /// Red, green, blue, and alpha are 8 bits apiece
        /// </summary>
        Rgba32,

        /// <summary>
        /// Red and green, each 16 bits apiece
        /// </summary>
        Rg32,

        /// <summary>
        /// Floating point formats
        /// </summary>
        R_FP16,
        R_FP32,
        Rgba_FP16,
        Rgba_FP32,
    }
}
