

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
   config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

// common services: carter, mediatr, fluentvalidation

var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(catalogAssembly,basketAssembly);

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
    .AddMediatRWithAssemblies(builder.Services, catalogAssembly, basketAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

//modeule services: catalog, basket, ordering

builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

builder.Services.AddExceptionHandler<CustomExceptionHandler>();



var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapCarter();

app.UseSerilogRequestLogging();

app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();

app.Run();
