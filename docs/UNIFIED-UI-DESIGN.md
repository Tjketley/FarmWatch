# FarmWatch Unified UI

## Product direction

FarmWatch is the product experience; ArchiSteamFarm (ASF) remains the management engine. The goal is one cohesive, premium infrastructure-management application, not an ASF page with a plugin dashboard bolted onto it.

The complete application must feel intentionally designed from end to end. ASF's existing features, API behaviour, authentication, permissions and safety controls remain available. The redesign must not remove or weaken native ASF capabilities.

## Visual system

### Brand character
- Premium, restrained, technical and calm.
- Dark-first interface with deep ink/navy surfaces, subtle neutral borders and clear text contrast.
- Neon green is a signature accent for selected states, positive health and key actions, not a page background or a universal highlight.
- Avoid generic freeware/plugin styling, excessive glow, decorative gradients, oversized cards and noisy dashboards.

### Layout
- Persistent, collapsible left navigation with clear groups: Overview, Operations, FarmWatch, Analytics and System.
- Compact top bar for page context, global connection/health state and account/session actions.
- Consistent content width, spacing scale, typography and responsive breakpoints.
- Prefer useful information density, aligned tables and purposeful panels over oversized statistic cards.
- Keep destructive or safety-sensitive actions visually distinct and require confirmation where appropriate.

### Components
- Shared design tokens for colour, spacing, typography, radii, elevation and focus states.
- Consistent buttons, inputs, selects, toggles, badges, tables, empty states, skeletons, alerts, dialogs and toasts.
- Accessible keyboard navigation, visible focus, semantic labels and sufficient contrast.
- Responsive layouts for desktop, tablet and mobile.

## Application information architecture

### Overview
System health, ASF version, bot connectivity, farming state, recent activity and important FarmWatch status.

### Operations
- Bots: bot list, connection state, farming state, actions and per-bot details.
- Commands: full ASF command terminal with history and useful output formatting.
- Logs: live stream, history, severity filters and search.

### FarmWatch
- Monitoring and eligibility.
- Claim queue and claim history.
- Safety controls, observe-only state and global/per-bot auto-claim switches.
- Diagnostics and event history.
- WatchFarm Connect configuration and delivery health.

### Analytics
Dedicated statistics experience with clearly defined time windows, activity charts, claim breakdowns, farming sessions and trade activity. Reuse the same metric definitions and visual language in WatchFarm Connect; Connect remains reporting-only.

### System
- ASF global configuration and schema-driven editing.
- Per-bot configuration.
- Bans, plugins, releases, UI preferences and other native ASF management features.
- Preserve all existing ASF routes and workflows unless a deliberate equivalent has been implemented and verified.

## Technical integration strategy

1. Pin a specific upstream ASF-ui source revision compatible with the ASF version targeted by FarmWatch. Do not track upstream main implicitly.
2. Preserve the official ASF-ui application structure and native API client/authentication layer as the functional baseline.
3. Apply a FarmWatch-owned design system and redesign the shared application shell and native views, rather than styling only the new FarmWatch pages.
4. Add FarmWatch views and navigation to the same router and shell. Do not expose a second, separate management interface.
5. Keep ASF APIs as the authority for ASF configuration, bot actions, commands and logs. Use FarmWatch IPC endpoints only for FarmWatch-specific data and controls.
6. Build the UI assets deterministically in CI and package the generated static output into the plugin.
7. Keep the existing /FarmWatch entry point until the full replacement UI has passed integration and runtime testing. Only then consider setting the plugin web path to /.

## Rollout gates

- Native ASF bot controls and all supported bot workflows verified.
- Global and per-bot configuration editing verified, including sensitive fields and validation.
- Commands, live logs and log history verified.
- Plugins, releases, bans, UI preferences and other native views accounted for.
- FarmWatch controls verified without relaxing observe-only or auto-claim safety defaults.
- Authentication and authorization remain enforced by ASF.
- Production UI build is included in the plugin artifact and works on the target ASF version.
- Test on Raspberry Pi OS 64-bit ARM64 with ASF 6.3.11.7 before changing the root web path.

## Upstream attribution and licensing

The official ASF-ui project is licensed under Apache License 2.0. If its source is vendored or modified, retain the license and applicable copyright/attribution notices, include prominent change notices in modified files, and record the exact upstream commit used. Do not imply that ASF-ui's upstream authors endorse FarmWatch.

## Non-negotiable constraints

- No loss of ASF functionality for visual convenience.
- No second interface users have to search for or bookmark.
- No automatic switch to the replacement root before the full UI is tested.
- No merge or release without explicit approval.
