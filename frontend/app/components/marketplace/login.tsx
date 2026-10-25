"use client";

import { ArrowRight, LockKeyhole, Mail } from "lucide-react";
import { FormEvent, useState } from "react";
import styles from "../../workspace.module.css";

const demos = [
  { email: "customer1@marketplace.test", role: "Customer" },
  { email: "customer2@marketplace.test", role: "Customer" },
  { email: "contractor1@marketplace.test", role: "Contractor" },
  { email: "contractor2@marketplace.test", role: "Contractor" }
] as const;

export function Login({ notice, onSubmit }: { notice: string; onSubmit: (email: string, password: string) => Promise<void> }) {
  const [email, setEmail] = useState<string>(demos[0].email);
  const [password, setPassword] = useState("Demo!12345");
  const [submitting, setSubmitting] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    try { await onSubmit(email, password); } finally { setSubmitting(false); }
  };

  return <main className={styles.login}><section className={styles.loginCopy}><a className={`${styles.brand} ${styles.loginBrand}`} href="#top">Jobs<span>Marketplace</span></a><p className={styles.kicker}>Local subcontractor network</p><h1>Good work,<br /><em>made visible.</em></h1><p>Review scopes, make private offers, and award the right contractor with a clear record at every step.</p><div className={styles.promise}>✦ Private quotes stay with the job owner.</div></section><section className={styles.loginForm}><div><p className={styles.kicker}>Welcome back</p><h2>Sign in to continue.</h2><form onSubmit={submit}><label>Work email <span className={styles.input}><Mail size={16} /><input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} /></span></label><label>Password <span className={styles.input}><LockKeyhole size={16} /><input required type="password" value={password} onChange={(event) => setPassword(event.target.value)} /></span></label><button className={styles.primary} disabled={submitting}>{submitting ? "Signing in…" : "Sign in"}<ArrowRight size={17} /></button></form><div className={styles.demoAccounts}><p>Demo accounts · password: Demo!12345</p>{demos.map((account) => <button key={account.email} type="button" aria-pressed={email === account.email} className={email === account.email ? styles.demoActive : ""} onClick={() => { setEmail(account.email); setPassword("Demo!12345"); }}>{account.email}<small>{account.role}</small></button>)}</div><p className={styles.loginNotice} role="status">{notice}</p></div></section></main>;
}
