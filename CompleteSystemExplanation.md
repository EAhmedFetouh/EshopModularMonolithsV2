# E-Commerce Modular Monolithic System - Complete Explanation

## Overview

This document provides a comprehensive explanation of your e-commerce modular monolithic system, focusing on the event-driven architecture for price updates between Catalog and Basket modules.

## 🏗️ Architecture Overview

Your system implements a **Modular Monolithic Architecture** using .NET Core with these key components:

- **Bootstrapper/Api**: Application entry point and module composition
- **Modules**: Business domains (Catalog, Basket, Ordering)
- **Shared**: Cross-cutting concerns (DDD, CQRS, messaging)

### Project Structure
```
src/
├── Bootstrapper/Api/          # Application composition
├── Modules/
│   ├── Basket/                # Shopping cart management
│   ├── Catalog/               # Product management
│   └── Ordering/              # Order processing
└── Shared/                   # Common infrastructure
    ├── Shared.Messaing/      # Event messaging
    └── Shared/               # DDD, CQRS, behaviors
```

## 🎯 Event-Driven Price Update System

### Core Concept
When a product's price changes in the Catalog module, all shopping carts containing that product must be updated with the new price. This is achieved through a **Domain Events** → **Integration Events** flow.

### Complete Flow Diagram
```
1. Price Change Request → Catalog API
2. Product.Update() → Raises ProductPriceChangedEvent (Domain Event)
3. Catalog Event Handler → Publishes ProductPriceChangedIntegrationEvent
4. MassTransit → Delivers event to Basket module
5. Basket Event Consumer → Sends UpdateItemPriceInBasketCommand
6. CQRS Handler → Updates all cart items with new price
7. Database → Persists changes
```

---

## 📁 File-by-File Code Review

### 1. Bootstrapper/Api/Program.cs
```csharp
using Shared.Messaing.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
   config.ReadFrom.Configuration(context.Configuration));

// Module assemblies for registration
var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(catalogAssembly,basketAssembly);

// CQRS setup with behaviors
MediatRExtentions
    .AddMediatRWithAssemblies(builder.Services, catalogAssembly, basketAssembly);

// Redis caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Event messaging setup
builder.Services.AddMassTransitWithAssemblies(catalogAssembly, basketAssembly);

// Register business modules
builder.Services
    .AddCatalogModule(builder.Configuration)
    .AddBasketModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration);

builder.Services.AddExceptionHandler<CustomExceptionHandler>();

var app = builder.Build();

app.MapCarter();
app.UseSerilogRequestLogging();

app
    .UseCatalogModule()
    .UseBasketModule()
    .UseOrderingModule();

app.Run();
```

### 2. Catalog Module - Product Aggregate (Catalog/Products/Models/Product.cs)
```csharp
using Catalog.Products.Events;

namespace Catalog.Products.Models
{
    public class Product : Aggregate<Guid>
    {
        public string Name { get; private set; } = default!;
        public List<string> Category { get; private set; } = new();
        public string Description { get; private set; }=default!;
        public string ImageFile { get; private set; } = default!;
        public decimal Price { get; private set; }

        public static Product Create(Guid id,string name, List<string> category,
                                   string description, string imageFile, decimal price)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
            var product= new Product
            {
                Id = id,
                Name = name,
                Category = category,
                Description = description,
                ImageFile = imageFile,
                Price = price
            };

            product.AddDomainEvent(new ProductCreatedEvent(product));
            return product;
        }

        public void Update(string name, List<string> category, string description,
                          string imageFile, decimal price)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
            Name = name;
            Category = category;
            Description = description;
            ImageFile = imageFile;

            // CRITICAL: Price change detection and domain event raising
            if(Price != price)
            {
                Price = price;
                AddDomainEvent(new ProductPriceChangedEvent(this));
            }
        }
    }
}
```

### 3. Catalog Domain Event (Catalog/Products/Events/ProductPriceChangedEvent.cs)
```csharp
namespace Catalog.Products.Events;

public record ProductPriceChangedEvent(Product product) : IDomainEvent;
```

### 4. Catalog Domain Event Handler (Catalog/Products/EventHandler/ProductPriceChangedEventHandler.cs)
```csharp
using MassTransit;
using Shared.Messaing.Events;

namespace Catalog.Products.EventHandler;

public class ProductPriceChangedEventHandler(IBus bus, ILogger<ProductPriceChangedEventHandler> logger)
    : INotificationHandler<ProductPriceChangedEvent>
{
    public async Task Handle(ProductPriceChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);

        // Convert domain event to integration event
        var integrationEvent = new ProductPriceChangedIntegrationEvent
        {
            ProductId = notification.product.Id,
            Name = notification.product.Name,
            Description = notification.product.Description,
            ImageFile = notification.product.ImageFile,
            category = notification.product.Category,
            Price = notification.product.Price, // Updated price from domain event
        };

        // Publish integration event via MassTransit
        await bus.Publish(integrationEvent, cancellationToken);
    }
}
```

