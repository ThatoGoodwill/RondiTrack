/** Role is scoped to one stokvel, never to the account or access token. */
export type RoleAwareStokvel = { role: string }
export function stokvelsWhereTreasurer<T extends RoleAwareStokvel>(items: readonly T[]): T[] {
  return items.filter(item => item.role === 'Treasurer')
}
