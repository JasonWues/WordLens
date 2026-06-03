using System;
using System.Buffers;
using SkiaSharp;

namespace WordLens.Features.Ocr;

internal static class OcrImageProcessor
{
    public static byte[] PreprocessBgraToPng(IntPtr pixels, int width, int height, int stride)
    {
        if (pixels == IntPtr.Zero)
            throw new ArgumentException("Pixel buffer address cannot be zero.", nameof(pixels));

        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image width and height must be greater than zero.");

        var minStride = checked(width * 4);
        if (stride < minStride)
            throw new ArgumentOutOfRangeException(nameof(stride), "Image stride must be at least width * 4.");

        var pixelCount = checked(width * height);
        var lumaBuffer = ArrayPool<byte>.Shared.Rent(pixelCount);
        try
        {
            var luma = lumaBuffer.AsSpan(0, pixelCount);

            BgraToLuma(pixels, width, height, stride, minStride, luma);
            StretchContrast(luma);
            SharpenLuma(luma, width, height);

            using var bitmap = BuildLumaBitmap(luma, width, height);
            using var scaledBitmap = ScaleForOcr(bitmap);

            return EncodePng(scaledBitmap ?? bitmap);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(lumaBuffer);
        }
    }

    private static unsafe void BgraToLuma(
        IntPtr pixels,
        int width,
        int height,
        int stride,
        int minStride,
        Span<byte> luma)
    {
        _ = checked(stride * height);

        var source = (byte*)pixels;
        var outputIndex = 0;

        for (var y = 0; y < height; y++)
        {
            var row = source + checked(y * stride);
            for (var x = 0; x < minStride; x += 4)
            {
                var b = row[x];
                var g = row[x + 1];
                var r = row[x + 2];
                var a = row[x + 3];

                var value = (r * 77 + g * 150 + b * 29) >> 8;
                luma[outputIndex++] = a < 255
                    ? (byte)((value * a + 255 * (255 - a)) / 255)
                    : (byte)value;
            }
        }
    }

    private static unsafe SKBitmap BuildLumaBitmap(ReadOnlySpan<byte> luma, int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        if (bitmap.IsNull)
            throw new InvalidOperationException("Failed to allocate grayscale OCR bitmap.");

        var destination = (byte*)bitmap.GetPixels();
        if (destination is null)
            throw new InvalidOperationException("Failed to access grayscale OCR bitmap pixels.");

        for (var y = 0; y < height; y++)
        {
            var sourceRow = luma.Slice(checked(y * width), width);
            var destinationRow = new Span<byte>(destination + checked(y * bitmap.RowBytes), width);
            sourceRow.CopyTo(destinationRow);
        }

        return bitmap;
    }

    private static byte PercentileFromHistogram(ReadOnlySpan<uint> histogram, uint total, double percentile)
    {
        if (total == 0)
            return 0;

        var threshold = (uint)Math.Round((total - 1) * percentile);
        var cumulative = 0u;

        for (var value = 0; value < histogram.Length; value++)
        {
            cumulative += histogram[value];
            if (cumulative > threshold)
                return (byte)value;
        }

        return 255;
    }

    private static void StretchContrast(Span<byte> buffer)
    {
        Span<uint> histogram = stackalloc uint[256];
        foreach (var value in buffer)
            histogram[value]++;

        var total = checked((uint)buffer.Length);
        var low = PercentileFromHistogram(histogram, total, 0.01);
        var high = PercentileFromHistogram(histogram, total, 0.99);

        if (high <= low + 8)
            return;

        var range = high - low;
        for (var i = 0; i < buffer.Length; i++)
        {
            var normalized = Math.Round((Math.Max(buffer[i] - low, 0) / (double)range) * 255);
            buffer[i] = (byte)Math.Clamp(normalized, 0, 255);
        }
    }

    private static void SharpenLuma(Span<byte> buffer, int width, int height)
    {
        if (width < 3 || height < 3)
            return;

        var sourceBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var source = sourceBuffer.AsSpan(0, buffer.Length);
            buffer.CopyTo(source);

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var index = y * width + x;
                    var sum = 0u;

                    for (var offsetY = y - 1; offsetY <= y + 1; offsetY++)
                    {
                        for (var offsetX = x - 1; offsetX <= x + 1; offsetX++)
                            sum += source[offsetY * width + offsetX];
                    }

                    var blurred = sum / 9.0;
                    var original = source[index];
                    var sharpened = Math.Round(original + (original - blurred) * 0.65);
                    buffer[index] = (byte)Math.Clamp(sharpened, 0, 255);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBuffer);
        }
    }

    private static SKBitmap? ScaleForOcr(SKBitmap bitmap)
    {
        var maxSide = Math.Max(bitmap.Width, bitmap.Height);
        var scale = maxSide < 900 ? 3 : maxSide < 1600 ? 2 : 1;

        if (scale <= 1)
            return null;

        var scaled = new SKBitmap(new SKImageInfo(
            checked(bitmap.Width * scale),
            checked(bitmap.Height * scale),
            bitmap.ColorType,
            bitmap.AlphaType));

        if (scaled.IsNull)
            throw new InvalidOperationException("Failed to allocate scaled OCR bitmap.");

        if (bitmap.ScalePixels(scaled, new SKSamplingOptions(SKCubicResampler.CatmullRom)))
            return scaled;

        scaled.Dispose();
        throw new InvalidOperationException("Failed to scale OCR bitmap.");
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap)
            ?? throw new InvalidOperationException("Failed to create OCR image for PNG encoding.");
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Failed to encode OCR image as PNG.");
        return data.ToArray();
    }
}
