# MatchMaking System

A game matchmaking system that groups players into matches using .NET 9, Kafka, and Redis.

## Architecture

- **MatchMaking.Service** — HTTP API for submitting match search requests and retrieving match results
- **MatchMaking.Worker** — Background worker that consumes player requests and forms matches when enough players are queued
- **Kafka** — Message broker for async communication between Service and Worker
- **Redis** — Shared state for player queue coordination and match result storage

### Project Structure

```
src/
├── MatchMaking.Contracts/          # Shared Kafka message contracts
│   ├── Constants/KafkaTopics.cs
│   └── Messages/
│       ├── MatchmakingRequest.cs
│       └── MatchmakingComplete.cs
├── MatchMaking.Service/            # ASP.NET Core Minimal API
│   ├── Api/Endpoints/              # HTTP endpoint definitions
│   ├── Application/                # CQRS commands, queries, abstractions
│   │   ├── Abstractions/           # Interfaces (IMatchRepository, IMatchmakingProducer)
│   │   ├── Commands/               # SearchMatchCommand + Handler
│   │   └── Queries/                # GetMatchQuery + Handler
│   ├── Domain/Models/              # Domain records (MatchInfo)
│   └── Infrastructure/             # Kafka, Redis, middleware implementations
│       ├── Configuration/
│       ├── Kafka/
│       ├── Middleware/
│       └── Redis/
└── MatchMaking.Worker/             # .NET Worker Service
    ├── Application/                # CQRS commands, abstractions
    │   ├── Abstractions/           # Interfaces (IPlayerQueue, IMatchCompleteProducer)
    │   └── Commands/               # AccumulatePlayerCommand + Handler
    └── Infrastructure/             # Kafka, Redis implementations
        ├── Configuration/
        ├── Kafka/
        └── Redis/
tests/
├── MatchMaking.Service.Tests/      # Unit tests for Service handlers
└── MatchMaking.Worker.Tests/       # Unit tests for Worker handlers
```

The project follows **Onion Architecture** with **CQRS** (via MediatR):
- **Domain** (innermost) — pure data models with no dependencies
- **Application** — commands, queries, and interface abstractions
- **Infrastructure** (outermost) — Kafka, Redis, and configuration implementations
- **Api** — HTTP endpoint definitions

### How It Works — Full Request Flow

#### Searching for a Match (`POST /matchmaking/search`)

```
Client → Service API → Kafka → Worker → Redis → Kafka → Service Consumer → Redis
```

1. **API Endpoint** — [MatchmakingEndpoints.cs](src/MatchMaking.Service/Api/Endpoints/MatchmakingEndpoints.cs) receives `{ "userId": "player1" }` and sends a MediatR command
2. **Command Handler** — [SearchMatchCommandHandler.cs](src/MatchMaking.Service/Application/Commands/SearchMatchCommandHandler.cs) calls `IMatchmakingProducer.PublishSearchRequestAsync(userId)`
3. **Kafka Producer** — [MatchmakingProducer.cs](src/MatchMaking.Service/Infrastructure/Kafka/MatchmakingProducer.cs) serializes the request and publishes to Kafka topic `matchmaking.request` (key = userId)
4. **Returns `204 No Content`** to the client immediately — matchmaking happens asynchronously
5. **Worker Consumer** — [MatchRequestConsumer.cs](src/MatchMaking.Worker/Infrastructure/Kafka/MatchRequestConsumer.cs) (BackgroundService running in 2 instances) picks up the message from Kafka
6. **Worker Handler** — [AccumulatePlayerCommandHandler.cs](src/MatchMaking.Worker/Application/Commands/AccumulatePlayerCommandHandler.cs) calls `IPlayerQueue.AddAndTryPopBatchAsync(userId)`
7. **Redis Lua Script** — [RedisPlayerQueue.cs](src/MatchMaking.Worker/Infrastructure/Redis/RedisPlayerQueue.cs) runs an atomic Lua script: adds the player to the queue, checks the count, and pops a batch if enough players (default 3)
8. **Match Formation** — If a batch is returned, the handler generates a GUID `matchId` and calls `IMatchCompleteProducer.PublishMatchCompleteAsync(matchId, players)`
9. **Kafka Producer** — [MatchCompleteProducer.cs](src/MatchMaking.Worker/Infrastructure/Kafka/MatchCompleteProducer.cs) publishes to Kafka topic `matchmaking.complete`
10. **Service Consumer** — [MatchCompleteConsumer.cs](src/MatchMaking.Service/Infrastructure/Kafka/MatchCompleteConsumer.cs) (BackgroundService) picks up the match result
11. **Redis Storage** — [RedisMatchRepository.cs](src/MatchMaking.Service/Infrastructure/Redis/RedisMatchRepository.cs) stores the match as JSON under `match:user:{userId}` for each player (TTL: 1 hour)

