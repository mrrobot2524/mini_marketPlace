using Marketplace.Api.DTOs;
using Marketplace.Api.Models;
using Marketplace.Api.Repositories;

namespace Marketplace.Api.Services;

public class ProductService
{
    private readonly ProductRepository _productRepository;

    public ProductService(ProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product> CreateAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            throw new ArgumentException("Product name is required.");
        }

        if (product.Price <= 0)
        {
            throw new ArgumentException("Product price must be greater than 0.");
        }

        if (product.StockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.");
        }

        return await _productRepository.CreateAsync(product, cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        return await _productRepository.GetByIdAsync(productId, cancellationToken);
    }

    public async Task<ProductListResponse> GetListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var products = await _productRepository
            .GetPagedAsync(page, pageSize, cancellationToken);

        var totalCount = await _productRepository
            .GetCountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new ProductListResponse
        {
            Items = products,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<bool> UpdateAsync(
        int productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Product name is required.");
        }

        if (request.Price <= 0)
        {
            throw new ArgumentException("Product price must be greater than 0.");
        }

        if (request.StockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.");
        }

        return await _productRepository.UpdateAsync(
            productId,
            request.Name,
            request.Price,
            request.StockQuantity,
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        return await _productRepository.DeleteAsync(productId, cancellationToken);
    }
}