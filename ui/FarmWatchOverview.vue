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
          <h2 class="fw-panel-title">Bot operations</h2>
          <div v-if="bots.length">
            <div v-for="bot in bots" :key="bot.name" style="display:flex;justify-content:space-between;align-items:center;padding:.75rem 0;border-bottom:1px solid var(--fw-border)">
              <div><strong>{{ bot.name }}</strong><div class="fw-muted" style="font-size:.78rem">{{ bot.configured ? 'FarmWatch configured' : 'ASF bot' }}</div></div>
              <span class="fw-status" :style="{color:bot.connected?'var(--fw-accent)':'#8998a3'}">{{ bot.connected ? 'Connected' : 'Offline' }}</span>
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
      <p v-if="error" class="fw-muted" style="margin-top:1rem">{{ error }}</p>
      <p class="fw-muted" style="font-size:.75rem;margin-top:1rem">Last refreshed: {{ refreshedAt || 'Loading…' }} <button class="btn btn-default btn-sm" style="margin-left:.5rem" @click="refresh">Refresh</button></p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchOverview',
  data() {
    return { status: {}, bots: [], claims: {}, farming: {}, error: '', refreshedAt: '' };
  },
  computed: {
    botCount() { return this.bots.length; },
    connectedCount() { return this.bots.filter(bot => bot.connected).length; },
  },
  created() { this.refresh(); },
  methods: {
    number(value) { return Number(value || 0).toLocaleString(); },
    hours(seconds) { return (Number(seconds || 0) / 3600).toFixed(1); },
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
