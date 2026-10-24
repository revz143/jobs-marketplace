import { CalendarDays, CircleDollarSign } from "lucide-react";
import styles from "../../workspace.module.css";
import { excerpt, money, shortDate } from "../../lib/format";
import type { Job, Role } from "../../lib/marketplace-types";
import { StatusBadge } from "./ui";

export function JobCard({ job, role, selected, onSelect }: { job: Job; role: Role; selected: boolean; onSelect: () => void }) {
  return <button type="button" className={`${styles.jobCard} ${selected ? styles.jobCardSelected : ""}`} onClick={onSelect} aria-pressed={selected}>
    <span className={styles.jobMeta}><StatusBadge status={job.status} />{role === "Customer" && job.offerCount !== null && <b>{job.offerCount} {job.offerCount === 1 ? "offer" : "offers"}</b>}{role === "Contractor" && job.hasCurrentUserOffer && <b className={styles.offeredTag}>Offer sent</b>}</span>
    <strong>{excerpt(job.description, 94)}</strong>
    <span className={styles.jobCopy}>{excerpt(job.description, 146)}</span>
    <span className={styles.jobFacts}><span><CircleDollarSign size={14} />{money(job.budget)}</span><span><CalendarDays size={14} />Due {shortDate(job.dueDate)}</span></span>
  </button>;
}
