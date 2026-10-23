class MarketplaceValidationError extends Error {}
class MarketplaceNotFoundError extends Error {}

function createMarketplace() {
  const jobs = [];
  const subcontractors = [];
  let nextJobId = 1;
  let nextSubcontractorId = 1;

  function listJobs() {
    return jobs.map((job) => ({ ...job }));
  }

  function listSubcontractors() {
    return subcontractors.map((subcontractor) => ({ ...subcontractor }));
  }

  function createJob(input) {
    const title = typeof input?.title === 'string' ? input.title.trim() : '';
    if (!title) {
      throw new MarketplaceValidationError('Job title is required');
    }

    const job = {
      id: nextJobId++,
      title,
      assignedSubcontractorId: null,
    };

    jobs.push(job);
    return { ...job };
  }

  function createSubcontractor(input) {
    const name = typeof input?.name === 'string' ? input.name.trim() : '';
    if (!name) {
      throw new MarketplaceValidationError('Subcontractor name is required');
    }

    const subcontractor = {
      id: nextSubcontractorId++,
      name,
    };

    subcontractors.push(subcontractor);
    return { ...subcontractor };
  }

  function outsourceJob(jobId, subcontractorId) {
    const job = jobs.find((item) => item.id === jobId);
    if (!job) {
      throw new MarketplaceNotFoundError('Job not found');
    }

    const subcontractor = subcontractors.find((item) => item.id === subcontractorId);
    if (!subcontractor) {
      throw new MarketplaceNotFoundError('Subcontractor not found');
    }

    job.assignedSubcontractorId = subcontractorId;
    return { ...job };
  }

  return {
    listJobs,
    listSubcontractors,
    createJob,
    createSubcontractor,
    outsourceJob,
  };
}

module.exports = {
  createMarketplace,
  MarketplaceValidationError,
  MarketplaceNotFoundError,
};
