# E-Commerce Modular Monolith - Troubleshooting & Docker Setup Guide

## Table of Contents
1. [Errors Encountered During Development](#errors-encountered-during-development)
2. [Docker Compose Configuration](#docker-compose-configuration)
3. [Running the Application](#running-the-application)
4. [Quick Reference](#quick-reference)

---

## Errors Encountered During Development

### 1. NullReferenceException in CreateOrderHandler
**Error:**
```
System.NullReferenceException: Object reference not set to an instance of an object.
at Ordering.Orders.Features.CreateOrder.CreateOrderHandler.CreateNewOrder(OrderDto orderDto)
Line: var shippingAddress = Address.Of(orderDto.ShippingAddress.FirstName,...)
```

**Root Cause:**
- `command.Order` (OrderDto) was null when passed to `CreateNewOrder()`
- Mapster's `.Adapt<>()` failed to map `CreateOrderRequest.Order` ? `CreateOrderCommand.OrderDto` due to property name mismatch

**Solution Applied:**
```csharp
// Changed from implicit mapping
var command = request.Adapt<CreateOrderCommand>();

// To explicit construction
var command = new CreateOrderCommand(request.Order);

// Added null-check in handler
if (command.Order is null)
{
    logger.LogError("CreateOrderCommand.Order is null");
    throw new ArgumentNullException(nameof(command.Order));
}
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/Features/CreateOrder/CreateOrderHandler.cs`
- `Modules/Ordering/Ordering/Orders/Features/CreateOrder/CreateOrderEndpoint.cs`

---

### 2. Property Name Casing Mismatch in BasketCheckoutDto
**Error:**
```
PostgreSQL Error 23502: null value in column "BillingAddress_LastName" of relation "Orders" violates not-null constraint
```

**Root Cause:**
- `BasketCheckoutDto` had `lastName` (lowercase) instead of `LastName`
- Caused mapping failures across the event pipeline
- BillingAddress.LastName ended up null, violating database constraints

**Solution Applied:**
```csharp
// Before (WRONG)
public record BasketCheckoutDto(
    string UserName,
    Guid CustomerId,
    decimal TotalPrice,
    string FirstName,
    string lastName,  // ? lowercase
    ...
);

// After (CORRECT)
public record BasketCheckoutDto(
    string UserName,
    Guid CustomerId,
    decimal TotalPrice,
    string FirstName,
    string LastName,  // ? PascalCase
    ...
);
```

**Files Modified:**
- `Modules/Basket/Basket/Basket/Dtos/BasketCheckoutDto.cs`

---

### 3. AddressDto Constructor Parameter Ordering Mismatch
**Error:**
```
PostgreSQL Error 23502: null value in column "BillingAddress_LastName" violates not-null constraint
```

**Root Cause:**
- `AddressDto` constructor parameters were passed in wrong order
- Integration event handler mapped fields incorrectly

**AddressDto Definition:**
```csharp
public record AddressDto(
 string FirstName, 
    string LastName, 
    string EmailAddress, 
  string Country, 
    string State, 
    string ZipCode, 
    string AddressLine
);
```

**Solution Applied:**
```csharp
// Before (WRONG ORDER)
var addressDto = new AddressDto(
    message.FirstName,
    message.LastName,
    message.EmailAddress,
    message.AddressLine,      // ? Wrong position
    message.State,
    message.Country,
    message.ZipCode
);

// After (CORRECT ORDER)
var addressDto = new AddressDto(
    message.FirstName,
    message.LastName,
    message.EmailAddress,
    message.Country,          // ? Correct position
    message.State,
    message.ZipCode,
    message.AddressLine    // ? Correct position
);
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/EventHandlers/BasketCheckoutIntegrationEventHandler.cs`

---

### 4. Billing Address Using Shipping Address Data
**Error:**
Database saved billing address with shipping address values

**Root Cause:**
- `CreateOrderHandler.CreateNewOrder()` used `orderDto.ShippingAddress` twice instead of using `orderDto.BillingAddress`

**Solution Applied:**
```csharp
// Before (WRONG)
var shippingAddress = Address.Of(orderDto.ShippingAddress.FirstName, ...);
var billingAddress = Address.Of(orderDto.ShippingAddress.FirstName, ...);  // ? Wrong DTO

// After (CORRECT)
var shippingAddress = Address.Of(orderDto.ShippingAddress.FirstName, ...);
var billingAddress = Address.Of(orderDto.BillingAddress.FirstName, ...);   // ? Correct DTO
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/Features/CreateOrder/CreateOrderHandler.cs`

---

### 5. CheckoutBasket Exception Swallowing
**Error:**
Checkout returned success even when exceptions occurred

**Root Cause:**
- Catch block returned `IsSuccess = true` instead of `false`
- Exceptions were logged but not properly signaled

**Solution Applied:**
```csharp
// Before (WRONG)
catch (Exception ex)
{
    await transaction.RollbackAsync(cancellationToken);
    return new CheckoutBasketResult(true);  // ? Always returns success
}

// After (CORRECT)
catch (Exception ex)
{
    await transaction.RollbackAsync(cancellationToken);
    logger.LogError(ex, "Error during checkout for user {UserName}", command.BasketCheckout.UserName);
    return new CheckoutBasketResult(false);  // ? Returns failure
}
```

**Files Modified:**
- `Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketHandler.cs`

---

### 6. Missing UserName in Checkout Request
**Error:**
```
Basket.Basket.Exceptions.BasketNotFoundException: Entity "ShoppingCart" (Basket is empty or does not exist.) was not found.
```

**Root Cause:**
- `CheckoutBasketEndpoint` didn't populate `UserName` from authenticated user
- Handler couldn't find the shopping cart because UserName was null/empty

**Solution Applied:**
```csharp
// Added to CheckoutBasketEndpoint
app.MapPost("/basket/checkout", async (
    CheckoutBasketRequest request,
    ISender sender,
 ClaimsPrincipal user) =>  // ? Added ClaimsPrincipal
{
    var userName = user.Identity?.Name;
    
    var updatedBasketCheckout = request.BasketCheckout with
    {
        UserName = userName!  // ? Set from authenticated user
    };
    
  var command = new CheckoutBasketCommand(updatedBasketCheckout);
    // ...
});
```

**Files Modified:**
- `Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketEndpoint.cs`

---

### 7. JSON Deserialization Case Sensitivity
**Error:**
Integration events failed to deserialize due to property name casing mismatches

**Root Cause:**
- Default JSON deserialization is case-sensitive
- Property names in serialized JSON didn't match C# property names

**Solution Applied:**
```csharp
// Added to OutboxProcessor
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true  // ? Make deserialization case-insensitive
};

eventMessage = JsonSerializer.Deserialize(message.Content, eventType, jsonOptions);
```

**Files Modified:**
- `Modules/Basket/Basket/Data/Processors/OutboxProcessor.cs`

---

### 8. Missing Validation for Required Address Fields
**Error:**
Silent failures when address data was incomplete, leading to database constraint violations

**Solution Applied:**
```csharp
// Added validation in CreateNewOrder()
if (string.IsNullOrWhiteSpace(orderDto.ShippingAddress?.FirstName) || 
    string.IsNullOrWhiteSpace(orderDto.ShippingAddress?.LastName))
{
    throw new ArgumentException("Shipping address first name and last name are required.");
}

if (orderDto.BillingAddress == null || 
    string.IsNullOrWhiteSpace(orderDto.BillingAddress.LastName) || 
    string.IsNullOrWhiteSpace(orderDto.BillingAddress.FirstName))
{
    throw new ArgumentException("Billing address first name and last name are required.");
}
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/Features/CreateOrder/CreateOrderHandler.cs`

---

### 9. TotalPrice Always Returns 0
**Error:**
`Order.TotalPrice` property always returned 0, never calculated

**Root Causes:**
1. `[NotMapped]` attribute prevented persistence to database
2. No calculation logic - property was never assigned a value
3. Getter returned default decimal value (0)

**Solution Applied:**
```csharp
// Before (WRONG)
[NotMapped]
public decimal TotalPrice { get; private set; }  // ? Never calculated

public void Add(Guid productId, int quantity, decimal price)
{
    var newItem = new OrderItem(Id, productId, price, quantity);
    _items.Add(newItem);
    // ? TotalPrice not updated
}

// After (CORRECT)
public decimal TotalPrice { get; private set; }  // ? Removed [NotMapped]

public void Add(Guid productId, int quantity, decimal price)
{
    var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
    
    if (existingItem != null)
    {
     existingItem.Quantity += quantity;
    }
    else
    {
 var newItem = new OrderItem(Id, productId, price, quantity);
    _items.Add(newItem);
    }
    
    // ? Recalculate total price
    TotalPrice = _items.Sum(item => item.Price * item.Quantity);
}

public void Remove(Guid productId)
{
    var orderItem = _items.FirstOrDefault(i => i.ProductId == productId);
    if (orderItem != null)
    {
        _items.Remove(orderItem);
   // ? Recalculate total price
        TotalPrice = _items.Sum(item => item.Price * item.Quantity);
    }
}
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/Models/Order.cs`

---

### 10. Missing Logging Throughout Pipeline
**Error:**
Difficult to diagnose failures in multi-step async flow (Basket ? Outbox ? MassTransit ? Ordering)

**Solution Applied:**
Added comprehensive logging at key points:

```csharp
// CreateOrderHandler
logger.LogInformation("CreateOrderHandler: Creating order for customer {CustomerId}", command.Order.CustomerId);
logger.LogInformation("CreateOrderHandler: Order created with Id={OrderId}", order.Id);
logger.LogError(ex, "CreateOrderHandler: Error creating order");

// BasketCheckoutIntegrationEventHandler
logger.LogDebug("Incoming BasketCheckoutIntegrationEvent: {@Message}", context.Message);
logger.LogDebug("Mapped CreateOrderCommand.Order: {@Order}", createOrderCommand.Order);

// CheckoutBasketHandler
logger.LogError(ex, "Error during checkout for user {UserName}", command.BasketCheckout.UserName);

// OutboxProcessor
logger.LogWarning(ex, "Could not deserialize message content for type: {type}. Content: {content}", message.Type, message.Content);
logger.LogInformation("Successfully Processed outbox message with ID: {id}", message.Id);
```

**Files Modified:**
- `Modules/Ordering/Ordering/Orders/Features/CreateOrder/CreateOrderHandler.cs`
- `Modules/Ordering/Ordering/Orders/EventHandlers/BasketCheckoutIntegrationEventHandler.cs`
- `Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketHandler.cs`
- `Modules/Basket/Basket/Data/Processors/OutboxProcessor.cs`

---

## Docker Compose Configuration

### Problem
The application was configured to run against `localhost` services (PostgreSQL, Redis, RabbitMQ, Keycloak, Seq), but needed to run in Docker containers where services communicate via Docker network using service names.

### Original Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "Database": "Server=localhost;port=5432;Database=EshopDb;User Id=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "MessageBroker": {
    "Host": "amqp://localhost:5672",
    "UserName": "guest",
    "Password": "guest"
  },
  "keycloak": {
    "auth-server-url": "http://localhost:9090/"
  },
  "Serilog": {
    "WriteTo": [
   {
     "Name": "Seq",
        "Args": {
    "serverUrl": "http://localhost:5341"
    }
      }
    ]
  }
}
```

### Updated Docker Compose Configuration

**File: `docker-compose.override.yml`**

```yaml
services:
  # PostgreSQL Database
  eshopdb:
    image: postgres:16
  container_name: eshopdb
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: EShopDb
    ports:
      - "5432:5432"
    volumes:
      - postgres_eshopdb:/var/lib/postgresql/data

  # Seq Logging Server
  seq:
    container_name: seq
    restart: always
    environment:
       - ACCEPT_EULA=Y
       - SEQ_FIRSTRUN_ADMINPASSWORD=P@ssw0rd
  ports:
      - "5341:5341"  # Seq ingestion
      - "9091:80"    # Seq UI

  # Redis Distributed Cache
  distributedcache:
    container_name: distributedcache
    restart: always
    ports:
      - "6379:6379"

  # RabbitMQ Message Broker
  messagebus:
    container_name: messagebus
environment:
  RABBITMQ_DEFAULT_USER: guest
    RABBITMQ_DEFAULT_PASS: guest
 restart: always
    ports:
     - "5672:5672"   # AMQP
     - "15672:15672" # Management UI

  # Keycloak Identity Server
  identity:
    container_name: identity
    environment:
 - KEYCLOAK_ADMIN=admin
      - KEYCLOAK_ADMIN_PASSWORD=P@ssw0rd
 - KC_DB=postgres
      - KC_DB_URL=jdbc:postgresql://eshopdb:5432/EshopDb?currentSchema=identity
      - KC_DB_USERNAME=postgres
      - KC_DB_PASSWORD=postgres
    ports:
      - "9090:8080"
restart: always
    command:
      - start-dev
    depends_on:
      - eshopdb

  # API Application
  api:
  container_name: api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=8080
    - ASPNETCORE_HTTPS_PORTS=8081
   
      # ? Database connection using service name
      - ConnectionStrings__Database=Server=eshopdb;Port=5432;Database=EShopDb;User Id=postgres;Password=postgres
 
      # ? Redis connection using service name
      - ConnectionStrings__Redis=distributedcache:6379
      
      # ? RabbitMQ message broker using service name
      - MessageBroker__Host=amqp://messagebus:5672
      - MessageBroker__UserName=guest
      - MessageBroker__Password=guest
      
    # ? Keycloak using service name
  - Keycloak__auth-server-url=http://identity:8080/
      
   # ? Serilog Seq using service name
      - Serilog__Using__0=Serilog.Sinks.Console
      - Serilog__Using__1=Serilog.Sinks.Seq
      - Serilog__MinimumLevel__Default=Information
      - Serilog__MinimumLevel__Override__Microsoft=Information
      - Serilog__MinimumLevel__Override__System=Warning
      - Serilog__WriteTo__0__Name=Console
      - Serilog__WriteTo__1__Name=Seq
    - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
      - Serilog__Enrich__0=FromLogContext
- Serilog__Enrich__1=WithMachineName
      - Serilog__Enrich__2=WithProcessId
      - Serilog__Enrich__3=WithThreadId
   - Serilog__Properties__Application=EShop ASP.NET Core App
      - Serilog__Properties__Environment=Development
      
    ports:
      - "5050:8080"  # HTTP
      - "5051:8081"  # HTTPS
    depends_on:
      - eshopdb
      - distributedcache
      - messagebus
      - identity
      - seq
    volumes:
      - ${APPDATA}/Microsoft/UserSecrets:/home/app/.microsoft/usersecrets:ro
      - ${APPDATA}/ASP.NET/Https:/home/app/.aspnet/https:ro

volumes:
  postgres_eshopdb:
```

### Key Changes Explained

| Configuration | Local (localhost) | Docker Compose (service names) | Why Changed |
|---------------|-------------------|-------------------------------|-------------|
| **Database** | `localhost:5432` | `eshopdb:5432` | Docker containers use service names as hostnames within the Docker network |
| **Redis** | `localhost:6379` | `distributedcache:6379` | Service name resolves to container IP internally |
| **RabbitMQ** | `amqp://localhost:5672` | `amqp://messagebus:5672` | MassTransit connects via internal service name |
| **Keycloak** | `http://localhost:9090/` | `http://identity:8080/` | Keycloak runs on port 8080 inside container, mapped to 9090 externally |
| **Seq** | `http://localhost:5341` | `http://seq:5341` | API sends logs to Seq using internal service name |

### Docker Networking Explained

```
???????????????????????????????????????????????????????????????
?         Docker Network (bridge)?
?              ?
?  ????????????    ????????????    ????????????             ?
?  ? eshopdb  ??????   api    ??????   seq    ?       ?
?  ? :5432    ?    ? :8080    ?    ? :5341    ?    ?
?  ????????????    ????????????    ????????????             ?
?       ?   ?
?  ????????????  ?         ????????????           ?
?  ?  redis   ?????????????????????? rabbitmq ?        ?
?  ? :6379    ?     ?         ? :5672    ?          ?
?  ????????????          ?         ????????????              ?
?          ?               ?
?  ????????????     ?           ?
?  ?keycloak  ????????????  ?
?  ? :8080    ?   ?
?  ????????????      ?
?         ?
???????????????????????????????????????????????????????????????
       ?
  ? Port Mappings
   ?
???????????????????????????????????????????????????????????????
?       Host Machine        ?
?   ?
?  localhost:5432  ? eshopdb:5432      ?
?  localhost:6379  ? distributedcache:6379       ?
?  localhost:5672  ? messagebus:5672             ?
?  localhost:9090  ? identity:8080       ?
?  localhost:5341  ? seq:5341           ?
?  localhost:5050  ? api:8080       ?
?  localhost:15672 ? rabbitmq:15672 (Management UI)           ?
?localhost:9091  ? seq:80 (UI)        ?
???????????????????????????????????????????????????????????????
```

---

## Running the Application

### Prerequisites
- Docker Desktop installed and running
- .NET 8 SDK installed (for local development)
- Git (for version control)

### Step 1: Clean Up Existing Containers
```bash
# Stop and remove all containers
docker-compose down

# If containers are stuck, force remove
docker rm -f api eshopdb seq distributedcache messagebus identity

# For complete clean (removes volumes too)
docker-compose down -v
```

### Step 2: Build and Start Services
```bash
# Build and start all services
docker-compose up --build

# Or run in detached mode (background)
docker-compose up --build -d
```

### Step 3: Wait for Services to Initialize
Services start in this order (due to `depends_on`):
1. **eshopdb** (PostgreSQL) - ~10 seconds
2. **seq** (Logging) - ~5 seconds
3. **distributedcache** (Redis) - ~2 seconds
4. **messagebus** (RabbitMQ) - ~10 seconds
5. **identity** (Keycloak) - ~30 seconds
6. **api** (Application) - ~15 seconds

Total startup time: ~1-2 minutes

### Step 4: Verify Services are Running
```bash
# Check all containers
docker-compose ps

# Expected output:
NAME     IMAGE           STATUS
api         api        Up
distributedcache    redis         Up
eshopdb      postgres:16        Up
identity         keycloak:24.0.3    Up
messagebus        rabbitmq:management Up
seq       datalust/seq:latest Up
```

### Step 5: Access the Application

| Service | URL | Credentials |
|---------|-----|-------------|
| **API** | https://localhost:5050 | Bearer token from Keycloak |
| **API (HTTP)** | http://localhost:5050 | Bearer token from Keycloak |
| **Keycloak Admin** | http://localhost:9090 | admin / P@ssw0rd |
| **RabbitMQ Management** | http://localhost:15672 | guest / guest |
| **Seq Logs** | http://localhost:9091 | admin / P@ssw0rd |
| **PostgreSQL** | localhost:5432 | postgres / postgres |
| **Redis** | localhost:6379 | (no auth) |

### Step 6: View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api
docker-compose logs -f eshopdb
docker-compose logs -f messagebus

# Last 100 lines
docker-compose logs --tail=100 api
```

### Step 7: Stop Services
```bash
# Stop containers (keeps data)
docker-compose down

# Stop and remove volumes (fresh start next time)
docker-compose down -v
```

---

## Quick Reference

### Common Docker Commands
```bash
# View running containers
docker ps

# View all containers (including stopped)
docker ps -a

# View container logs
docker logs api -f

# Execute command in running container
docker exec -it api bash

# Restart a specific service
docker-compose restart api

# Rebuild a specific service
docker-compose up --build api

# Remove all stopped containers
docker container prune

# Remove all unused images
docker image prune -a

# View Docker networks
docker network ls

# Inspect a network
docker network inspect modularmonolithic_default
```

### Database Commands
```bash
# Connect to PostgreSQL
docker exec -it eshopdb psql -U postgres -d EShopDb

# View all schemas
\dn

# View tables in basket schema
\dt basket.*

# View tables in ordering schema
\dt ordering.*

# Exit psql
\q
```

### Useful SQL Queries
```sql
-- View all baskets
SELECT * FROM basket."ShoppingCarts";

-- View basket items
SELECT * FROM basket."ShoppingCartItems";

-- View outbox messages
SELECT * FROM basket."OutboxMessages";

-- View orders
SELECT * FROM ordering."Orders";

-- View order items
SELECT * FROM ordering."OrderItems";
```

### RabbitMQ Management
```bash
# List queues
docker exec -it messagebus rabbitmqctl list_queues

# List exchanges
docker exec -it messagebus rabbitmqctl list_exchanges

# List bindings
docker exec -it messagebus rabbitmqctl list_bindings
```

### Troubleshooting Commands
```bash
# Check if ports are in use (Windows)
netstat -ano | findstr :5432
netstat -ano | findstr :5672
netstat -ano | findstr :6379

# Check API health
curl http://localhost:5050/health

# Test database connection
docker exec -it eshopdb pg_isready -U postgres

# Test Redis connection
docker exec -it distributedcache redis-cli ping
```

---

## API Testing Flow

### 1. Get Authentication Token from Keycloak
```bash
# First, configure Keycloak realm and client
# Then request token:
curl -X POST http://localhost:9090/realms/myrealm/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=myclient" \
  -d "username=testuser" \
  -d "password=password" \
  -d "grant_type=password"
```

### 2. Create a Basket
```bash
curl -X POST https://localhost:5050/basket \
  -H "Authorization: Bearer {YOUR_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "shoppingCart": {
    "id": "00000000-0000-0000-0000-000000000000",
  "userName": "myuser",
      "items": [
        {
          "id": "00000000-0000-0000-0000-000000000000",
          "shoppingCartId": "00000000-0000-0000-0000-000000000000",
     "productId": "5334c996-8457-4cf0-815c-ed2b77c4ff61",
          "productName": "Product 1",
          "quantity": 2,
      "price": 500,
 "color": "Red"
   }
      ],
      "totalPrice": 0
    }
  }'
