using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenStreamServer.Services;

public class ScreenCaptureService
{
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    public ScreenCaptureService()
    {
        // Определяем размер основного экрана
        // Для .NET Core можно использовать System.Windows.Forms.Screen, но для простоты зададим вручную
        // В реальном приложении лучше получить размеры динамически
        _screenWidth = 1920; // Пример, замените на актуальное значение
        _screenHeight = 1080; // Пример, замените на актуальное значение
    }

    /// <summary>
    /// Захватывает текущий экран и возвращает массив байтов JPEG.
    /// </summary>
    /// <param name="quality">Качество JPEG (0-100).</param>
    /// <returns>Байты изображения в формате JPEG.</returns>
    public byte[] CaptureFrame(int quality = 50)
    {
        using var bitmap = new Bitmap(_screenWidth, _screenHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Захват всего экрана
            graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        // Сохранение в JPEG с указанным качеством
        using var ms = new MemoryStream();
        var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        var jpegCodec = GetEncoder(ImageFormat.Jpeg);
        bitmap.Save(ms, jpegCodec, encoderParameters);
        return ms.ToArray();
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageDecoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
    }
}