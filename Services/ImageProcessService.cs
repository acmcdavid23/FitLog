using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Size = SixLabors.ImageSharp.Size;

namespace FitLog.Services
{
    public class ImageProcessService
    {
        private const int AvatarSize = 512;
        public const long MaxUploadBytes = 15 * 1024 * 1024;

        public static bool IsAllowedImage(IFormFile file)
        {
            if (file == null || file.Length == 0 || file.Length > MaxUploadBytes) return false;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp";
        }

        public async Task SaveSquareJpegAsync(IFormFile file, string outputPath, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, cancellationToken);
            image.Mutate(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(AvatarSize, AvatarSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                });
            });
            await image.SaveAsJpegAsync(outputPath, cancellationToken: cancellationToken);
        }
    }
}
