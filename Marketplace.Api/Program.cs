using Marketplace.Api.Repositories;
using Marketplace.Api.Services;
using Marketplace.Api.Repositories;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString!));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();