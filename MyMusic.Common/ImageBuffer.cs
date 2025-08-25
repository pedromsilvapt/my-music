using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using TagLib;

namespace MyMusic.Common;

public class ImageBuffer
{
    public byte[] Data { get; protected set; }

    public ImageFormat Format { get; protected set; }

    public string MimeType => GetMimeTypeFromImageFormat(Format);

    public Size Size => ToImage().Size;

    protected ImageBuffer(byte[] data, ImageFormat format)
    {
        Data = data;
        Format = format;
    }

    public ImageBuffer ToFormat(ImageFormat targetFormat)
    {
        if (Format.Guid == targetFormat.Guid)
        {
            return this;
        }

        var imageObject = new Bitmap(new MemoryStream(Data));

        var targetStream = new MemoryStream();
        imageObject.Save(targetStream, targetFormat);

        return new ImageBuffer(targetStream.ToArray(), targetFormat);
    }

    public IPicture ToPicture(PictureType pictureType = PictureType.FrontCover)
    {
        return new Picture(new ByteVector(Data, Data.Length))
        {
            Type = pictureType,
            MimeType = MimeType
        };
    }

    public string ToBase64Url()
    {
        return "data:" + MimeType + ";base64," + Convert.ToBase64String(Data);
    }

    public Image ToImage()
    {
        var dataStream = new MemoryStream(Data, writable: false);
        return Image.FromStream(dataStream);
    }

    /// <summary>
    /// Resize the image to the specified width and height.
    /// </summary>
    /// <param name="image">The image to resize.</param>
    /// <param name="width">The width to resize to.</param>
    /// <param name="height">The height to resize to.</param>
    /// <returns>The resized image.</returns>
    public ImageBuffer ToResized(int width, int height = -1)
    {
        var image = ToImage();

        if (width >= 0 && height <= -1)
        {
            var aspectRatio = (double)image.Height / image.Width;

            height = (int)(width * aspectRatio);
        }

        if (width <= 0 && height >= -1)
        {
            var aspectRatio = image.Width / image.Height;

            width = height * aspectRatio;
        }

        var destRect = new Rectangle(0, 0, width, height);
        var destImage = new Bitmap(width, height);

        // Removed when migrating to Linux because `image.HorizontalResolution` and `image.VerticalResolution`
        // were given as 0 on some images, causing an exception to be thrown
        // destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        using (var graphics = Graphics.FromImage(destImage))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var wrapMode = new ImageAttributes())
            {
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }
        }

        var targetStream = new MemoryStream();
        destImage.Save(targetStream, Format);

