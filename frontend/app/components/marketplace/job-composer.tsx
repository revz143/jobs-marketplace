"use client";

import { FormEvent, useState } from "react";
import { Plus, X } from "lucide-react";
import styles from "../../workspace.module.css";

export type NewJob = { startDate: string; dueDate: string; budget: string; description: string };

export function JobComposer({ open, onClose, onCreate }: { open: boolean; onClose: () => void; onCreate: (job: NewJob) => Promise<void> }) {
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  if (!open) return null;

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const job = { startDate: String(form.get("startDate") ?? ""), dueDate: String(form.get("dueDate") ?? ""), budget: String(form.get("budget") ?? ""), description: String(form.get("description") ?? "") };
    if (job.dueDate < job.startDate) { setError("The due date must be on or after the start date."); return; }
    setError(null); setSubmitting(true);
    try { await onCreate(job); } catch (reason) { setError(reason instanceof Error ? reason.message : "Could not publish the job."); } finally { setSubmitting(false); }
  };

  return <div className={styles.modalBackdrop} role="presentation"><section className={`${styles.modal} ${styles.composer}`} role="dialog" aria-modal="true" aria-labelledby="composer-title"><header><div><p className={styles.kicker}>New listing</p><h2 id="composer-title">Post a job</h2></div><button type="button" aria-label="Close job composer" onClick={onClose}><X /></button></header><form onSubmit={submit}>{error && <p className={styles.formError} role="alert">{error}</p>}<div className={styles.twoFields}><label>Start date<input required name="startDate" type="date" /></label><label>Due date<input required name="dueDate" type="date" /></label></div><label>Allocated budget (PHP)<input required name="budget" type="number" min="1" step="0.01" placeholder="125000" /></label><label>Job brief &amp; requirements<textarea required name="description" minLength={1} maxLength={5000} placeholder="Describe the work, expected outcome, and any requirements a contractor should know." /></label><footer><button type="button" className={styles.secondary} onClick={onClose}>Cancel</button><button className={styles.primary} disabled={submitting}><Plus size={17} />{submitting ? "Publishing…" : "Publish job"}</button></footer></form></section></div>;
}
