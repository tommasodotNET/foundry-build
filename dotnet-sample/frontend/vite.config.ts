import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const tripAgentTarget = process.env.TRIP_READINESS_AGENT_HTTPS ?? process.env.TRIP_READINESS_AGENT_HTTP;

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: Number.parseInt(process.env.PORT ?? '5173', 10),
    proxy: tripAgentTarget
      ? {
          '/responses': {
            target: tripAgentTarget,
            changeOrigin: true,
            secure: false,
          },
        }
      : undefined,
  },
});
