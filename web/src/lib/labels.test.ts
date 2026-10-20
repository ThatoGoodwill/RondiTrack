import { expect, it } from 'vitest'
import { isTreasurer, roleText } from './labels'

it('words the Treasurer role', () => expect(roleText('Treasurer')).toBe("You're the Treasurer"))
it('words the Member role', () => expect(roleText('Member')).toBe("You're a Member"))
it('words an unknown role', () => expect(roleText('Admin')).toBe("You're Admin"))
it('only the Treasurer is a treasurer', () => {
  expect(isTreasurer('Treasurer')).toBe(true)
  expect(isTreasurer('Member')).toBe(false)
})
