import type { Messages } from '../i18n';

export function Placeholder({ title, messages }: { title: string; messages: Messages }) {
  return <main className="content"><section className="card"><h1>{title}</h1><p>{messages.comingSoon}</p></section></main>;
}
