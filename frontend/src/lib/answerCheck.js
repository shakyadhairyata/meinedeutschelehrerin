// Matching helpers for typed vocabulary answers: normalisation, a small Levenshtein
// distance for typo tolerance, and a cloze builder that blanks a word in its example.

export function normalize(s) {
  return (s || '')
    .toLowerCase()
    .trim()
    .replace(/["„“»«().,!?;:]/g, '')
    .replace(/\s+/g, ' ')
}

export function levenshtein(a = '', b = '') {
  const m = a.length
  const n = b.length
  if (!m) return n
  if (!n) return m
  const dp = Array.from({ length: m + 1 }, (_, i) => i)
  for (let j = 1; j <= n; j++) {
    let prev = dp[0]
    dp[0] = j
    for (let i = 1; i <= m; i++) {
      const tmp = dp[i]
      dp[i] = a[i - 1] === b[j - 1] ? prev : 1 + Math.min(prev, dp[i], dp[i - 1])
      prev = tmp
    }
  }
  return dp[m]
}

// Allow more slack the longer the target word, so a small typo still counts.
function tolerance(len) {
  if (len >= 9) return 2
  if (len >= 5) return 1
  return 0
}

export function fuzzyEquals(input, expected) {
  const a = normalize(input)
  const b = normalize(expected)
  if (!a || !b) return false
  return a === b || levenshtein(a, b) <= tolerance(b.length)
}

// Vocab items keep the article inside `german` (e.g. "die Prämisse"). Return just the noun.
export function stripArticle(german, article) {
  if (article && german.toLowerCase().startsWith(article.toLowerCase() + ' ')) {
    return german.slice(article.length + 1)
  }
  return german
}

// Typed recall accepts the full form (with article) or just the noun.
export function checkGermanWord(input, german, article) {
  const noun = stripArticle(german, article)
  return fuzzyEquals(input, german) || (noun !== german && fuzzyEquals(input, noun))
}

function escapeRegExp(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// If the target word appears as a whole word in the example, split it out so the UI can
// blank it. Returns { before, answer, after } or null when the word isn't present verbatim
// (e.g. conjugated/separable verbs), in which case the card isn't usable for cloze.
export function buildCloze(example, german, article) {
  if (!example) return null
  const word = stripArticle(german, article)
  if (/\s/.test(word)) return null
  const re = new RegExp(`(^|[^\\p{L}])(${escapeRegExp(word)})(?![\\p{L}])`, 'iu')
  const m = re.exec(example)
  if (!m) return null
  const start = m.index + m[1].length
  return {
    before: example.slice(0, start),
    answer: example.slice(start, start + word.length),
    after: example.slice(start + word.length),
  }
}
