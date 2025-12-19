
namespace Catalog.Products.Features.CreateProduct;

public record CreateProductCommand
    (ProductDto product)
    :ICommand<CreateProductResult>;

public record CreateProductResult(Guid Id);

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.product.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.product.Category).NotEmpty().WithMessage("Category is required");
        RuleFor(x => x.product.ImageFile).NotEmpty().WithMessage("ImageFile is required");
        RuleFor(x => x.product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}

public class CreateProductHandler  
    (CatalogDbContext dbContext):
    ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = CreateNewProduct(command.product);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id);
    }

    private Product CreateNewProduct(ProductDto productDto)
    {
        var product = Product.Create(
            Guid.NewGuid(),
            productDto.Name,
            productDto.Category,
            productDto.Description,
            productDto.ImageFile,
            productDto.Price
            );
        return product;
    }
}
