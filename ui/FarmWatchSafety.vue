<template>
  <main class="main-container">
    <section class="fw-page">
      <div class="fw-eyebrow">FarmWatch / Protection</div>
      <h1 class="fw-title">Safety & controls</h1>
      <p class="fw-subtitle">Manage claim behaviour and optional outbound statistics sharing.</p>

      <section class="fw-panel" style="margin-top:1.5rem">
        <h2 class="fw-panel-title">Global safeguards</h2>
        <div class="fw-setting">
          <div><strong>Observe-only mode</strong><p class="fw-muted">Monitor eligibility and record activity without claiming packages.</p></div>
          <label class="fw-switch"><input type="checkbox" v-model="observeOnly" :disabled="savingGlobal" @change="saveGlobal"><span></span></label>
        </div>
        <div class="fw-setting">
          <div><strong>Global auto-claim</strong><p class="fw-muted">Master switch for eligible Free-to-Keep package claims. Demos, free weekends and temporary access remain excluded.</p></div>
          <label class="fw-switch"><input type="checkbox" v-model="autoClaimEnabled" :disabled="savingGlobal" @change="saveGlobal"><span></span></label>
        </div>
        <div class="fw-safety-note"><strong>Safety recommendation</strong><p>Keep observe-only enabled while validating bot configuration and eligibility. Auto-claim is off by default and requires deliberate activation at the global and bot levels.</p></div>
      </section>

      <section class="fw-panel" style="margin-top:1rem">
        <div style="display:flex;justify-content:space-between;align-items:center;gap:1rem;flex-wrap:wrap">
          <div><h2 class="fw-panel-title">Per-bot controls</h2><p class="fw-muted" style="margin:0;font-size:.8rem">Each bot has an independent auto-claim switch.</p></div>
          <button class="btn btn-default btn-sm" @click="refresh">Refresh</button>
        </div>
        <div v-if="bots.length">
          <div v-for="bot in bots" :key="bot.name" class="fw-setting">
            <div><strong>{{ bot.name }}</strong><p class="fw-muted">{{ bot.connected ? 'Connected' : 'Offline' }} · {{ bot.configured ? 'FarmWatch configured' : 'Configure FarmWatch from Overview first' }}</p></div>
            <label class="fw-switch"><input type="checkbox" :checked="!!bot.autoClaimEnabled" :disabled="savingBot===bot.name || !bot.configured" @change="saveBot(bot,$event.target.checked)"><span></span></label>
          </div>
        </div>
        <p v-else class="fw-muted">No ASF bots detected.</p>
      </section>

      <section class="fw-panel" style="margin-top:1rem">
        <div class="fw-eyebrow">Optional integration</div>
        <h2 class="fw-panel-title" style="margin-top:.35rem">WatchFarm Connect</h2>
        <p class="fw-muted">Send aggregate FarmWatch statistics to your WordPress WatchFarm dashboard. This is outbound-only, uses HTTPS and does not enable claiming.</p>
        <div class="fw-row"><span>Connection</span><strong>{{ connectStatus.enabled ? 'Enabled' : 'Disabled' }}</strong></div>
        <div class="fw-row"><span>Pending uploads</span><strong>{{ number(connectStatus.pendingUploads) }}</strong></div>
        <div class="fw-row"><span>Last successful upload</span><strong>{{ connectStatus.lastSuccessUtc ? new Date(connectStatus.lastSuccessUtc).toLocaleString() : 'Never' }}</strong></div>
        <div class="fw-row"><span>Status</span><strong>{{ connectStatus.status || 'Unknown' }}</strong></div>
        <label class="fw-field-label">WordPress upload endpoint</label>
        <input class="form-control" v-model.trim="connectEndpoint" type="url" placeholder="https://your-site.example/wp-json/watchfarm-connect/v1/upload" autocomplete="url">
        <label class="fw-field-label" style="margin-top:.8rem">Bearer token</label>
        <input class="form-control" v-model.trim="connectToken" type="password" placeholder="Paste token from WatchFarm admin" autocomplete="new-password">
        <p class="fw-muted" style="font-size:.75rem">The token is write-only here and is never displayed by the status endpoint. Use a 32–128 character token from your WordPress WatchFarm settings.</p>
        <div style="display:flex;gap:.6rem;flex-wrap:wrap;margin-top:1rem">
          <button class="btn btn-primary btn-sm" :disabled="savingConnect" @click="saveConnect(true)">{{ savingConnect ? 'Saving…' : 'Enable Connect' }}</button>
          <button class="btn btn-default btn-sm" :disabled="savingConnect || !connectStatus.enabled" @click="saveConnect(false)">Disable Connect</button>
          <button class="btn btn-default btn-sm" :disabled="savingConnect" @click="loadConnect">Refresh status</button>
        </div>
      </section>

      <p v-if="message" class="fw-status" style="margin-top:1rem">{{ message }}</p>
      <p v-if="error" class="fw-error" style="margin-top:1rem">{{ error }}</p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchSafety',
  data() { return { observeOnly: true, autoClaimEnabled: false, bots: [], message: '', error: '', savingBot: '', savingGlobal: false, connectStatus: {}, connectEndpoint: '', connectToken: '', savingConnect: false }; },
  created() { this.refresh(); this.loadConnect(); },
  methods: {
    number(value) { return Number(value || 0).toLocaleString(); },
    async refresh() {
      this.error = '';
      try {
        const result = await this.$http.get('farmwatch/Status');
        this.observeOnly = !!result.observeOnly;
        this.autoClaimEnabled = !!result.globalAutoClaimEnabled;
        this.bots = Array.isArray(result.bots) ? result.bots : [];
      } catch (error) { this.error = error.message || 'Could not load FarmWatch settings.'; }
    },
    async saveGlobal() {
      this.message = ''; this.error = ''; this.savingGlobal = true;
      if (this.autoClaimEnabled && !this.observeOnly && !window.confirm('Enable global auto-claim with observe-only disabled? FarmWatch may redeem verified eligible Free-to-Keep packages.')) {
        await this.refresh(); this.savingGlobal = false; return;
      }
      try {
        const result = await this.$http.post('farmwatch/Settings', { autoClaimEnabled: this.autoClaimEnabled, observeOnly: this.observeOnly });
        this.observeOnly = !!result.observeOnly;
        this.autoClaimEnabled = !!result.globalAutoClaimEnabled;
        this.message = 'Global safety settings saved.';
      } catch (error) { this.error = error.message || 'Could not save global settings.'; await this.refresh(); }
      finally { this.savingGlobal = false; }
    },
    async saveBot(bot, enabled) {
      this.savingBot = bot.name; this.message = ''; this.error = '';
      if (enabled && !this.observeOnly && !window.confirm('Enable auto-claim for ' + bot.name + '?')) { this.savingBot = ''; await this.refresh(); return; }
      try {
        await this.$http.post('farmwatch/Settings', { botName: bot.name, botAutoClaimEnabled: enabled });
        this.message = 'Saved settings for ' + bot.name + '.';
        await this.refresh();
      } catch (error) { this.error = error.message || 'Could not save bot settings.'; await this.refresh(); }
      finally { this.savingBot = ''; }
    },
    async loadConnect() {
      try { this.connectStatus = await this.$http.get('farmwatch/ConnectStatus') || {}; }
      catch (error) { this.error = error.message || 'Could not load WatchFarm Connect status.'; }
    },
    async saveConnect(enabled) {
      this.savingConnect = true; this.error = ''; this.message = '';
      try {
        const result = await this.$http.post('farmwatch/Connect', { enabled, endpoint: this.connectEndpoint, token: this.connectToken });
        this.connectStatus = result.status || {};
        if (enabled) this.connectToken = '';
        this.message = result.message || (enabled ? 'WatchFarm Connect enabled.' : 'WatchFarm Connect disabled.');
        await this.loadConnect();
      } catch (error) { this.error = error.message || 'Could not update WatchFarm Connect.'; }
      finally { this.savingConnect = false; }
    },
  },
};
</script>
