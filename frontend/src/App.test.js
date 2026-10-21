import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, expect, test, vi } from 'vitest'
import App from './App.vue'

afterEach(() => {
  vi.unstubAllGlobals()
})

test('loads USD rates on mount and renders them', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({
      baseCurrency: 'USD',
      timestamp: '2026-10-21T10:00:00+00:00',
      rates: [
        { currency: 'EUR', rate: 0.92 },
        { currency: 'GBP', rate: 0.79 },
      ],
    }),
  })
  vi.stubGlobal('fetch', fetchMock)

  const wrapper = mount(App)
  await flushPromises()

  expect(fetchMock).toHaveBeenCalledWith('/api/rates?base=USD')
  const rows = wrapper.findAll('tbody tr').map((row) => row.text())
  expect(rows).toEqual(['EUR0.92', 'GBP0.79'])
  expect(wrapper.text()).toContain('Last updated:')
})
