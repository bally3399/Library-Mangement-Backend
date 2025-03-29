
#region cloudinary
//namespace FortunaeLibraryManagementSystem.Service.Services;
//using CloudinaryDotNet;
//using CloudinaryDotNet.Actions;
//using FortunaeLibraryManagementSystem.Service.Interfaces;
//using Microsoft.AspNetCore.Http;

//public class ImageService : IImageService
//{
//    private readonly Cloudinary _cloudinary;

//    public ImageService(Cloudinary cloudinary)
//    {
//        _cloudinary = cloudinary;
//    }

//    public async Task<string> UploadImageAsync(IFormFile file)
//    {
//        using (var stream = file.OpenReadStream())
//        {
//            var uploadParams = new ImageUploadParams
//            {
//                File = new FileDescription(file.FileName, stream),
//                Folder = "books",
//                PublicId = Guid.NewGuid().ToString()
//            };

//            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

//            if (uploadResult.Error != null)
//            {
//                throw new Exception($"Cloudinary error: {uploadResult.Error.Message}");
//            }

//            return uploadResult.SecureUrl.ToString();
//        }
//    }
//}

#endregion cloudinary

//using Amazon.S3;
//using Amazon.S3.Model;
//using Amazon.S3.Transfer;
using fortunae.Service.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using fortunae.Service.DTOs;

namespace fortunae.Service.Services
 {

    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;

         public ImageService(ILogger<ImageService> logger)
        {
            _logger = logger;
        }



        public async Task<byte[]> ProcessImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No image file provided.");
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing image: {Message}", ex.Message);
                throw;
            }
        }

    }
}