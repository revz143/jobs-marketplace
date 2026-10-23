const http = require('http');
const { createMarketplace } = require('./marketplace');

class HttpError extends Error {
  constructor(statusCode, message) {
    super(message);
    this.statusCode = statusCode;
  }
}

function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    let rawData = '';

    req.on('data', (chunk) => {
      rawData += chunk;
    });

    req.on('end', () => {
      if (!rawData) {
        resolve({});
        return;
      }

      try {
        resolve(JSON.parse(rawData));
      } catch {
        reject(new Error('Invalid JSON body'));
      }
    });

    req.on('error', reject);
  });
}

function sendJson(res, statusCode, body) {
  res.statusCode = statusCode;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify(body));
}

function createApiServer() {
  const marketplace = createMarketplace();

  return http.createServer(async (req, res) => {
    const method = req.method || 'GET';
    const url = new URL(req.url || '/', 'http://localhost');

    try {
      if (method === 'GET' && url.pathname === '/health') {
        sendJson(res, 200, { status: 'ok' });
        return;
      }

      if (method === 'GET' && url.pathname === '/jobs') {
        sendJson(res, 200, { jobs: marketplace.listJobs() });
        return;
      }

      if (method === 'POST' && url.pathname === '/jobs') {
        const payload = await readJsonBody(req);
        let job;
        try {
          job = marketplace.createJob(payload);
        } catch (error) {
          throw new HttpError(400, error.message);
        }
        sendJson(res, 201, { job });
        return;
      }

      if (method === 'GET' && url.pathname === '/subcontractors') {
        sendJson(res, 200, { subcontractors: marketplace.listSubcontractors() });
        return;
      }

      if (method === 'POST' && url.pathname === '/subcontractors') {
        const payload = await readJsonBody(req);
        let subcontractor;
        try {
          subcontractor = marketplace.createSubcontractor(payload);
        } catch (error) {
          throw new HttpError(400, error.message);
        }
        sendJson(res, 201, { subcontractor });
        return;
      }

      const outsourceMatch = /^\/jobs\/(\d+)\/outsource$/.exec(url.pathname);
      if (method === 'POST' && outsourceMatch) {
        const jobId = Number(outsourceMatch[1]);
        const payload = await readJsonBody(req);
        const subcontractorId = payload.subcontractorId;
        if (typeof subcontractorId !== 'number' || !Number.isInteger(subcontractorId) || subcontractorId < 1) {
          throw new HttpError(400, 'Valid subcontractorId is required');
        }
        let job;
        try {
          job = marketplace.outsourceJob(jobId, subcontractorId);
        } catch (error) {
          if (error.message === 'Job not found' || error.message === 'Subcontractor not found') {
            throw new HttpError(404, error.message);
          }

          throw new HttpError(400, error.message);
        }
        sendJson(res, 200, { job });
        return;
      }

      sendJson(res, 404, { error: 'Not found' });
    } catch (error) {
      if (error instanceof HttpError) {
        sendJson(res, error.statusCode, { error: error.message });
        return;
      }

      sendJson(res, 500, { error: 'Internal server error' });
    }
  });
}

module.exports = {
  createApiServer,
};

if (require.main === module) {
  const server = createApiServer();
  const port = Number(process.env.PORT) || 3000;

  server.listen(port, () => {
    // eslint-disable-next-line no-console
    console.log(`Jobs marketplace API listening on :${port}`);
  });
}
