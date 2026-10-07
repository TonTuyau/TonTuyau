// Generates mock/fixtures/*.json: a synthetic library (fictional channels, made-up titles, no real data).
// Deterministic (seeded), so a regeneration only changes what you changed here.
// Run: pnpm run mock:fixtures   (then edit the JSON by hand if you like; it's what the mock serves)
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import type { Track, VideoType } from '../src/lib/api/types.ts'
import { fixturesDir, type ChannelFixture, type VideoFixture } from './db.ts'
import { bannerWidths, channelThumbWidths, versionsOf, videoWidths } from './imageSizes.ts'

/** "Now" of the fixtures: the mock library was synced at this time. */
const SYNCED_AT = Date.parse('2026-10-02T18:00:00Z')
const HOUR = 3_600_000
const DAY = 24 * HOUR

// --- Seeded random ---------------------------------------------------------------------------------------
let seed = 20261003
function random(): number {
  seed = (seed + 0x6d2b79f5) | 0
  let x = Math.imul(seed ^ (seed >>> 15), 1 | seed)
  x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x
  return ((x ^ (x >>> 14)) >>> 0) / 4294967296
}
const between = (min: number, max: number) => Math.floor(min + random() * (max - min + 1))
const pick = <T>(items: readonly T[]): T => items[Math.floor(random() * items.length)] as T
const idChars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'
const videoId = () => Array.from({ length: 11 }, () => pick([...idChars])).join('')

// --- Channels --------------------------------------------------------------------------------------------
interface ChannelPlan {
  channel: ChannelFixture
  since: number
  counts: Record<VideoType, number>
  titles: readonly string[]
  topics: readonly string[]
  tags: readonly string[]
  categories: readonly string[]
  lang: 'fr' | 'en'
  viewScale: number
}

function channel(
  id: string,
  name: string,
  subscribers: number,
  description: string,
  tags: string[],
  extra: Partial<ChannelFixture> = {},
): ChannelFixture {
  return {
    id,
    name,
    subscribers,
    thumbnailUrl: `/art/channels/${id}/thumb`,
    thumbnails: versionsOf(`/art/channels/${id}/thumb`, channelThumbWidths, 512),
    description,
    bannerUrl: `/art/channels/${id}/banner`,
    banners: versionsOf(`/art/channels/${id}/banner`, bannerWidths, 2560),
    tags,
    active: true,
    ...extra,
  }
}

