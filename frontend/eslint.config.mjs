import { FlatCompat } from "@eslint/eslintrc";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  {
    ignores: [".next/**", "node_modules/**", "out/**", "dist/**", "coverage/**"],
  },
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    files: ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"],
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector:
            "MemberExpression[object.object.name='process'][object.property.name='env'][property.name='API_BASE_URL']",
          message:
            "Reading process.env.API_BASE_URL outside src/lib/server/backend-invoke.ts is forbidden by FR-16 / AC-26.",
        },
      ],
    },
  },
  {
    files: ["src/lib/server/backend-invoke.ts"],
    rules: {
      "no-restricted-syntax": "off",
    },
  },
];

export default eslintConfig;
