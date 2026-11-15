<template>
  <main class="main-container">
    <section class="fw-page">
      <div class="fw-eyebrow">FarmWatch / Intelligence</div>
      <h1 class="fw-title">Analytics</h1>
      <p class="fw-subtitle">A clear view of claims, farming activity and trade history.</p>

      <div class="fw-grid">
        <article class="fw-metric"><div class="fw-metric-label">Total claims</div><div class="fw-metric-value">{{ fmt(claims.total) }}</div><div class="fw-muted">Lifetime recorded</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Free-to-keep</div><div class="fw-metric-value">{{ fmt(claims.freeToKeepPackages) }}</div><div class="fw-muted">Package claims</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Farming time</div><div class="fw-metric-value">{{ duration(farming.lifetimeSeconds) }}</div><div class="fw-muted">{{ fmt(farming.totalSessions) }} sessions</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Completed trades</div><div class="fw-metric-value">{{ fmt(trades.completedTrades) }}</div><div class="fw-muted">Live callback history</div></article>
      </div>

      <section class="fw-panel" style="margin-bottom:1rem">
        <div style="display:flex;justify-content:space-between;gap:1rem;align-items:flex-start;flex-wrap:wrap">
          <div><h2 class="fw-panel-title">30-day activity</h2><p class="fw-muted" style="margin:0;font-size:.8rem">Daily claims, completed trades and farming time</p></div>
          <button class="btn btn-default btn-sm" @click="refresh">Refresh data</button>
        </div>
        <div class="fw-chart">
          <div v-for="day in activity" :key="day.date" class="fw-chart-day">
            <div class="fw-chart-bars">
              <span class="fw-bar fw-bar--claims" :style="{height:barHeight(day.claims, maxClaims)}" :title="day.claims + ' claims'"></span>
              <span class="fw-bar fw-bar--trades" :style="{height:barHeight(day.trades, maxTrades)}" :title="day.trades + ' trades'"></span>
              <span class="fw-bar fw-bar--farming" :style="{height:barHeight(day.farmingSeconds / 3600, maxFarming)}" :title="duration(day.farmingSeconds) + ' farming'"></span>
            </div>
            <span class="fw-chart-label">{{ labelDate(day.date) }}</span>
          </div>
        </div>
        <div class="fw-legend"><span><i class="fw-dot fw-dot--claims"></i>Claims</span><span><i class="fw-dot fw-dot--trades"></i>Trades</span><span><i class="fw-dot fw-dot--farming"></i>Farming hours</span></div>
      </section>

      <div class="fw-grid" style="grid-template-columns:repeat(3,minmax(0,1fr))">
        <section class="fw-panel"><h2 class="fw-panel-title">Claims</h2><div class="fw-row"><span>Today</span><strong>{{ fmt(claims.today) }}</strong></div><div class="fw-row"><span>This month</span><strong>{{ fmt(claims.thisMonth) }}</strong></div><div class="fw-row"><span>Free DLC apps</span><strong>{{ fmt(claims.freeDlcApps) }}</strong></div></section>
        <section class="fw-panel"><h2 class="fw-panel-title">Farming</h2><div class="fw-row"><span>Active sessions</span><strong>{{ fmt(farming.activeSessions) }}</strong></div><div class="fw-row"><span>Today</span><strong>{{ duration(farming.todaySeconds) }}</strong></div><div class="fw-row"><span>This month</span><strong>{{ duration(farming.thisMonthSeconds) }}</strong></div><div class="fw-row"><span>Unique games</span><strong>{{ fmt(farming.uniqueGames) }}</strong></div></section>
        <section class="fw-panel"><h2 class="fw-panel-title">Trades</h2><div class="fw-row"><span>Today</span><strong>{{ fmt(trades.today) }}</strong></div><div class="fw-row"><span>This month</span><strong>{{ fmt(trades.thisMonth) }}</strong></div><div class="fw-row"><span>Items sent</span><strong>{{ fmt(trades.itemsSent) }}</strong></div><div class="fw-row"><span>Items received</span><strong>{{ fmt(trades.itemsReceived) }}</strong></div></section>
      </div>

      <section class="fw-panel" style="margin-top:1rem">
        <h2 class="fw-panel-title">Recent claims</h2>
        <div v-if="claims.recent && claims.recent.length" class="fw-table-wrap">
          <table class="fw-table"><thead><tr><th>Type</th><th>Target ID</th><th>Bot</th><th>Claimed</th></tr></thead>
            <tbody><tr v-for="item in claims.recent" :key="item.eventId"><td>{{ item.targetType === 'Sub' ? 'Free-to-keep' : 'App / DLC' }}</td><td>{{ item.targetId }}</td><td>{{ item.bot }}</td><td>{{ dateTime(item.claimedAtUtc) }}</td></tr></tbody>
          </table>
        </div>
        <p v-else class="fw-muted">No claim history recorded yet.</p>
      </section>
      <p v-if="error" class="fw-muted" style="margin-top:1rem">{{ error }}</p>
      <p class="fw-muted" style="font-size:.75rem;margin-top:1rem">Data refreshed: {{ refreshedAt || 'Loading…' }}. Trade history reflects events captured by live callbacks.</p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchAnalytics',
  data() { return { stats: {}, error: '', refreshedAt: '' }; },
  computed: {
    claims() { return this.stats.claims || {}; },
    farming() { return this.stats.farming || {}; },
    trades() { return this.stats.trades || {}; },
    activity() { return this.stats.dailyActivity || []; },
    maxClaims() { return Math.max(1, ...this.activity.map(x => Number(x.claims || 0))); },
    maxTrades() { return Math.max(1, ...this.activity.map(x => Number(x.trades || 0))); },
    maxFarming() { return Math.max(1, ...this.activity.map(x => Number(x.farmingSeconds || 0) / 3600)); },
  },
  created() { this.refresh(); },
  methods: {
    fmt(value) { return Number(value || 0).toLocaleString(); },
    duration(seconds) { const n = Math.max(0, Math.floor(Number(seconds || 0) / 3600)); const m = Math.floor((Number(seconds || 0) % 3600) / 60); return n + 'h ' + m + 'm'; },
    barHeight(value, max) { return Math.max(2, Math.round((Number(value || 0) / max) * 100)) + '%'; },
    labelDate(value) { return value ? value.slice(8, 10) : ''; },
    dateTime(value) { return value ? new Date(value).toLocaleString() : '—'; },
    async refresh() { this.error = ''; try { this.stats = await this.$http.get('farmwatch/Statistics', { limit: 20 }) || {}; this.refreshedAt = new Date().toLocaleTimeString(); } catch (error) { this.error = error.message || 'Analytics could not be loaded.'; } },
  },
};
</script>