const plans: ChannelPlan[] = [
  {
    channel: channel(
      'UCmock-cuisine-margaux0',
      'Cuisine de Margaux',
      224_000,
      'Recettes de saison, techniques de base et grands classiques de la cuisine française.\nUne nouvelle vidéo chaque vendredi, un short le mardi.\n\nToutes les recettes : https://example.org/margaux/recettes\nContact : voir la page « À propos ».',
      ['Cuisine', 'Recettes', 'Pâtisserie'],
    ),
    since: 2016,
    counts: { video: 22, short: 8, stream: 0 },
    titles: [
      '{t} : la recette inratable',
      'Je teste {t} pendant une semaine',
      '{t}, comme au restaurant',
      'Les 5 erreurs à éviter avec {t}',
      '{t} en 15 minutes chrono',
      'Pourquoi votre {t} rate (et comment le réussir)',
    ],
    topics: [
      'la crème brûlée',
      'le bœuf bourguignon',
      'la pâte feuilletée',
      'les crêpes',
      'la ratatouille',
      'le pain au levain',
      'les macarons',
      'la tarte Tatin',
      'la quiche lorraine',
      'le soufflé au fromage',
    ],
    tags: ['cuisine', 'recette', 'pâtisserie', 'facile', 'français'],
    categories: ['Howto & Style'],
    lang: 'fr',
    viewScale: 80_000,
  },
  {
    channel: channel(
      'UCmock-atelier-bois-fer',
      'Atelier Bois & Fer',
      58_300,
      "Menuiserie, soudure et restauration d'outils anciens dans un petit atelier de campagne.",
      ['Bricolage', 'Menuiserie'],
    ),
    since: 2018,
    counts: { video: 15, short: 0, stream: 0 },
    titles: ['Restauration : {t}', 'Fabriquer {t} avec des chutes de bois', '{t} — de A à Z', 'Mon atelier : {t}'],
    topics: [
      'un rabot de 1920',
      'une table de ferme',
      "un établi d'atelier",
      'une étagère murale',
      'un tabouret en chêne',
      'une enclume rouillée',
    ],
    tags: ['bois', 'menuiserie', 'restauration', 'DIY', 'atelier'],
    categories: ['Howto & Style'],
    lang: 'fr',
    viewScale: 30_000,
  },
  {
    channel: channel(
      'UCmock-orbital-mechanics',
      'Orbital Mechanics Lab',
      1_310_000,
      'Rockets, orbits and the maths behind spaceflight, explained with simulations.\nLive launch coverage on Sundays.',
      ['Science', 'Space'],
    ),
    since: 2014,
    counts: { video: 12, short: 0, stream: 6 },
    titles: [
      'How {t} actually works',
      'The surprising physics of {t}',
      '{t}, explained in 10 minutes',
      'Why {t} is so hard',
    ],
    topics: [
      'a gravity assist',
      'orbital rendezvous',
      'a lunar landing',
      'reentry heating',
      'a Hohmann transfer',
      'Lagrange points',
      'reusable boosters',
    ],
    tags: ['space', 'physics', 'rockets', 'orbits', 'science'],
    categories: ['Science & Technology'],
    lang: 'en',
    viewScale: 600_000,
  },
  {
    channel: channel(
      'UCmock-chroniques-eleonore',
      "Les Chroniques d'Éléonore",
      412_000,
      "Histoire de France et d'Europe, racontée sans raccourcis. Sources en description de chaque épisode.",
      ['Histoire', 'Documentaire'],
    ),
    since: 2015,
    counts: { video: 14, short: 0, stream: 0 },
    titles: [
      '{t} : ce que les manuels ne disent pas',
      "L'incroyable histoire de {t}",
      '{t}, une enquête',
      'Pourquoi {t} a tout changé',
    ],
    topics: [
      'la peste noire',
      "Aliénor d'Aquitaine",
      'la Révolution de 1848',
      'les cathédrales gothiques',
      'la guerre de Cent Ans',
      'le canal du Midi',
      'les Templiers',
    ],
    tags: ['histoire', 'documentaire', 'moyen âge', 'France', 'éducation'],
    categories: ['Education'],
    lang: 'fr',
    viewScale: 250_000,
  },
  {
    channel: channel(
      'UCmock-pixelforge-gaming',
      'PixelForge Gaming',
      3_870_000,
      'Speedruns, retro games and long Friday-night streams. Be nice in chat!',
      ['Gaming', 'Speedrun'],
    ),
    since: 2012,
    counts: { video: 10, short: 8, stream: 12 },
    titles: [
      '{t} speedrun (new personal best!)',
      'I beat {t} without taking damage',
      '{t} — 100% playthrough',
      'Blind run: {t}',
    ],
    topics: ['Castle Quest II', 'Neon Drift', 'Star Miner 64', 'Dungeon Bakery', 'Moonlight Racer', 'Pixel Knights'],
    tags: ['gaming', 'speedrun', 'retro', 'stream', 'challenge'],
    categories: ['Gaming'],
    lang: 'en',
    viewScale: 1_500_000,
  },
  {
    channel: channel(
      'UCmock-minute-meca000000',
      'Minute Méca',
      96_500,
      'Une astuce de mécanique en moins d’une minute. Que des shorts.',
      ['Auto', 'Mécanique'],
    ),
    since: 2021,
    counts: { video: 0, short: 14, stream: 0 },
    titles: ['{t} en 30 secondes', 'Astuce : {t}', '{t} ? Facile.'],
    topics: [
      'changer un essuie-glace',
      'vérifier sa pression',
      'purger un radiateur',
      'régler ses phares',
      'nettoyer un injecteur',
    ],
    tags: ['mécanique', 'astuce', 'voiture', 'shorts'],
    categories: ['Autos & Vehicles'],
    lang: 'fr',
    viewScale: 40_000,
  },
  {
    channel: channel(
      'UCmock-quiet-trails00000',
      'Quiet Trails',
      12_400,
      'Long, slow hiking films with no music. YouTube can no longer be reached for this channel since 2025; the archive stays.',
      ['Outdoors', 'Hiking'],
      { active: false, bannerUrl: null },
    ),
    since: 2017,
    counts: { video: 9, short: 0, stream: 0 },
    titles: ['{t} — a silent walk', 'Three days on {t}', 'Winter on {t}'],
    topics: ['the Pyrenean ridge', 'the Vosges crest', 'a Scottish glen', 'the Lofoten coast', 'the Alpine lakes'],
    tags: ['hiking', 'nature', 'relaxing', 'no music'],
    categories: ['Travel & Events'],
    lang: 'en',
    viewScale: 9_000,
  },
  {
    channel: channel(
      'UCmock-direct-dimanche00',
      'Le Direct du Dimanche',
      31_700,
      "L'émission en direct chaque dimanche soir : actualité tech, questions du chat, invités.",
      ['Talk-show', 'Tech'],
    ),
    since: 2022,
    counts: { video: 0, short: 0, stream: 6 },
    titles: ['Le Direct #{n} : {t}', 'Direct spécial : {t}'],
    topics: [
      "l'IA au quotidien",
      'les nouveaux smartphones',
      'vos questions',
      'le jeu vidéo indépendant',
      'la cybersécurité',
    ],
    tags: ['direct', 'tech', 'émission'],
    categories: ['Entertainment'],
    lang: 'fr',
    viewScale: 15_000,
  },
  {
    channel: channel(
      'UCmock-code-et-cafe00000',
      'Code & Café',
      189_000,
      'Programmation web et systèmes, en français et parfois in English. Live coding le mercredi.',
      ['Programmation', 'Tech'],
      { thumbnailUrl: null },
    ),
    since: 2019,
    counts: { video: 10, short: 5, stream: 3 },
    titles: ['{t} : le guide complet', 'Live coding : {t}', '{t} in 100 seconds', 'Comprendre {t} (enfin)'],
    topics: [
      'les closures en JavaScript',
      'Rust ownership',
      'Docker networking',
      'les index Elasticsearch',
      'Svelte 5 runes',
      'HTTP range requests',
      'les regex',
    ],
    tags: ['programmation', 'développement', 'web', 'tutorial', 'code'],
    categories: ['Science & Technology'],
    lang: 'fr',
    viewScale: 70_000,
  },
  {
    channel: channel(
      'UCmock-megastar-hits0000',
      'Mégastar Hits Officiel',
      98_500_000,
      'Chaîne officielle. Clips, coulisses et performances live.',
      ['Musique', 'Pop'],
    ),
    since: 2009,
    counts: { video: 8, short: 4, stream: 0 },
    titles: ['{t} (Clip officiel)', '{t} — Live au Stade', '{t} (Lyrics)'],
    topics: ['Étoile Filante', 'Danse Encore', 'Midnight Paris', 'Cœur Battant', 'Summer Nights'],
    tags: ['musique', 'clip', 'pop', 'officiel'],
    categories: ['Music'],
    lang: 'fr',
    viewScale: 120_000_000,
  },
  {
    channel: channel(
      'UCmock-archives-attente0',
      'Archives en attente',
      320,
      'Chaîne abonnée dont aucune vidéo n’a encore été téléchargée.',
      [],
    ),
    since: 2024,
    counts: { video: 0, short: 0, stream: 0 },
    titles: [],
    topics: [],
    tags: [],
    categories: [],
    lang: 'fr',
    viewScale: 0,
  },
]

