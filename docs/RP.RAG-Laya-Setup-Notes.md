# RP.RAG — Laya Setup Notes

This document records the steps completed and the commands to run Laya locally with Docker on Windows.

## Current state

- Laya repository: `D:\laya`
- Checked-out version reported by Git: `v0.4.11`
- Docker Compose recognizes both services when the upstream Compose files are combined:

  ```powershell
  docker compose -f compose.yaml -f compose.http.yaml config --services
  ```

  Expected services:

  ```text
  laya
  laya-serve
  ```

- Desired host port: `8090`
- We are testing Laya independently before changing any RP.RAG C# files.

## 1. Verify the source checkout

In PowerShell:

```powershell
cd D:\laya
git describe --tags --exact-match
```

Expected output for the current checkout:

```text
v0.4.11
```

If Git reports another version, treat that output as the actual local checkout version.

## 2. Configure port 8090

The upstream `compose.http.yaml` uses `LAYA_PORT` for both the host port and the container port. The easiest persistent local configuration is a `.env` file in `D:\laya`.

Open `D:\laya\.env` and add this line, preserving any existing settings:

```dotenv
LAYA_PORT=8090
```

Do not overwrite an existing `.env` file; append the setting or edit it in place. Docker Compose reads `.env` for variable substitution in its Compose configuration.

Alternative: set the variable only for the current PowerShell session (it will not persist in a new terminal):

```powershell
$env:LAYA_PORT = "8090"
```

## 3. Validate the merged Compose configuration

Run from `D:\laya`:

```powershell
docker compose -f compose.yaml -f compose.http.yaml config --services
```

Inspect the effective port mapping:

```powershell
docker compose -f compose.yaml -f compose.http.yaml config
```

Look for the `laya-serve` service and a port mapping equivalent to `127.0.0.1:8090:8090`.

## 4. Build and start the HTTP service

Foreground mode (shows logs in the terminal):

```powershell
docker compose -f compose.yaml -f compose.http.yaml up --build laya-serve
```

Background mode:

```powershell
docker compose -f compose.yaml -f compose.http.yaml up --build -d laya-serve
```

Check the service and port:

```powershell
docker compose -f compose.yaml -f compose.http.yaml ps laya-serve
```

Follow logs:

```powershell
docker compose -f compose.yaml -f compose.http.yaml logs -f laya-serve
```

Stop the service using the same Compose file combination:

```powershell
docker compose -f compose.yaml -f compose.http.yaml down
```

Stopping it this way does not normally delete the named model-cache volume.

## 5. Check HTTP health

In a second PowerShell window:

```powershell
curl.exe -i http://localhost:8090/health
```

This verifies HTTP liveness only. A healthy response does not guarantee the model has loaded or that inference succeeds.

## 6. Test actual inference

Laya's documented decision endpoint is `POST /v1/systemone`. The repository includes a sample request file at `examples/docker/request.json`.

Run this from `D:\laya` after the server is running:

```powershell
curl.exe -i -X POST "http://localhost:8090/v1/systemone" -H "Content-Type: application/json" --data-binary "@examples/docker/request.json"
```

A successful inference response is the real check that the model executed. The first inference may download model weights and take longer than later requests. Check container logs if the request fails.

## 7. Useful troubleshooting commands

```powershell
# List containers and published ports
docker compose -f compose.yaml -f compose.http.yaml ps

# Recent service logs
docker compose -f compose.yaml -f compose.http.yaml logs --tail 200 laya-serve

# Confirm Docker Compose's effective configuration
docker compose -f compose.yaml -f compose.http.yaml config

# Show image and container details
docker image ls

docker inspect $(docker compose -f compose.yaml -f compose.http.yaml ps -q laya-serve)
```

If port 8090 is already in use, Docker will fail to bind it. Find and stop the conflicting process or select another port by changing `LAYA_PORT` in `.env`.

## 8. RP.RAG integration

The integration is implemented. For a normal Ask with up to 64 retrieved chunks, RP.RAG makes two Laya HTTP calls: one multiclass relevance/injection decision per chunk, then one combined check for the original question's policy and the assembled prompt. The second call runs after internal read-only retrieval but before answer generation. Periodic conversation-memory distillation may add another Laya call.

When RP.RAG itself runs on the host, it can initially call `http://localhost:8090`. If the API is later containerized in the same Docker Compose network, it should use the service DNS name and the container port, such as `http://laya-serve:8090` **only if the Compose service is actually named `laya-serve` and both containers share a network**. Confirm the effective Compose configuration before using that URL.

Laya's `/v1/systemone` API is a typed-decision endpoint, not a generic chat-completions endpoint. RP.RAG sends typed questions and applies the resulting choices/probabilities using application-configured thresholds.

## Official references

- Repository: https://github.com/NandhaKishorM/laya/tree/v0.4.11
- Docker guide: https://github.com/NandhaKishorM/laya/blob/v0.4.11/docs/docker.md
- Base Compose file: https://github.com/NandhaKishorM/laya/blob/v0.4.11/compose.yaml
- HTTP Compose overlay: https://github.com/NandhaKishorM/laya/blob/v0.4.11/compose.http.yaml
- Sample decision request: https://github.com/NandhaKishorM/laya/blob/v0.4.11/examples/docker/request.json
