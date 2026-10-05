export default {
  extends: ["@commitlint/config-conventional"],
  rules: {
    "scope-enum": [2, "always", { scopes: ["mobile", "server", "ui", "cli"], delimiters: [","] }],
    "body-leading-blank": [2, "always"],
    "footer-leading-blank": [2, "always"],
  },
};
