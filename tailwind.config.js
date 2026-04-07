/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Views/**/*.cshtml", "./wwwroot/js/**/*.js"],
  theme: {
    extend: {
      colors: {
        // Unified brand palette
        'brand': {
          50:  '#eff6ff',
          100: '#dbeafe',
          200: '#bfdbfe',
          300: '#93c5fd',
          400: '#60a5fa',
          500: '#3b82f6',
          600: '#1d4ed8',
          700: '#1e40af',
          800: '#1e3a8a',
          900: '#172554',
        },
        'surface': {
          DEFAULT: '#f8fafc',
          card: '#ffffff',
          muted: '#f1f5f9',
          border: 'rgba(30,58,138,0.06)',
        },
        'txt': {
          DEFAULT: '#1e293b',
          secondary: '#64748b',
          muted: '#94a3b8',
          hint: '#86868b',
        },
        'univera-red': '#DD2F42',
        'univera-red-hover': '#B93042',
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'SF Pro Display', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        'apple': '12px',
        'card': '16px',
      },
      boxShadow: {
        'card': '0 1px 3px rgba(30,58,138,0.04), 0 4px 12px rgba(30,58,138,0.03)',
        'card-hover': '0 4px 16px rgba(30,58,138,0.08), 0 12px 40px rgba(30,58,138,0.06)',
        'input-focus': '0 0 0 4px rgba(99, 102, 241, 0.15)',
      },
    },
  },
  plugins: [],
}
