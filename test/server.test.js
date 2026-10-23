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

async function withServer(run) {
  const server = createApiServer();
  await new Promise((resolve) => server.listen(0, resolve));

  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;

  try {
    await run(baseUrl);
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
}

test('creates jobs and outsources them to subcontractors', async () => {
  await withServer(async (baseUrl) => {
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

    const outsourceResponse = await request(baseUrl, `/jobs/${createJobBody.job.id}/outsource`, {
      method: 'POST',
      body: JSON.stringify({ subcontractorId: createSubcontractorBody.subcontractor.id }),
    });
    assert.equal(outsourceResponse.status, 200);

    const jobsResponse = await request(baseUrl, '/jobs');
    assert.equal(jobsResponse.status, 200);
    const jobsBody = await jobsResponse.json();

    assert.deepEqual(jobsBody.jobs, [
      {
        id: createJobBody.job.id,
        title: 'Build landing page',
        assignedSubcontractorId: createSubcontractorBody.subcontractor.id,
      },
    ]);
  });
});

test('returns validation errors for missing required fields', async () => {
  await withServer(async (baseUrl) => {
    const createJobResponse = await request(baseUrl, '/jobs', {
      method: 'POST',
      body: JSON.stringify({}),
    });

    assert.equal(createJobResponse.status, 400);
    const body = await createJobResponse.json();
    assert.equal(body.error, 'Job title is required');
  });
});

test('returns not found when outsourcing a missing job', async () => {
  await withServer(async (baseUrl) => {
    const createSubcontractorResponse = await request(baseUrl, '/subcontractors', {
      method: 'POST',
      body: JSON.stringify({ name: 'Acme Agency' }),
    });
    assert.equal(createSubcontractorResponse.status, 201);
    const createSubcontractorBody = await createSubcontractorResponse.json();

    const outsourceResponse = await request(baseUrl, '/jobs/999/outsource', {
      method: 'POST',
      body: JSON.stringify({ subcontractorId: createSubcontractorBody.subcontractor.id }),
    });

    assert.equal(outsourceResponse.status, 404);
    const body = await outsourceResponse.json();
    assert.equal(body.error, 'Job not found');
  });
});

test('returns validation error for invalid subcontractorId', async () => {
  await withServer(async (baseUrl) => {
    const createJobResponse = await request(baseUrl, '/jobs', {
      method: 'POST',
      body: JSON.stringify({ title: 'Design logo' }),
    });
    assert.equal(createJobResponse.status, 201);
    const createJobBody = await createJobResponse.json();

    const outsourceResponse = await request(baseUrl, `/jobs/${createJobBody.job.id}/outsource`, {
      method: 'POST',
      body: JSON.stringify({ subcontractorId: 'not-a-number' }),
    });

    assert.equal(outsourceResponse.status, 400);
    const body = await outsourceResponse.json();
    assert.equal(body.error, 'Valid subcontractorId is required');
  });
});
