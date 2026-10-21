import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, expect, test, vi } from 'vitest'
import AlertsPanel from './AlertsPanel.vue'

const currencies = ['EUR', 'GBP', 'USD']

const activeAlert = {
  id: 7,
  baseCurrency: 'USD',
  quoteCurrency: 'EUR',
  targetRate: 0.95,
  createdAt: '2026-10-21T10:00:00+00:00',
  triggeredAt: null,
}
const triggeredAlert = {
  id: 3,
  baseCurrency: 'USD',
  quoteCurrency: 'GBP',
  targetRate: 0.7,
  createdAt: '2026-10-21T09:00:00+00:00',
  triggeredAt: '2026-10-21T09:30:00+00:00',
}

// fetch responses are returned in call order.
function stubFetch(...responses) {
  const fetchMock = vi.fn()
  for (const { status = 200, body } of responses) {
    fetchMock.mockResolvedValueOnce({ ok: status >= 200 && status < 300, status, json: async () => body })
  }
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function mountPanel() {
  const wrapper = mount(AlertsPanel, { props: { currencies } })
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

test('loads alerts on mount and shows pair, target and status', async () => {
  const fetchMock = stubFetch({ body: [activeAlert, triggeredAlert] })

  const wrapper = await mountPanel()

  expect(fetchMock).toHaveBeenCalledWith('/api/alerts')
  const rows = wrapper.findAll('tbody tr')
  expect(rows).toHaveLength(2)
  expect(rows[0].text()).toContain('USD/EUR')
  expect(rows[0].text()).toContain('0.95')
  expect(rows[0].text()).toContain('Active')
  expect(rows[1].text()).toContain('USD/GBP')
  expect(rows[1].text()).toContain('Triggered')
})

test('disables creation until two currencies are available, then selects two different ones', async () => {
  stubFetch({ body: [] })
  const wrapper = mount(AlertsPanel, { props: { currencies: ['USD'] } })
  await flushPromises()

  expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
  expect(wrapper.text()).toContain('Alerts can be created once exchange rates have loaded.')

  await wrapper.setProps({ currencies: ['CAD', 'EUR', 'USD'] })

  const base = wrapper.find('select[name="baseCurrency"]').element.value
  const quote = wrapper.find('select[name="quoteCurrency"]').element.value
  expect(base).toBe('USD')
  expect(['CAD', 'EUR']).toContain(quote)
  expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
  expect(wrapper.text()).not.toContain('once exchange rates have loaded')
})

test('creates an alert and reloads the list', async () => {
  const fetchMock = stubFetch({ body: [] }, { status: 201, body: activeAlert }, { body: [activeAlert] })
  const wrapper = await mountPanel()

  await wrapper.find('select[name="baseCurrency"]').setValue('USD')
  await wrapper.find('select[name="quoteCurrency"]').setValue('EUR')
  await wrapper.find('input[name="targetRate"]').setValue('0.95')
  await wrapper.find('form').trigger('submit')
  await flushPromises()

  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/alerts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ baseCurrency: 'USD', quoteCurrency: 'EUR', targetRate: 0.95 }),
  })
  expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/alerts')
  expect(wrapper.findAll('tbody tr')).toHaveLength(1)
})

test('shows the backend ProblemDetails message when creation is rejected', async () => {
  const fetchMock = stubFetch(
    { body: [] },
    { status: 400, body: { title: 'Bad Request', detail: "Unsupported quote currency 'XYZ'." } },
  )
  const wrapper = await mountPanel()

  await wrapper.find('input[name="targetRate"]').setValue('0.95')
  await wrapper.find('form').trigger('submit')
  await flushPromises()

  expect(wrapper.find('[role="alert"]').text()).toContain("Unsupported quote currency 'XYZ'.")
  expect(fetchMock).toHaveBeenCalledTimes(2) // no reload after a failed create
})

function evaluateButton(wrapper) {
  return wrapper.findAll('button').find((button) => button.text() === 'Evaluate alerts')
}

test('evaluates alerts, shows the triggered count, reloads the list, and clears the message on a later change', async () => {
  const nowTriggered = { ...activeAlert, triggeredAt: '2026-10-21T11:00:00+00:00' }
  const fetchMock = stubFetch(
    { body: [activeAlert] },
    { body: [nowTriggered] },
    { body: [nowTriggered] },
    { status: 204 },
    { body: [] },
  )
  const wrapper = await mountPanel()

  await evaluateButton(wrapper).trigger('click')
  await flushPromises()

  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/alerts/evaluate', { method: 'POST' })
  expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/alerts')
  expect(wrapper.find('[role="status"]').text()).toBe('1 alert triggered')
  expect(wrapper.find('tbody tr').text()).toContain('Triggered')

  await wrapper.find('tbody tr button').trigger('click') // delete
  await flushPromises()

  expect(wrapper.find('[role="status"]').exists()).toBe(false)
})

test('shows a message when evaluation triggers nothing', async () => {
  stubFetch({ body: [activeAlert] }, { body: [] }, { body: [activeAlert] })
  const wrapper = await mountPanel()

  await evaluateButton(wrapper).trigger('click')
  await flushPromises()

  expect(wrapper.find('[role="status"]').text()).toBe('No alerts triggered')
})

test('deletes an alert and reloads the list', async () => {
  const fetchMock = stubFetch({ body: [activeAlert] }, { status: 204 }, { body: [] })
  const wrapper = await mountPanel()

  await wrapper.find('tbody tr button').trigger('click')
  await flushPromises()

  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/alerts/7', { method: 'DELETE' })
  expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/alerts')
  expect(wrapper.findAll('tbody tr')).toHaveLength(0)
  expect(wrapper.text()).toContain('No alerts yet.')
})
