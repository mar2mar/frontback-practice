<script setup>
import { computed, onMounted, ref, watch } from 'vue'

const props = defineProps({
  // Supported currency codes, taken from the rates view.
  currencies: { type: Array, required: true },
})

const alerts = ref([])
const error = ref('')
const loading = ref(false)
const baseCurrency = ref('USD')
const quoteCurrency = ref('EUR')
const targetRate = ref('')

const canCreate = computed(
  () =>
    props.currencies.length >= 2 &&
    props.currencies.includes(baseCurrency.value) &&
    props.currencies.includes(quoteCurrency.value) &&
    baseCurrency.value !== quoteCurrency.value,
)

// Keep both selections valid options and different whenever the currency list changes.
watch(
  () => props.currencies,
  (currencies) => {
    if (!currencies.includes(baseCurrency.value)) {
      baseCurrency.value = currencies[0] ?? ''
    }
    if (!currencies.includes(quoteCurrency.value) || quoteCurrency.value === baseCurrency.value) {
      quoteCurrency.value = currencies.find((c) => c !== baseCurrency.value) ?? ''
    }
  },
  { immediate: true },
)

// Prefers the ProblemDetails "detail" returned by the backend for 4xx responses.
async function errorMessage(response) {
  try {
    const problem = await response.json()
    if (problem?.detail) {
      return problem.detail
    }
  } catch {
    // no JSON body
  }
  return `request failed with status ${response.status}`
}

async function loadAlerts() {
  try {
    const response = await fetch('/api/alerts')
    if (!response.ok) {
      throw new Error(`request failed with status ${response.status}`)
    }
    alerts.value = await response.json()
  } catch (e) {
    error.value = `Could not load alerts: ${e.message}`
  }
}

async function createAlert() {
  if (!canCreate.value) {
    return
  }
  loading.value = true
  error.value = ''
  try {
    const response = await fetch('/api/alerts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        baseCurrency: baseCurrency.value,
        quoteCurrency: quoteCurrency.value,
        targetRate: Number(targetRate.value),
      }),
    })
    if (!response.ok) {
      error.value = `Could not create alert: ${await errorMessage(response)}`
      return
    }
    targetRate.value = ''
    await loadAlerts()
  } catch (e) {
    error.value = `Could not create alert: ${e.message}`
  } finally {
    loading.value = false
  }
}

async function deleteAlert(id) {
  loading.value = true
  error.value = ''
  try {
    const response = await fetch(`/api/alerts/${id}`, { method: 'DELETE' })
    if (response.status === 404) {
      error.value = 'That alert no longer exists.'
    } else if (!response.ok) {
      error.value = `Could not delete alert: ${await errorMessage(response)}`
      return
    }
    await loadAlerts()
  } catch (e) {
    error.value = `Could not delete alert: ${e.message}`
  } finally {
    loading.value = false
  }
}

onMounted(loadAlerts)
</script>

<template>
  <section>
    <h2>Alerts</h2>

    <form class="controls" @submit.prevent="createAlert">
      <label>
        Base
        <select v-model="baseCurrency" name="baseCurrency" :disabled="loading">
          <option v-for="currency in currencies" :key="currency" :value="currency">{{ currency }}</option>
        </select>
      </label>
      <label>
        Quote
        <select v-model="quoteCurrency" name="quoteCurrency" :disabled="loading">
          <option v-for="currency in currencies" :key="currency" :value="currency">{{ currency }}</option>
        </select>
      </label>
      <label>
        Alert above
        <input v-model="targetRate" name="targetRate" type="number" step="0.000001" min="0.000001" required :disabled="loading" />
      </label>
      <button type="submit" :disabled="loading || !canCreate">Add alert</button>
    </form>
    <p v-if="currencies.length < 2" class="hint">Alerts can be created once exchange rates have loaded.</p>
    <p v-else-if="baseCurrency === quoteCurrency" class="hint">Choose two different currencies.</p>

    <p v-if="error" role="alert" class="error">{{ error }}</p>

    <p v-if="alerts.length === 0">No alerts yet.</p>
    <table v-else>
      <thead>
        <tr>
          <th>Pair</th>
          <th>Target</th>
          <th>Created</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="alert in alerts" :key="alert.id">
          <td>{{ alert.baseCurrency }}/{{ alert.quoteCurrency }}</td>
          <td class="rate">{{ alert.targetRate }}</td>
          <td>{{ new Date(alert.createdAt).toLocaleString() }}</td>
          <td>
            {{ alert.triggeredAt ? `Triggered ${new Date(alert.triggeredAt).toLocaleString()}` : 'Active' }}
          </td>
          <td>
            <button type="button" :disabled="loading" @click="deleteAlert(alert.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<!-- Same look as the rates view in App.vue (scoped styles don't reach child components). -->
<style scoped>
section {
  margin-top: 2rem;
}

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
