using MoeezMobile.Api.Models.Common;
using MoeezMobile.Api.Models.Dtos;
using MoeezMobile.Api.Repositories;

namespace MoeezMobile.Api.Services;

public interface IProductService
{
    Task<int> CreateAsync(ProductFormDto dto, CancellationToken ct = default);
    Task UpdateAsync(int id, ProductFormDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _products;
    private readonly IFileStorageService _files;

    public ProductService(IProductRepository products, IFileStorageService files)
    {
        _products = products;
        _files = files;
    }

    public async Task<int> CreateAsync(ProductFormDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await EnsureBarcodeFreeAsync(dto.Barcode, null);

        string? imagePath = null;
        if (dto.Image is not null)
            imagePath = await _files.SaveProductImageAsync(dto.Image, ct);

        try
        {
            return await _products.CreateAsync(dto, imagePath);
        }
        catch
        {
            // Don't leave an orphaned file behind if the insert fails.
            _files.DeleteIfExists(imagePath);
            throw;
        }
    }

    public async Task UpdateAsync(int id, ProductFormDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await EnsureBarcodeFreeAsync(dto.Barcode, id);

        var existingImage = await _products.GetImagePathAsync(id);

        string? newImagePath = null;
        var changeImage = false;

        if (dto.Image is not null)
        {
            newImagePath = await _files.SaveProductImageAsync(dto.Image, ct);
            changeImage = true;
        }
        else if (dto.RemoveImage)
        {
            changeImage = true;   // newImagePath stays null -> clears the column
        }

        try
        {
            await _products.UpdateAsync(id, dto, newImagePath, changeImage);
        }
        catch
        {
            _files.DeleteIfExists(newImagePath);
            throw;
        }

        if (changeImage && !string.IsNullOrWhiteSpace(existingImage) && existingImage != newImagePath)
            _files.DeleteIfExists(existingImage);
    }

    public async Task DeleteAsync(int id)
    {
        // Soft delete: history keeps referencing the row, it just stops appearing in pickers.
        if (!await _products.SoftDeleteAsync(id))
            throw new NotFoundException(MessageKeys.ProductNotFound);
    }

    private static void Validate(ProductFormDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException(MessageKeys.ProductNameRequired);

        if (dto.PurchasePrice < 0 || dto.WholesalePrice < 0 || dto.RetailPrice < 0)
            throw new BusinessException(MessageKeys.NegativePrice);

        if (dto.OpeningQuantity < 0)
            throw new BusinessException(MessageKeys.NegativeQuantity);
    }

    private async Task EnsureBarcodeFreeAsync(string? barcode, int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        if (await _products.BarcodeInUseAsync(barcode, excludeId))
            throw new BusinessException(MessageKeys.BarcodeInUse);
    }
}
