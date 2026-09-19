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

    public async Task<Product> CreateAsync(Product product)
    {
        if (string.IsNullOrWhiteSpace((product.Name)))
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

        return await _productRepository.CreateAsync(product);
    }
}