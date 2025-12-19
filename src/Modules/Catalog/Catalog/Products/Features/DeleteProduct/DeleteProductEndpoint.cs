


namespace Catalog.Products.Features.DeleteProduct;

//public record DeleteProductRequest(Guid Id);

public record DeleteProductResponse(bool IsSuccess);
public class DeleteProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/products/{id}", async (Guid id, ISender sender) =>
        {
            
            var result =await sender.Send(new DeleteProductCommand(id));

            var response = result.Adapt<UpdateProductResponse>();

            return Results.Ok(response);
        })
            .WithName("DeleteProduct")
            .Produces<CreateProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Delete Product")
            .WithDescription("Delete Product");
    }
}
