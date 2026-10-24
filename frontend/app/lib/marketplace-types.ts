export type Role = "Customer" | "Contractor";
export type JobStatus = "Open" | "Awarded";
export type Scope = "all" | "owned" | "offered";

export type Me = {
  email: string;
  role: Role;
  profileId: number;
  displayName: string;
};

export type Job = {
  id: number;
  customerId: number;
  startDate: string;
  dueDate: string;
  budget: number;
  description: string;
  status: JobStatus;
  acceptedBy: number | null;
  offerCount: number | null;
  hasCurrentUserOffer: boolean;
  createdAt: string;
};

export type Offer = {
  id: number;
  jobId: number;
  contractorId: number;
  contractorName: string;
  contractorRating: number;
  price: number;
  isOverBudget: boolean;
  createdAt: string;
};

export type JobPage = { items: Job[]; nextCursor: string | null };
