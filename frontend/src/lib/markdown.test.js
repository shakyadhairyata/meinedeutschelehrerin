import { describe, it, expect } from 'vitest'
import { renderMarkdown } from './markdown'

describe('renderMarkdown', () => {
  it('renders an h2 heading', () => {
    expect(renderMarkdown('## Das Verb »sein«')).toContain('<h2>Das Verb »sein«</h2>')
  })

  it('renders bold and inline code', () => {
    const html = renderMarkdown('Ich **bin** ein `Student`.')
    expect(html).toContain('<strong>bin</strong>')
    expect(html).toContain('<code>Student</code>')
  })

  it('renders bullet lists', () => {
    const html = renderMarkdown('- eins\n- zwei')
    expect(html).toContain('<ul>')
    expect(html).toContain('<li>eins</li>')
    expect(html).toContain('<li>zwei</li>')
  })

  it('renders a table and skips the separator row', () => {
    const html = renderMarkdown('| Person | Form |\n|---|---|\n| ich | bin |')
    expect(html).toContain('<table>')
    expect(html).toContain('<th>Person</th>')
    expect(html).toContain('<td>ich</td>')
    expect(html).not.toContain('---')
  })

  it('escapes raw HTML to avoid injection', () => {
    const html = renderMarkdown('<script>alert(1)</script>')
    expect(html).toContain('&lt;script&gt;')
    expect(html).not.toContain('<script>')
  })
})
