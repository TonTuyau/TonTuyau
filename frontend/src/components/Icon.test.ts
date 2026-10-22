import { render } from '@testing-library/svelte'
import Icon, { iconFiles, iconPath, type IconName } from './Icon.svelte'

const inFolder = Object.keys(import.meta.glob('../assets/icons/*.svg')).map((path) =>
  path.replace('../assets/icons/', '').replace(/\.svg$/, ''),
)
const names = Object.keys(iconFiles) as IconName[]
const navigation: IconName[] = [
  'home',
  'shorts',
  'channels',
  'videos',
  'streams',
  'dashboard',
  'tasks',
  'smartDisplay',
  'key',
]

describe('Icon', () => {
  it('has a file for each icon, with its one path', () => {
    expect(names.filter((name) => iconPath(iconFiles[name]) === null)).toEqual([])
  })

  it('has a filled form for each icon of the navigation', () => {
    expect(navigation.filter((name) => iconPath(`${iconFiles[name]}-fill`) === null)).toEqual([])
  })

  it('uses every file of the folder', () => {
    const used = new Set(names.flatMap((name) => [iconFiles[name], `${iconFiles[name]}-fill`]))

    expect(inFolder.filter((file) => !used.has(file))).toEqual([])
  })

  it('draws the outline, or the filled form when asked and there is one', () => {
    const path = (filled: boolean, name: IconName = 'home') =>
      render(Icon, { name, filled }).container.querySelector('path')?.getAttribute('d')

    expect(path(false)).toBe(iconPath('home'))
    expect(path(true)).toBe(iconPath('home-fill'))
    expect(path(true, 'search')).toBe(iconPath('search'))
  })
})
