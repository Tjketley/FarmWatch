<template>
  <main class="main-container">
    <section class="fw-page">
      <div class="fw-eyebrow">FarmWatch / Control centre</div>
      <h1 class="fw-title">Overview</h1>
      <p class="fw-subtitle">Your ASF operation and FarmWatch activity at a glance.</p>

      <div class="fw-grid">
        <article class="fw-metric"><div class="fw-metric-label">Managed bots</div><div class="fw-metric-value">{{ botCount }}</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Connected bots</div><div class="fw-metric-value">{{ connectedCount }}</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Games claimed</div><div class="fw-metric-value">{{ number(claims.total) }}</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Farming hours</div><div class="fw-metric-value">{{ hours(farming.lifetimeSeconds) }}</div></article>
      </div>

      <div class="fw-grid" style="grid-template-columns: minmax(0,1.5fr) minmax(260px,1fr)">
        <section class="fw-panel">
          <div style="display:flex;justify-content:space-between;align-items:center;gap:1rem;flex-wrap:wrap">
            <h2 class="fw-panel-title">Bot operations</h2>
            <button class="btn btn-default btn-sm" :disabled="configuringAll || !bots.some(bot => !bot.configured)" @click="configureAll">{{ configuringAll ? 'Configuring…' : 'Configure all bots' }}</button>
          </div>
          <div v-if="bots.length">
            <div v-for="bot in bots" :key="bot.name" style="display:flex;justify-content:space-between;align-items:center;padding:.75rem 0;border-bottom:1px solid var(--fw-border)">
              <div style="min-width:0"><strong>{{ bot.name }}</strong><div class="fw-muted" style="font-size:.78rem">{{ bot.configured ? 'FarmWatch configured' : 'ASF bot · setup required' }}</div></div>
              <div style="display:flex;align-items:center;gap:.8rem;flex-wrap:wrap;justify-content:flex-end">
                <span class="fw-status" :style="{color:bot.connected?'var(--fw-accent)':'#8998a3'}">{{ bot.connected ? 'Connected' : 'Offline' }}</span>
                <button v-if="!bot.configured" class="btn btn-primary btn-sm" :disabled="configuringBot===bot.name" @click="configureBot(bot)">{{ configuringBot===bot.name ? 'Configuring…' : 'Configure FarmWatch' }}</button>
              </div>
            </div>
          </div>
          <p v-else class="fw-muted">No bot information is available yet.</p>
        </section>
        <section class="fw-panel">
          <h2 class="fw-panel-title">FarmWatch safety</h2>
          <p><span class="fw-muted">Mode</span><br><strong>{{ status.observeOnly ? 'Observe only' : 'Active monitoring' }}</strong></p>
          <p><span class="fw-muted">Global auto-claim</span><br><strong>{{ status.globalAutoClaimEnabled ? 'Enabled' : 'Disabled' }}</strong></p>
          <p class="fw-muted" style="font-size:.8rem">Auto-claim remains off unless explicitly enabled in FarmWatch settings.</p>
        </section>
      </div>
      <p v-if="message" class="fw-status" style="margin-top:1rem">{{ message }}</p>
      <p v-if="error" class="fw-error" style="margin-top:1rem">{{ error }}</p>
      <p class="fw-muted" style="font-size:.75rem;margin-top:1rem">Last refreshed: {{ refreshedAt || 'Loading…' }} <button class="btn btn-default btn-sm" style="margin-left:.5rem" @click="refresh">Refresh</button></p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchOverview',
  data() {
    return { status: {}, bots: [], claims: {}, farming: {}, error: '', message: '', refreshedAt: '', configuringAll: false, configuringBot: '' };
  },
  computed: {
    botCount() { return this.bots.length; },
    connectedCount() { return this.bots.filter(bot => bot.connected).length; },
  },
  created() { this.refresh(); },
  methods: {
    number(value) { return Number(value || 0).toLocaleString(); },
    hours(seconds) { return (Number(seconds || 0) / 3600).toFixed(1); },
    async configureBot(bot) {
      this.error = ''; this.message = ''; this.configuringBot = bot.name;
      try {
        const result = await this.$http.post('farmwatch/ConfigureBot', { botName: bot.name });
        this.message = result.message || ('FarmWatch configured for ' + bot.name + '.');
        await this.refresh();
      } catch (error) {
        this.error = error.message || ('Could not configure ' + bot.name + '.');
      } finally { this.configuringBot = ''; }
    },
    async configureAll() {
      if (!window.confirm('Add FarmWatch configuration to every loaded ASF bot? Auto-claim will remain disabled for every bot. Affected bots must be restarted.')) return;
      this.error = ''; this.message = ''; this.configuringAll = true;
      try {
        const result = await this.$http.post('farmwatch/ConfigureAllBots');
        const failures = Array.isArray(result.failed) ? result.failed : [];
        this.message = (result.message || 'Bulk setup completed.') + ' Configured: ' + (result.configured || 0) + ', already configured: ' + (result.unchanged || 0) + ', failed: ' + failures.length + (failures.length ? ' — ' + failures.join('; ') : '');
        await this.refresh();
      } catch (error) {
        this.error = error.message || 'Could not configure all bots.';
      } finally { this.configuringAll = false; }
    },
    async refresh() {
      this.error = '';
      try {
        const [status, stats] = await Promise.all([
          this.$http.get('farmwatch/Status'),
          this.$http.get('farmwatch/Statistics', { limit: 8 }),
        ]);
        this.status = status || {};
        this.bots = status && Array.isArray(status.bots) ? status.bots : [];
        this.claims = stats && stats.claims ? stats.claims : {};
        this.farming = stats && stats.farming ? stats.farming : {};
        this.refreshedAt = new Date().toLocaleTimeString();
      } catch (error) {
        this.error = error.message || 'FarmWatch data could not be loaded.';
      }
    },
  },
};
</script>
