import { describe, expect, it } from 'vitest';
import { stokvelsWhereTreasurer } from './treasurer';
it('filters Treasurer separately for each stokvel', () => {
  expect(stokvelsWhereTreasurer([{ id:'family', role:'Treasurer' }, { id:'work', role:'Member' }])).toEqual([{ id:'family', role:'Treasurer' }]);
  expect(stokvelsWhereTreasurer([{ id:'work', role:'Member' }])).toEqual([]);
  expect(stokvelsWhereTreasurer([])).toEqual([]);
});
