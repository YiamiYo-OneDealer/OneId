import { defineConfig } from '@playwright/test';

export default defineConfig({
  use: {
    launchOptions: {
      args: ['--force-color-profile=srgb'],
    },
  },
});
