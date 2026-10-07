# Fatoura — UAE VAT Sales, Purchases & Invoicing

Fatoura covers a UAE business's day-to-day trading in a web app and a mobile app:
- clients and suppliers
- products and services, with stock tracking
- quotations, tax invoices and tax credit notes, with an optional discount on the whole document
- documents in AED or another currency (rates kept in Settings); reports and the VAT return stay in AED
- purchases
- financial reports: Sales, Purchases, Profit & Loss, and a VAT 201-style return

It is built for UAE VAT: 5% VAT, TRN validation and gap-free sequential invoice numbers. Printed documents follow the reference invoice in [`docs/reference/`](docs/reference/).

- **Requirements:** [`docs/BRD.md`](docs/BRD.md) (Arabic, as supplied)
- **Decisions and assumptions:** [`docs/DECISIONS.md`](docs/DECISIONS.md)

| | Technology |
|---|---|
| API | ASP.NET Core (.NET 10), EF Core, SQL Server, JWT auth, QuestPDF, ClosedXML |
| Web | React 19 + Vite + Ant Design 6, Arabic/English with full RTL |
| Mobile | React Native with Expo SDK 57 (Expo Router, React Native Paper) |
| Shared | `@fatoura/shared`: typed API client (generated from OpenAPI), VAT calculator, TRN and numbering rules, translations |

## Repository layout

```
backend/            .NET solution (Fatoura.Domain = pure rules, Fatoura.Api = HTTP/EF/PDF), unit + integration tests
clients/shared/     @fatoura/shared — openapi.json (generated), API client, calc/trn/numbering, i18n (en/ar)
clients/web/        React web app + Playwright end-to-end tests
mobile/             Expo app (iOS/Android, also builds for web)
spec/               JSON fixtures shared by C# and TypeScript tests (VAT math, TRNs, numbering, report scenario)
docs/               BRD, design decisions, reference invoice PDF
docker-compose.yml  SQL Server + API + web (nginx)
```

## Running it

### Option A: everything in Docker
```bash
cp .env.example .env        # set real passwords and a long random JWT_SIGNING_KEY
docker compose up -d --build
```
Open http://localhost:8080 and sign in with `ADMIN_EMAIL` / `ADMIN_PASSWORD` from `.env`. This first Admin is created on the first start.

Then:
1. Fill in **Settings → Company**: name, address, TRN, logo and stamp. Documents cannot be issued until the name, address and TRN are set.
   To issue documents in other currencies, add them with their AED rates under **Settings → Currencies**.
2. Add cashiers under **Users**.

> If `docker compose up` reports that port 1433 (or 8080) is already allocated, set `MSSQL_PORT` (or `WEB_PORT`) in `.env` to a free port, e.g. `MSSQL_PORT=14330`.

> In production, serve the site over **HTTPS**. The session cookie is `Secure`, so browsers accept it only over HTTPS, or over plain HTTP on `localhost`.

### Option B: local development
Prerequisites: .NET 10 SDK, Node 22, and Docker (for SQL Server).

```bash
docker compose up -d mssql                       # SQL Server only (uses .env)

cd backend
dotnet run --project src/Fatoura.Api             # http://localhost:5080 (Development settings)
#  → migrates the "Fatoura" database and seeds admin@fatoura.local / Admin@12345
#  → API reference (Scalar): http://localhost:5080/scalar

cd ../clients
npm install
npm run dev -w @fatoura/web                      # http://localhost:5173, proxies /api to :5080

cd ../mobile
npm install
EXPO_PUBLIC_API_URL=http://<your-lan-ip>:5080 npx expo start
```

`appsettings.Development.json` expects SQL Server on `localhost,1433` with SA password `Fatoura_Dev_2026!`. Override it with `ConnectionStrings__Fatoura`.

### Mobile app notes
- **API address:** the app calls the API set in `EXPO_PUBLIC_API_URL`, which is fixed at build time. The web build uses the same origin.
- **Development builds:** native modules (secure store, file system, sharing) need a development build: `npx expo run:android`, `npx expo run:ios`, or EAS Build (`npx eas-cli build --profile development`). Expo Go also works for most screens.
- **Language direction:** switching to Arabic flips the app to right-to-left and restarts it so the native layout direction applies.
- **Web-only areas:** Settings and user management are only in the web app.

## Configuration (API)

| Key | Purpose |
|---|---|
| `ConnectionStrings__Fatoura` | SQL Server connection string |
| `Jwt__SigningKey` | HMAC key, at least 32 characters (**required**; keep it secret) |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | Token lifetimes (defaults: 15 minutes / 14 days) |
| `Jwt__RefreshReuseGraceSeconds` | How long a just-rotated refresh token may be replayed (two tabs refreshing at once) before reuse counts as theft (default 30) |
| `RateLimiting__LoginPerMinute` | Login attempts allowed per client IP per minute (default 20; `0` disables). Accounts also lock for 15 minutes after 5 wrong passwords |
| `Seed__AdminEmail` / `Seed__AdminPassword` / `Seed__AdminName` | First Admin, created only when no Admin exists |
| `Database__Migrate` | Apply EF migrations at start-up (default `true`) |

## Tests

```bash
# Backend: unit tests, plus integration tests against a real SQL Server.
# FATOURA_TEST_SQL selects the server; without it, Testcontainers starts one.
cd backend
export FATOURA_TEST_SQL='Server=localhost,1433;User Id=sa;Password=Fatoura_Dev_2026!;TrustServerCertificate=True'
dotnet test --project tests/Fatoura.UnitTests
dotnet test --project tests/Fatoura.IntegrationTests

# Shared + web unit tests, then web end-to-end tests
# (Playwright starts the API on :5081 and Vite on :5174).
cd clients
npm test
cd web && npx playwright test

# Mobile
cd mobile
npm run typecheck && npm test
npx expo export -p web --output-dir dist && npx playwright test   # smoke test of the web build
```

What the tests cover:
- The VAT calculator (including discounts and currency conversion), TRN rule and numbering are tested in C# and TypeScript against the **same** JSON fixtures in `spec/`.
- The reference invoice must total 46,000.00 + 2,300.00 VAT = 48,300.00.
- `spec/report-scenario.json` replays a hand-computed month through the API and checks every Sales, Purchases, P&L and VAT figure.
- 50 concurrent invoice issues must get gap-free numbers 1..50.

## API contract
- **Where it lives:** the API's OpenAPI 3.1 document is regenerated on every Debug build into `clients/shared/openapi.json`.
- **TypeScript types:** `npm run gen:api -w @fatoura/shared` regenerates them from that document.
- **Drift check:** CI fails if either file is out of date.

## Licences
- **QuestPDF:** used under the Community licence, which is free for organisations with under USD 1M annual gross revenue. Larger organisations need a QuestPDF commercial licence.
- **Fonts:** Carlito and Noto Naskh Arabic are embedded for PDF rendering, under the SIL Open Font License ([`backend/src/Fatoura.Api/Pdf/Fonts/OFL.txt`](backend/src/Fatoura.Api/Pdf/Fonts/OFL.txt)).
