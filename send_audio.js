const fs = require('fs');
const WebSocket = require('ws');

// Read base64 audio from file
const base64Audio = fs.readFileSync('audio.b64', 'utf8').replace(/\r?\n/g, '');

// Connect to the local WebSocket endpoint
const ws = new WebSocket('ws://localhost:62564/api/call/stream');

ws.on('open', function open() {
  // Send start event
  ws.send(JSON.stringify({ event: 'start', start: { callSid: 'testcall' } }));

  // Send media event with base64 audio payload
  ws.send(JSON.stringify({
    event: 'media',
    start: { callSid: 'testcall' },
    media: { payload: base64Audio }
  }));

  // Optionally, send stop event
  ws.send(JSON.stringify({ event: 'stop', stop: { callSid: 'testcall' } }));
});

ws.on('message', function message(data) {
  console.log('Received:', data.toString());
});

ws.on('close', function close() {
  console.log('WebSocket closed.');
});

ws.on('error', function error(err) {
  console.error('WebSocket error:', err);
});
