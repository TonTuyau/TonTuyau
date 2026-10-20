// What the Tasks page shows of a run (1.4), from what the API gave: never a count it didn't send.
import type { Run, RunTask } from './api/types'
import { formatDateTime, formatNumber } from './format'
import { i18n, t } from './i18n/i18n.svelte'

/** How long it ran, or has been running (in seconds), at `now`. */
export function runSeconds(run: Run, now: number): number {
  const end = run.finishedAt ? new Date(run.finishedAt).getTime() : now
  return Math.max(0, (end - new Date(run.startedAt).getTime()) / 1000)
}

/** Who started it: itself (a refresh resuming), the command line, the startup, or the account (the Tasks page). */
export function startedBy(run: Run): string {
  return run.trigger === 'admin' ? (run.startedBy ?? 'admin') : t(`run.by.${run.trigger}`)
}

/** The command line that does the same as each task. */
export const taskCommands: Record<RunTask, string> = {
  import: 'tontuyau import',
  indexRebuild: 'tontuyau index rebuild',
  refreshVideo: 'tontuyau youtube video <id>',
  refreshChannel: 'tontuyau youtube channel <id>',
  refreshChannelVideos: 'tontuyau youtube channel-videos <id>',
  downloadVideo: 'tontuyau youtube download <id>',
}

/** The refreshes on YouTube (1.5), as the Tasks page shows them. */
export const refreshTasks: RunTask[] = ['refreshVideo', 'refreshChannel', 'refreshChannelVideos']

/** A refresh on YouTube (1.5): a video, a channel, or a channel and its videos. */
export function isRefresh(run: Run): boolean {
  return run.task === 'refreshVideo' || run.task === 'refreshChannel' || run.task === 'refreshChannelVideos'
}

/** A run that asks YouTube (1.5: a refresh; 2.0: a download): it can be put on hold. */
export function asksYouTube(run: Run): boolean {
  return isRefresh(run) || run.task === 'downloadVideo'
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
    case 'onHold':
      return t(run.resumeAt ? 'run.result.onHold' : 'run.result.onHoldByHand', {
        at: run.resumeAt ? formatDateTime(run.resumeAt, i18n.lang) : '–',
        read: n(counts.videosRead + counts.channelsRead),
        left: n(run.left),
      })
    default:
      break
  }
  if (run.task === 'indexRebuild') {
    return t('run.result.rebuild')
  }
  if (isRefresh(run)) {
    return t('run.result.refresh', {
      found: n(counts.foundOnYouTube),
      unlisted: n(counts.unlistedOnYouTube),
      gone: n(counts.goneFromYouTube),
      private: n(counts.privateOnYouTube),
      failed: n(counts.checkFailed),
    })
  }
  return t('run.result.import', { created: n(counts.created), unchanged: n(counts.unchanged) })
}
