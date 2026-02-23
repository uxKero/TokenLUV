/** @type {import('tailwindcss').Config} */
export default {
  content: ['./src/renderer/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        'anthropic': '#d946ef',
        'openai': '#00a67e',
        'openrouter': '#36a3ff',
        'xai': '#000000',
        'gemini': '#ea4335'
      }
    }
  },
  plugins: [],
  darkMode: 'class'
}