#### Retrieving a Match (`GET /matchmaking/match/{userId}`)

```
Client → Service API → Redis → Client
```

1. **API Endpoint** — [MatchmakingEndpoints.cs](src/MatchMaking.Service/Api/Endpoints/MatchmakingEndpoints.cs) receives the GET request and sends a MediatR query
2. **Query Handler** — [GetMatchQueryHandler.cs](src/MatchMaking.Service/Application/Queries/GetMatchQueryHandler.cs) calls `IMatchRepository.GetMatchByUserIdAsync(userId)`
3. **Redis Read** — [RedisMatchRepository.cs](src/MatchMaking.Service/Infrastructure/Redis/RedisMatchRepository.cs) reads from Redis key `match:user:{userId}`
4. **Response** — Returns `{ matchId, userIds[] }` as `200 OK`, or `404 Not Found` if no match exists yet

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose

## How to Run

```bash
docker-compose up --build
```

This starts all infrastructure:
- **Kafka** (KRaft mode, no ZooKeeper) on port 9092
- **Redis** on port 6379
- **MatchMaking.Service** on port 8080
- **MatchMaking.Worker** (2 instances)

## API Endpoints

### Search for a Match

```bash
curl -X POST http://localhost:8080/matchmaking/search \
  -H "Content-Type: application/json" \
  -d '{"userId": "player1"}'
```

**Response:** `204 No Content`

### Get Match Result

```bash
curl http://localhost:8080/matchmaking/match/player1
```

**Response (200):**
```json
{
  "matchId": "45ae548e-d72f-438d-bf1a-f1692a699a81",
  "userIds": ["player1", "player2", "player3"]
}
```

**Response (404):** Match not yet formed.

## Testing the Full Flow

```bash
# Submit 3 players
curl -X POST http://localhost:8080/matchmaking/search -H "Content-Type: application/json" -d '{"userId": "player1"}'
curl -X POST http://localhost:8080/matchmaking/search -H "Content-Type: application/json" -d '{"userId": "player2"}'
curl -X POST http://localhost:8080/matchmaking/search -H "Content-Type: application/json" -d '{"userId": "player3"}'

# Wait a moment for processing, then retrieve the match
curl http://localhost:8080/matchmaking/match/player1
```

## Running Tests

```bash
dotnet test
```

14 unit tests covering all command/query handlers (xUnit + NSubstitute).

## Swagger UI

Once running, the API documentation is available at:

```
http://localhost:8080/swagger
```

## Configuration

### Worker (appsettings.json)

| Setting | Default | Description |
|---------|---------|-------------|
| `Matchmaking:PlayersPerMatch` | 3 | Number of players required to form a match |
| `Kafka:BootstrapServers` | localhost:9092 | Kafka broker address |
| `Redis:ConnectionString` | localhost:6379 | Redis connection string |

### Service (appsettings.json)

| Setting | Default | Description |
|---------|---------|-------------|
| `Kafka:BootstrapServers` | localhost:9092 | Kafka broker address |
| `Redis:ConnectionString` | localhost:6379 | Redis connection string |
| `Redis:MatchTtlMinutes` | 60 | TTL for stored match results |
| `RateLimiter:WindowMs` | 100 | Rate limit window in milliseconds |
| `RateLimiter:PermitLimit` | 1 | Max requests per window |

## Design Decisions

- **Atomic Redis Lua Script** — The player queue uses a Lua script that runs atomically inside Redis, preventing race conditions when 2 Worker instances consume messages concurrently. No two workers can pop the same players.
- **CQRS via MediatR** — Separates read (GetMatchQuery) and write (SearchMatchCommand, AccumulatePlayerCommand) operations, keeping handlers focused and testable.
- **Manual Kafka Commits** — Offsets are committed only after successful processing, ensuring no messages are lost if a consumer crashes mid-processing.
- **Idempotent Kafka Producers** — Both producers use `Acks.All` and `EnableIdempotence = true` to guarantee exactly-once delivery semantics.
- **Player Re-queuing on Failure** — If publishing a match result to Kafka fails, players are re-added to the Redis queue so they are not lost.
- **Rate Limiting** — Fixed-window rate limiter (1 request per 100ms) applied to the search endpoint as per requirements.
