import { describe, expect, it } from 'vitest';
import { ContributionIntentKey } from './intent';
const payment = { stokvelId: 's', cycleId: 'c', memberId: 'm', amountInCents: 15050 };
describe('contribution intent', () => {
  it('reuses the key for an unknown outcome including 429/network/5xx', () => {
    const keys = new ContributionIntentKey();
    const original = keys.current(payment);
    expect(keys.current(payment)).toBe(original);
    expect(keys.current(payment)).toBe(original);
  });
  it('makes a fresh key when the amount changes', () => {
    const keys = new ContributionIntentKey();
    expect(keys.current({ ...payment, amountInCents: 17050 })).not.toBe(keys.current(payment));
  });
  it('makes a fresh key after a confirmed success', () => {
    const keys = new ContributionIntentKey();
    const original = keys.current(payment);
    keys.confirmRecorded();
    expect(keys.current(payment)).not.toBe(original);
  });
});
