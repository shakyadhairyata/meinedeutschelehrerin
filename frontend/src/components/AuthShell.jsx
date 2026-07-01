export default function AuthShell({ title, subtitle, children, footer }) {
  return (
    <div className="grid min-h-screen md:grid-cols-2">
      <div className="relative hidden flex-col justify-between overflow-hidden p-10 text-white md:flex"
        style={{ backgroundImage: 'linear-gradient(160deg,#065f46 0%,#0a0f0d 70%)' }}>
        <div className="pointer-events-none absolute -right-16 -top-16 h-64 w-64 rounded-full bg-brand-400/20 blur-3xl" />
        <div className="flex items-center gap-2 font-display text-xl font-bold">
          <span className="grid h-10 w-10 place-items-center rounded-xl text-xl shadow-glow"
            style={{ backgroundImage: 'linear-gradient(135deg,#10d96a,#10b981)' }}>👩‍🏫</span>
          Meine<span className="text-brand-300">Deutsche</span>Lehrerin
        </div>
        <div className="relative">
          <h1 className="font-display text-4xl font-bold leading-tight">
            Dein Weg von <span className="text-brand-300">A1 bis C1</span> – mit Spaß.
          </h1>
          <p className="mt-4 max-w-md text-white/80">
            Lektionen, Grammatik, Wortschatz, Lesen, Hören, Sprechen und Schreiben — mit Sofort-Feedback,
            Fortschritts-Tracking und einem persönlichen Lernplan für die Goethe-Prüfung.
          </p>
          <div className="mt-6 flex flex-wrap gap-2 text-sm font-bold">
            {['A1', 'A2', 'B1', 'B2', 'C1'].map((l, i) => (
              <span key={l} className="rounded-full bg-brand-400/15 px-4 py-1.5 ring-1 ring-brand-400/30 animate-float"
                style={{ animationDelay: `${i * 0.3}s` }}>{l}</span>
            ))}
          </div>
        </div>
        <p className="relative text-sm text-brand-300/80">Üben. Wiederholen. Bestehen.</p>
      </div>
      <div className="flex items-center justify-center p-6">
        <div className="w-full max-w-sm animate-pop">
          <h2 className="font-display text-3xl font-bold text-white">{title}</h2>
          <p className="mt-1 text-white/60">{subtitle}</p>
          <div className="mt-6">{children}</div>
          {footer && <div className="mt-6 text-sm text-white/60">{footer}</div>}
        </div>
      </div>
    </div>
  )
}
