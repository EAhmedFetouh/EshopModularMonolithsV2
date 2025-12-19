
namespace Catalog.Data.Seed;

public static class IntialData
{
    public static IEnumerable<Product> Products =>
        new List<Product>
        {
           Product.Create(new Guid(),"IPhone X",["category1"], "Long Description","imageFile", 500),
           Product.Create(new Guid(),"Samsung 10",["category1"], "Long Description","imageFile", 600),
           Product.Create(new Guid(),"Huawei Plus",["category2"], "Long Description","imageFile", 450),
           Product.Create(new Guid(),"Xiaomi Mi",["category2"], "Long Description","imageFile", 350),
        };
}