### 5. Integration Event (Shared/Shared.Messaing/Events/ProductPriceChangedIntegrationEvent.cs)
```csharp
namespace Shared.Messaing.Events;

public class ProductPriceChangedIntegrationEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public List<string> category { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public decimal Price { get; set; } = default!;
}
```

### 6. Basket Integration Event Handler (Basket/EventHandlers/ProductPriceChangedIntegrationEventHandler.cs)
```csharp
using Basket.Basket.Features.UpdateItemPriceInBasket;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaing.Events;

namespace Basket.Basket.EventHandlers;

public class ProductPriceChangedIntegrationEventHandler
    (ISender sender, ILogger<ProductPriceChangedIntegrationEventHandler> logger)
    : IConsumer<ProductPriceChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProductPriceChangedIntegrationEvent> context)
    {
       logger.LogInformation("Integration Event Handled: {IntegrationEvent}",
                           context.Message.GetType().Name);

        // Convert integration event to CQRS command
        var command = new UpdateItemPriceinBasketCommand(
            context.Message.ProductId,
            context.Message.Price);

        var result = await sender.Send(command);

        if (!result.IsSuccess)
            logger.LogError("Error updating price in basket for product id: {ProductId}",
                          context.Message.ProductId);

        logger.LogInformation("Price for product id: {ProductId} updated in basket:",
                            context.Message.ProductId);
    }
}
```

### 7. Basket Update Command & Handler (Basket/Features/UpdateItemPriceInBasket/UpdateItemPriceInBasketHandler.cs)
```csharp
namespace Basket.Basket.Features.UpdateItemPriceInBasket;

public record UpdateItemPriceinBasketCommand(Guid ProductId, decimal Price)
    : ICommand<UpdateItemPriceinBasketResult>;

public record UpdateItemPriceinBasketResult(bool IsSuccess);

public class UpdateItemPriceinBasketValidator : AbstractValidator<UpdateItemPriceinBasketCommand>
{
    public UpdateItemPriceinBasketValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("ProductId is required");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}

public class UpdateItemPriceInBasketHandler(BasketDbContext dbContext)
    : ICommandHandler<UpdateItemPriceinBasketCommand, UpdateItemPriceinBasketResult>
{
    public async Task<UpdateItemPriceinBasketResult> Handle(
        UpdateItemPriceinBasketCommand command, CancellationToken cancellationToken)
    {
        // Find ALL cart items containing this product
        var itemsToUpdate = await dbContext.ShoppingCartItems
            .Where(x => x.ProductId == command.ProductId)
            .ToListAsync(cancellationToken);

        if (!itemsToUpdate.Any())
            return new UpdateItemPriceinBasketResult(false);

        // Update price for each cart item
        foreach (var item in itemsToUpdate)
        {
            item.UpdatePrice(command.Price);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new UpdateItemPriceinBasketResult(true);
    }
}
```

### 8. Shopping Cart Item Model (Basket/Models/ShoppingCartItem.cs)
```csharp
using System.Text.Json.Serialization;

namespace Basket.Basket.Models;

public class ShoppingCartItem : Entity<Guid>
{
    public Guid ShoppingCartId { get; private set; } = default!;
    public Guid ProductId { get; private set; } = default!;
    public int Quantity { get; internal set; } = default!;
    public string Color { get; private set; } = default!;
    public decimal Price { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;

    internal ShoppingCartItem(Guid shoppingCartId, Guid productId, int quantity,
                            string color, decimal price, string productName)
    {
        ShoppingCartId = shoppingCartId;
        ProductId = productId;
        Quantity = quantity;
        Color = color;
        Price = price;
        ProductName = productName;
    }

    [JsonConstructor]
    internal ShoppingCartItem(Guid id, Guid shoppingCartId, Guid productId, int quantity,
                            string color, decimal price, string productName)
    {
        Id = id;
        ShoppingCartId = shoppingCartId;
        ProductId = productId;
        Quantity = quantity;
        Color = color;
        Price = price;
        ProductName = productName;
    }

    // Price update method called by the handler
    public void UpdatePrice(decimal newPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newPrice);
        Price = newPrice;
    }
}
```

### 9. Basket Module Registration (Basket/BasketModule.cs)
```csharp
using Basket.Data.Repository;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Behaviors;
using Shared.Data;
using Shared.Data.Interceptors;

namespace Basket;

public static class BasketModule
{
    public static IServiceCollection AddBasketModule(this IServiceCollection services,
         IConfiguration configuration)
    {
        // Repository with caching decorator pattern
        services.AddScoped<IBasketRepository, BasketRepository>();
        services.Decorate<IBasketRepository, CachedBasketRepository>();

        // Database context with interceptors
        var connectionString = configuration.GetConnectionString("Database");
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<BasketDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        return services;
    }

    public static IApplicationBuilder UseBasketModule(this IApplicationBuilder app)
    {
        app.UseMigration<BasketDbContext>();
        return app;
    }
}
```

