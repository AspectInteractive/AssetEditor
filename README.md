# Asset Editor

This asset editor will enable in-game modification of OpenRA assets, as well as the ability to hot reload any modifications to YAML files from within the game.

## Asset Editor Component

The asset editor component will feature a GUI with sections for each type of asset. Additionally, adding properties to these assets will be guided by preset options and contextual information. Below are some of the proposed features of the asset editor

- Actor Creation Wizard with preset templates for different types of actors in the game, that prompts for and auto-populates traits, sequences and other details necessary for actor creation

- Trait creation menu that categorises traits by their function, and only lists traits that are available for the given actor

- Trait creator that auto-populates the necessary attributes for the trait to function

- Value drop-down menus, ranges, and check boxes for trait attributes, that are restricted to valid values only

- Sequence Adjuster to enable easier tweaking of offsets and other visual values without having to re-open the game each time

## Hot Reload Component

The Hot Reload component enables the ability to reload any YAML file or object within a YAML file directly from within the game by entering the `/reload <arg>` command. At present the `arg` can be one of two values:

- The name of a YAML file ending in the extension ".yaml", including the folder it is contained in if the same filename exists in multiple folders

- The name of an actor as it is defined within one or more YAML files, e.g. V2RL for the V2 Rocket Launcher unit found in the vehicles.yaml file

Due to complexities in the way traits and attributes are managed by the game, the Hot Reload only supports modifying a limited number of traits and attributes. Below is a list of all the traits and attributes that have so far been confirmed to be modifiable.

NOTE: **Yes*** means it is reloadable, but changes will only appear on newly built units in the game, any existing units will remain unchanged.

| Trait or Trait Attribute                  | Type            | Reloadable? | Description                                                                                       |
| ----------------------------------------- | --------------- | ----------- | ------------------------------------------------------------------------------------------------- |
| Mobile: Speed: *speed*                    | Trait Attribute | Yes*        | Determines the base movement speed of the unit                                                    |
| Mobile                                    | Trait           | No          | Provides all information about any movement that a unit can perform                               |
| Health: HP: *amount*                      | Trait Attribute | Yes*        | Determines the health of the unit                                                                 |
| Valued: Cost: *cost*                      | Trait Attribute | Yes*        | Determines the cost to produce the unit at a production facility                                  |
| RevealsShroud: Range: *range*             | Trait Attribute | Yes*        | Determines the sight range that a unit is able to reveal shroud to                                |
| Transforms: IntoActor: *building*         | Trait Attribute | Yes*        | Determines the building a unit can transform into (refer to MCV unit)                             |
| Armament: Weapon: *weapon*                | Trait Attribute | Yes*        | Determines the weapon that a unit uses                                                            |
| Armament: LocalOffset: *x,y,z*            | Trait Attribute | Yes*        | Determines the offseted position of the firing sprite on the unit relative to the unit's position |
| Armament                                  | Trait           | No          | Determines all of the attack properties of a weapon on the unit                                   |
| Selectable: DecorationBounds: *bounds*    | Trait Attribute | Yes*        | Determines the size of a selection box on a unit                                                  |
| Selectable: Bounds: *bounds*              | Trait Attribute | Yes*        | Determines the size of a selection box on a building                                              |
| Buildable: Queue: *queue*                 | Trait Attribute | No          | Determines which production facility a unit can be built from                                     |
| Tooltip: Name: *name*                     | Trait Attribute | Yes*        | Determines which translation string is  used for a unit's tooltip                                 |
| Power: Amount: *amount*                   | Trait Attribute | Yes*        | Determines the amount of power a building provides                                                |
| Power                                     | Trait           | Yes*        | Determines whether a building provides power or not                                               |
| Building: Footprint: *footprint*          | Trait Attribute | Yes*        | Determines the size of the building's footprint                                                   |
| Building: Dimensions: *dimensions*        | Trait Attribute | Yes*        | Determines the size of the building's dimensions                                                  |
| Buildable: Prerequisites: *prerequisites* | Trait Attribute | No          | Determines what prerequisites a building needs to be built                                        |

## OpenRA

This asset editor is built on top of OpenRA, for use with OpenRA. OpenRA is a Libre/Free Real Time Strategy game engine supporting early Westwood classics.

* Website: [https://www.openra.net](https://www.openra.net)
* Chat: [#openra on Libera](ircs://irc.libera.chat:6697/openra) ([web](https://web.libera.chat/#openra)) or [Discord](https://discord.openra.net) ![Discord Badge](https://discordapp.com/api/guilds/153649279762694144/widget.png)
* Repository: [https://github.com/OpenRA/OpenRA](https://github.com/OpenRA/OpenRA) ![Continuous Integration](https://github.com/OpenRA/OpenRA/workflows/Continuous%20Integration/badge.svg)

## License

OpenRA is Copyright (c) OpenRA Developers and Contributors
This file is part of OpenRA, which is free software. It is made
available to you under the terms of the GNU General Public License
as published by the Free Software Foundation, either version 3 of
the License, or (at your option) any later version. For more
information, see [COPYING](https://github.com/OpenRA/OpenRA/blob/bleed/COPYING).
