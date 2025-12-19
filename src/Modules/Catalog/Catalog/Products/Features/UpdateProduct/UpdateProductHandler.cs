


using Catalog.Exceptions;

namespace Catalog.Products.Features.UpdateProduct;


public record UpdateProductCommand(ProductDto Product) : ICommand<UpdateProductResult>;

public record UpdateProductResult(bool IsSuccess);


public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.product.Id).NotEmpty().WithMessage("Id is required");
        RuleFor(x => x.product.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}

public class UpdateProductHandler (CatalogDbContext dbContext):
    ICommandHandler<UpdateProductCommand, UpdateProductResult>
{
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync(command.Product.Id, cancellationToken);

        if (product == null)
            throw new ProductNotfoundException(command.Product.Id);

       UpdateProductWithNewValues(product,command.Product);
       dbContext.Products.Update(product);
       await dbContext.SaveChangesAsync(cancellationToken);
       return new UpdateProductResult(true);
    }

    private void UpdateProductWithNewValues(Product product,ProductDto productDto)
    {
        product.Update(
           productDto.Name,
           productDto.Category,
           productDto.Description,
           productDto.ImageFile,
           productDto.Price
            );

    }
}
