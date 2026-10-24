import { CheckCircle2, FileText, SearchX } from "lucide-react";
import type { ReactNode } from "react";
import styles from "../../workspace.module.css";
import type { JobStatus } from "../../lib/marketplace-types";

export function StatusBadge({ status }: { status: JobStatus }) {
  return <span className={`${styles.status} ${status === "Awarded" ? styles.statusAwarded : ""}`}>{status}</span>;
}

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <div className={styles.emptyState}><span>{title.toLowerCase().includes("search") ? <SearchX /> : <FileText />}</span><h2>{title}</h2><p>{description}</p>{action}</div>;
}

export function Toast({ message }: { message: string }) {
  return <p className={styles.toast} role="status"><CheckCircle2 aria-hidden="true" />{message}</p>;
}
