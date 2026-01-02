# شرح تفصيلي للنظام - E-Commerce Modular Monolith

## 📚 جدول المحتويات
1. [نظرة عامة على البنية المعمارية](#architecture-overview)
2. [Domain-Driven Design (DDD)](#ddd-concepts)
3. [الأحداث (Events) في النظام](#events-system)
4. [Message Queue & Integration Events](#message-queue)
5. [أمثلة عملية من الكود](#practical-examples)

---

## 🏗️ نظرة عامة على البنية المعمارية {#architecture-overview}

### البنية الأساسية للمشروع

```
src/
├── Bootstrapper/Api/          ← التطبيق الرئيسي (يشغل كل شيء)
├── Modules/                   ← الموديولات المستقلة
│   ├── Catalog/              ← موديول المنتجات
│   ├── Basket/               ← موديول سلة التسوق
│   └── Ordering/             ← موديول الطلبات
└── Shared/                    ← الأكواد المشتركة
    ├── Shared/               ← DDD, Behaviors, Extensions
    ├── Shared.Contracts/     ← CQRS Interfaces
    └── Shared.Messaging/     ← Integration Events
```

### المفهوم الأساسي: Modular Monolith

**ما هو؟**
- تطبيق واحد (Monolith) لكنه مقسم لموديولات مستقلة
- كل موديول له:
  - قاعدة بيانات خاصة (Schema مستقل)
  - Business Logic خاص
  - Models و Events خاصة
- الموديولات تتواصل مع بعضها عن طريق **Integration Events** فقط

**ليه مش Microservices؟**
- أسهل في الإدارة والتطوير
- مافيش Network Latency
- Transactions أسهل
- لكن جاهز للتحويل لـ Microservices لو احتجت

---

## 🎯 Domain-Driven Design (DDD) {#ddd-concepts}

### 1. Entity - الكيان الأساسي

**ما هو Entity؟**
- كائن له هوية فريدة (ID)
- الـ ID بيميزه عن باقي الكائنات حتى لو نفس البيانات

```csharp
// في Shared/Shared/DDD/Entity.cs
public class Entity<T> : IEntity<T>
{
    public T Id { get; set; } = default!;           // الهوية الفريدة
    public DateTime? CreatedAt { get; set; }         // متى اتعمل
    public string? CreatedBy { get; set; }          // مين عمله
    public DateTime? LastModified { get; set; }     // آخر تعديل
    public string? LastModifiedBy { get; set; }     // مين عدله
}
```

**مثال عملي:**
```csharp
// Product هو Entity
public class Product : Entity<Guid>
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// كل Product له ID مختلف
var product1 = new Product { Id = Guid.NewGuid(), Name = "iPhone" };
var product2 = new Product { Id = Guid.NewGuid(), Name = "iPhone" };
// حتى لو نفس الاسم، هما مختلفين بسبب الـ ID
```

---

### 2. Value Object - كائن القيمة

**ما هو Value Object؟**
- كائن ليس له هوية خاصة
- يتعرف عليه من خلال قيمه (Properties)
- لو قيمتين متطابقة = نفس الكائن

```csharp
// في Ordering/Orders/ValueObjects/Address.cs
public record Address
{
    public string FirstName { get; } = default!;
    public string LastName { get; } = default!;
    public string EmailAddress { get; } = default!;
    public string AddressLine { get; } = default!;
    public string Country { get; } = default!;
    public string State { get; } = default!;
    public string ZipCode { get; } = default!;
    
    // Factory Method للإنشاء
    public static Address Of(string firstName, string lastName, 
        string email, string addressLine, string country, 
        string state, string zipCode)
    {
        // هنا ممكن تحط Validation
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        
        return new Address
        {
            FirstName = firstName,
            LastName = lastName,
            EmailAddress = email,
            AddressLine = addressLine,
            Country = country,
            State = state,
            ZipCode = zipCode
        };
    }
}
```

**الفرق بين Entity و Value Object:**

| Entity | Value Object |
|--------|-------------|
| له ID فريد | مافيهوش ID |
| بيتقارن بالـ ID | بيتقارن بالقيم |
| ممكن يتغير (Mutable) | لا يتغير (Immutable) |
| مثال: Order, Product | مثال: Address, Money |

---

### 3. Aggregate - المجموعة المترابطة

**ما هو Aggregate؟**
- مجموعة من Entities و Value Objects مترابطين مع بعض
- له **Aggregate Root** (الكيان الرئيسي)
- كل التعديلات لازم تمر عبر الـ Root
- بيحمي consistency البيانات

```csharp
// في Shared/Shared/DDD/Aggregate.cs
public class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{
    // قائمة الأحداث اللي حصلت على الـ Aggregate
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyList<IDomainEvent> DomainEvents => 
        _domainEvents.AsReadOnly();

    // إضافة حدث جديد
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    // مسح الأحداث بعد معالجتها
    public IDomainEvent[] ClearDomainEvents()
    {
        IDomainEvent[] dequeuedEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return dequeuedEvents;
    }
}
```

**مثال عملي: Order Aggregate**

```csharp
// Order هو Aggregate Root
public class Order : Aggregate<Guid>
{
    // OrderItems هم جزء من الـ Aggregate
    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Guid CustomerId { get; private set; }
    public string OrderName { get; private set; } = default!;
    public Address ShippingAddress { get; private set; } = default!;
    public Payment Payment { get; private set; } = default!;
    public decimal TotalPrice { get; private set; }

    // ✅ Factory Method - الطريقة الوحيدة لإنشاء Order
    public static Order Create(
        Guid id, 
        Guid customerId, 
        string orderName,
        Address shippingAddress, 
        Address billingAddress, 
        Payment payment)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            OrderName = orderName,
            ShippingAddress = shippingAddress,
            Payment = payment,
            TotalPrice = 0
        };

        // 🎯 إضافة Domain Event
        order.AddDomainEvent(new OrderCreatedEvent(order));
        
        return order;
    }

    // ✅ إضافة منتج - مع Business Logic
    public void Add(Guid productId, int quantity, decimal price)
    {
        // Validation
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem != null)
        {
            // زيادة الكمية لو المنتج موجود
            existingItem.Quantity += quantity;
        }
        else
        {
            // إضافة منتج جديد
            var newItem = new OrderItem(Id, productId, price, quantity);
            _items.Add(newItem);
        }

        // إعادة حساب السعر الإجمالي
        TotalPrice = _items.Sum(item => item.Price * item.Quantity);
    }

    // ✅ حذف منتج
    public void Remove(Guid productId)
    {
        var orderItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (orderItem != null)
        {
            _items.Remove(orderItem);
            TotalPrice = _items.Sum(item => item.Price * item.Quantity);
        }
    }
}
```

**ليه نستخدم Aggregate؟**
1. ✅ **حماية البيانات**: مافيش حد يقدر يعدل OrderItem مباشرة
2. ✅ **Business Rules**: كل التعديلات فيها Validation
3. ✅ **Consistency**: الـ TotalPrice دايماً صحيح
4. ✅ **Domain Events**: كل تغيير بيولد Event

---

## 📢 الأحداث (Events) في النظام {#events-system}

### أنواع الأحداث في النظام

```
┌─────────────────────────────────────────────────────┐
│                  APPLICATION                        │
│                                                     │
│  ┌──────────────┐        ┌──────────────┐         │
│  │   MODULE A   │        │   MODULE B   │         │
│  │   Catalog    │        │   Basket     │         │
│  │              │        │              │         │
│  │  ┌────────┐  │        │  ┌────────┐  │         │
│  │  │ Domain │  │        │  │ Domain │  │         │
│  │  │ Events │──┼──┐     │  │ Events │  │         │
│  │  └────────┘  │  │     │  └────────┘  │         │
│  │      ↓       │  │     │      ↓       │         │
│  │  ┌────────┐  │  │     │  ┌────────┐  │         │
│  │  │Event   │  │  │     │  │Event   │  │         │
│  │  │Handler │  │  │     │  │Handler │  │         │
│  │  └────────┘  │  │     │  └────────┘  │         │
│  │      ↓       │  │     │              │         │
│  │  ┌────────┐  │  │     │              │         │
│  │  │Publish │  │  │     │              │         │
│  │  │to Bus  │──┼──┼─────┼──────────────┼───┐     │
│  │  └────────┘  │  │     │              │   │     │
│  └──────────────┘  │     └──────────────┘   │     │
│         ↓          │            ↑            │     │
│    ┌────────────┐  │            │            │     │
│    │Integration │  │    ┌───────┴────────┐   │     │
│    │Event       │  │    │   Integration  │   │     │
│    │(RabbitMQ)  │──┼────│   Event        │   │     │
│    └────────────┘  │    │   Consumer     │   │     │
│         ↓          │    └────────────────┘   │     │
│    ┌────────────┐  │                         │     │
│    │  Outbox    │  │                         │     │
│    │  Pattern   │  └─────────────────────────┘     │
│    └────────────┘                                  │
└─────────────────────────────────────────────────────┘
```

### 1. Domain Events - أحداث النطاق

**ما هي؟**
- أحداث تحصل **داخل** الموديول الواحد
- بتحصل لما Business Rule ينفذ
- بتتعالج بواسطة **MediatR** (in-memory)
- **مابتخرجش** من الموديول

```csharp
// في Shared/Shared/DDD/IDomainEvent.cs
public interface IDomainEvent : INotification  // من MediatR
{
    Guid EventId => Guid.NewGuid();
    DateTime OccurredOn => DateTime.Now;
    string EventType => GetType().AssemblyQualifiedName;
}
```

**مثال: ProductPriceChangedEvent**

```csharp
// في Catalog/Products/Events/ProductPriceChangedEvent.cs
public record ProductPriceChangedEvent(Product product) : IDomainEvent;
```

**متى يحصل؟**
```csharp
// في UpdateProductHandler
public async Task<UpdateProductResult> Handle(
    UpdateProductCommand command, 
    CancellationToken cancellationToken)
{
    var product = await dbContext.Products
        .FindAsync([command.Product.Id], cancellationToken);
    
    if (product == null)
        throw new ProductNotFoundException(command.Product.Id);

    // تحديث السعر
    decimal oldPrice = product.Price;
    product.Update(
        command.Product.Name,
        command.Product.Category,
        command.Product.Description,
        command.Product.ImageFile,
        command.Product.Price
    );

    // ✅ إضافة Domain Event لو السعر اتغير
    if (oldPrice != product.Price)
    {
        product.AddDomainEvent(new ProductPriceChangedEvent(product));
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    
    return new UpdateProductResult(true);
}
```

**معالجة Domain Event:**

```csharp
// في Catalog/Products/EventHandler/ProductPriceChangedEventHandler.cs
public class ProductPriceChangedEventHandler 
    (IBus bus, ILogger<ProductPriceChangedEventHandler> logger)
    : INotificationHandler<ProductPriceChangedEvent>  // MediatR Handler
{
    public async Task Handle(
        ProductPriceChangedEvent notification, 
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Domain Event handled: {DomainEvent}", 
            notification.GetType().Name
        );

        // 🔄 تحويل Domain Event إلى Integration Event
        var integrationEvent = new ProductPriceChangedIntegrationEvent
        {
            ProductId = notification.product.Id,
            Name = notification.product.Name,
            Price = notification.product.Price,
            // ... باقي البيانات
        };

        // 📤 نشر الـ Integration Event للموديولات التانية
        await bus.Publish(integrationEvent, cancellationToken);
    }
}
```

---

### 2. Integration Events - أحداث التكامل

**ما هي؟**
- أحداث تربط بين الموديولات المختلفة
- بتتنقل عبر **RabbitMQ** (Message Broker)
- بتستخدم **MassTransit** للإدارة
- بتضمن أن كل موديول يعرف بالتغييرات في الموديولات التانية

```csharp
// في Shared.Messaging/Events/IntegrationEvent.cs
public record IntegrationEvent
{
    public Guid EventId => Guid.NewGuid();
    public DateTime OccurredOn => DateTime.Now;
    public string EventType => GetType().AssemblyQualifiedName;
}
```

**مثال: ProductPriceChangedIntegrationEvent**

```csharp
// في Shared.Messaging/Events/ProductPriceChangedIntegrationEvent.cs
public record ProductPriceChangedIntegrationEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public List<string> category { get; set; } = default!;
    public decimal Price { get; set; }
}
```

**مثال: BasketCheckoutIntegrationEvent**

```csharp
// في Shared.Messaging/Events/BasketCheckoutIntegrationEvent.cs
public record BasketCheckoutIntegrationEvent : IntegrationEvent 
{
    public string UserName { get; set; } = default!;
    public Guid CustomerId { get; set; } = default!;
    public decimal TotalPrice { get; set; } = default!;
    
    // Shipping Address
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string EmailAddress { get; set; } = default!;
    public string AddressLine { get; set; } = default!;
    public string Country { get; set; } = default!;
    
    // Payment
    public string CardName { get; set; } = default!;
    public string CardNumber { get; set; } = default!;
    public string Expiration { get; set; } = default!;
    public string Cvv { get; set; } = default!;
    public int PaymentMethod { get; set; } = default!;
}
```

**معالجة Integration Event:**

```csharp
// في Basket/EventHandlers/ProductPriceChangedIntegrationEventHandler.cs
public class ProductPriceChangedIntegrationEventHandler 
    (ISender sender, ILogger<...> logger)
    : IConsumer<ProductPriceChangedIntegrationEvent>  // MassTransit Consumer
{
    public async Task Consume(
        ConsumeContext<ProductPriceChangedIntegrationEvent> context)
    {
        logger.LogInformation(
            "Integration Event Handled: {IntegrationEvent}", 
            context.Message.GetType().Name
        );

        // 📝 إنشاء Command لتحديث السعر في السلة
        var command = new UpdateItemPriceInBasketCommand(
            context.Message.ProductId,
            context.Message.Price
        );
        
        var result = await sender.Send(command);

        if (!result.IsSuccess)
            logger.LogError(
                "Error updating price in basket for product id: {ProductId}", 
                context.Message.ProductId
            );

        logger.LogInformation(
            "Price for product id: {ProductId} updated in basket", 
            context.Message.ProductId
        );
    }
}
```

---

## 🔄 Message Queue & Integration Events {#message-queue}

### معمارية Message Queue

```
┌────────────────────────────────────────────────────────────┐
│                    APPLICATION                             │
│                                                            │
│  ┌─────────────┐                    ┌─────────────┐       │
│  │   CATALOG   │                    │   BASKET    │       │
│  │   MODULE    │                    │   MODULE    │       │
│  │             │                    │             │       │
│  │  Product    │                    │  Basket     │       │
│  │  Price      │                    │  Items      │       │
│  │  Changed    │                    │             │       │
│  │      │      │                    │             │       │
│  │      ↓      │                    │             │       │
│  │  ┌────────┐ │                    │             │       │
│  │  │Domain  │ │                    │             │       │
│  │  │Event   │ │                    │             │       │
│  │  │Handler │ │                    │             │       │
│  │  └────┬───┘ │                    │             │       │
│  │       │     │                    │             │       │
│  │       ↓     │                    │             │       │
│  │  ┌────────┐ │    ┌──────────┐   │  ┌────────┐ │       │
│  │  │Publish │ │    │ RabbitMQ │   │  │Consumer│ │       │
│  │  │to Bus  │─┼───→│  Queue   │───┼→│Handler │ │       │
│  │  └────────┘ │    │          │   │  └───┬────┘ │       │
│  │             │    │ ┌──────┐ │   │      ↓      │       │
│  │             │    │ │ MSG  │ │   │  Update     │       │
│  │             │    │ │ MSG  │ │   │  Basket     │       │
│  │             │    │ │ MSG  │ │   │  Prices     │       │
│  │             │    │ └──────┘ │   │             │       │
│  └─────────────┘    └──────────┘   └─────────────┘       │
│                                                            │
│  ┌──────────────────────────────────────────────┐         │
│  │           OUTBOX PATTERN                     │         │
│  │                                              │         │
│  │  Database                   Background       │         │
│  │  ┌──────────────┐          ┌──────────┐     │         │
│  │  │OutboxMessages│  <─Read──│ Outbox   │     │         │
│  │  │  Table       │          │Processor │     │         │
│  │  │              │          └────┬─────┘     │         │
│  │  │ Id           │               │           │         │
│  │  │ Type         │               ↓           │         │
│  │  │ Content      │          Publish to       │         │
│  │  │ ProcessedOn  │          RabbitMQ         │         │
│  │  └──────────────┘                           │         │
│  └──────────────────────────────────────────────┘         │
└────────────────────────────────────────────────────────────┘
```

### RabbitMQ - Message Broker

**ما هو RabbitMQ؟**
- نظام إدارة الرسائل (Message Broker)
- بيشتغل كـ "صندوق بريد" بين الموديولات
- بيضمن توصيل الرسائل حتى لو في مشاكل

**المكونات الأساسية:**

```
Publisher          Queue           Consumer
(المرسل)          (الطابور)       (المستقبل)
   │                 │                 │
   │  ┌───────┐      │                 │
   │→ │Message│ ─────→  ┌─────────┐   │
   │  └───────┘      │  │ Message │   │
   │                 │  │ Message │ ──→ Handler
   │  ┌───────┐      │  │ Message │   │
   │→ │Message│ ─────→  └─────────┘   │
   │  └───────┘      │                 │
```

**الإعداد في docker-compose:**

```yaml
messagebus:
  image: rabbitmq:management
  container_name: messagebus
  environment:
    RABBITMQ_DEFAULT_USER: guest
    RABBITMQ_DEFAULT_PASS: guest
  ports:
    - "5672:5672"    # AMQP Protocol
    - "15672:15672"  # Management UI
```

---

### MassTransit - إدارة الرسائل

**ما هو MassTransit؟**
- مكتبة .NET لإدارة Message Queue
- بتسهل التعامل مع RabbitMQ
- بتوفر Retry, Error Handling, و Monitoring

**الإعداد:**

```csharp
// في Shared/Messaging/Extensions/MassTransitExtensions.cs
public static IServiceCollection AddMassTransitWithAssemblies(
    this IServiceCollection services,
    IConfiguration configuration,
    params Assembly[] assemblies)
{
    services.AddMassTransit(busConfig =>
    {
        // ✅ تسجيل جميع Consumers من الـ Assemblies
        busConfig.SetKebabCaseEndpointNameFormatter();
        busConfig.SetInMemorySagaRepositoryProvider();
        busConfig.AddConsumers(assemblies);
        busConfig.AddSagaStateMachines(assemblies);
        busConfig.AddSagas(assemblies);
        busConfig.AddActivities(assemblies);

        // ✅ إعداد RabbitMQ
        busConfig.UsingRabbitMq((context, configurator) =>
        {
            configurator.Host(new Uri(configuration["MessageBroker:Host"]!), 
                h =>
                {
                    h.Username(configuration["MessageBroker:UserName"]!);
                    h.Password(configuration["MessageBroker:Password"]!);
                });
            
            configurator.ConfigureEndpoints(context);
        });
    });

    return services;
}
```

**كيف تنشر رسالة:**

```csharp
// في أي Handler
public class MyHandler(IBus bus)
{
    public async Task Handle(...)
    {
        var integrationEvent = new MyIntegrationEvent
        {
            Data = "some data"
        };
        
        // 📤 نشر الرسالة
        await bus.Publish(integrationEvent, cancellationToken);
    }
}
```

**كيف تستقبل رسالة:**

```csharp
// Consumer في موديول تاني
public class MyEventConsumer : IConsumer<MyIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MyIntegrationEvent> context)
    {
        // 📨 استقبال ومعالجة الرسالة
        var message = context.Message;
        
        // Do something with the message
        await ProcessMessage(message);
    }
}
```

---

### Outbox Pattern - نمط صندوق الصادر

**المشكلة:**
```
❌ لو حصل Crash بعد حفظ البيانات وقبل إرسال الرسالة؟
❌ لو RabbitMQ مش شغال وقت الإرسال؟
❌ لو في Network Issue؟

النتيجة: البيانات اتحفظت لكن الموديولات التانية مش عارفة!
```

**الحل: Outbox Pattern**

```
✅ الخطوات:
1. حفظ البيانات في Database
2. حفظ الرسالة في جدول OutboxMessages (نفس Transaction)
3. Background Service يقرأ من OutboxMessages
4. ينشر الرسائل لـ RabbitMQ
5. يعلم الرسالة كـ Processed
```

**جدول OutboxMessages:**

```csharp
// في Basket/Data/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;      // نوع الرسالة
    public string Content { get; set; } = default!;   // محتوى الرسالة (JSON)
    public DateTime OccurredOn { get; set; }          // وقت الحدث
    public DateTime? ProcessedOn { get; set; }        // وقت الإرسال (null = لم يُرسل)
}
```

**حفظ رسالة في Outbox:**

```csharp
// في CheckoutBasketHandler
public async Task<CheckoutBasketResult> Handle(
    CheckoutBasketCommand command,
    CancellationToken cancellationToken)
{
    // ✅ حذف السلة
    var basket = await dbContext.Baskets
        .FirstOrDefaultAsync(x => x.UserName == command.BasketCheckout.UserName);
    
    if (basket == null)
        throw new BasketNotFoundException(command.BasketCheckout.UserName);
    
    dbContext.Baskets.Remove(basket);

    // ✅ إنشاء Integration Event
    var basketCheckoutEvent = command.BasketCheckout
        .Adapt<BasketCheckoutIntegrationEvent>();

    // ✅ حفظ الرسالة في Outbox (نفس Transaction مع حذف السلة)
    var outboxMessage = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = typeof(BasketCheckoutIntegrationEvent).AssemblyQualifiedName!,
        Content = JsonSerializer.Serialize(basketCheckoutEvent),
        OccurredOn = DateTime.UtcNow
    };
    
    dbContext.OutboxMessages.Add(outboxMessage);

    // ✅ حفظ كل شيء في Transaction واحد
    await dbContext.SaveChangesAsync(cancellationToken);

    return new CheckoutBasketResult(true);
}
```

**معالج Outbox - Background Service:**

```csharp
// في Basket/Data/Processors/OutboxProcessor.cs
public class OutboxProcessor(
    IServiceProvider serviceProvider, 
    IBus bus, 
    ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 🔄 يشتغل في الخلفية باستمرار
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<BasketDbContext>();

                // 📖 قراءة الرسائل اللي لم تُرسل
                var outboxMessages = await dbContext.OutboxMessages
                    .Where(m => m.ProcessedOn == null)
                    .ToListAsync(stoppingToken);

                foreach (var message in outboxMessages)
                {
                    // 🔄 تحويل JSON إلى Object
                    var eventType = Type.GetType(message.Type);
                    if (eventType == null)
                    {
                        logger.LogWarning("Could not resolve type: {type}", 
                            message.Type);
                        continue;
                    }

                    var eventMessage = JsonSerializer.Deserialize(
                        message.Content, 
                        eventType
                    );

                    if (eventMessage == null)
                    {
                        logger.LogWarning("Deserialized message was null");
                        continue;
                    }

                    // 📤 نشر الرسالة إلى RabbitMQ
                    await bus.Publish(eventMessage, stoppingToken);

                    // ✅ تعليم الرسالة كـ Processed
                    message.ProcessedOn = DateTime.UtcNow;

                    logger.LogInformation(
                        "Successfully Processed outbox message with ID: {id}", 
                        message.Id
                    );
                }

                // ✅ حفظ التحديثات
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            // ⏰ انتظر 10 ثواني قبل المحاولة التالية
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

**مميزات Outbox Pattern:**

```
✅ Reliable Messaging - الرسائل مضمونة التوصيل
✅ Transactional Consistency - البيانات والرسائل في نفس Transaction
✅ Automatic Retry - لو فشل الإرسال، سيحاول مرة أخرى
✅ No Message Loss - لو تم إيقاف التطبيق، الرسائل محفوظة
✅ Order Preservation - الرسائل تُرسل بنفس الترتيب
```

---

## 💡 أمثلة عملية من الكود {#practical-examples}

### مثال كامل 1: تغيير سعر منتج

```
🎯 السيناريو:
1. Admin يغير سعر منتج في Catalog Module
2. لازم نحدث السعر في كل السلات اللي فيها المنتج ده

🔄 المسار:
Catalog Module → Domain Event → Integration Event → Basket Module
```

#### الخطوة 1: تحديث المنتج (Catalog)

```csharp
// في Catalog/Products/Features/UpdateProduct/UpdateProductHandler.cs
public async Task<UpdateProductResult> Handle(
    UpdateProductCommand command, 
    CancellationToken cancellationToken)
{
    // 📖 جلب المنتج
    var product = await dbContext.Products
        .FindAsync([command.Product.Id], cancellationToken);
    
    if (product == null)
        throw new ProductNotFoundException(command.Product.Id);

    // 💾 حفظ السعر القديم
    decimal oldPrice = product.Price;
    
    // ✏️ تحديث البيانات
    product.Update(
        command.Product.Name,
        command.Product.Category,
        command.Product.Description,
        command.Product.ImageFile,
        command.Product.Price
    );

    // 🎯 إضافة Domain Event لو السعر اتغير
    if (oldPrice != product.Price)
    {
        product.AddDomainEvent(
            new ProductPriceChangedEvent(product)
        );
    }

    // ✅ حفظ في Database
    // الـ Domain Events هتتنشر تلقائياً عن طريق MediatR
    await dbContext.SaveChangesAsync(cancellationToken);
    
    return new UpdateProductResult(true);
}
```

#### الخطوة 2: معالجة Domain Event (داخل Catalog)

```csharp
// في Catalog/Products/EventHandler/ProductPriceChangedEventHandler.cs
public class ProductPriceChangedEventHandler 
    (IBus bus, ILogger<ProductPriceChangedEventHandler> logger)
    : INotificationHandler<ProductPriceChangedEvent>
{
    public async Task Handle(
        ProductPriceChangedEvent notification, 
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", 
            notification.GetType().Name);

        // 🔄 تحويل Domain Event إلى Integration Event
        var integrationEvent = new ProductPriceChangedIntegrationEvent
        {
            ProductId = notification.product.Id,
            Name = notification.product.Name,
            Description = notification.product.Description,
            ImageFile = notification.product.ImageFile,
            category = notification.product.Category,
            Price = notification.product.Price
        };

        // 📤 نشر Integration Event إلى RabbitMQ
        await bus.Publish(integrationEvent, cancellationToken);
    }
}
```

#### الخطوة 3: استقبال Integration Event (في Basket)

```csharp
// في Basket/EventHandlers/ProductPriceChangedIntegrationEventHandler.cs
public class ProductPriceChangedIntegrationEventHandler 
    (ISender sender, ILogger<...> logger)
    : IConsumer<ProductPriceChangedIntegrationEvent>
{
    public async Task Consume(
        ConsumeContext<ProductPriceChangedIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event Handled: {IntegrationEvent}", 
            context.Message.GetType().Name);

        // 📝 إنشاء Command لتحديث السعر في كل السلات
        var command = new UpdateItemPriceInBasketCommand(
            context.Message.ProductId,
            context.Message.Price
        );
        
        // ✅ تنفيذ التحديث
        var result = await sender.Send(command);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Error updating price in basket for product id: {ProductId}", 
                context.Message.ProductId
            );
        }
        else
        {
            logger.LogInformation(
                "Price for product id: {ProductId} updated in basket", 
                context.Message.ProductId
            );
        }
    }
}
```

#### الخطوة 4: تحديث السعر في السلات

```csharp
// في Basket/Features/UpdateItemPriceInBasket/UpdateItemPriceInBasketHandler.cs
public async Task<UpdateItemPriceInBasketResult> Handle(
    UpdateItemPriceInBasketCommand command, 
    CancellationToken cancellationToken)
{
    // 📖 جلب كل السلات اللي فيها المنتج
    var baskets = await dbContext.Baskets
        .Where(b => b.Items.Any(i => i.ProductId == command.ProductId))
        .ToListAsync(cancellationToken);

    if (!baskets.Any())
    {
        logger.LogInformation(
            "No baskets found with product: {ProductId}", 
            command.ProductId
        );
        return new UpdateItemPriceInBasketResult(true);
    }

    // 🔄 تحديث السعر في كل سلة
    foreach (var basket in baskets)
    {
        var item = basket.Items
            .FirstOrDefault(i => i.ProductId == command.ProductId);
        
        if (item != null)
        {
            item.Price = command.Price;
            logger.LogInformation(
                "Updated price for product {ProductId} in basket {BasketName}", 
                command.ProductId, 
                basket.UserName
            );
        }
    }

    // ✅ حفظ التغييرات
    await dbContext.SaveChangesAsync(cancellationToken);

    return new UpdateItemPriceInBasketResult(true);
}
```

**Flow Diagram:**

```
┌─────────────────────────────────────────────────────────┐
│                   CATALOG MODULE                        │
│                                                         │
│  Admin Updates Product Price                           │
│           │                                             │
│           ↓                                             │
│  UpdateProductHandler                                  │
│           │                                             │
│           ├─→ Update Product in DB                     │
│           │                                             │
│           └─→ AddDomainEvent(ProductPriceChangedEvent)│
│                       │                                 │
│                       ↓                                 │
│           MediatR publishes Domain Event               │
│                       │                                 │
│                       ↓                                 │
│  ProductPriceChangedEventHandler                       │
│           │                                             │
│           └─→ Create Integration Event                 │
│                       │                                 │
│                       ↓                                 │
│           Publish to RabbitMQ                          │
└───────────────────────┼───────────────────────────────┘
                        │
                        │ RabbitMQ Queue
                        │
                        ↓
┌─────────────────────────────────────────────────────────┐
│                   BASKET MODULE                         │
│                                                         │
│  ProductPriceChangedIntegrationEventHandler            │
│           │                                             │
│           ↓                                             │
│  Consume Event from RabbitMQ                           │
│           │                                             │
│           ↓                                             │
│  Send UpdateItemPriceInBasketCommand                   │
│           │                                             │
│           ↓                                             │
│  UpdateItemPriceInBasketHandler                        │
│           │                                             │
│           ├─→ Find all Baskets with this Product      │
│           │                                             │
│           ├─→ Update Price in each Basket              │
│           │                                             │
│           └─→ SaveChanges to DB                        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

### مثال كامل 2: Checkout السلة

```
🎯 السيناريو:
1. User يعمل Checkout للسلة
2. Basket Module يحفظ البيانات في Outbox
3. Background Service ينشر الحدث
4. Ordering Module يستقبل ويعمل Order جديد
```

#### الخطوة 1: Checkout Endpoint

```csharp
// في Basket/Features/CheckoutBasket/CheckoutBasketEndpoint.cs
public class CheckoutBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/basket/checkout", async (
            CheckoutBasketRequest request,
            ISender sender,
            ClaimsPrincipal user) =>
        {
            // 🔐 جلب اسم المستخدم من Token
            var userName = user.Identity?.Name;

            // ✅ إضافة userName للبيانات
            var updatedBasketCheckout = request.BasketCheckout with
            {
                UserName = userName!
            };

            // 📝 إنشاء Command
            var command = new CheckoutBasketCommand(updatedBasketCheckout);
            
            // ✉️ إرسال Command للـ Handler
            var result = await sender.Send(command);
            
            var response = result.Adapt<CheckoutBasketResponse>();
            return Results.Ok(response);
        })
        .WithName("CheckoutBasket")
        .RequireAuthorization();  // 🔐 يتطلب Authentication
    }
}
```

#### الخطوة 2: Checkout Handler + Outbox

```csharp
// في Basket/Features/CheckoutBasket/CheckoutBasketHandler.cs
public async Task<CheckoutBasketResult> Handle(
    CheckoutBasketCommand command,
    CancellationToken cancellationToken)
{
    // 📖 جلب السلة
    var basket = await dbContext.Baskets
        .FirstOrDefaultAsync(
            x => x.UserName == command.BasketCheckout.UserName,
            cancellationToken
        );
    
    if (basket == null)
        throw new BasketNotFoundException(command.BasketCheckout.UserName);
    
    // ❌ حذف السلة (Checkout = إنهاء السلة)
    dbContext.Baskets.Remove(basket);

    // 🎯 إنشاء Integration Event
    var basketCheckoutEvent = command.BasketCheckout
        .Adapt<BasketCheckoutIntegrationEvent>();

    // 📦 حفظ في Outbox (لضمان الإرسال)
    var outboxMessage = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = typeof(BasketCheckoutIntegrationEvent)
            .AssemblyQualifiedName!,
        Content = JsonSerializer.Serialize(basketCheckoutEvent),
        OccurredOn = DateTime.UtcNow
    };
    
    dbContext.OutboxMessages.Add(outboxMessage);

    // ✅ حفظ كل شيء (Delete Basket + Save Outbox Message)
    await dbContext.SaveChangesAsync(cancellationToken);

    return new CheckoutBasketResult(true);
}
```

#### الخطوة 3: Background Service يرسل من Outbox

```csharp
// OutboxProcessor يقرأ من جدول OutboxMessages
// كل 10 ثواني ويرسل الرسائل اللي ProcessedOn = null

// شفناه قبل كده بالتفصيل
```

#### الخطوة 4: Ordering Module يستقبل الحدث

```csharp
// في Ordering/EventHandlers/BasketCheckoutIntegrationEventHandler.cs
public class BasketCheckoutIntegrationEventHandler 
    (ISender sender, ILogger<...> logger)
    : IConsumer<BasketCheckoutIntegrationEvent>
{
    public async Task Consume(
        ConsumeContext<BasketCheckoutIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event handled: {IntegrationEvent}", 
            context.Message.GetType().Name);

        // 🔄 تحويل Integration Event إلى CreateOrderCommand
        var createOrderCommand = MapToCreateOrderCommand(context.Message);

        // 📝 إنشاء Order جديد
        await sender.Send(createOrderCommand);
    }

    private CreateOrderCommand MapToCreateOrderCommand(
        BasketCheckoutIntegrationEvent message)
    {
        // 📍 إنشاء Address
        var addressDto = new AddressDto(
            message.FirstName,
            message.LastName,
            message.EmailAddress,
            message.Country,
            message.State,
            message.ZipCode,
            message.AddressLine
        );

        // 💳 إنشاء Payment
        var paymentDto = new PaymentDto(
            message.CardName,
            message.CardNumber,
            message.Expiration,
            message.Cvv,
            message.PaymentMethod
        );

        var orderId = Guid.NewGuid();

        // 🛒 إنشاء Order مع Items
        var orderDto = new OrderDto(
            Id: orderId,
            CustomerId: message.CustomerId,
            OrderName: message.UserName,
            ShippingAddress: addressDto,
            BillingAddress: addressDto,
            Payment: paymentDto,
            Items:
            [
                // TODO: Get items from basket data
                new OrderItemDto(orderId, productId1, 2, 500),
                new OrderItemDto(orderId, productId2, 1, 400)
            ]
        );

        return new CreateOrderCommand(orderDto);
    }
}
```

#### الخطوة 5: إنشاء Order

```csharp
// في Ordering/Features/CreateOrder/CreateOrderHandler.cs
public async Task<CreateOrderResult> Handle(
    CreateOrderCommand command,
    CancellationToken cancellationToken)
{
    // ✅ إنشاء Order Aggregate
    var order = CreateNewOrder(command.Order);

    // 💾 حفظ في Database
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    // 📢 الـ Domain Events (OrderCreatedEvent) هيتنشر تلقائياً

    return new CreateOrderResult(order.Id);
}

private Order CreateNewOrder(OrderDto orderDto)
{
    // 📍 إنشاء Address Value Object
    var shippingAddress = Address.Of(
        orderDto.ShippingAddress.FirstName,
        orderDto.ShippingAddress.LastName,
        orderDto.ShippingAddress.EmailAddress,
        orderDto.ShippingAddress.AddressLine,
        orderDto.ShippingAddress.Country,
        orderDto.ShippingAddress.State,
        orderDto.ShippingAddress.ZipCode
    );

    var billingAddress = Address.Of(
        orderDto.BillingAddress.FirstName,
        orderDto.BillingAddress.LastName,
        orderDto.BillingAddress.EmailAddress,
        orderDto.BillingAddress.AddressLine,
        orderDto.BillingAddress.Country,
        orderDto.BillingAddress.State,
        orderDto.BillingAddress.ZipCode
    );

    // 💳 إنشاء Payment Value Object
    var payment = Payment.Of(
        orderDto.Payment.CardName,
        orderDto.Payment.CardNumber,
        orderDto.Payment.Expiration,
        orderDto.Payment.Cvv,
        orderDto.Payment.PaymentMethod
    );

    // ✅ إنشاء Order باستخدام Factory Method
    var order = Order.Create(
        orderDto.Id,
        orderDto.CustomerId,
        orderDto.OrderName,
        shippingAddress,
        billingAddress,
        payment
    );

    // 🛒 إضافة Items
    foreach (var item in orderDto.Items)
    {
        order.Add(item.ProductId, item.Quantity, item.Price);
    }

    return order;
}
```

**Complete Flow:**

```
USER                BASKET MODULE           RABBITMQ         ORDERING MODULE
  │                      │                      │                  │
  │ POST /checkout       │                      │                  │
  ├─────────────────────→│                      │                  │
  │                      │                      │                  │
  │                      │ 1. Delete Basket     │                  │
  │                      │ 2. Save to Outbox    │                  │
  │                      │                      │                  │
  │                      ├──────────┐           │                  │
  │                      │          │           │                  │
  │                      │   Background         │                  │
  │                      │   Service            │                  │
  │                      │   (Every 10s)        │                  │
  │                      │          │           │                  │
  │                      │   Read Outbox        │                  │
  │                      │          │           │                  │
  │                      │          ↓           │                  │
  │                      │   Publish Event      │                  │
  │                      ├──────────────────────→                  │
  │                      │                      │                  │
  │                      │                      │  Consume Event   │
  │                      │                      ├─────────────────→│
  │                      │                      │                  │
  │                      │                      │  Create Order    │
  │                      │                      │  Save to DB      │
  │                      │                      │                  │
  │  ← 200 OK ──────────┤                      │                  │
  │  {IsSuccess: true}   │                      │                  │
```

---

## 📊 ملخص المفاهيم

### DDD Summary

| المفهوم | الوصف | مثال |
|---------|-------|------|
| **Entity** | كائن له هوية فريدة (ID) | `Product`, `Order`, `Customer` |
| **Value Object** | كائن بدون هوية، يتعرف من قيمه | `Address`, `Money`, `Email` |
| **Aggregate** | مجموعة Entities مترابطة | `Order` + `OrderItems` |
| **Aggregate Root** | نقطة الدخول للـ Aggregate | `Order` (لتعديل OrderItems) |
| **Domain Event** | حدث داخل الموديول | `ProductPriceChangedEvent` |

### Events Summary

| النوع | الاستخدام | المكتبة | النطاق |
|-------|-----------|---------|--------|
| **Domain Event** | داخل الموديول | MediatR | In-Memory |
| **Integration Event** | بين الموديولات | MassTransit | RabbitMQ |

### Patterns Summary

| النمط | المشكلة | الحل |
|-------|---------|------|
| **CQRS** | خلط Read/Write | فصل Queries عن Commands |
| **Outbox Pattern** | فقدان الرسائل | حفظ الرسائل في DB قبل الإرسال |
| **Repository Pattern** | تعقيد الوصول للبيانات | طبقة وسيطة بين Domain و DB |
| **Mediator Pattern** | تعقيد العلاقات بين Objects | MediatR يربط بينهم بدون dependency مباشر |

---

## 🎓 نصائح للفهم

### 1. افتح المشروع وتتبع Flow

```
ابدأ من Endpoint → Handler → Repository → Events
مثلاً: UpdateProduct من أول ما User يضغط زرار لحد ما Basket يتحدث
```

### 2. استخدم Logs

```csharp
// كل Handler فيه Logs
logger.LogInformation("Domain Event handled: {DomainEvent}", ...);
logger.LogInformation("Integration Event Handled: {IntegrationEvent}", ...);

// تابع الـ Logs عشان تفهم المسار:
docker-compose logs api -f
```

### 3. افحص RabbitMQ Management UI

```
URL: http://localhost:15672
User: guest
Password: guest

شوف:
- Queues: عدد الرسائل المنتظرة
- Exchanges: كيف الرسائل بتتوزع
- Connections: مين متصل
```

### 4. اقرأ الكود بالترتيب

```
1. Models (Entities, Value Objects, Aggregates)
2. Commands/Queries (CQRS)
3. Handlers (Business Logic)
4. Events (Domain & Integration)
5. Event Handlers (Reactions)
```

---

## 🔗 Resources مفيدة

### Documentation
- [MassTransit Docs](https://masstransit.io/)
- [MediatR Wiki](https://github.com/jbogard/MediatR/wiki)
- [Domain-Driven Design](https://martinfowler.com/tags/domain%20driven%20design.html)

### Videos (ابحث عن)
- "Domain Driven Design Fundamentals"
- "CQRS and Event Sourcing"
- "Microservices Messaging with RabbitMQ"

---

## 🎯 الخلاصة

**المشروع ده:**
- ✅ **Modular**: كل موديول مستقل
- ✅ **Scalable**: ممكن تفصل أي موديول لـ Microservice
- ✅ **Maintainable**: الكود منظم وواضح
- ✅ **Reliable**: Outbox Pattern بيضمن delivery
- ✅ **Testable**: كل جزء ممكن يتاختبر لوحده

**النصيحة الأهم:**
```
ماتحاولش تفهم كل حاجة مرة واحدة!
ابدأ بموديول واحد (مثلاً Catalog)
اتتبع flow كامل من Endpoint لحد Database
بعدين شوف ازاي بيتواصل مع موديول تاني
```

---

**هل محتاج توضيح أكتر لأي جزء؟ 🤔**
