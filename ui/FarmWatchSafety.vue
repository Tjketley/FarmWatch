<template>
  <main class="main-container">
    <section class="fw-page">
      <div class="fw-eyebrow">FarmWatch / Protection</div>
      <h1 class="fw-title">Safety & controls</h1>
      <p class="fw-subtitle">Manage claim behaviour without changing ASF's underlying bot configuration.</p>

      <section class="fw-panel" style="margin-top:1.5rem">
        <h2 class="fw-panel-title">Global safeguards</h2>
        <div class="fw-setting">
          <div><strong>Observe-only mode</strong><p class="fw-muted">FarmWatch monitors eligibility and records activity without claiming packages.</p></div>
          <label class="fw-switch"><input type="checkbox" v-model="observeOnly" @change="saveGlobal"><span></span></label>
        </div>
        <div class="fw-setting">
          <div><strong>Global auto-claim</strong><p class="fw-muted">Allow eligible free-to-keep package claims. This does not include demos, free weekends or temporary access.</p></div>
          <label class="fw-switch"><input type="checkbox" v-model="autoClaimEnabled" @change="saveGlobal"><span></span></label>
        </div>
        <div class="fw-safety-note"><strong>Safety recommendation</strong><p>Keep observe-only enabled while validating bot configuration and eligibility. Auto-claim should only be enabled when you deliberately want FarmWatch to redeem eligible packages.</p></div>
      </section>

      <section class="fw-panel" style="margin-top:1rem">
        <div style="display:flex;justify-content:space-between;align-items:center;gap:1rem;flex-wrap:wrap">
          <div><h2 class="fw-panel-title">Per-bot controls</h2><p class="fw-muted" style="margin:0;font-size:.8rem">Individual bot switches are saved separately from global settings.</p></div>
          <button class="btn btn-default btn-sm" @click="refresh">Refresh</button>
        </div>
        <div v-if="bots.length">
          <div v-for="bot in bots" :key="bot.name" class="fw-setting">
            <div><strong>{{ bot.name }}</strong><p class="fw-muted">{{ bot.connected ? 'Connected' : 'Offline' }} · {{ bot.configured ? 'FarmWatch configured' : 'Not configured' }}</p></div>
            <label class="fw-switch"><input type="checkbox" :checked="!!bot.autoClaimEnabled" :disabled="savingBot===bot.name || !bot.configured" @change="saveBot(bot,$event.target.checked)"><span></span></label>
          </div>
        </div>
        <p v-else class="fw-muted">No bots detected.</p>
      </section>
      <p v-if="message" class="fw-status" style="margin-top:1rem">{{ message }}</p>
      <p v-if="error" class="fw-error" style="margin-top:1rem">{{ error }}</p>
    </section>
  </main>
</template>

<script>
export default {
  name: 'FarmWatchSafety',
  data() { return { observeOnly: true, autoClaimEnabled: false, bots: [], message: '', error: '', savingBot: '' }; },
  created() { this.refresh(); },
  methods: {
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
      this.message = ''; this.error = '';
      if (this.autoClaimEnabled && !this.observeOnly && !window.confirm('Enable global auto-claim with observe-only disabled? FarmWatch may redeem eligible free-to-keep packages.')) {
        this.autoClaimEnabled = false; return;
      }
      try {
        const result = await this.$http.post('farmwatch/Settings', { autoClaimEnabled: this.autoClaimEnabled, observeOnly: this.observeOnly });
        this.observeOnly = !!result.observeOnly;
        this.autoClaimEnabled = !!result.globalAutoClaimEnabled;
        this.message = 'Global safety settings saved.';
      } catch (error) { this.error = error.message || 'Could not save global settings.'; await this.refresh(); }
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
  },
};
</script>
