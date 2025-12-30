


using Keycloak.AuthServices.Authentication;
using Ordering;
using Shared.Messaing.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
   config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

// common services: carter, mediatr, fluentvalidation

var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;
var orderingAssembly = typeof(OrderingModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(catalogAssembly,basketAssembly, orderingAssembly);

#region  was commented out
//builder.Services.AddMediatR(cfg =>
//{
//    cfg.RegisterServicesFromAssemblies(catalogAssembly, basketAssembly);
//    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
//    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
//});
//builder.Services.AddValidatorsFromAssemblies([catalogAssembly, basketAssembly]);
#endregion

MediatRExtentions
    .AddMediatRWithAssemblies(builder.Services, catalogAssembly, basketAssembly, orderingAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});


builder.Services.AddMassTransitWithAssemblies(builder.Configuration,catalogAssembly, basketAssembly,orderingAssembly);


builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

//module services: catalog, basket, ordering

builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

builder.Services.AddExceptionHandler<CustomExceptionHandler>();



var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapCarter();

app.UseSerilogRequestLogging();
app.UseExceptionHandler(options =>{});
app.UseAuthentication();
app.UseAuthorization();

app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();

app.Run();
