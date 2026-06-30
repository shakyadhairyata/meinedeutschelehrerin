import { renderMarkdown } from '../lib/markdown'
import { speak, ttsSupported } from '../lib/speech'

export function Markdown({ children }) {
  return <div className="prose-lesson" dangerouslySetInnerHTML={{ __html: renderMarkdown(children) }} />
}

// Renders inline **bold** and `code` from a trusted string (used for explanations).
export function RichText({ children }) {
  const parts = String(children || '').split(/(\*\*[^*]+\*\*|`[^`]+`)/g)
  return (
    <>
      {parts.map((p, i) => {
        if (p.startsWith('**') && p.endsWith('**')) return <strong key={i} className="font-bold text-brand-300">{p.slice(2, -2)}</strong>
        if (p.startsWith('`') && p.endsWith('`')) return <code key={i} className="rounded bg-white/10 px-1 text-brand-300">{p.slice(1, -1)}</code>
        return p
      })}
    </>
  )
}

export function Spinner({ label = 'Lädt…' }) {
  return (
    <div className="flex items-center justify-center gap-3 py-10 text-white/60">
      <span className="h-5 w-5 animate-spin rounded-full border-2 border-brand-500/30 border-t-brand-400" />
      {label}
    </div>
  )
}

export function ProgressBar({ value, className = '' }) {
  return (
    <div className={`h-2.5 w-full overflow-hidden rounded-full bg-white/10 ${className}`}>
      <div className="h-full rounded-full transition-all duration-500"
        style={{ width: `${Math.min(100, value || 0)}%`, backgroundImage: 'linear-gradient(90deg,#10d96a,#34d399)' }} />
    </div>
  )
}

const SKILL_STYLE = {
  Grammar: 'bg-violet-400/15 text-violet-300 ring-violet-400/30',
  Vocabulary: 'bg-amber-400/15 text-amber-300 ring-amber-400/30',
  Reading: 'bg-sky-400/15 text-sky-300 ring-sky-400/30',
  Listening: 'bg-brand-400/15 text-brand-300 ring-brand-400/30',
  Speaking: 'bg-rose-400/15 text-rose-300 ring-rose-400/30',
  Writing: 'bg-indigo-400/15 text-indigo-300 ring-indigo-400/30',
}
const SKILL_ICON = {
  Grammar: '📐', Vocabulary: '📒', Reading: '📖', Listening: '🎧', Speaking: '🗣️', Writing: '✍️',
}
export function SkillBadge({ skill }) {
  return (
    <span className={`chip ring-1 ${SKILL_STYLE[skill] || 'bg-white/10 text-white/70 ring-white/15'}`}>
      {SKILL_ICON[skill] || '•'} {skill}
    </span>
  )
}

export function LevelPill({ code }) {
  return <span className="chip bg-brand-400/15 font-bold text-brand-300 ring-1 ring-brand-400/30">{code}</span>
}

export function Stat({ label, value, sub }) {
  return (
    <div className="card">
      <div className="font-display text-3xl font-bold text-brand-300">{value}</div>
      <div className="mt-1 text-sm text-white/60">{label}</div>
      {sub && <div className="mt-0.5 text-xs text-white/40">{sub}</div>}
    </div>
  )
}

export function AudioButton({ text, label = 'Anhören' }) {
  if (!ttsSupported()) return <span className="text-xs text-white/40">(Audio im Browser nicht verfügbar)</span>
  return (
    <button type="button" className="btn-ghost" onClick={() => speak(text)}>
      🔊 {label}
    </button>
  )
}

export function Alert({ kind = 'info', children }) {
  const styles = {
    info: 'bg-sky-400/10 text-sky-200 ring-sky-400/30',
    error: 'bg-rose-400/10 text-rose-200 ring-rose-400/30',
    success: 'bg-brand-400/10 text-brand-200 ring-brand-400/30',
  }
  return <div className={`rounded-xl px-4 py-3 text-sm ring-1 ${styles[kind]}`}>{children}</div>
}
