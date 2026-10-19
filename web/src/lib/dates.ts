const saDateTime = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Africa/Johannesburg',
})
const saDate = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeZone: 'Africa/Johannesburg',
})
const calendarDate = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeZone: 'UTC',
})

// A UTC instant ("...Z") shown in Johannesburg time, date and time.
export const formatInstant = (iso: string) => saDateTime.format(new Date(iso))

// A UTC instant shown as a Johannesburg date only.
export const formatInstantDate = (iso: string) => saDate.format(new Date(iso))

// A calendar date like "2026-10-31", with no timezone shift.
export const formatCalendarDate = (day: string) =>
  calendarDate.format(new Date(`${day}T00:00:00Z`))
