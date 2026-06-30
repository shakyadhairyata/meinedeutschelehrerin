/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        // Brand = green (black / white / green theme)
        brand: {
          50: '#ecfdf5', 100: '#d1fae5', 200: '#a7f3d0', 300: '#6ee7b7',
          400: '#34d399', 500: '#10d96a', 600: '#10b981', 700: '#059669',
          800: '#047857', 900: '#065f46',
        },
        ink: {
          900: '#050807', 800: '#0a0f0d', 700: '#111815', 600: '#18211d',
          500: '#1f2a25',
        },
      },
      fontFamily: {
        sans: ['Nunito', 'Inter', 'system-ui', 'Segoe UI', 'sans-serif'],
        display: ['Fredoka', 'Nunito', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        glow: '0 0 0 1px rgba(16,217,106,0.25), 0 8px 30px -8px rgba(16,217,106,0.35)',
      },
      keyframes: {
        pop: { '0%': { transform: 'scale(0.96)', opacity: '0' }, '100%': { transform: 'scale(1)', opacity: '1' } },
        float: { '0%,100%': { transform: 'translateY(0)' }, '50%': { transform: 'translateY(-6px)' } },
      },
      animation: {
        pop: 'pop 0.18s ease-out',
        float: 'float 4s ease-in-out infinite',
      },
    },
  },
  plugins: [],
}
