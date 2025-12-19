# Increase Dice Limit
[![Push to nuget feed on release](https://github.com/TaleSpire-Modding/IncreaseDiceLimit/actions/workflows/release.yml/badge.svg)](https://github.com/TaleSpire-Modding/IncreaseDiceLimit/actions/workflows/release.yml)

Is 40 dice not enough? This mod allows for more dice in a group.

## Install

Currently you need to either follow the build guide down below or use the R2ModMan. 

## Usage
The Plugin allows for increasing the limit of dice in a group/pool by config (default 400). The plugin also allow bulk update of dice at x10 and x100 by holding leftalt or leftctrl whilst clicking adding/removing dice repsectively. This mod doesn't require other players to have it installed to see the dice but requireds to be installed if they want to use the functionality. If you set the limit to -1, it will remove the cap completely (Past experience you don't want to go past 1000 but feel free to experiment. This comes with 0 warranty).

## How to Compile / Modify

Open ```IncreaseDiceLimit.sln``` in Visual Studio.

Build the project (We now use Nuget).

Browse to the newly created ```bin/Debug``` or ```bin/Release``` folders and copy the ```.dll``` to ```Steam\steamapps\common\TaleSpire\BepInEx\plugins```

## Changelog
- 1.3.0: BR Dice fixes and improvements
- 1.2.1: Pipeline deploy and upgrade
- 1.2.0: 4.8 Framework update and Nuget Release
- 1.2.0: Fixed clamping of Dice limit
- 1.1.0: Cyberpunk fix
- 1.0.0: Initial release

## Shoutouts
Shoutout to my Patreons on https://www.patreon.com/HolloFox recognising your
mighty contribution to my caffeine addiciton:
- John Fuller
- [Tales Tavern](https://talestavern.com/) - MadWizard