// --- Videos ----------------------------------------------------------------------------------------------
const codecs = {
  av1: { mime: 'video/mp4; codecs="av01.0.08M.08, opus"', video: 'av1', audio: 'opus' },
  vp9: { mime: 'video/mp4; codecs="vp09.00.40.08, opus"', video: 'vp9', audio: 'opus' },
  h264: { mime: 'video/mp4; codecs="avc1.640028, opus"', video: 'h264', audio: 'opus' },
  hevc: { mime: 'video/mp4; codecs="hvc1.1.6.L120.90, mp4a.40.2"', video: 'hevc', audio: 'aac' },
  vvc: { mime: 'video/mp4; codecs="vvc1.1.L123.CQA.O1+3, opus"', video: 'vvc', audio: 'opus' },
} as const

type CodecFamily = keyof typeof codecs

function tracks(family: CodecFamily, type: VideoType): { tracks: Track[]; bitrate: number } {
  const { video, audio } = codecs[family]
  const [width, height] =
    type === 'short'
      ? pick([
          [1080, 1920],
          [720, 1280],
        ] as const)
      : pick([
          [3840, 2160],
          [1920, 1080],
          [1280, 720],
        ] as const)
  const videoBitrate = Math.round((width * height * between(8, 14)) / 10) // ≈ bits/s for a 25–60 fps encode
  const audioBitrate = between(96_000, 160_000)
  return {
    tracks: [
      { kind: 'video', codec: video, width, height, bitrate: videoBitrate },
      { kind: 'audio', codec: audio, width: null, height: null, bitrate: audioBitrate },
    ],
    bitrate: videoBitrate + audioBitrate,
  }
}

