import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// the API allows http://localhost:5173 through CORS, so keep this port
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
});
