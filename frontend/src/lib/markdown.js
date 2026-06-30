// Minimal Markdown renderer for the trusted lesson content we author (headings,
// tables, bullet lists, bold, inline code, paragraphs). Avoids a heavy dependency.

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function inline(s) {
  return escapeHtml(s)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/`(.+?)`/g, '<code>$1</code>')
}

export function renderMarkdown(md) {
  const lines = (md || '').replace(/\r\n/g, '\n').split('\n')
  const html = []
  let i = 0

  while (i < lines.length) {
    const line = lines[i]

    // Tables: a run of lines that start with '|'
    if (line.trim().startsWith('|')) {
      const block = []
      while (i < lines.length && lines[i].trim().startsWith('|')) { block.push(lines[i]); i++ }
      const rows = block
        .map((r) => r.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim()))
        .filter((cells) => !cells.every((c) => /^:?-+:?$/.test(c) || c === ''))
      if (rows.length) {
        const [head, ...body] = rows
        html.push('<table><thead><tr>' + head.map((c) => `<th>${inline(c)}</th>`).join('') + '</tr></thead><tbody>')
        body.forEach((r) => html.push('<tr>' + r.map((c) => `<td>${inline(c)}</td>`).join('') + '</tr>'))
        html.push('</tbody></table>')
      }
      continue
    }

    // Bullet lists
    if (/^\s*-\s+/.test(line)) {
      html.push('<ul>')
      while (i < lines.length && /^\s*-\s+/.test(lines[i])) {
        html.push(`<li>${inline(lines[i].replace(/^\s*-\s+/, ''))}</li>`)
        i++
      }
      html.push('</ul>')
      continue
    }

    if (line.startsWith('### ')) { html.push(`<h3>${inline(line.slice(4))}</h3>`); i++; continue }
    if (line.startsWith('## ')) { html.push(`<h2>${inline(line.slice(3))}</h2>`); i++; continue }
    if (line.trim() === '') { i++; continue }

    // Paragraph (merge consecutive non-empty, non-special lines)
    const para = []
    while (i < lines.length && lines[i].trim() !== '' && !lines[i].trim().startsWith('|') &&
           !/^\s*-\s+/.test(lines[i]) && !lines[i].startsWith('#')) {
      para.push(lines[i]); i++
    }
    html.push(`<p>${inline(para.join(' '))}</p>`)
  }
  return html.join('\n')
}
