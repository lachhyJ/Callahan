self.addEventListener('push', (event) => {
  let data = { title: 'Callahan', body: 'Rest is over' }
  try {
    if (event.data) data = event.data.json()
  } catch {
    // fall back to default
  }

  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: '/icon-192.png',
      badge: '/icon-192.png',
      // Vibration is a separate iOS toggle ("Vibrate on Silent") from the
      // mute switch's sound-muting, so this may still fire audibly-felt
      // even when the ringer is off — unlike the notification sound itself.
      vibrate: [200, 100, 200],
    })
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  event.waitUntil(
    self.clients.matchAll({ type: 'window' }).then((clientList) => {
      for (const client of clientList) {
        if ('focus' in client) return client.focus()
      }
      if (self.clients.openWindow) return self.clients.openWindow('/')
      return undefined
    })
  )
})