```

### 3. Checkout Basket
```bash
curl -X POST https://localhost:5050/basket/checkout \
  -H "Authorization: Bearer {YOUR_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "basketCheckout": {
 "userName": "myuser",
      "customerId": "189dc8dc-990f-48e0-a37b-e6f2b60b9d7d",
      "totalPrice": 1000,
   "firstName": "John",
    "lastName": "Doe",
      "emailAddress": "john.doe@example.com",
      "addressLine": "123 Main St",
      "country": "USA",
      "zipCode": "12345",
      "state": "CA",
      "cardName": "John Doe",
    "cardNumber": "4111111111111111",
      "expiration": "12/25",
    "cvv": "123",
      "paymentMethod": 1
    }
  }'
```

### 4. View Orders
```bash
curl https://localhost:5050/orders \
  -H "Authorization: Bearer {YOUR_TOKEN}"
```

---

## Environment-Specific Configuration

### Local Development (without Docker)
Use `appsettings.Development.json` with localhost connections:
```json
{
  "ConnectionStrings": {
    "Database": "Server=localhost;port=5432;...",
    "Redis": "localhost:6379"
  }
}
```

### Docker Compose
Environment variables in `docker-compose.override.yml` override appsettings:
```yaml
environment:
  - ConnectionStrings__Database=Server=eshopdb;Port=5432;...
```

### Production (Kubernetes/Cloud)
Use ConfigMaps and Secrets:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: api-config
data:
  ConnectionStrings__Database: "Server=prod-db;..."
```

---

## Summary of Changes

### Code Changes
1. ? Fixed NullReferenceException in CreateOrderHandler
2. ? Fixed property name casing (lastName ? LastName)
3. ? Fixed AddressDto parameter ordering
4. ? Fixed billing address mapping
5. ? Fixed exception handling in checkout
6. ? Added UserName population from ClaimsPrincipal
7. ? Made JSON deserialization case-insensitive
8. ? Added validation for required fields
9. ? Fixed TotalPrice calculation
10. ? Added comprehensive logging

### Docker Configuration Changes
1. ? Changed all service connections from localhost to service names
2. ? Added proper environment variable overrides
3. ? Configured service dependencies
4. ? Mapped ports for external access
5. ? Configured volumes for data persistence

---

## Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [Seq Documentation](https://docs.datalust.co/docs)

---

**Document Version:** 1.0  
**Last Updated:** 2025  
**Maintained By:** Development Team
