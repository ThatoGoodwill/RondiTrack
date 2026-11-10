import type { Metadata } from 'next'
import { NuqsAdapter } from 'nuqs/adapters/next/app'
import { AppQueryProvider } from '@/components/AppQueryProvider'
import './globals.css'
export const metadata: Metadata = { title: 'RondiTrack', description: 'Contributions and stokvels' }
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en-ZA"><body><NuqsAdapter><AppQueryProvider>{children}</AppQueryProvider></NuqsAdapter></body></html>
}
