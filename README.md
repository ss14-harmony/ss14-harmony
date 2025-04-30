<div class="header" align="center">
<img alt="Harmony Station" src="https://raw.githubusercontent.com/ss14-harmony/ss14-harmony/refs/heads/master/Resources/Textures/Logo/logo.png">
</div>

Harmony is a fork of [Space Station 14](https://github.com/space-wizards/space-station-14). We aim to stay as close to vanilla SS14 as possible, while adding cosmetic changes that make Harmony our home, and features that enable MRP (medium roleplay) playstyle.

Space Station 14 is a remake of SS13 that runs on [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), a homegrown engine written in C#.

## Links

[Harmony Wiki](http://wiki.harmony14.com/) | [Website (SS14)](https://spacestation14.io/) | [Steam (SS14)](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Standalone Download (SS14)](https://spacestation14.io/about/nightlies/) | [Builds (Harmony)](http://cdn.harmony14.com/fork/harmony/)

## Documentation/Wiki

SS14 [docs site](https://docs.spacestation14.com/) has documentation on SS14's content, engine, game design, and more.
Additionally, see these resources for license and attribution information:
- [Robust Generic Attribution](https://docs.spacestation14.com/en/specifications/robust-generic-attribution.html)
- [Robust Station Image](https://docs.spacestation14.com/en/specifications/robust-station-image.html)

It also has lots of resources for new contributors to the project.

## Contributing

We are happy to accept contributions from anybody. Get in Discord if you want to help and don't be afraid to ask for help either.

Make sure to read our contributing guidelines in [CONTRIBUTING.md](/CONTRIBUTING.md) if you are new to Harmony!

## Building

1. Clone this repo:
```shell
git clone https://github.com/space-wizards/space-station-14.git
```
2. Go to the project folder and run `RUN_THIS.py` to initialize the submodules and load the engine:
```shell
cd space-station-14
python RUN_THIS.py
```
3. Compile the solution:

Build the server using `dotnet build`.

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

Code contributed to this repository after commit `7d6a6073f9c1a3954d17b78d535d43659307cbe9` is licensed under the **GNU Affero General Public License version 3.0** license, unless otherwise stated. See [LICENSE-AGPLv3.txt](LICENSE-AGPLv3.txt).

Code contributed to this repository before commit `7d6a6073f9c1a3954d17b78d535d43659307cbe9` is licensed under the **MIT** license.
See [LICENSE-MIT.TXT](LICENSE-MIT.txt).

Most assets are licensed under [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless stated otherwise. Assets have their license and the copyright in the metadata file. [Example](https://github.com/ss14-harmony/ss14-harmony/blob/master/Resources/Textures/_Harmony/Clothing/Uniforms/Jumpsuit/hop_turtle.rsi/meta.json).

> [!NOTE]
> Some assets are licensed under the non-commercial [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar non-commercial licenses and will need to be removed if you wish to use this project commercially.
