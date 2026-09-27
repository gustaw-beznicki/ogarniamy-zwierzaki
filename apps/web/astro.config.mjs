// @ts-check
import { defineConfig } from 'astro/config';

// https://astro.build/config
export default defineConfig({
	vite: {
		server: {
			// Dev-only: mirror the Azure Static Web Apps /api route to the local API.
			proxy: {
				'/api': {
					target: process.env.API_PROXY_TARGET ?? 'http://localhost:5180',
					changeOrigin: true,
				},
			},
		},
	},
});
