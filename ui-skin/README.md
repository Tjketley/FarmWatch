# FarmWatch ASF-ui skin

This is a theme overlay for the **official ASF-ui** source, not a modified ASF core.
It preserves the upstream Vue application, routes, authentication and API integration,
then overrides the design tokens and component styling with the FarmWatch graphite /
near-black / neon-green palette.

## Apply to upstream ASF-ui

Clone the official UI source, then run:

```bash
./ui-skin/apply-theme.sh /path/to/ASF-ui
cd /path/to/ASF-ui
npm ci
npm run deploy
```

Deploy the generated `dist` contents into the ASF home `www/` directory after
backing up the existing directory. Keep the ASF `config/`, `plugins/`, and
`logs/` directories intact.

ASF's `--path` argument changes the entire ASF home directory, not only its web
assets. Do not point it at a theme-only folder or move the existing configuration
to install this skin.

The overlay is deliberately isolated from the ASF core submodule. This makes it
possible to rebase against upstream ASF-ui updates without carrying a fork of ASF
itself. The FarmWatch bot-management actions remain provided by the plugin API.
