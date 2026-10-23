const test = require('node:test');
const assert = require('node:assert/strict');
const { createApiServer } = require('../src/server');

function request(baseUrl, path, options = {}) {
  return fetch(`${baseUrl}${path}`, {
    ...options,
    headers: {
      'content-type': 'application/json',
      ...(options.headers || {}),
    },
  });
}

test('creates jobs and outsources them to subcontractors', async () => {
  const server = createApiServer();
  await new Promise((resolve) => server.listen(0, resolve));

  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;

  try {
    const createJobResponse = await request(baseUrl, '/jobs', {
      method: 'POST',
      body: JSON.stringify({ title: 'Build landing page' }),
    });
    assert.equal(createJobResponse.status, 201);
    const createJobBody = await createJobResponse.json();
    assert.equal(createJobBody.job.title, 'Build landing page');

    const createSubcontractorResponse = await request(baseUrl, '/subcontractors', {
      method: 'POST',
      body: JSON.stringify({ name: 'Acme Agency' }),
    });
    assert.equal(createSubcontractorResponse.status, 201);
    const createSubcontractorBody = await createSubcontractorResponse.json();

    const outsourceResponse = await request(baseUrl, '/jobs/1/outsource', {
      method: 'POST',
      body: JSON.stringify({ subcontractorId: createSubcontractorBody.subcontractor.id }),
    });
    assert.equal(outsourceResponse.status, 200);

    const jobsResponse = await request(baseUrl, '/jobs');
    assert.equal(jobsResponse.status, 200);
    const jobsBody = await jobsResponse.json();

    assert.deepEqual(jobsBody.jobs, [
      {
        id: 1,
        title: 'Build landing page',
        assignedSubcontractorId: 1,
      },
    ]);
  } finally {
    await new Promise((resolve, reject) => {
      server.close((error) => {
        if (error) {
          reject(error);
          return;
        }
        resolve();
      });
    });
  }
});

test('returns validation errors for missing required fields', async () => {
  const server = createApiServer();
  await new Promise((resolve) => server.listen(0, resolve));

  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;

  try {
    const createJobResponse = await request(baseUrl, '/jobs', {
      method: 'POST',
      body: JSON.stringify({}),
    });

    assert.equal(createJobResponse.status, 400);
    const body = await createJobResponse.json();
    assert.equal(body.error, 'Job title is required');
  } finally {
    await new Promise((resolve, reject) => {
      server.close((error) => {
        if (error) {
          reject(error);
          return;
        }
        resolve();
      });
    });
  }
});
