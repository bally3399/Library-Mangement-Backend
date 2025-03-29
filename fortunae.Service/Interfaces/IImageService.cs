// fortunae.Service.Interfaces/IImageService.cs
using Microsoft.AspNetCore.Http;

namespace fortunae.Service.Interfaces
{
    public interface IImageService
    {
        Task<byte[]> ProcessImageAsync(IFormFile file);
    }
}