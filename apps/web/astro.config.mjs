// @ts-check
import { defineConfig } from 'astro/config';
import react from '@astrojs/react';

// https://astro.build/config
export default defineConfig({
	integrations: [react()],
	i18n: {
		locales: ['pl', 'en'],
		defaultLocale: 'pl',
		routing: { prefixDefaultLocale: false },
	},
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
