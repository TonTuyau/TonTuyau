// hls.js's light build (no subtitles, alternate audio nor DRM: TonTuyau has none) has no type declarations of its
// own: the same API as the full build's.
declare module 'hls.js/light' {
  export * from 'hls.js'
  export { default } from 'hls.js'
}
