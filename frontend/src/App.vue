<script setup>
import { onMounted, ref } from 'vue'

const apiStatus = ref('checking...')

onMounted(async () => {
  try {
    const response = await fetch('/api/health')
    const body = await response.json()
    apiStatus.value = body.status
  } catch {
    apiStatus.value = 'unreachable'
  }
})
</script>

<template>
  <h1>RateWatch</h1>
  <p>API status: {{ apiStatus }}</p>
</template>
