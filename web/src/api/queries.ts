import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { api, unwrap } from './client'
import { ApiError, login, logout, onSignedOut } from '../auth/session'
import type { components } from './schema'

type Me = components['schemas']['UserResponse']
type Home = components['schemas']['HomeResponse']
type StokvelList = components['schemas']['StokvelResponse'][]
type CycleList = components['schemas']['ContributionCycleResponse'][]

export const queryKeys = {
  me: ['me'] as const,
  home: ['me', 'home'] as const,
  stokvels: ['stokvels'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  cycles: (id: string) => ['stokvels', id, 'cycles'] as const,
  contributions: (id: string) => ['stokvels', id, 'contributions'] as const,
}

// ---------------- Auth ----------------

export function useMe() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: async () => unwrap<Me>(api.GET('/api/v1/me', { credentials: 'include' })),
    staleTime: 60_000,
  })
}

export function useHome() {
  return useQuery({
    queryKey: queryKeys.home,
    queryFn: async () => unwrap<Home>(api.GET('/api/v1/me/home', { credentials: 'include' })),
    staleTime: 10_000,
  })
}

export function useLogin() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (input: { email: string; password: string }) => {
      await login(input.email, input.password)
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['me'] }),
  })
}

export function useLogout() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async () => await logout(),
    onSettled: () => qc.clear(),
  })
}

// ---------------- Stokvels ----------------

export function useStokvels() {
  return useQuery({
    queryKey: queryKeys.stokvels,
    // FIX 1: The API returns an array directly, not { items: [...] }
    queryFn: async () =>
      unwrap<StokvelList>(api.GET('/api/v1/stokvels', { credentials: 'include' })),
    staleTime: 30_000,
  })
}

export function useStokvel(id: string | undefined) {
  return useQuery({
    queryKey: id ? queryKeys.stokvel(id) : ['stokvels', 'none'],
    enabled: !!id,
    queryFn: async () =>
      unwrap<components['schemas']['StokvelResponse']>(
        api.GET('/api/v1/stokvels/{stokvelId}', {
          params: { path: { stokvelId: id! } },
          credentials: 'include',
        }),
      ),
    staleTime: 30_000,
  })
}

export function useCycles(id: string | undefined) {
  return useQuery({
    queryKey: id ? queryKeys.cycles(id) : ['stokvels', 'none', 'cycles'],
    enabled: !!id,
    queryFn: async () =>
      unwrap<CycleList>(
        api.GET('/api/v1/stokvels/{stokvelId}/cycles', {
          params: { path: { stokvelId: id! } },
          credentials: 'include',
        }),
      ),
    staleTime: 10_000,
  })
}

// ---------------- Contributions (paged) ----------------

type ContributionsPage = {
  items: components['schemas']['ContributionResponse'][]
  nextPageToken: string | null
}

export function useContributions(id: string | undefined) {
  // FIX 2: Tell useInfiniteQuery exactly what a page looks like
  return useInfiniteQuery<ContributionsPage, Error, { pages: ContributionsPage[]; pageParams: (string | null)[] }, readonly unknown[], string | null>({
    queryKey: id ? queryKeys.contributions(id) : ['stokvels', 'none', 'contributions'],
    enabled: !!id,
    initialPageParam: null,
    getNextPageParam: (lastPage) => lastPage.nextPageToken ?? undefined,
    queryFn: async ({ pageParam }) => {
      return unwrap<ContributionsPage>(
        api.GET('/api/v1/stokvels/{stokvelId}/contributions', {
          params: {
            path: { stokvelId: id! },
            query: pageParam ? { pageToken: pageParam } : undefined,
          },
          credentials: 'include',
        }),
      )
    },
    staleTime: 10_000,
  })
}

export function wireSignOutToCache(qc: ReturnType<typeof useQueryClient>) {
  return onSignedOut(() => qc.clear())
}

export { ApiError }