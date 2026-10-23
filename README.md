# jobs-marketplace

A demo marketplace outsourcing jobs to subcontractors, backed by an HTTP API.

## Running locally

```bash
npm start
```

The API listens on `PORT` (default `3000`).

## HTTP API

- `GET /health` - health check
- `GET /jobs` - list jobs
- `POST /jobs` - create a job with JSON body `{ "title": "..." }`
- `GET /subcontractors` - list subcontractors
- `POST /subcontractors` - create a subcontractor with JSON body `{ "name": "..." }`
- `POST /jobs/:id/outsource` - outsource a job with JSON body `{ "subcontractorId": number }`

## Tests

```bash
npm test
```
