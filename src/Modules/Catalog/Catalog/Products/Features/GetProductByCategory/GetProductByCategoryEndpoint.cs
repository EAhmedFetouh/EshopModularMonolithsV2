

namespace Catalog.Products.Features.GetProductByCategory;

//public record GetProductByCategoryRequest(string Category);

public record GetProductByCategoryResponse(IEnumerable<ProductDto> Products);
public class GetProductByCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products/category/{category}", async (string Category, ISender sender) =>
        {
            var result = await sender.Send(new GetProductByCategoryQuery(Category));

            var response= result.Adapt<GetProductByCategoryResponse>();

            return Results.Ok(response);
        })
            .WithName("GetProductbyCategory")
            .Produces<CreateProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get Product by Category")
            .WithDescription("Get Product by Category");
    }
}
