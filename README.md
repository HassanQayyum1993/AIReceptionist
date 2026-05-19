# AI Receptionist (ASP.NET Core)

This project is a reference implementation of an AI Voice Receptionist using ASP.NET Core (.NET 8).

Features:

See `appsettings.json` for configuration.

Run locally:

## Run locally
Build the solution:

```powershell
dotnet build AIReceptionist.sln -clp:Summary
```

Run the API on localhost:5000:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5000'
dotnet run --project AIReceptionist.Api.csproj
```

Swagger UI: http://localhost:5000/swagger

Health check: `GET /api/health` → `{ "status": "ok" }`

## API Examples
- Upload knowledge

```bash
curl -X POST http://localhost:5000/api/knowledge/upload \
	-H "Content-Type: application/json" \
	-d '{"title":"FAQ","content":"Office hours are 9-5"}'
```

- Twilio incoming call webhook (returns TwiML to start a Media Stream):
	POST `https://<your-host>/api/call/incoming` (Twilio will POST form data).

## Exposing locally

Preferred: use Cloudflare Tunnel (free) which supports WebSockets (WSS) and works well for Twilio Media Streams — see the Cloudflare Tunnel section below.

Alternative: `ngrok` can expose `http://localhost:5000` to the internet. Free ngrok plans support HTTPS webhooks (HTTP POST) but forwarding raw WebSocket (WSS) traffic typically requires a paid plan. If you only need Twilio to fetch TwiML (no media stream), `ngrok` free may be sufficient.

## Cloudflare Tunnel (free, supports WSS)
1. Install `cloudflared`: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/
2. Start a tunnel to your local API:

```powershell
cloudflared tunnel --url http://localhost:5000
```

3. `cloudflared` prints a `https://...trycloudflare.com` host. Use that host for Twilio webhook and for `Streaming:Url` (use `wss://<host>/api/call/stream`).

Notes:
- Cloudflare Tunnel supports WebSockets on the free tier, so it's preferred over free ngrok for media streams.
- After starting the tunnel, set your Twilio Voice webhook to `https://<host>/api/call/incoming` (POST). Ensure `Streaming:Url` is `wss://<host>/api/call/stream`.
 - After starting the tunnel, set your Twilio Voice webhook to `https://<host>/api/call/incoming` (POST). Ensure `Streaming:Url` is `wss://<host>/api/call/stream`.

## Quick start checklist

- Start the Cloudflare tunnel (keeps a public URL that supports WSS):

```powershell
cd scripts
.\cloudflared.exe tunnel --url http://localhost:5000
```

- Start the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5000'
dotnet run --project AIReceptionist.Api.csproj
```

- Verify local health endpoint:

```powershell
curl http://localhost:5000/api/health
```

- Update `appsettings.json`:

Set `Streaming:Url` to the tunnel WSS endpoint, for example:

```json
"Streaming": { "Url": "wss://novel-notification-contracts-disable.trycloudflare.com/api/call/stream" }
```

- Configure Twilio (or alternate provider):
	- Voice webhook (POST): `https://<your-tunnel>/api/call/incoming`
	- Ensure the provider can reach the public URL and that any geo permissions allow inbound calls from your country.

- Test flow:
	- Place a call to the provider number (or simulate via curl/Postman) and watch the API logs for an incoming POST to `/api/call/incoming` and a WebSocket connection to `/api/call/stream`.

## Twilio setup
- Configure your Twilio voice webhook to POST to `https://<ngrok-host>/api/call/incoming`.
- The endpoint validates the Twilio signature using config from `Twilio` in `appsettings.json` (set `AccountSid` and `AuthToken` or ApiKey/ApiSecret).
- The response will contain TwiML that instructs Twilio to start a Media Stream to `Streaming:Url`.

## Configuration (secrets)
Set secrets either in environment variables or use `dotnet user-secrets`:
- `OpenAI:ApiKey` — embeddings/LLM
- `Deepgram:ApiKey` — realtime STT (optional; falls back to mock)
- `ElevenLabs:ApiKey` — TTS (optional; falls back to mock)
- `Pinecone:*` — if using Pinecone vector store
- `Twilio:AccountSid`, `Twilio:AuthToken` — for request validation

Do NOT commit real API keys into the repo.

## Troubleshooting
- If `dotnet build` fails due to file locks, stop any running dotnet processes that have this repo as working directory and rebuild.
- If Swagger is not visible, ensure the server is running and open `http://localhost:5000/swagger`.

## Next steps
- Use ngrok + Twilio to test end-to-end streaming, or adapt `Streaming:Url` to a deployed, public WSS endpoint.
- If you want, I can help generate curl examples for Twilio-signed requests or set up a small script to upload sample docs programmatically.
