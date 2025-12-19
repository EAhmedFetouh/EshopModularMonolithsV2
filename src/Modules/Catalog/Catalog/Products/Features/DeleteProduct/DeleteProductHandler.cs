
using Catalog.Exceptions;

namespace Catalog.Products.Features.DeleteProduct;

public record DeleteProductCommand (Guid Id) : ICommand <DeleteProductResult>;


public record DeleteProductResult(bool IsSuccess);



public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.product.Id).NotEmpty().WithMessage("Id is required");
    }
}

public class DeleteProductHandler  (CatalogDbContext dbContext)
    : ICommandHandler<DeleteProductCommand, DeleteProductResult>
{
    public async Task<DeleteProductResult> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync(command.Id, cancellationToken);

        if (product == null)
            throw new ProductNotfoundException(command.Id);


        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DeleteProductResult(true);
    }
}