### 10. MassTransit Extensions (Shared/Shared.Messaing/Extensions/MassTransitExtensions.cs)
```csharp
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Shared.Messaing.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddMassTransitWithAssemblies
        (this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();
            config.SetInMemorySagaRepositoryProvider();

            // Automatically register consumers from provided assemblies
            config.AddConsumers(assemblies);
            config.AddSagaStateMachines(assemblies);
            config.AddSagas(assemblies);
            config.AddActivities(assemblies);

            config.UsingInMemory((context, configurator) =>
            {
                configurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

---

## 🔄 Step-by-Step Event Flow

### Step 1: Price Update Request
- User/API calls product update endpoint in Catalog module
- `Product.Update()` method is called with new price

### Step 2: Domain Event Creation
- `Product.Update()` detects price change
- Creates `ProductPriceChangedEvent` and adds it to domain events collection

### Step 3: Domain Event Publishing
- EF Core interceptor (`DispatchDomainEventsInterceptor`) automatically publishes domain events after `SaveChanges()`
- `ProductPriceChangedEventHandler` in Catalog module receives the domain event

### Step 4: Integration Event Publishing
- Catalog event handler creates `ProductPriceChangedIntegrationEvent`
- Publishes to MassTransit message bus

### Step 5: Cross-Module Communication
- MassTransit (in-memory transport) delivers event to Basket module
- `ProductPriceChangedIntegrationEventHandler` in Basket consumes the event

### Step 6: CQRS Command Execution
- Basket event handler creates `UpdateItemPriceinBasketCommand`
- Sends command via MediatR to the handler

### Step 7: Database Update
- Handler queries all `ShoppingCartItem` entities with matching `ProductId`
- Calls `UpdatePrice()` on each cart item
- Saves changes to database

### Step 8: Consistency Achieved
- All shopping carts now reflect the new product price
- System maintains eventual consistency across modules

---

## 🏛️ Architectural Patterns Used

### 1. Domain-Driven Design (DDD)
- **Aggregates**: `Product`, `ShoppingCart` maintain consistency boundaries
- **Domain Events**: Business-significant events trigger cross-module updates
- **Entities**: `ShoppingCartItem` with identity and lifecycle
- **Value Objects**: Price, Product details

### 2. CQRS (Command Query Responsibility Segregation)
- **Commands**: Write operations (`UpdateItemPriceinBasketCommand`)
- **Queries**: Read operations (not shown but follow same pattern)
- **Separation**: Commands and queries use different models/handlers

### 3. Event-Driven Architecture
- **Domain Events**: Within bounded contexts
- **Integration Events**: Cross-module communication
- **Eventual Consistency**: Asynchronous updates

### 4. Clean Architecture
```
Presentation Layer (API) → Carter endpoints
Application Layer (CQRS) → Command/Query handlers
Domain Layer (Business) → Aggregates, domain events
Infrastructure Layer → Repositories, external services
```

### 5. Repository Pattern
- **Interface Segregation**: `IBasketRepository` abstraction
- **Decorator Pattern**: Transparent caching added to repository

### 6. Interceptor Pattern
- **Auditing**: Automatic CreatedBy/ModifiedBy tracking
- **Domain Events**: Automatic publishing after save operations

---

## 🎯 Key Benefits of This Design

### 1. **Loose Coupling**
- Basket module doesn't directly reference Catalog
- Communication via contracts and events

### 2. **Eventual Consistency**
- Price updates propagate asynchronously
- System remains responsive during updates

### 3. **Scalability**
- Can handle high-volume price changes
- Asynchronous processing doesn't block user requests

### 4. **Maintainability**
- Clear separation of concerns
- Each module focuses on single business domain

### 5. **Testability**
- Modules can be unit tested independently
- Event handlers can be tested in isolation

### 6. **Auditability**
- All price changes are logged
- Full traceability of business events

---

## 🚨 Important Considerations

### Race Conditions
- Multiple simultaneous price updates are handled by event ordering
- Database constraints prevent invalid states

### Error Handling
- Failed price updates are logged but don't break the system
- Basket continues to function even if price sync fails

### Performance
- Bulk updates in single database transaction
- Cached basket repository reduces database load

### Data Consistency
- Eventual consistency vs immediate consistency trade-off
- Business accepts that cart prices may lag behind catalog prices temporarily

---

## 🔧 Configuration & Infrastructure

### Development Setup (In-Memory)
- MassTransit uses in-memory transport
- PostgreSQL for data persistence
- Redis for caching
- Serilog for logging

### Production Considerations
- Replace in-memory transport with RabbitMQ/Kafka
- Database connection pooling
- Distributed caching strategies
- Health checks and monitoring

This architecture provides a solid foundation for a scalable e-commerce platform while maintaining clean separation of concerns and business domain boundaries.
