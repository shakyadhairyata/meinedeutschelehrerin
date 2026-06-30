// Browser speech helpers. Listening uses SpeechSynthesis (German TTS); Speaking uses
// the Web Speech API for on-device transcription. Both no-op when the browser lacks them.

export function speak(text, lang = 'de-DE') {
  if (!('speechSynthesis' in window)) return false
  window.speechSynthesis.cancel()
  const u = new SpeechSynthesisUtterance(text)
  u.lang = lang
  const voice = window.speechSynthesis.getVoices().find((v) => v.lang?.toLowerCase().startsWith('de'))
  if (voice) u.voice = voice
  u.rate = 0.95
  window.speechSynthesis.speak(u)
  return true
}

export function ttsSupported() {
  return 'speechSynthesis' in window
}

export function getRecognition() {
  const SR = window.SpeechRecognition || window.webkitSpeechRecognition
  if (!SR) return null
  const r = new SR()
  r.lang = 'de-DE'
  r.interimResults = false
  r.maxAlternatives = 1
  return r
}

export const recognitionSupported = () => !!(window.SpeechRecognition || window.webkitSpeechRecognition)
