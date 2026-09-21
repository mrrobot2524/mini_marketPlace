using Marketplace.Api.DTOs;
using Marketplace.Api.Models;
using Marketplace.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Marketplace.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }
    
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Products endpoint works!");
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        var product = new Product(0, request.Name, request.Price, request.StockQuantity);
        
        var createProduct = await _productService.CreateAsync(product);
        return Ok(createProduct);
    }

    [HttpGet("database-test")]
    public async Task<IActionResult> DatabaseTest()
    {
        var connectionString = "Host=localhost;Port=5432;Database=marketplace;Username=postgres;Password=123456";

        await using var connection = new NpgsqlConnection(connectionString);
        return Ok("PostgreSQL connection test works!");
    }
}