function duration(type: VideoType): number {
  if (type === 'short') return between(8, 59)
  if (type === 'stream') return between(3_600, 6 * 3_600)
  return random() < 0.15 ? between(3_600, 2 * 3_600 + 1_800) : between(180, 2_700)
}

const descriptions = {
  fr: [
    "Dans cette vidéo, on prend le temps de tout expliquer, étape par étape.\n\n00:00 Introduction\n02:15 Le matériel\n07:40 La méthode\n15:02 Les erreurs fréquentes\n\nMerci d'avoir regardé ! N'hésitez pas à poser vos questions.",
    'Un épisode un peu différent cette fois-ci : plus long, plus détaillé.\nToutes les sources sont listées ci-dessous.\n\n• Source 1 : archives départementales\n• Source 2 : entretiens\n\nBon visionnage !',
    'Version courte : ça marche. Version longue : regardez la vidéo.',
  ],
  en: [
    'In this video we go through everything step by step.\n\n00:00 Intro\n03:10 The setup\n09:45 The hard part\n18:20 Wrap-up\n\nThanks for watching! Questions welcome.',
    'A longer, more detailed episode than usual.\nAll sources are listed below.\n\n- Source 1: public archives\n- Source 2: interviews\n\nEnjoy!',
    'Short version: it works. Long version: watch the video.',
  ],
} as const

function iso(time: number): string {
  return new Date(Math.floor(time / 1000) * 1000).toISOString().replace('.000Z', 'Z')
}

function makeVideo(plan: ChannelPlan, type: VideoType, index: number, overrides: Partial<VideoFixture> = {}) {
  const id = overrides.id ?? videoId()
  const family: CodecFamily = overrides.mimeType ? 'h264' : pick(['av1', 'av1', 'av1', 'vp9', 'vp9', 'h264'] as const)
  const media = tracks(family, type)
  const durationSeconds = overrides.durationSeconds ?? duration(type)
  const start = Date.UTC(plan.since, 0, 1)
  const published = start + random() * (SYNCED_AT - 3 * DAY - start)
  const views = Math.max(3, Math.round(plan.viewScale * Math.pow(random(), 2.2) * (type === 'short' ? 2 : 1)))
  const raw = pick(plan.titles)
    .replace('{t}', pick(plan.topics))
    .replace('{n}', String(index + 1))
  const title = raw.charAt(0).toUpperCase() + raw.slice(1)
  const video: VideoFixture = {
    id,
    title: type === 'short' ? `${title} #shorts` : title,
    type,
    published: iso(published),
    durationSeconds,
    views,
    active: plan.channel.active ? random() > 0.08 : random() > 0.6,
    thumbnailUrl: `/art/videos/${id}`,
    thumbnails: [],
    channelId: plan.channel.id,
    description: pick(descriptions[plan.lang]),
    downloaded: iso(Math.min(SYNCED_AT - HOUR, published + between(1, 400) * DAY)),
    likes: Math.round(views * (0.01 + random() * 0.05)),
    tags: plan.tags.filter(() => random() > 0.35),
    categories: [...plan.categories],
    fileSize: Math.round((media.bitrate * durationSeconds) / 8),
    mimeType: codecs[family].mime,
    tracks: media.tracks,
    mediaUrl: `/media/${id}`,
    downloadUrl: `/media/${id}/download`,
    mediaAvailable: true,
    ...overrides,
  }
  return video
}

