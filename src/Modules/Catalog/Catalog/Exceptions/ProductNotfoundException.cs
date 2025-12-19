
namespace Catalog.Exceptions
{
    public class ProductNotfoundException : NotFoundException
    {
        public ProductNotfoundException(Guid id)
            : base("Product", id)
        {
        }
    }
}
