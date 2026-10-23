import type { Metadata } from "next";
import "./styles.css";

export const metadata: Metadata = { title: "JobsMarketplace — Marketplace Demo", description: "A local subcontractor marketplace demonstration" };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="en"><body>{children}</body></html>; }
