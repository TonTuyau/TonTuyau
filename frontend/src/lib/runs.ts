// What the Tasks page shows of a run (1.4), from what the API gave: never a count it didn't send.
import type { Run } from './api/types'
import { formatNumber } from './format'
import { i18n, t } from './i18n/i18n.svelte'

/** How long it ran, or has been running (in seconds), at `now`. */
export function runSeconds(run: Run, now: number): number {
  const end = run.finishedAt ? new Date(run.finishedAt).getTime() : now
  return Math.max(0, (end - new Date(run.startedAt).getTime()) / 1000)
}

/** Who started it: the schedule, the command line, the startup rebuild, or the account (the Tasks page). */
export function startedBy(run: Run): string {
  return run.trigger === 'admin' ? (run.startedBy ?? 'admin') : t(`run.by.${run.trigger}`)
}

/** Its result in a few words: what it changed, or why it stopped. */
export function runResult(run: Run): string {
  const n = (value: number) => formatNumber(value, i18n.lang)
  const counts = run.counts
  switch (run.state) {
    case 'running':
      return t('run.result.running')
    case 'failed':
      return run.error ?? ''
    case 'canceled':
      return t('run.result.canceled')
    default:
      break
  }
  if (run.task === 'applyDeletions') {
    return t('run.result.apply', { deleted: n(counts.deleted), kept: n(counts.kept) })
  }
  const sync = t('run.result.sync', { created: n(counts.created), updated: n(counts.updated) })
  if (run.task === 'incremental') {
    return sync
  }
  const deletions = run.deletionsBlocked
    ? t('run.result.blocked', { count: n(run.deletionsPlanned) })
    : t('run.result.deleted', { count: n(counts.deleted) })
  return `${sync} · ${deletions}`
}
