// スプラッシュは React を使わず、最小の JS だけで言語を反映する（描画を遅らせない）
const lang = new URLSearchParams(location.search).get('lang') === 'ja' ? 'ja' : 'en'
document.documentElement.lang = lang
const label = document.getElementById('status')
if (label) label.textContent = lang === 'ja' ? '起動しています' : 'Starting up'

export {}
