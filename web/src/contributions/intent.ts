/** One idempotency key per payment intent, not per HTTP request. */
export type PaymentIntent = { stokvelId: string; cycleId: string; memberId: string; amountInCents: number };
export class ContributionIntentKey {
  private key: string | null = null;
  private fingerprint: string | null = null;
  current(intent: PaymentIntent): string {
    if (!Number.isSafeInteger(intent.amountInCents) || intent.amountInCents < 0) throw new Error('Invalid cents');
    const fingerprint = JSON.stringify([intent.stokvelId, intent.cycleId, intent.memberId, intent.amountInCents]);
    if (this.fingerprint !== fingerprint || this.key === null) {
      this.key = crypto.randomUUID();
      this.fingerprint = fingerprint;
    }
    return this.key;
  }
  /** Call only after a confirmed successful response, including an idempotent replay. */
  confirmRecorded(): void { this.key = null; this.fingerprint = null; }
  /** Call on sign-out or when a different member signs in. */
  clear(): void { this.key = null; this.fingerprint = null; }
}
