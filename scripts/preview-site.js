// Serves the built website (_site/) on this computer so you can look at it in a browser.
// Used by scripts/preview-site.sh. Only listens on this computer (127.0.0.1).
const http = require("http");
const fs = require("fs");
const path = require("path");

const root = path.resolve("_site");
const port = Number(process.argv[2]) || 8080;
const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".xml": "application/xml",
  ".txt": "text/plain; charset=utf-8",
};

function send(res, status, file) {
  fs.readFile(file, (error, data) => {
    if (error) {
      res.writeHead(500);
      res.end("Could not read the file.");
      return;
    }
    res.writeHead(status, { "Content-Type": types[path.extname(file)] || "application/octet-stream" });
    res.end(data);
  });
}

http
  .createServer((req, res) => {
    let urlPath = "/";
    try {
      urlPath = decodeURIComponent(new URL(req.url, "http://localhost").pathname);
    } catch {
      urlPath = "/missing";
    }
    const target = path.join(root, urlPath.endsWith("/") ? urlPath + "index.html" : urlPath);
    if (!target.startsWith(root)) {
      send(res, 404, path.join(root, "404.html"));
      return;
    }
    fs.stat(target, (error, stats) => {
      if (error || !stats.isFile()) send(res, 404, path.join(root, "404.html"));
      else send(res, 200, target);
    });
  })
  .listen(port, "127.0.0.1", () => console.log(`Serving _site on http://localhost:${port}`));
