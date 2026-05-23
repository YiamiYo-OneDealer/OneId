export function SuspendedPage() {
  return (
    <div className="min-h-screen bg-background text-foreground flex items-center justify-center p-8">
      <div>
        <h1 className="text-2xl font-semibold mb-2">Account Suspended</h1>
        <p className="text-muted-foreground">Your organization&apos;s access has been suspended.</p>
      </div>
    </div>
  )
}
