import type { paths } from './schema'

type HomeBody =
  paths['/api/v1/me/home']['get']['responses']['200']['content']['application/json']
export type MeHomeEntry = HomeBody['stokvels'][number]

type ContributionsBody =
  paths['/api/v1/stokvels/{stokvelId}/contributions']['get']['responses']['200']['content']['application/json']
export type ContributionItem = ContributionsBody['items'][number]

type CyclesBody =
  paths['/api/v1/stokvels/{stokvelId}/cycles']['get']['responses']['200']['content']['application/json']
export type CycleItem = CyclesBody[number]