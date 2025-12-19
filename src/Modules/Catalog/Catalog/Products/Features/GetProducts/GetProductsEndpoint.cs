
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

//public record GetProductRequest(PaginationRequest PaginationRequest);

public record GetProductResponse(PaginatedResult<ProductDto> Products);
public class GetProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async ([AsParameters] PaginationRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetProductsQuery(request));

            var response = result.Adapt<GetProductResponse>();

            return Results.Ok(response);
        })
            .WithName("GetProducts")
            .Produces<CreateProductResponse>(StatusCodes.Status200OK)
            .  ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get Products")
            .WithDescription("Get Products");
    }
}
