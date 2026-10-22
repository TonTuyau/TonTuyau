# TonTuyau's icons

[Material Symbols](https://fonts.google.com/icons), **Rounded**, weight 400, by Google, under the
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0): the SVG files of `@material-symbols/svg-400` 0.48.1,
as they come, named as Google names them. `<name>-fill.svg` is a symbol's filled form, used for the navigation's
current page.

`tontuyau_shorts.svg` and `tontuyau_shorts-fill.svg` are TonTuyau's own (GPL-3.0-or-later, as the rest): a portrait
frame with a play triangle, drawn to match, in place of YouTube's Shorts logo.

To add one: copy `rounded/<name>.svg` from the package (https://cdn.jsdelivr.net/npm/@material-symbols/svg-400@0.48.1/rounded/)
here, and name it in `src/components/Icon.svelte`. `Icon.test.ts` checks that each icon has its file and each file is used.
