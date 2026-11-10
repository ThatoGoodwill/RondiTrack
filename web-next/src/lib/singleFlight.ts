/** Server-instance single-flight only. Not a substitute for cross-instance refresh rotation safety. */
export function singleFlight<K, V>(operation: (key: K) => Promise<V>): (key: K) => Promise<V> {
  const inFlight = new Map<K, Promise<V>>();
  return key => {
    const existing = inFlight.get(key);
    if (existing) return existing;
    const result = Promise.resolve().then(() => operation(key));
    inFlight.set(key, result);
    void result.finally(() => { if (inFlight.get(key) === result) inFlight.delete(key); }).catch(() => {});
    return result;
  };
}
