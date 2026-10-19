export const isTreasurer = (role: string) => role === 'Treasurer'

export function roleText(role: string): string {
  if (role === 'Treasurer') return "You're the Treasurer"
  if (role === 'Member') return "You're a Member"
  return `You're ${role}`
}
