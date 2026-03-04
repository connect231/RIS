/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Views/**/*.cshtml", "./wwwroot/js/**/*.js"],
  theme: {
    extend: {
      colors: {
        'univera-red': '#DD2F42',
        'univera-red-hover': '#B93042',
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      },
    },
  },
  plugins: [],
}
