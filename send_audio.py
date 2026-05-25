import websocket
import json

# Read base64 audio from file
with open('audio.b64', 'r') as f:
    base64_audio = f.read().replace('\n', '').replace('\r', '')

# Connect to the local WebSocket endpoint
ws = websocket.create_connection('ws://localhost:62564/api/call/stream')

# Send start event
ws.send(json.dumps({"event": "start", "start": {"callSid": "testcall"}}))

# Send media event with base64 audio payload
ws.send(json.dumps({
    "event": "media",
    "start": {"callSid": "testcall"},
    "media": {"payload": base64_audio}
}))

# Optionally, send stop event
ws.send(json.dumps({"event": "stop", "stop": {"callSid": "testcall"}}))

# Print any response from the server
try:
    while True:
        msg = ws.recv()
        print("Received:", msg)
except websocket._exceptions.WebSocketConnectionClosedException:
    print("WebSocket closed.")

ws.close()
