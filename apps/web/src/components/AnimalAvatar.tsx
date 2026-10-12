import type { JSX } from 'react';
import type { Locale } from '../i18n';
import type { Animal } from '../lib/api';

type AvatarTone = 'peach' | 'mint' | 'lavender';
const tones: readonly AvatarTone[] = ['peach', 'mint', 'lavender'];

// A stable colour for an animal, derived from its id only, so it does not change with the list order or when another
// animal's activity changes.
export function avatarTone(id: string): AvatarTone {
  let hash = 0;
  for (let index = 0; index < id.length; index++) hash = (hash * 31 + id.charCodeAt(index)) >>> 0;
  return tones[hash % tones.length];
}

// The first user-perceived character of the name, uppercased for the locale.
function initial(name: string, locale: Locale): string {
  const trimmed = name.trim();
  const first = typeof Intl.Segmenter === 'function'
    ? new Intl.Segmenter(locale, { granularity: 'grapheme' }).segment(trimmed)[Symbol.iterator]().next().value?.segment
    : Array.from(trimmed)[0];
  return (first ?? '').toLocaleUpperCase(locale);
}

// The initial-letter tile used in list rows, desktop cards and the profile hero. It is decorative: the surrounding
// link or heading already names the animal. Inactive animals always use the neutral tile.
export function AnimalAvatar({ animal, locale, size }: {
  animal: Pick<Animal, 'id' | 'name' | 'isActive'>;
  locale: Locale;
  size: 'row' | 'card' | 'hero';
}): JSX.Element {
  const tone = animal.isActive ? `avatar-${avatarTone(animal.id)}` : 'avatar-inactive';
  return <span className={`animal-avatar animal-avatar-${size} ${tone}`} aria-hidden="true">{initial(animal.name, locale)}</span>;
}
