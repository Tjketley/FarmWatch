<template>
  <main class="main-container">
    <section class="fw-page">
      <div class="fw-eyebrow">FarmWatch / System health</div>
      <h1 class="fw-title">Diagnostics</h1>
      <p class="fw-subtitle">Inspect plugin health, bot cache state and the persistent FarmWatch event timeline.</p>
      <div class="fw-grid" style="grid-template-columns:repeat(3,minmax(0,1fr))">
        <article class="fw-metric"><div class="fw-metric-label">Managed handlers</div><div class="fw-metric-value">{{ summary.handlerCount || 0 }}</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Retained events</div><div class="fw-metric-value">{{ events.retainedEvents || 0 }}</div></article>
        <article class="fw-metric"><div class="fw-metric-label">Event storage</div><div class="fw-metric-value" style="font-size:1rem">{{ events.persistent ? 'Persistent' : 'Session only' }}</div></article>
      </div>
      <section class="fw-panel">
        <div style="display:flex;justify-content:space-between;align-items:center;gap:1rem;flex-wrap:wrap">
          <h2 class="fw-panel-title">Event timeline</h2>
          <div style="display:flex;gap:.5rem;align-items:center">
            <select class="form-control" v-model="level" style="max-width:150px"><option value="">All levels</option><option value="info">Info</option><option value="success">Success</option><option value="warning">Warning</option><option value="error">Error</option></select>
            <button class="btn btn-default btn-sm" :disabled="loading" @click="refresh">{{ loading ? 'Loading…' : 'Refresh' }}</button>
            <button class="btn btn-default btn-sm" @click="copyReport">Copy report</button>
            <label class="fw-muted" style="font-size:.75rem;display:flex;align-items:center;gap:.35rem"><input type="checkbox" v-model="autoRefresh" @change="setAutoRefresh"> Auto-refresh</label>
          </div>
        </div>
        <p class="fw-muted" style="font-size:.75rem">Showing up to 200 most recent events · {{ events.storageFile || 'Storage path unavailable' }}</p>
        <div v-if="filteredEvents.length">
          <div v-for="(event,index) in filteredEvents" :key="event.id || index" class="fw-row" style="align-items:flex-start">
            <div style="min-width:0">
              <strong>{{ event.message || event.event || 'FarmWatch event' }}</strong>
              <div class="fw-muted" style="font-size:.75rem">{{ event.category || 'system' }}<span v-if="event.bot"> · {{ event.bot }}</span><span v-if="event.details"> · {{ event.details }}</span></div>
            </div>
            <div style="text-align:right;flex:0 0 auto"><span class="fw-status">{{ event.level || 'info' }}</span><div class="fw-muted" style="font-size:.7rem">{{ event.timestampUtc ? new Date(event.timestampUtc).toLocaleString() : '' }}</div></div>
          </div>
        </div>
        <p v-else class="fw-muted">No events match this filter.</p>
      </section>
      <section class="fw-panel" style="margin-top:1rem">
        <h2 class="fw-panel-title">Live diagnostics</h2>
        <p class="fw-muted" style="font-size:.78rem">Cache and queue snapshots for each configured FarmWatch bot.</p>
        <div v-if="diagnostics.bots && diagnostics.bots.length">
          <div v-for="bot in diagnostics.bots" :key="bot.name" style="padding:1rem 0;border-bottom:1px solid var(--fw-border)">
            <div style="display:flex;justify-content:space-between;gap:1rem"><strong>{{ bot.name }}</strong><span class="fw-muted">{{ bot.connected ? 'Connected' : 'Offline' }}</span></div>
            <div class="fw-grid" style="grid-template-columns:repeat(4,minmax(0,1fr));margin:.7rem 0 0">
              <div class="fw-metric"><div class="fw-metric-label">Queued</div><div class="fw-metric-value">{{ bot.cache.queuedPackages }}</div></div>
              <div class="fw-metric"><div class="fw-metric-label">Changed apps</div><div class="fw-metric-value">{{ bot.cache.changedApps }}</div></div>
              <div class="fw-metric"><div class="fw-metric-label">Changed packages</div><div class="fw-metric-value">{{ bot.cache.changedPackages }}</div></div>
              <div class="fw-metric"><div class="fw-metric-label">Activations</div><div class="fw-metric-value">{{ bot.cache.activationsInCache }}</div></div>
            </div>
            <p class="fw-muted" style="font-size:.75rem;margin:.5rem 0 0">Queue: {{ bot.queueStatus || 'Unknown' }} · Filter ready: {{ bot.filterReady ? 'Yes' : 'No' }} · Bot enabled: {{ bot.botEnabled ? 'Yes' : 'No' }}</p>
            <div class="fw-muted" style="font-size:.75rem;margin-top:.65rem">
              <div>Pending app IDs: {{ (bot.pendingChanges.appIds || []).join(', ') || 'None' }}</div>
              <div>Pending package IDs: {{ (bot.pendingChanges.packageIds || []).join(', ') || 'None' }}</div>
            </div>
            <div v-if="bot.queue && bot.queue.length" style="margin-top:.6rem">
              <div class="fw-muted" style="font-size:.7rem;font-weight:700;margin-bottom:.25rem">Package queue (first {{ bot.queue.length }})</div>
              <div v-for="(item,index) in bot.queue" :key="item.type + '-' + item.id + '-' + index" class="fw-row" style="font-size:.75rem">
                <span>{{ item.type }} · {{ item.id }}</span>
                <span>{{ item.filterHashPresent ? 'Filter hash ready' : 'No filter hash' }} · {{ item.hasCurrentClaimEvidence ? 'Evidence current' : 'Evidence pending' }}</span>
              </div>
            </div>
          </div>
        </div>
        <p v-else class="fw-muted">No configured FarmWatch handlers are available.</p>
      </section>
      <p v-if="error" class="fw-error" style="margin-top:1rem">{{ error }}</p>
      <p v-if="message" class="fw-status" style="margin-top:1rem">{{ message }}</p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchDiagnostics',
  data() { return { diagnostics: {}, events: {}, level: '', error: '', message: '', loading: false, autoRefresh: false, autoTimer: null }; },
  computed: {
    summary() { return this.diagnostics.global || {}; },
    filteredEvents() { return (this.events.events || []).filter(event => !this.level || String(event.level || 'info').toLowerCase() === this.level); },
  },
  created() { this.refresh(); },
  beforeDestroy() { if (this.autoTimer) clearInterval(this.autoTimer); },
  methods: {
    setAutoRefresh() {
      if (this.autoTimer) { clearInterval(this.autoTimer); this.autoTimer = null; }
      if (this.autoRefresh) this.autoTimer = setInterval(() => this.refresh(), 15000);
    },
    async refresh() {
      this.loading = true; this.error = ''; this.message = '';
      try {
        const [diagnostics, events] = await Promise.all([
          this.$http.get('farmwatch/Diagnostics'),
          this.$http.get('farmwatch/Events', { limit: 200 }),
        ]);
        this.diagnostics = diagnostics || {};
        this.events = events || {};
      } catch (error) { this.error = error.message || 'Diagnostics could not be loaded.'; }
      finally { this.loading = false; }
    },
    async copyReport() {
      try {
        await navigator.clipboard.writeText(JSON.stringify({ diagnostics: this.diagnostics, events: this.events }, null, 2));
        this.message = 'Diagnostic report copied.';
      } catch (error) { this.error = error.message || 'Clipboard access is unavailable.'; }
    },
  },
};
</script>