        return new ImageBuffer(targetStream.ToArray(), Format);
    }

    public static ImageBuffer FromBase64Url(string url)
    {
        int? semicolonIndex = null;

        // Cap the search in the first 30 characters
        for (int i = "data:".Length + 1; i < url.Length && i < 30; i++)
        {
            if (url[i] == ';')
            {
                semicolonIndex = i;
                break;
            }
        }

        if (semicolonIndex is null)
        {
            throw new Exception("Invalid base64 data url format, expected a semicolon after a mime type string.");
        }

        int commaIndex = semicolonIndex.Value + "base64,".Length;

        if (url.Length > commaIndex + 1 + "base64,".Length &&
            string.Equals(url.Substring(commaIndex + 1, "base64,".Length), "base64,"))
        {
            throw new Exception("Invalid base64 data url format, expected the string 'base64,' after the mime type.");
        }

        int start = "data:".Length;
        int length = semicolonIndex.Value - start;

        // Extract the mimetype and the base64 contents
        var mimeType = url.Substring(start, length);
        var data = url.Substring(commaIndex + 1);

        // Prepare the values to initialize the ImageBuffer class
        var dataBytes = Convert.FromBase64String(data);
        var imageFormat = GetImageFormatFromMimeType(mimeType);

        return new ImageBuffer(dataBytes, imageFormat);
    }

    public static ImageBuffer FromWebUrl(string url)
    {
        string? mimeType = null;
        // We know that by the time `data` is used, it will not be null, because the task will have executed
        byte[] data = null!;

        Task.Run(async () =>
        {
            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url));

            mimeType = response.Content.Headers.ContentType?.MediaType;

            data = await response.Content.ReadAsByteArrayAsync();
        }).Wait();

        ImageFormat format;

        if (mimeType is null)
        {
            var stream = new MemoryStream(data, writable: false);
            using var image = Image.FromStream(stream);

            format = image.RawFormat;
        }
        else
        {
            format = GetImageFormatFromMimeType(mimeType);
        }

        return new ImageBuffer(data, format);
    }

    public static ImageBuffer FromFile(string filePath, string? mimeType = null)
    {
        if (mimeType is null)
        {
            var extension = Path.GetExtension(filePath);

            mimeType = GetMimeTypeFromFileExtension(extension);

            if (mimeType is null)
            {
                throw new Exception($"No mime type could be derived from the file extension {extension}");
            }
        }

        var response = System.IO.File.ReadAllBytes(filePath);

        var stream = new MemoryStream();
        using var image = Image.FromStream(stream);

        return new ImageBuffer(response, image.RawFormat);
    }

    public static ImageBuffer FromString(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return FromBase64Url(url);
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return FromWebUrl(url);
        }
        else
        {
            return FromFile(url);
        }
    }

    public static async Task<ImageBuffer> FromWebUrlAsync(string url)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url));

        string? mimeType = response.Content.Headers.ContentType?.MediaType;

        byte[] data = await response.Content.ReadAsByteArrayAsync();

        ImageFormat format;

        if (mimeType is null)
        {
            var stream = new MemoryStream(data, writable: false);
            using var image = Image.FromStream(stream);

            format = image.RawFormat;
        }
        else
        {
            format = GetImageFormatFromMimeType(mimeType);
        }

        return new ImageBuffer(data, format);
    }

    public static async Task<ImageBuffer> FromFileAsync(string filePath, string? mimeType = null)
    {
        if (mimeType is null)
        {
            var extension = Path.GetExtension(filePath);

            mimeType = GetMimeTypeFromFileExtension(extension);

            if (mimeType is null)
            {
                throw new Exception($"No mime type could be derived from the file extension {extension}");
            }
        }

        var response = await System.IO.File.ReadAllBytesAsync(filePath);

        var imageFormat = GetImageFormatFromMimeType(mimeType);

        return new ImageBuffer(response, imageFormat);
    }

    public static Task<ImageBuffer> FromStringAsync(string url, CancellationToken cancellationToken = default)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(FromBase64Url(url));
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return FromWebUrlAsync(url);
        }
        else
        {
            return FromFileAsync(url);
        }
    }

    public static ImageBuffer FromPicture(IPicture picture)
    {
        var imageFormat = GetImageFormatFromMimeType(picture.MimeType);

        return new ImageBuffer(picture.Data.Data, imageFormat);
    }

    public static ImageBuffer FromResource(string resourceUri, string? mimeType = null)
    {
        var assembly = Assembly.GetExecutingAssembly();

        return FromResource(assembly, resourceUri, mimeType);
    }

    public static ImageBuffer FromResource(Assembly assembly, string resourceUri, string? mimeType = null)
    {
        var bufferStream = new MemoryStream();

        using (Stream stream = assembly.GetManifestResourceStream(resourceUri)!)
        {
            stream.CopyTo(bufferStream);
        }

        if (mimeType is null)
        {
            var extension = Path.GetExtension(resourceUri);

            mimeType = GetMimeTypeFromFileExtension(extension);

            if (mimeType is null)
            {
                throw new Exception($"No mime type could be derived from the file extension {extension}");
            }
        }

        var imageFormat = GetImageFormatFromMimeType(mimeType);

        return new ImageBuffer(bufferStream.ToArray(), imageFormat);
    }

    public static ImageBuffer FromBytes(byte[] bytes, string mimeType)
    {
        var imageFormat = GetImageFormatFromMimeType(mimeType);

        return FromBytes(bytes, imageFormat);
    }

    public static ImageBuffer FromBytes(byte[] bytes, ImageFormat imageFormat)
    {
        return new ImageBuffer(bytes, imageFormat);
    }

    #region Private Static Utilities

    private static HttpClient _httpClient = new HttpClient();

    private static string GetMimeTypeFromImageFormat(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.First(codec => codec.FormatID == format.Guid).MimeType!;
    }

    private static ImageFormat? TryGetImageFormatFromMimeType(string mimeType)
    {
        if (string.Equals(mimeType, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = "image/jpeg";
        }

        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

        var formatId = codecs.FirstOrDefault(codec => string.Equals(codec.MimeType, mimeType, StringComparison.InvariantCultureIgnoreCase))?.FormatID;

        if (formatId is not null)
        {
            return SupportImageFormats.FirstOrDefault(fmt => fmt.Guid == formatId);
        }

        return null;
    }

    private static ImageFormat GetImageFormatFromMimeType(string mimeType)
    {
        var imageFormat = TryGetImageFormatFromMimeType(mimeType);

        if (imageFormat is null)
        {
            throw new Exception($"No image format could be derived from mime type {mimeType}");
        }

        return imageFormat;
    }

    private static string? GetMimeTypeFromFileExtension(string? extension)
    {
        if (extension is null) return null;

        if (string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)) return "image/bmp";
        if (string.Equals(extension, ".dib", StringComparison.OrdinalIgnoreCase)) return "image/bmp";
        if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase)) return "image/gif";
        if (string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase)) return "image/x-icon";
        if (string.Equals(extension, ".jpe", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        if (string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (string.Equals(extension, ".pnz", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)) return "image/tiff";
        if (string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase)) return "image/tiff";

        return null;
    }

    private static readonly ImageFormat[] SupportImageFormats =
    [
        ImageFormat.Jpeg,
        ImageFormat.Png,
        ImageFormat.Bmp,
        ImageFormat.Emf,
        ImageFormat.Exif,
        ImageFormat.Gif,
        ImageFormat.Icon,
        ImageFormat.MemoryBmp,
        ImageFormat.Tiff,
        ImageFormat.Wmf,
    ];

    #endregion Private Static Utilities
}