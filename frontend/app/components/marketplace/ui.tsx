import { CheckCircle2, FileText, SearchX, X } from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";
import styles from "../../workspace.module.css";
import type { JobStatus } from "../../lib/marketplace-types";

export function StatusBadge({ status }: { status: JobStatus }) {
  return <span className={`${styles.status} ${status === "Awarded" ? styles.statusAwarded : ""}`}>{status}</span>;
}

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <div className={styles.emptyState}><span>{title.toLowerCase().includes("search") ? <SearchX /> : <FileText />}</span><h2>{title}</h2><p>{description}</p>{action}</div>;
}

export function Toast({ message }: { message: string }) {
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    setVisible(true);
    const timeout = window.setTimeout(() => setVisible(false), 4500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  if (!visible) return null;
  return <div className={styles.toast} role="status"><CheckCircle2 aria-hidden="true" /><span>{message}</span><button type="button" onClick={() => setVisible(false)} aria-label="Dismiss notification"><X size={15} aria-hidden="true" /></button></div>;
}