const videos: VideoFixture[] = []
for (const plan of plans) {
  for (const type of ['video', 'short', 'stream'] as const) {
    for (let index = 0; index < plan.counts[type]; index++) {
      videos.push(makeVideo(plan, type, index))
    }
  }
}

// --- Edge cases with readable ids (listed in docs/milestones/M5a-front-mockup.md) -------------------------
const planOf = (name: string): ChannelPlan => plans.find((plan) => plan.channel.name === name) as ChannelPlan
function edge(name: string, type: VideoType, overrides: Partial<VideoFixture> & { id: string }) {
  const plan = planOf(name)
  const video = makeVideo(plan, type, 0, overrides)
  if (overrides.mimeType) {
    const family = (Object.keys(codecs) as CodecFamily[]).find((key) => codecs[key].mime === overrides.mimeType)
    if (family) video.tracks = tracks(family, type).tracks
  }
  videos.push(video)
}

edge('Quiet Trails', 'video', {
  id: 'mockInactiv',
  title: 'The Pyrenean ridge — unavailable on YouTube',
  active: false,
})
edge('Cuisine de Margaux', 'video', {
  id: 'mockNoFile0',
  title: 'Le croissant maison (fichier manquant sur le partage)',
  mediaAvailable: false,
})
edge('Orbital Mechanics Lab', 'video', {
  id: 'mockHevc000',
  title: 'Aerobraking explained (HEVC file: plays in some browsers only)',
  mimeType: codecs.hevc.mime,
})
edge('Code & Café', 'video', {
  id: 'mockVvc0000',
  title: 'Le codec VVC (H.266) : aucun navigateur ne sait le lire',
  mimeType: codecs.vvc.mime,
})
edge("Les Chroniques d'Éléonore", 'video', {
  id: 'mockLongTtl',
  title:
    "L'extraordinaire et méconnue épopée d'Éléonore de Saint-Étienne-du-Rouvray, châtelaine, cartographe et diplomate, qui négocia la paix de 1347 à travers les forêts gelées de Bourgogne — partie 1 sur 12 (version intégrale, sous-titrée, restaurée en 4K)",
  description:
    'Épisode intégral. '.repeat(40) +
    '\n\nChapitres :\n00:00 Prologue\n12:30 Les forêts gelées\n48:10 La négociation\n1:35:00 Épilogue\n\nBibliographie complète : voir le livret.',
  durationSeconds: 2 * 3_600 + 14 * 60 + 9,
})
edge('Atelier Bois & Fer', 'video', { id: 'mockNoThumb', title: 'Vidéo sans miniature', thumbnailUrl: null })
edge('Atelier Bois & Fer', 'video', {
  id: 'mockArt404x',
  title: "Miniature annoncée mais introuvable (404 sur l'image)",
})
edge('Mégastar Hits Officiel', 'video', {
  id: 'mockHugeVws',
  title: 'Étoile Filante (Clip officiel) — 3 milliards de vues',
  views: 3_456_789_012,
  likes: 21_400_000,
  published: '2011-06-17T16:00:00Z',
})
edge('Atelier Bois & Fer', 'video', {
  id: 'mockNoDescr',
  title: 'Sans description, sans mots-clés',
  description: '',
  tags: [],
  categories: [],
})
edge('PixelForge Gaming', 'short', {
  id: 'mockShort01',
  title: 'Frame-perfect jump in 9 seconds #shorts',
  durationSeconds: 9,
})
edge('PixelForge Gaming', 'stream', {
  id: 'mockStream1',
  title: '6-hour charity marathon — every Castle Quest game back to back',
  durationSeconds: 6 * 3_600 + 2 * 60 + 41,
})
edge('Orbital Mechanics Lab', 'video', {
  id: 'mockFresh01',
  title: 'Launch recap: published two hours before the sync',
  published: iso(SYNCED_AT - 2 * HOUR),
  views: 1,
  likes: 1,
})
edge('Cuisine de Margaux', 'short', {
  id: 'mockInactSh',
  title: 'Short indisponible sur YouTube #shorts',
  active: false,
})
// Links in a description (QA06, M5a-7): linkified; timestamps and markup stay plain text.
edge('Code & Café', 'video', {
  id: 'mockLinks00',
  title: 'Description avec des liens (et du HTML qui doit rester du texte)',
  description:
    'Le code de la vidéo : https://example.org/code-et-cafe/episode-42?lang=fr&v=2#setup.\n' +
    'La doc (https://example.com/docs/intro_(fr)) et le dépôt https://example.net/depot, entre autres.\n' +
    'Pas un lien : www.example.org, ftp://example.org/fichier, javascript:alert(1)\n' +
    'Du HTML affiché tel quel : <b>gras ?</b> <img src=x onerror=alert(1)>\n\n' +
    'Chapitres :\n00:00 Intro\n03:15 Le code\n1:02:03 Questions',
})
// canPlayType "maybe" (M5a-16): an ambiguous codec string (no profile). Chromium answers "maybe" for it (and
// Media Source Extensions say no); the file itself is the H.264 test pattern, so it plays.
edge('Orbital Mechanics Lab', 'video', {
  id: 'mockMaybe00',
  title: 'Format incertain : le navigateur répond « maybe »',
  mimeType: 'video/mp4; codecs="avc1"',
})

