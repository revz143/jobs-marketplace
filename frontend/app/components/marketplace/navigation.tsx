import { LogOut } from "lucide-react";
import styles from "../../workspace.module.css";
import type { Me } from "../../lib/marketplace-types";

export function Navigation({ me, onSignOut }: { me: Me; onSignOut: () => void }) {
  return <header className={styles.topbar}><a className={styles.brand} href="#top">Jobs<span>Marketplace</span></a><div className={styles.account}><span className={styles.avatar}>{me.displayName.slice(0, 1).toUpperCase()}</span><span><strong>{me.displayName}</strong><small>{me.role}</small></span><button type="button" onClick={onSignOut}><LogOut size={15} /> <span>Sign out</span></button></div></header>;
}
