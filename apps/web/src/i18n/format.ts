import type { Locale } from './index';
import type { Messages } from './en';

type CountedKey = 'fileCount' | 'photoCount';
type PluralCategory = 'one' | 'few' | 'many' | 'other';

// Replaces {name} placeholders in a translated template.
export function interpolate(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in values ? String(values[name]) : match));
}

// A translated count with the locale's plural form, for example "1 file" / "5 plików".
export function countLabel(locale: Locale, messages: Messages, key: CountedKey, count: number): string {
  const category = new Intl.PluralRules(locale).select(count);
  const form: PluralCategory = category === 'one' || category === 'few' || category === 'many' ? category : 'other';
  return interpolate(messages[`${key}_${form}`], { count });
}

// A calendar date (yyyy-MM-dd) shown as-is, independent of the device's time zone.
export function formatEventDate(locale: Locale, isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  if (!year || !month || !day) return isoDate;
  const date = new Date(0);
  // setUTCFullYear keeps years 1–99 literal, unlike Date.UTC.
  date.setUTCFullYear(year, month - 1, day);
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeZone: 'UTC' }).format(date);
}

// A server timestamp shown in the device's local time.
export function formatTimestamp(locale: Locale, isoTimestamp: string): string {
  const date = new Date(isoTimestamp);
  if (Number.isNaN(date.getTime())) return isoTimestamp;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeStyle: 'short' }).format(date);
}

// Today on this device, as yyyy-MM-dd.
export function localToday(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// The UUID query parameter of the current page, or null when it is missing or malformed.
export function uuidQueryParameter(name: string): string | null {
  const value = new URLSearchParams(window.location.search).get(name);
  return value && uuidPattern.test(value) ? value : null;
}
