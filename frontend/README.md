# Frontend

React 19 + Vite + Tailwind single-page app for MeineDeutscheLehrerin.

```bash
npm install
npm run dev      # http://localhost:5173, proxies /api to the API on :5099
npm run build    # production build to dist/
npm test         # Vitest
```

Pages live in `src/pages`, shared UI in `src/components` (the exercise engine is
`components/ExercisePlayer.jsx`), and the API client with token refresh is in `src/api`.
