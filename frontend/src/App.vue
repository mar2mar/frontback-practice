<script setup>
import { onMounted, ref } from 'vue'

const baseCurrency = ref('USD')
const currencies = ref(['USD'])
const snapshot = ref(null)
const error = ref('')
const loading = ref(false)

async function loadRates() {
  loading.value = true
  error.value = ''
  try {
    const response = await fetch(`/api/rates?base=${encodeURIComponent(baseCurrency.value)}`)
    if (!response.ok) {
      throw new Error(`request failed with status ${response.status}`)
    }
    snapshot.value = await response.json()
    currencies.value = [snapshot.value.baseCurrency, ...snapshot.value.rates.map((r) => r.currency)].sort()
  } catch (e) {
    error.value = `Could not load rates: ${e.message}`
  } finally {
    loading.value = false
  }
}

onMounted(loadRates)
</script>

<template>
  <h1>RateWatch</h1>

  <div class="controls">
    <label>
      Base currency
      <select v-model="baseCurrency" :disabled="loading" @change="loadRates">
        <option v-for="currency in currencies" :key="currency" :value="currency">{{ currency }}</option>
      </select>
    </label>
    <button :disabled="loading" @click="loadRates">Refresh</button>
  </div>

  <p v-if="error" role="alert" class="error">{{ error }}</p>

  <template v-if="snapshot">
    <table>
      <thead>
        <tr>
          <th>Currency</th>
          <th>1 {{ snapshot.baseCurrency }} =</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="rate in snapshot.rates" :key="rate.currency">
          <td>{{ rate.currency }}</td>
          <td class="rate">{{ rate.rate }}</td>
        </tr>
      </tbody>
    </table>
    <p>Last updated: {{ new Date(snapshot.timestamp).toLocaleString() }}</p>
  </template>
</template>

<style scoped>
.controls {
  display: flex;
  gap: 1rem;
  align-items: center;
}

table {
  margin-top: 1rem;
  border-collapse: collapse;
}

th,
td {
  padding: 0.25rem 1rem;
  border-bottom: 1px solid #ccc;
  text-align: left;
}

.rate {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.error {
  color: #b00020;
}
</style>
