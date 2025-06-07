# Agent Guidelines

- Use 4-space indentation in C# files.
- Namespace should begin with `TradeFlex`.
- Keep code compatible with .NET 9 (`net9.0`).
- Run `dotnet test` after any change to ensure the solution builds and any tests pass (tests may be empty).
- No other build steps are required.

## Development Plan

### 1. Problem Context
**Current state**: No unified tool exists for hobbyist/indie traders to develop, back-test, and deploy C# trading algorithms without stitching together disparate libraries.

**Business goal**: Ship a single-install, opinionated framework that lets a trader go from idea → historical proof → paper trading → live execution while enforcing consistent risk controls.

**Primary stakeholders**
- End-user traders (technical hobbyists)
- Maintainers of brokerage integrations
- Core dev team responsible for platform stability and compliance

### 2. Functional Requirements (FR)
ID | Requirement
---|---
FR-1 | Pluggable `ITradingAlgorithm` interface exposing `OnEntry`, `OnExit`, `OnRiskCheck`, and `OnBar` hooks.
FR-2 | Back-test runner that replays historical ticks/bars and routes them through an algorithm at up to 100k events/s.
FR-3 | CLI and GUI "sandbox" mode that runs an algo against a single trading day for smoke tests.
FR-4 | JUnit-style unit-test harness (`NUnit` / `xUnit`) auto-discovering algorithm test cases in `/tests`.
FR-5 | Shadow-trading service that mirrors orders to a broker's demo endpoint and records fills without touching capital.
FR-6 | Live-trading service that authenticates to at least one supported broker (e.g., Interactive Brokers) and submits real orders once an algo is marked *Validated*.

### 3. Non-Functional Requirements (NFR)
ID | Requirement
---|---
NFR-1 | Latency ≤ 50 ms from market event → risk check → order placement in live mode.
NFR-2 | Horizontal back-test scale: run 1-day simulation for 5 years of minute data (<4 GB) in ≤ 2 min on commodity laptop.
NFR-3 | 99.9 % availability for live-trading gateway during market hours (failover to secondary region).
NFR-4 | TLS 1.3 encryption for all broker and data-provider traffic; secrets stored in Azure Key Vault.
NFR-5 | Cost ceiling: <$50/month per active live trader on default cloud plan.
NFR-6 | Observability: structured logging (Serilog) + OpenTelemetry tracing + Prometheus metrics.

### 4. External Interfaces
**Inputs**
Source | Protocol | Auth | Payload
---|---|---|---
Historical data S3 bucket | HTTPS | IAM role | Parquet/CSV bar files
Live market data feed | WebSocket/ FIX | API key | JSON ticks/bars
Strategy package upload | gRPC via CLI | JWT | .dll + manifest

**Outputs**
Target | Protocol | Auth | Payload
---|---|---|---
Broker order gateway | FIX / REST | OAuth2, client cert | `NewOrderSingle`, `Cancel`, `Replace`
Shadow-trade DB | PostgreSQL | IAM role | orders, fills, pnl tables

**Error semantics**
- Retry network-level errors with exponential back-off (max 3).
- Reject algorithm exceptions and roll back current bar state.
- Circuit-break live trading if 3 consecutive risk-check failures within 5 s.

### 5. Internal Design
```
+------------------------------+
|  TradeFlex.Core              |
|  - AlgorithmRunner           |
|  - RiskEngine                |
|  - OrderRouter               |
+--------------+---------------+
               |
+--------------v---------------+
|  TradeFlex.Backtest          |
|  - DataFeedAdapter           |
|  - SimulationClock           |
+--------------+---------------+
               |
+--------------v---------------+        +-------------------------+
|  TradeFlex.BrokerAdapters    |<------>|  Broker SDK / API       |
|  (IBKR, Alpaca, etc.)        |        +-------------------------+
+--------------+---------------+
               |
+--------------v---------------+
|  TradeFlex.UI (WPF / Blazor) |
+------------------------------+
```
**Key modules**
- `AlgorithmRunner` – orchestrates calls to user code; supports DI for testability.
- `RiskEngine` – pluggable risk rules (max drawdown, position limits).
- `SimulationClock` – deterministic time source for back-tests.
- `OrderRouter` – strategy pattern to switch between Shadow and Live adapters.

Data persistence: Back-tests store compressed results in LiteDB; live mode streams to PostgreSQL.

Concurrency: Use `Channel<T>` queues; consumer group per symbol to avoid cross-symbol locking.

