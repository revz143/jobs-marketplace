export const money = (value: number) =>
  `₱${Number(value).toLocaleString("en-PH", { maximumFractionDigits: 2 })}`;

export const date = (value: string) =>
  new Intl.DateTimeFormat("en-PH", { month: "short", day: "numeric", year: "numeric" }).format(
    new Date(`${value}T00:00:00`),
  );

export const shortDate = (value: string) =>
  new Intl.DateTimeFormat("en-PH", { month: "short", day: "numeric" }).format(
    new Date(`${value}T00:00:00`),
  );

export const excerpt = (value: string, length = 128) =>
  value.length > length ? `${value.slice(0, length).trimEnd()}…` : value;
