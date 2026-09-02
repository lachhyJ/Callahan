import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: Number(process.env.PORT) || 5173,
  },
  test: {
    // Plain node — everything under test is pure logic. Component tests would
    // need jsdom; there aren't any yet, and the bugs worth catching here have
    // been in date maths and formatting rather than in rendering.
    environment: 'node',
    include: ['src/**/*.test.{js,jsx}'],
  },
})
