"use client";

import { FormEvent, useState } from "react";
import { ArrowLeft, Check, CircleDollarSign, Clock3, ShieldCheck, Star } from "lucide-react";
import styles from "../../workspace.module.css";
import { date, money } from "../../lib/format";
import type { Job, Me, Offer } from "../../lib/marketplace-types";
import { EmptyState, StatusBadge } from "./ui";

export function JobDetail({ job, offers, me, onBack, onOffer, onAccept, onPostJob }: { job: Job | null; offers: Offer[]; me: Me; onBack: () => void; onOffer: (price: string) => Promise<void>; onAccept: (offer: Offer) => Promise<void>; onPostJob: () => void }) {
  const [price, setPrice] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [confirming, setConfirming] = useState<Offer | null>(null);
  if (!job) return <section className={styles.detail}><EmptyState title="Select a job" description="Choose a project from the marketplace to review its brief, budget, dates, and private offers." action={me.role === "Customer" ? <button className={styles.primary} onClick={onPostJob}>Post a job</button> : undefined} /></section>;

  const owner = me.role === "Customer" && job.customerId === me.profileId;
  const existingOffer = me.role === "Contractor" && offers.some((offer) => offer.contractorId === me.profileId);
  const submitOffer = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setError(null); setSubmitting(true); try { await onOffer(price); setPrice(""); } catch (reason) { setError(reason instanceof Error ? reason.message : "Could not submit the offer."); } finally { setSubmitting(false); } };
  const accept = async () => { if (!confirming) return; setSubmitting(true); try { await onAccept(confirming); setConfirming(null); } catch (reason) { setError(reason instanceof Error ? reason.message : "Could not award the offer."); } finally { setSubmitting(false); } };

  return <section className={styles.detail}><button className={styles.backButton} onClick={onBack}><ArrowLeft size={16} />All jobs</button><div className={styles.detailHead}><div><p className={styles.kicker}>Job #{job.id}</p><small>{owner ? "Your posted job" : "Marketplace job"}</small></div><StatusBadge status={job.status} /></div><h2>{job.description}</h2><div className={styles.facts}><div><small><CircleDollarSign size={14} />Budget</small><strong>{money(job.budget)}</strong></div><div><small><Clock3 size={14} />Start date</small><strong>{date(job.startDate)}</strong></div><div><small><Clock3 size={14} />Due date</small><strong>{date(job.dueDate)}</strong></div></div><section className={styles.scope}><h3>Scope of work</h3><p>{job.description}</p></section>{job.status === "Awarded" && <p className={styles.awarded}><Check size={18} />This job has been awarded. Private bidding is closed.</p>}
    {me.role === "Contractor" && job.status === "Open" && (existingOffer ? <p className={styles.saved}><Check size={18} />Your private offer is saved below.</p> : <form className={styles.offerForm} onSubmit={submitOffer}><label>Your offer (PHP)<input required type="number" min="1" step="0.01" value={price} onChange={(event) => setPrice(event.target.value)} placeholder="Enter your price" /></label><button className={styles.primary} disabled={submitting}>Submit offer</button></form>)}
    {error && <p className={styles.formError} role="alert">{error}</p>}
    <section className={styles.offers}><div><p className={styles.kicker}>{owner ? "Private offers" : me.role === "Contractor" ? "Your offer" : "Offer access"}</p><h3>{owner ? `Offers received (${offers.length})` : me.role === "Contractor" ? "Your submitted offer" : "Offers are private"}</h3></div>{owner ? <OfferList offers={offers} job={job} onAccept={setConfirming} /> : me.role === "Contractor" ? <OfferList offers={offers} job={job} /> : <p className={styles.privateNotice}><ShieldCheck size={17} />Only the job owner can view incoming offers.</p>}</section>
    {confirming && <div className={styles.modalBackdrop} role="presentation"><section className={styles.confirm} role="dialog" aria-modal="true" aria-labelledby="award-title"><p className={styles.kicker}>Confirm award</p><h3 id="award-title">Award this job to {confirming.contractorName}?</h3><p>You will award the job for <strong>{money(confirming.price)}</strong>. Other contractors can no longer submit offers.</p><footer><button className={styles.secondary} onClick={() => setConfirming(null)}>Cancel</button><button className={styles.primary} onClick={accept} disabled={submitting}>Confirm &amp; award</button></footer></section></div>}
  </section>;
}

function OfferList({ offers, job, onAccept }: { offers: Offer[]; job: Job; onAccept?: (offer: Offer) => void }) {
  if (!offers.length) return <EmptyState title="No offers yet" description="Qualified contractors will appear here after submitting a private offer." />;
  return <div className={styles.offerList}>{offers.map((offer) => <article key={offer.id} className={styles.offer}><div className={styles.contractor}><span>{offer.contractorName.slice(0, 1).toUpperCase()}</span><div><strong>{offer.contractorName}</strong><small><Star size={12} fill="currentColor" />{offer.contractorRating.toFixed(1)} contractor rating</small></div></div><div className={styles.offerPrice}><strong>{money(offer.price)}</strong>{offer.isOverBudget && <small>Over budget</small>}</div>{onAccept && job.status === "Open" && <button className={styles.acceptButton} onClick={() => onAccept(offer)}>Award</button>}</article>)}</div>;
}
