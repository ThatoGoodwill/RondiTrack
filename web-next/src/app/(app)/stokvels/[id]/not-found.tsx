import Link from 'next/link'
export default function NotFound() { return <section className="card space-y-3">
  <h1 className="text-xl font-semibold">We couldn’t find that stokvel.</h1>
  <p>It may have been removed, or you may no longer belong to it.</p>
  <Link className="a" href="/stokvels">Back to your stokvels</Link>
</section> }
