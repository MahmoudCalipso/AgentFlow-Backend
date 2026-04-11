# AgentFlow Management API — OpenAPI 3.0 Specification

## Base URL
```
https://<host>/api/v1
```

All endpoints require:
- `Authorization: Bearer <jwt>` header
- `X-Tenant-ID: <tenant_id>` header
- `Content-Type: application/json`

---

## 🔧 Management Endpoints

### `GET /graphs`
List all registered workflow graphs.

**Response 200**
```json
{ "graphs": [{ "id": "string", "name": "string", "node_count": 0, "created_at": "ISO8601" }] }
```

---

### `POST /graphs`
Create a new workflow graph.

**Request**
```json
{ "name": "string", "nodes": [], "edges": [] }
```

**Response 201**
```json
{ "id": "string", "name": "string" }
```

---

### `GET /graphs/{workflowId}`
Get a graph definition by ID.

**Response 200** — Full `GraphDefinition` object.

---

### `PUT /graphs/{workflowId}`
Update an existing graph.

---

### `DELETE /graphs/{workflowId}`
Delete a graph. Cannot delete if active executions exist.

---

### `POST /graphs/{workflowId}/execute`
Trigger a manual workflow execution.

**Request**
```json
{ "initial_payload": {} }
```

**Response 202**
```json
{ "correlation_id": "string", "status": "queued" }
```

---

### `GET /executions/{correlationId}`
Get status and result of an execution.

**Response 200**
```json
{
  "correlation_id": "string",
  "workflow_id": "string",
  "status": "running | completed | failed",
  "started_at": "ISO8601",
  "completed_at": "ISO8601",
  "output": {}
}
```

---

### `POST /executions/{correlationId}/cancel`
Cancel a running execution.

---

### `POST /executions/{correlationId}/retry`
Retry a failed execution from its last checkpoint.

---

## 🔍 Validation Endpoints

### `POST /validate`
Validate a graph definition before saving.

**Request** — `GraphDefinition` object

**Response 200**
```json
{ "valid": true, "errors": [], "warnings": [] }
```

---

## 💰 Preview & Cost Endpoints

### `GET /preview/{workflowId}`
Get cost/latency/success probability preview before execution.

**Response 200**
```json
{
  "workflow_id": "string",
  "valid": true,
  "node_count": 0,
  "ai_node_count": 0,
  "estimated_latency_ms": 0,
  "estimated_cost_usd": 0.0,
  "success_probability": 0.0,
  "generated_at": "ISO8601"
}
```

---

### `GET /cost/tenant/{tenantId}`
Get cost attribution for a tenant.

**Query Params:** `from` (ISO8601), `to` (ISO8601)

**Response 200**
```json
{ "tenant_id": "string", "total_cost_usd": 0.0, "breakdown": [] }
```

---

### `GET /cost/workflow/{workflowId}`
Per-workflow cost report.

---

### `GET /cost/execution/{correlationId}`
Per-execution cost breakdown.

---

### `POST /cost/budget`
Set a budget alert threshold for a tenant.

**Request**
```json
{ "tenant_id": "string", "threshold_usd": 100.0 }
```

---

## ⏪ Replay / Time-Travel Endpoints

### `GET /replay/{correlationId}/snapshots`
List execution snapshots for time-travel debugging.

**Response 200**
```json
{
  "correlation_id": "string",
  "snapshots": [{ "snapshot_id": "string", "node_id": "string", "timestamp": "ISO8601" }]
}
```

---

### `POST /replay/{correlationId}/replay`
Replay from a specific snapshot with optional input overrides.

**Request**
```json
{ "snapshot_id": "string", "input_overrides": {} }
```

**Response 202**
```json
{ "new_correlation_id": "string", "source_correlation_id": "string" }
```

---

## 🧠 AI Copilot Endpoints

### `POST /copilot/suggest/{workflowId}`
Get AI-generated optimization suggestions for a workflow.

---

### `POST /copilot/debug`
Run the autonomous debug agent on a failed execution.

**Request**
```json
{ "workflow_id": "string", "error_context": "string" }
```

**Response 200**
```json
{ "patch_applied": true, "description": "string", "patch_json": "string" }
```

---

## 🔒 Security & Compliance Endpoints

### `POST /audit/export`
Export audit log for compliance reporting.

**Request**
```json
{ "tenant_id": "string", "from": "ISO8601", "to": "ISO8601", "format": "json | csv" }
```

**Response 200** — Raw JSON or CSV audit report.

---

### `GET /nodes`
List all registered node types (MCP + native).

**Response 200**
```json
{
  "node_count": 50,
  "nodes": [{ "type": "string", "category": "string", "description": "string" }]
}
```

---

## 🏥 Health Check

### `GET /health`
**Response 200**
```json
{
  "status": "healthy",
  "mode": "native-aot",
  "nodes_registered": 50,
  "version": "1.0.0",
  "timestamp": "ISO8601"
}
```

---

## Error Response Format

All error responses follow:
```json
{
  "error": "human-readable error message",
  "code": "ERROR_CODE",
  "correlation_id": "string",
  "timestamp": "ISO8601"
}
```

| HTTP Status | Meaning |
|-------------|---------|
| 400 | Invalid input / validation error |
| 401 | Missing or invalid JWT |
| 403 | Tenant permission denied |
| 404 | Resource not found |
| 409 | Conflict (e.g., duplicate idempotency key) |
| 429 | Rate limit exceeded |
| 500 | Internal server error |
| 503 | Service unavailable (circuit breaker open) |
