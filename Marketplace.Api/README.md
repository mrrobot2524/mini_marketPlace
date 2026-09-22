# 🛒 Marketplace API

Mini marketplace uchun buyurtma va ombor boshqaruvchi backend service. PostgreSQL + Redis + JWT auth + Docker. Bir nechta item'li buyurtmalar, idempotency, concurrency-safe stock reservation va avtomatik bekor qilish bilan.

> **ER-diagramma:** [dbdiagram.io/d/6ab2af685869425612621aa8](https://dbdiagram.io/d/6ab2af685869425612621aa8)

---

## 📋 Talablar (Requirements)

- ✅ `POST /products` — mahsulot yaratish
- ✅ `POST /orders` — bir nechta item'li buyurtma, **Idempotency-Key header** majburiy
- ✅ `GET /orders/{id}` — status: `pending` → `confirmed` / `cancelled`
- ✅ `POST /orders/{id}/cancel` — reserved stock qaytarilishi
- ✅ JWT authorization
- ✅ Fon jarayon: 15 daqiqada to'lanmagan pending buyurtmalarni avtomatik bekor qilish
- ✅ PostgreSQL (raw SQL, ORM yo'q) + Redis caching
- ✅ Concurrency correctness: 50 parallel so'rov bitta productga (stock=10) → 10 tasi muvaffaqiyatli, 40 tasi `409 Conflict`
- ✅ Qatlamli arxitektura: Controller → Service → Repository
- ✅ Docker Compose — bitta buyruqda ko'tariladi, `:8080` portda ishlaydi

---

## 🛠 Texnik stek

| Qatlam            | Texnologiya                                  |
|-------------------|---------------------------------------------|
| Framework         | .NET 10 / ASP.NET Core                      |
| Database          | PostgreSQL 16 (raw SQL via Npgsql)          |
| Cache             | Redis 7 (StackExchange.Redis)               |
| Auth              | JWT Bearer tokens                            |
| API docs          | Swagger / OpenAPI                            |
| Containerization  | Docker + Docker Compose                      |
| C# features       | `record`, `readonly struct`, `delegate` (`Func<...>`) |

---

## 🏗 Arxitektura

```
Marketplace.Api/
├── Controllers/              # HTTP endpointlar
│   ├── AuthController.cs
│   ├── ProductsController.cs
│   └── OrdersController.cs
├── Services/                 # Biznes logika
│   ├── ProductService.cs
│   ├── OrderService.cs
│   ├── RedisCacheService.cs
│   └── PendingOrderCancellationService.cs   # BackgroundService (15 min)
├── Repositories/             # Ma'lumotlar bazasi bilan ishlash
│   ├── DbSession.cs          # record (Connection + Transaction)
│   ├── IdempotencyKeyRepository.cs
│   ├── OrderItemRepository.cs
│   ├── OrderRepository.cs
│   ├── ProductRepository.cs
│   └── Converters/
│       └── OrderStatusConverter.cs
├── Models/                   # Domain modellari (records & structs)
│   ├── Product.cs
│   ├── Order.cs
│   ├── OrderItem.cs
│   ├── OrderItemLine.cs
│   ├── OrderStatus.cs        # enum
│   └── OrderCacheKey.cs      # readonly struct
├── DTOs/                     # Request/Response modellari (records)
├── Exceptions/               # Custom exceptions + GlobalExceptionHandler
├── Options/                  # Strongly-typed configuration (Jwt, Redis, Postgres)
├── Program.cs                # DI, middleware, pipeline
├── init.sql                  # PostgreSQL schema
├── Dockerfile
├── docker-compose.yml
└── .env / .env.example
```

### Design patternlar

- **Layered architecture** — Controller → Service → Repository. Har qatlam o'z mas'uliyatiga ega.
- **Unit of Work (lite)** — `DbSession` record `NpgsqlConnection` + `NpgsqlTransaction` ni birgalikda ko'chiradi.
- **Strategy via `delegate`** — `ExecuteInTransactionAsync<T>(Func<DbSession, CancellationToken, Task<T>>)` orqali transaction boilerplate yagona joyga jamlangan.
- **Idempotency via unique constraint** — `(user_id, idempotency_key)` UNIQUE PostgreSQL darajasida kafolatlaydi.
- **Pessimistic locking** — `SELECT ... FOR UPDATE` stock race condition oldini oladi.

---

## 🗃 Ma'lumotlar bazasi sxemasi

To'liq ER-diagramma: **[dbdiagram.io/d/6ab2af685869425612621aa8](https://dbdiagram.io/d/6ab2af685869425612621aa8)**

### Jadval umumiy ko'rinishi

| Jadval             | Maqsad                                                  |
|--------------------|--------------------------------------------------------|
| `products`         | Mahsulot katalogi (narx, qoldiq)                       |
| `orders`           | Buyurtmalar (status: pending/confirmed/cancelled)     |
| `order_items`      | Buyurtma tarkibi (snapshot narxi bilan)               |
| `idempotency_keys` | Idempotency kafolati (UNIQUE user_id + key)            |

### CHECK constraints (init.sql ichida)

- `products.price > 0`
- `products.stock_quantity >= 0`
- `orders.status IN ('pending', 'confirmed', 'cancelled')`
- `order_items.quantity > 0`
- `order_items.price >= 0`

### Indexlar

- `orders(user_id)` — foydalanuvchi buyurtmalari
- `orders(status, created_at)` — background job uchun (composite index)
- `order_items(order_id)` — buyurtma pozitsiyalari
- `order_items(product_id)` — mahsulot bo'yicha analitika
- `idempotency_keys(user_id, idempotency_key) UNIQUE` — idempotency kafolati

---

## 🚀 Tez boshlash (Quick Start)

### 1. Talablar

- Docker (Engine v24+)
- Docker Compose v2 (`docker compose` — probel bilan)
- `curl` yoki Postman test uchun

### 2. Konfiguratsiya

```bash
cd Marketplace.Api

# .env faylini nusxalash va to'ldirish
cp .env.example .env
```

`.env` faylini oching va qiymatlarni o'zgartiring:

```env
POSTGRES_DB=marketplace
POSTGRES_USER=postgres
POSTGRES_PASSWORD=change-me-strong-password

JWT_KEY=change-this-to-a-very-long-random-string-at-least-32-chars
JWT_EXPIRATION_HOURS=1

REDIS_CONNECTION_STRING=redis:6379
```

> ⚠️ **Muhim:** `JWT_KEY` kamida 32 belgidan iborat bo'lishi shart. Aks holda ilova ishga tushishda xato bilan to'xtaydi (Options Validator).

### 3. Konteynerni ishga tushirish

```bash
docker compose up --build -d
```

Docker quyidagilarni bajardi:
1. `postgres:16` ni ko'tardi (init.sql ni avtomatik bajaradi)
2. `redis:7` ni ko'tardi
3. `marketplace-api` .NET 10 ilovasini ko'tardi (port `:8080`)
4. Healthcheck'larni yoqdi — API faqat BД va Redis tayyor bo'lgach ishga tushadi

### 4. Tayyorlikni tekshirish

```bash
# Health endpoint
curl -s http://localhost:8080/health | jq .

# Javob:
# {
#   "status": "Healthy",
#   "entries": {
#     "postgres": { "status": "Healthy", ... },
#     "redis": { "status": "Healthy", ... }
#   }
# }

# Jadval mavjudligini tekshirish
docker exec -it marketplace-postgres psql -U postgres -d marketplace -c "\dt"
```

### 5. Swagger UI

Brauzerda oching: [http://localhost:8080/swagger](http://localhost:8080/swagger)

---

## 📡 API endpointlar

### Auth

| Method | Endpoint            | Tavsif                          |
|--------|---------------------|---------------------------------|
| POST   | `/auth/login?userId=1` | JWT token olish (demo uchun)  |

**Misol:**
```bash
curl -s -X POST "http://localhost:8080/auth/login?userId=1"
# {"token":"eyJhbGciOi..."}
```

### Products

| Method | Endpoint               | Tavsif                       |
|--------|------------------------|------------------------------|
| GET    | `/api/products?page=1&pageSize=10` | Ro'yxat (paginatsiya) |
| GET    | `/api/products/{id}`   | Bitta mahsulot              |
| POST   | `/api/products`        | Yangi mahsulot              |
| PUT    | `/api/products/{id}`   | Mahsulotni yangilash       |
| DELETE | `/api/products/{id}`   | Mahsulotni o'chirish        |

**Misol:**
```bash
# Yaratish
curl -X POST http://localhost:8080/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop","price":999.99,"stockQuantity":10}'

# Javob (201 Created):
# {
#   "id": 1,
#   "name": "Laptop",
#   "price": 999.99,
#   "stockQuantity": 10
# }

# Paginatsiya
curl "http://localhost:8080/api/products?page=1&pageSize=10"

# Yangilash
curl -X PUT http://localhost:8080/api/products/1 \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop Pro","price":1299.99,"stockQuantity":5}'

# O'chirish
curl -X DELETE http://localhost:8080/api/products/1
```

### Orders (JWT talab qilinadi)

| Method | Endpoint                    | Tavsif                                |
|--------|-----------------------------|---------------------------------------|
| POST   | `/orders`                   | Yangi buyurtma (Idempotency-Key bilan)|
| GET    | `/orders?page=1&pageSize=10`| Ro'yxat (paginatsiya)                |
| GET    | `/orders/{id}`              | Bitta buyurtma                       |
| POST   | `/orders/{id}/cancel`       | Buyurtmani bekor qilish             |

**Misol:**
```bash
TOKEN=$(curl -s -X POST "http://localhost:8080/auth/login?userId=1" | jq -r .token)

# Buyurtma yaratish (Idempotency-Key header MAJBURIY!)
curl -i -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: test-key-1" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productId":1,"quantity":2}]}'

# Javob (200 OK):
# {"id":1}

# Bir xil key bilan qayta yuborish — bir xil order_id qaytadi, stock 2-marta kamamaydi
curl -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: test-key-1" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productId":1,"quantity":2}]}'
# {"id":1}   <-- bir xil id

# Bekor qilish — stock qaytariladi
curl -X POST http://localhost:8080/orders/1/cancel \
  -H "Authorization: Bearer $TOKEN"
```

---

## 🧱 Concurrency correctness (50 parallel so'rov)

Vazifa talabiga ko'ra: `stock = 10` bo'lgan productga 50 ta parallel so'rov yuboriladi. 10 tasi muvaffaqiyatli (`200 OK`), qolgan 40 tasi `409 Conflict` qaytarishi kerak.

```bash
TOKEN=$(curl -s -X POST "http://localhost:8080/auth/login?userId=1" | jq -r .token)

# Mahsulot yaratish (stock=10)
curl -X POST http://localhost:8080/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Rare Item","price":1000,"stockQuantity":10}'

# 50 ta parallel so'rov
for i in {1..50}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST http://localhost:8080/orders \
    -H "Authorization: Bearer $TOKEN" \
    -H "Idempotency-Key: concurrency-test-$i" \
    -H "Content-Type: application/json" \
    -d '{"items":[{"productId":1,"quantity":1}]}' &
done; wait
```

Kutilgan natija: `200` 10 marta, `409` 40 marta.

### Bu qanday ishlaydi?

1. Har bir so'rov o'z transaction'ida `SELECT ... FOR UPDATE` bilan mahsulotni bloklaydi.
2. Birinchi 10 so'rov `stock` ni 10 → 0 ga tushiradi.
3. Qolgan 40 so'rov `stock = 0` ko'rib, `InsufficientStockException` otadi.
4. `GlobalExceptionHandler` bu exceptionni `409 Conflict` ga map qiladi.

---

## 🔁 Caching strategiyasi

- **Read-through:** `GET /orders/{id}` avval Redis'dan o'qiydi. Bo'lmasa, BД'dan o'qiydi va Redis'ga yozadi (TTL 5 daqiqa).
- **Write-through (invalidate):** `POST /orders/{id}/cancel` dan keyin Redis'dagi cache o'chiriladi.
- **Key structure:** `order:{userId}:{orderId}` — `OrderCacheKey` readonly struct orqali tip xavfsiz.

---

## 🛑 Fon jarayon — avtomatik bekor qilish

`PendingOrderCancellationService` — `BackgroundService`. Har 1 daqiqada:

1. `SELECT id FROM orders WHERE status='pending' AND created_at <= NOW() - INTERVAL '15 minutes'`
2. Har bir topilgan order uchun:
    - `SELECT FOR UPDATE` bilan order'ni bloklaydi
    - `order_items` ni yuklaydi
    - Har bir product uchun `stock` ni qaytaradi
    - Statusni `cancelled` ga o'zgartiradi
3. Cache'dan o'chiradi

---

## ⚠️ Xatolarni qaytarish (RFC 7807 ProblemDetails)

Barcha xatolar bir xil formatda qaytadi:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Not enough stock for product 1.",
  "status": 409,
  "detail": "Not enough stock for product 1.",
  "instance": "/orders",
  "traceId": "00-abc123..."
}
```

| Exception                          | HTTP Status |
|------------------------------------|-------------|
| `ProductNotFoundException`         | 404         |
| `OrderNotFoundException`           | 404         |
| `InsufficientStockException`       | 409         |
| `OrderCannotBeCancelledException`  | 409         |
| `ArgumentException`               | 400         |
| Boshqa exception'lar              | 500         |

---

## 🩺 Health Checks

```bash
curl http://localhost:8080/health
# {"status":"Healthy","entries":{"postgres":{...},"redis":{...}}}
```

Docker Compose healthcheck API'ni har 30 soniyada tekshiradi.

---

## 🛠 Rivojlantirish (Development)

### Loyihani lokalda ishga tushirish (Docker'siz)

```bash
cd Marketplace.Api

# PostgreSQL va Redis Docker'da ishga tushiring (faqat ularni)
docker compose up -d postgres redis

# .NET ilovasini ishga tushiring
dotnet run
```

`appsettings.json` dagi connection string `localhost:5433` va `localhost:6380` ga ulanadi (Docker port mapping orqali).

### O'zgarishlarni qo'llash

```bash
docker compose down
docker compose up --build -d
```

> ⚠️ Agar `.env` dagi parolni o'zgartirsangiz, PostgreSQL volume'ni o'chirish kerak:
> ```bash
> docker compose down -v
> docker compose up --build -d
> ```


---

## 📦 Git tarixi

Loyiha bosqichma-bosqich rivojlantirildi:

1. **Refactoring** — `OrderRepository` ni 4 ta kichik repozitoriyga ajratish, `record` / `struct` / `delegate` qo'shish
2. **Infrastructure** — `.env`, Docker healthchecks, toza `init.sql` (CHECK constraints + indexlar)
3. **Product CRUD** — to'liq paginated CRUD
4. **OrderStatus enum** — magic string'lardan xalos bo'lish
5. **Global Exception Handler** — controller'lardan try/catch ni olib tashlash
6. **Options Pattern** — strongly-typed configuration + startup validation
7. **Health Checks** — PostgreSQL + Redis monitoring

---

## 📄 Litsenziya

MIT — bemalol foydalaning.