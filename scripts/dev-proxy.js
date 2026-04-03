#!/usr/bin/env node
const http = require('http');

const NEXT_PORT = parseInt(process.env.NEXT_INTERNAL_PORT || '3050', 10);
const LISTEN_PORT = parseInt(process.env.PROXY_PORT || '5000', 10);
let ready = false;

const LOADING_HTML = `<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>Loading...</title>
<meta http-equiv="refresh" content="2">
<style>
body{display:flex;align-items:center;justify-content:center;height:100vh;margin:0;
font-family:system-ui,sans-serif;background:#f8f9fa;color:#333}
.spinner{width:40px;height:40px;border:4px solid #e0e0e0;border-top-color:#666;
border-radius:50%;animation:spin .8s linear infinite;margin-right:16px}
@keyframes spin{to{transform:rotate(360deg)}}
</style></head>
<body><div class="spinner"></div><div>Starting up&hellip;</div></body></html>`;

let consecutiveErrors = 0;

function proxyRequest(req, res) {
  const opts = {
    hostname: '127.0.0.1',
    port: NEXT_PORT,
    path: req.url,
    method: req.method,
    headers: { ...req.headers, host: req.headers.host },
  };
  const proxy = http.request(opts, (upstream) => {
    consecutiveErrors = 0;
    res.writeHead(upstream.statusCode, upstream.headers);
    upstream.pipe(res, { end: true });
  });
  proxy.on('error', () => {
    consecutiveErrors++;
    if (consecutiveErrors >= 3) {
      console.log('[proxy] Next.js unreachable — re-entering warmup mode');
      ready = false;
      warmup();
    }
    res.writeHead(502, { 'content-type': 'text/html' });
    res.end(LOADING_HTML);
  });
  req.pipe(proxy, { end: true });
}

const server = http.createServer((req, res) => {
  if (ready) {
    proxyRequest(req, res);
    return;
  }
  if (req.headers.upgrade) {
    res.writeHead(503);
    res.end();
    return;
  }
  res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
  res.end(LOADING_HTML);
});

server.on('upgrade', (req, socket, head) => {
  if (!ready) { socket.destroy(); return; }
  const opts = {
    hostname: '127.0.0.1',
    port: NEXT_PORT,
    path: req.url,
    method: req.method,
    headers: req.headers,
  };
  const proxy = http.request(opts);
  proxy.on('upgrade', (proxyRes, proxySocket, proxyHead) => {
    socket.write(
      `HTTP/1.1 101 Switching Protocols\r\n` +
      Object.entries(proxyRes.headers).map(([k, v]) => `${k}: ${v}`).join('\r\n') +
      '\r\n\r\n'
    );
    if (proxyHead && proxyHead.length) socket.write(proxyHead);
    proxySocket.on('error', () => socket.destroy());
    socket.on('error', () => proxySocket.destroy());
    proxySocket.pipe(socket);
    socket.pipe(proxySocket);
  });
  proxy.on('error', () => socket.destroy());
  if (head && head.length) proxy.write(head);
  proxy.end();
});

server.listen(LISTEN_PORT, '0.0.0.0', () => {
  console.log(`[proxy] Listening on :${LISTEN_PORT}, waiting for Next.js on :${NEXT_PORT}`);
});

function warmup() {
  const start = Date.now();
  const MAX_WAIT = 120000;
  function attempt() {
    if (Date.now() - start > MAX_WAIT) {
      console.log('[proxy] Warmup timeout — forwarding anyway');
      ready = true;
      return;
    }
    const req = http.get(`http://127.0.0.1:${NEXT_PORT}/login`, (res) => {
      let body = '';
      res.on('data', (c) => body += c);
      res.on('end', () => {
        if (res.statusCode === 200 || res.statusCode === 302 || res.statusCode === 307) {
          console.log(`[proxy] Next.js ready (HTTP ${res.statusCode}) — now proxying`);
          ready = true;
        } else {
          console.log(`[proxy] /login returned ${res.statusCode}, retrying in 2s...`);
          setTimeout(attempt, 2000);
        }
      });
    });
    req.on('error', () => {
      setTimeout(attempt, 1000);
    });
  }
  setTimeout(attempt, 3000);
}

warmup();
