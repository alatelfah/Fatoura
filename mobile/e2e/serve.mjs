// Serves the exported web build (dist/) and proxies /api to the Fatoura API, like a production reverse proxy.
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer, request } from 'node:http';
import { extname, join, normalize } from 'node:path';

const root = new URL('../dist/', import.meta.url).pathname;
const port = Number(process.env.PORT ?? 8090);
const api = new URL(process.env.FATOURA_API ?? 'http://localhost:5082');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json', '.ttf': 'font/ttf' };

createServer((req, res) => {
  if (req.url.startsWith('/api/')) {
    const upstream = request({ hostname: api.hostname, port: api.port, path: req.url, method: req.method, headers: req.headers }, (r) => {
      res.writeHead(r.statusCode ?? 502, r.headers);
      r.pipe(res);
    });
    upstream.on('error', () => res.writeHead(502).end());
    req.pipe(upstream);
    return;
  }
  const path = normalize(join(root, decodeURIComponent(req.url.split('?')[0])));
  const file = path.startsWith(root) && existsSync(path) && statSync(path).isFile() ? path : join(root, 'index.html');
  res.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' });
  createReadStream(file).pipe(res);
}).listen(port, () => console.log(`serving dist on :${port}, /api -> ${api.origin}`));