### 6. Deployment & Ops
Item | Choice
---|---
Container | `mcr.microsoft.com/dotnet/aspnet:9.0-alpine`
Orchestration | Kubernetes (Helm chart provided)
CI/CD | GitHub Actions → Azure Container Registry → Argo CD
Config | ASP.NET options pattern; secrets => Key Vault via MSI
Logging | Serilog to stdout + Loki
Metrics | Prometheus scraper + Grafana dashboards (pnl, latency, error_rate)

### 7. Edge Cases & Failure Modes
- **Historical gap** – Missing bar in data set → back-tester should either interpolate or halt with descriptive exception.
- **Broker outage** – Live router detects heartbeat loss → auto-fails to Shadow mode, emits alert.
- **Clock skew** – Market data timestamp ahead of system clock → adjust or discard to avoid negative latencies.
- **Algo runaway orders** – >N orders/second trip rate-limiter → temp ban strategy until manual override.
- **Schema evolution** – New OrderType added → versioned protobuf contracts, old clients ignore unknown fields.

### 8. Sequence Diagram (entry signal → order)
```
Client UI --> AlgorithmRunner : Load DLL
loop every bar
    MarketDataFeed -> AlgorithmRunner : OnBar(data)
    AlgorithmRunner -> RiskEngine : Validate(position, data)
    alt Pass
        RiskEngine --> AlgorithmRunner : OK
        AlgorithmRunner -> OrderRouter : PlaceOrder(order)
        OrderRouter -> BrokerAdapter : FIX NewOrderSingle
    else Fail
        RiskEngine --> AlgorithmRunner : Reject(reason)
end
```

### 9. Acceptance Criteria
- ✅ All FR-1 … FR-6 unit/integration tests passing in CI.
- ✅ Back-test of sample SMA crossover strategy matches known reference PnL within ±0.5 %.
- ✅ Live gateway sustains 100 orders/min in soak test with <50 ms p99 latency.
- ✅ Grafana dashboard shows CPU, mem, PnL, error-rate for each algo.
- ✅ Security review sign-off: OWASP top-10 scan clean, secrets encrypted at rest.
- ✅ "Hello-World" tutorial walks user from clone → first back-test → first shadow trade.

### 10. Open Questions & Assumptions
- Which broker API will be first-class (IBKR vs Alpaca)?
- Will historical data be shipped, or must user supply exchange-licensed data?
- Minimum viable GUI: WPF desktop vs Blazor WASM?
- Hosting model: officially supported cloud SKU or self-host only?
- Compliance: any geographic restrictions (GDPR, SEC record-keeping) we must meet?

### 10 (b). Resolved Questions & Assumptions
Topic | Decision | Rationale
---|---|---
Primary broker | Interactive Brokers (IBKR) TWS API | Largest retail reach, deep asset coverage, proven C# bindings.
Secondary broker | Alpaca (REST/FIX) | Modern, paper-trading friendly; hedge against IBKR outages.
Historical data source | User-supplied or opt-in Polygon.io bundle (minute bars, equities) downloaded by CLI. | Keeps repo license-clean while giving newcomers a plug-and-play option.
GUI stack | Blazor WebAssembly hosted by ASP.NET Core | Single codebase for desktop & remote; C# end-to-end.
Hosting model | Self-host or official Azure helm chart (AKS). | Lets hobbyists run locally yet offers a known-good cloud path.
Compliance baseline | U.S. SEC Rules 17a-4(f) & 15c3-5 for record retention and risk checks; GDPR opt-out (no PII stored by default). | Keeps us inside common U.S. retail-trade norms without over-scoping the MVP.

### 11. High-Level Roadmap (each row ≈ one 2-week sprint)
Phase | Core Outcomes | Exit Criteria
---|---|---
P-0: Bootstrap | Repo scaffolding, CI/CD pipeline, code style & branching rules. | "Hello World" back-test passes in CI; docker image builds & pushes.
P-1: Core API | `ITradingAlgorithm`, `AlgorithmRunner`, basic event loop + in-memory data feed. | Sample SMA strategy produces deterministic PnL on test data.
P-2: Back-tester | File-based data adapter, simulation clock, results serializer, unit-test suite. | 5-year minute data sim <2 min on dev laptop, tests >90 % coverage.
P-3: Sandbox UI | Blazor one-page app to load DLL, pick data slice, run & chart PnL curve. | Trader can run a 1-day sim via browser with no code changes.
P-4: Shadow trade | Order router, Alpaca paper adapter, Postgres order/fill tables, Grafana PnL. | Shadow mode mirrors 100 % of sandbox orders with live quotes.
P-5: Live trade v1 | IBKR adapter + risk engine (max position, drawdown), circuit-breaker. | Live account can trade 1 symbol in small size with <50 ms p99 latency.
P-6: Observability | Serilog→Loki, OpenTelemetry tracing, Prometheus metrics, alerting rules. | Dashboards for latency, error-rate, PnL; alert fires on >1 % order rejects.
P-7: Hardening & Docs | End-to-end E2E tests, SEC retention S3 archive, full user guide & API docs. | Cloud demo stack redeployable from scratch in <30 min; docs reviewed.