// The player's shape (M5): the box takes the video track's ratio, so none of these has black bars. The mock serves a
// test pattern of the same shape (plugin.ts picks it from the track).
const shaped = (width: number, height: number): Track[] => [
  { kind: 'video', codec: 'h264', width, height, bitrate: 900_000 },
  { kind: 'audio', codec: 'aac', width: null, height: null, bitrate: 128_000 },
]
const h264Aac = 'video/mp4; codecs="avc1.640028, mp4a.40.2"'
edge("Les Chroniques d'Éléonore", 'video', {
  id: 'mock4by3000',
  title: 'Archive 4:3 : le journal télévisé de 1987 (sans bandes noires)',
  mimeType: h264Aac,
  tracks: shaped(640, 480),
})
edge('Orbital Mechanics Lab', 'video', {
  id: 'mockVertic0',
  title: 'A vertical video that is not a short (filmed on a phone)',
  mimeType: h264Aac,
  tracks: shaped(1080, 1920),
})
edge('PixelForge Gaming', 'video', {
  id: 'mockWide000',
  title: 'Cinematic trailer in 2.35:1 (ultra-wide)',
  mimeType: h264Aac,
  tracks: shaped(1920, 816),
})
// No video track in the API: the box starts 16:9, then takes the loaded file's shape (the 4:3 test pattern).
edge('Code & Café', 'video', {
  id: 'mockNoVTrk0',
  title: 'Sans piste vidéo dans les métadonnées (forme lue dans le fichier)',
  mimeType: h264Aac,
  tracks: [{ kind: 'audio', codec: 'aac', width: null, height: null, bitrate: 128_000 }],
})
// mediaAvailable is true but the file isn't on the share (ids starting with "mockGone": the media answers 404):
// the player's error path asks the server and shows "file unavailable".
edge('Cuisine de Margaux', 'video', {
  id: 'mockGoneFil',
  title: 'Fichier annoncé mais absent du partage (404 à la lecture)',
  mimeType: h264Aac,
})

// Resized versions (v1.4) whose originals don't exist yet: only the original is listed.
edge('Atelier Bois & Fer', 'video', {
  id: 'mockOrigOnl',
  title: 'Miniature sans versions réduites (pas encore faites par la synchro)',
  thumbnails: [{ width: 1280, url: '/art/videos/mockOrigOnl' }],
})

// The version lists (v1.4) follow each video's thumbnail: the mock draws its art at any width, the original being
// 1280 px wide (720 px for a short). A list given by an edge case is kept as is.
for (const video of videos) {
  if (video.thumbnails.length === 0) {
    video.thumbnails = versionsOf(video.thumbnailUrl, videoWidths, video.type === 'short' ? 720 : 1280)
  }
}

// --- Write -----------------------------------------------------------------------------------------------
const pretty = (value: unknown) => `${JSON.stringify(value, null, 2)}\n`
writeFileSync(join(fixturesDir, 'channels.json'), pretty(plans.map((plan) => plan.channel)))
writeFileSync(join(fixturesDir, 'videos.json'), pretty(videos))
console.log(`Wrote ${plans.length} channels and ${videos.length} videos to ${fixturesDir}`)
