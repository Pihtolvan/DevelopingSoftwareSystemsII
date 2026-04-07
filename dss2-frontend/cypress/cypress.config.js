const { defineConfig } = require("cypress");

module.exports = defineConfig({
  e2e: {
    baseUrl: "http://localhost:5173",
    env: {
      apiBaseUrl: process.env.CYPRESS_API_BASE_URL || "http://localhost:3087"
    },
    setupNodeEvents(on, config) {
      // implement node event listeners here
    },
  },
});
