# 🌌 AgentFlow Intelligence Backend

AgentFlow is a high-performance, AI-first orchestration engine designed to empower autonomous agents with the world's largest integration ecosystem. Built on .NET 8 with Native AOT compliance, it represents the next generation of workflow automation, bridging the gap between static pipelines and dynamic, self-healing agentic operations.

## 🚀 Vision
While traditional tools like n8n excel at manual workflow automation, AgentFlow is built for the **Agentic Era**. It doesn't just execute nodes; it provides a sandboxed, high-performance environment where AI models can discover tools, handle errors autonomously, and execute code with microsecond latency.

## 🛠 Core Architecture

### 1. Model Context Protocol (MCP) Integration
AgentFlow decouples integrations from the core engine. By utilizing MCP, we enable a fleet of containerized, language-agnostic "nodes" (Go, Python, TypeScript) that communicate over a standardized, secure bridge.
- **21+ Native Controllers**: Pre-built, high-performance Go-based servers for 🗑️ GitHub, 💬 Slack, ☁️ AWS, 🗄️ Postgres, and more.
- **Service Discovery**: Dynamic endpoint-based discovery that scales to thousands of nodes without bloating the core binary.

### 2. High-Performance Runtime
- **Native AOT**: The backend is 100% compliant with Ahead-of-Time compilation. This means instant startup, minimal memory footprint, and bare-metal execution speed.
- **WASM Sandboxing**: Untrusted code (JavaScript/Python) runs in a strictly isolated WebAssembly environment with sub-millisecond overhead.
- **Distributed State**: Multi-node coordination via Redis with a Supabase persistence layer.

### 3. AI-Native Features
- **Predictive Routing**: Analyzes workflow history to optimize shard placement and execution paths.
- **Auto-Remediation**: Integrated `AiCopilotService` that can inspect execution failures and suggest or apply fixes in real-time.
- **Natural Language Compiler**: Compiles human-readable intent directly into AgentFlow graph definitions.

## ⚖️ AgentFlow vs. n8n

| Feature | n8n | AgentFlow |
| :--- | :--- | :--- |
| **Engine** | Node.js (Interpreted) | .NET 8 (Native AOT / Compiled) |
| **Performance** | High overhead for large workflows | Near-zero overhead; optimized for high-concurrency |
| **Security** | Process-level isolation | **WASM-level hardware isolation** |
| **Integrations** | 400+ JS-bound nodes | **2,200+ MCP-compatible nodes** (Universal) |
| **AI Focus** | Visual building for humans | **Autonomous tool-use for Agents** |
| **Extensibility** | Requires JS knowledge | Language-agnostic (Go, Python, etc. via MCP) |
| **Footprint** | Moderate (Containerized Node.js) | **Micro-footprint (Single Native Binary)** |

## 🌟 Key Benefits

1. **Unmatched Scale**: Handle thousands of concurrent executions across a distributed cluster without the "Node.js overhead."
2. **Infinite Integrations**: Connect any service by simply deploying an MCP container. You aren't limited to a specific runtime or library.
3. **Enterprise Defense**: The combination of WASM and MCP-API-Key injection ensures that integrations and custom code never compromise your core platform.
4. **Agent-Centric**: Designed to be the "hands" of an LLM. Our tool manifests are structured specifically for model compatibility.

## 📦 Getting Started

### Prerequisites
- .NET 8 SDK (for development)
- Docker & Docker Compose
- Redis & Supabase (for state/persistence)

### Quick Start
1. **Configure Orchestration**: Navigate to `McpServers/Scripts`.
2. **Generate Manifest**: Run `node generate-mcp-compose.js` to sync your 21+ MCP nodes.
3. **Launch Stack**:
   ```bash
   docker compose -f docker-compose.mcp.yml up -d
   ```
4. **Start Backend**: Configure your `appsettings.json` and run the native binary.

---
*Built for the future of Autonomous Intelligence.*
