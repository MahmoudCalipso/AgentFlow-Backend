FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AgentFlow.Backend/AgentFlow.Backend.csproj ./AgentFlow.Backend/
RUN dotnet restore ./AgentFlow.Backend/AgentFlow.Backend.csproj

COPY AgentFlow.Backend/ ./AgentFlow.Backend/

WORKDIR /src/AgentFlow.Backend
RUN dotnet publish -c Release \
    -r linux-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishAot=false \
    -o /app/publish

FROM debian:bookworm-slim AS final
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libssl3 \
    libicu-dev \
    curl && \
    rm -rf /var/lib/apt/lists/*

RUN groupadd -r agentflow && useradd -r -g agentflow -s /sbin/nologin agentflow

COPY --from=build --chown=agentflow:agentflow /app/publish .

RUN mkdir -p /app/git-graphs /app/binary_data && \
    chown -R agentflow:agentflow /app

USER agentflow

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["./AgentFlow.Backend"]
