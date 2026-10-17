import type { components } from "./api";

export type Home = components["schemas"]["HomeResponse"];

// The total a member still owes across ALL their stokvels, in cents (integers: exact in JavaScript).
// Written defensively so it compiles whether the generator marks a property optional or not.
export function totalOwedCents(home: Home): number {
  return (home.stokvels ?? []).reduce((sum, s) => sum + (s.currentCycle?.owedCents ?? 0), 0);
}
