// What the Tasks page shows of a run (1.4), from what the API gave: never a count it didn't send.
import type { Run, RunTask } from './api/types'
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

/** The command line that does the same as each task. */
export const taskCommands: Record<RunTask, string> = {
  incremental: 'tontuyau sync incremental',
  full: 'tontuyau sync full',
  applyDeletions: 'tontuyau sync apply-deletions <run>',
  refreshVideo: 'tontuyau youtube video <id>',
  refreshChannel: 'tontuyau youtube channel <id>',
  refreshChannelVideos: 'tontuyau youtube channel-videos <id>',
}

/** The refreshes on YouTube (1.5), as the Tasks page shows them. */
export const refreshTasks: RunTask[] = ['refreshVideo', 'refreshChannel', 'refreshChannelVideos']

/** A refresh on YouTube (1.5): a video, a channel, or a channel and its videos. */
export function isRefresh(run: Run): boolean {
  return run.task === 'refreshVideo' || run.task === 'refreshChannel' || run.task === 'refreshChannelVideos'
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
  if (isRefresh(run)) {
    return t('run.result.refresh', {
      found: n(counts.foundOnYouTube),
      gone: n(counts.goneFromYouTube),
      failed: n(counts.checkFailed),
    })
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