### 12. Ordered User-Story Backlog
Notation – `[P-n]` means "belongs to Phase n" above.
Stories are intentionally small (≤1 dev-day) to keep flow fast.

Order | Epic | User Story (in Gherkin-like format) | Acceptance Criteria
---|---|---|---
1 | Core API | As a dev I can scaffold a new solution TradeFlex.sln with multi-project layout so each layer compiles separately. | `dotnet build` succeeds; projects have references but no TODO warnings.
2 | Core API | As a dev I can define ITradingAlgorithm with Initialize, OnBar, OnEntry, OnExit, OnRiskCheck. | Interface lives in TradeFlex.Abstractions; XML docs present.
3 | Core API | As a dev I can implement AlgorithmRunner that loads algorithm DLL via reflection. | Given sample DLL, runner calls OnBar and returns no exceptions.
4 | Back-tester | As a trader I can load Parquet minute-bar data from /data folder. | Loader returns IEnumerable<Bar>; unit test validates count.
5 | Back-tester | As a dev I can drive AlgorithmRunner with a deterministic SimulationClock. | Two runs on same seed yield identical trade timestamps.
6 | Tests | As a maintainer I can run dotnet test and see >70 % coverage enforced by coverlet gate. | Pipeline fails below threshold.
7 | Sandbox UI | As a trader I can upload DLL + CSV and click "Run" to view equity-curve chart. | Browser shows chart rendered with PnL line; all JS/CSS locally hosted.
8 | Sandbox UI | As a trader I can download JSON report of all simulated orders. | File matches schema; integration test checks first/last order.
9 | Shadow | As a trader I can toggle "Shadow Mode" which routes orders to Alpaca's paper endpoint. | Orders visible on Alpaca dashboard; Postgres has matching insert.
10 | Shadow | As a dev I can view real-time PnL and position in Grafana via Prom metrics. | Dashboard panel updates ≤5 s lag; unit test for exporter.
11 | Risk | As a risk officer I can set config: MaxPosition, MaxDrawdownPct. | Exceeding limit rejects new order; unit test simulates breach.
12 | Live | As a trader I can link IBKR credentials (stored in Key Vault) and place real orders. | Live order fills in IB TWS; audit log entry stored.
13 | Live | As a platform I automatically circuit-break an algo after 3 consecutive order rejects in 5 s. | Integration test fires mock rejects; router disables algo.
14 | Observability | As an SRE I can trace a single live order from OnBar → FIX NewOrderSingle via OpenTelemetry. | Jaeger span graph shows all hops with <5 % missing spans.
15 | Compliance | As legal I can export a WORM-storage archive of all orders ≥7 years per SEC 17a-4(f). | CLI `tradeflex archive --from 2023-01-01` uploads to S3 glacier; checksum logged.
16 | Docs | As a new user I can read a step-by-step "Build → Test → Shadow → Live" tutorial. | Docs render in GitHub Pages; manual test follows steps successfully.

*(Continue backlog with stretch goals: options support, multi-asset risk, strategy marketplace, etc.)*

### 13. Mini Design-Doc Checklist (½-page each component)
Section | What to Write | Approx. Length
---|---|---
Problem | 1–2 sentences: what gap this comp fills. | 2–3 lines
Context | Where it lives in subsystem graph; upstream/downstream deps. | 4–6 lines
Decision | Bullet the chosen approach + 1-sentence why rejected alternates lost. | 4–5 bullets
Data Flow / Diagram | ASCII or Excalidraw link; highlight stateful pieces. | 1 picture
Public API | Signature blocks or sample JSON/FIX. | ≤15 lines code
Risks & Mitigations | What can break; guardrails planned. | 3–5 bullets
Test Plan | Unit, integration, perf—each as one bullet. | 3–4 bullets
Open Issues | Anything still fuzzy. | 2–3 bullets

**How to Apply the Checklist**
Before starting a story that introduces a new component, spin up the design doc using the checklist headings. This keeps architectural rationale close to the code and makes onboarding the next contributor painless.
