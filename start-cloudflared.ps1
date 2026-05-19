# Helper to start Cloudflare Tunnel for local testing
# Requires cloudflared installed: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/

if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    Write-Error "cloudflared not found. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/"
    exit 1
}

Write-Host "Starting cloudflared tunnel to http://localhost:5000..."
Write-Host "Press Ctrl+C to stop the tunnel."

# This will print the forwarding URL to the console. Copy it and set Streaming:Url accordingly.
cloudflared tunnel --url http://localhost:5000
