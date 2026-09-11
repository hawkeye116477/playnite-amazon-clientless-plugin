# playnite-amazon-clientless-plugin
Fully clientless Amazon Games library integration for Playnite 11+.

[![Crowdin](https://badges.crowdin.net/playnite-legendary-plugin/localized.svg)](https://crowdin.com/project/playnite-legendary-plugin)
[![Releases (latest by date)](https://img.shields.io/github/downloads/hawkeye116477/playnite-amazon-clientless-plugin/latest/total)](https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/releases/latest)

## **License**
This project is distributed under the terms of the [GPLv3 license](LICENSE.md) and uses third-party libraries that are distributed under their own terms (see [ThirdPartyLicenses.txt](ThirdPartyLicenses.txt)).

## **Credits**
This project is based on research from following projects:
* https://github.com/JosefNemec/PlayniteExtensions/tree/master/source/Libraries/AmazonGamesLibrary
* https://github.com/utkarshdalal/GameNative/blob/master/docs/AMAZON_API_SPEC.json
* https://github.com/imLinguin/nile

Thanks for all creators.

## **Bugs**
If you encounter any bug, then you can report it at [github.com/hawkeye116477/playnite-amazon-clientless-plugin/issues](https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/issues/new?template=bugs.yml), but before opening any ticket you should read [Troubleshooting section on wiki](https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/wiki/Troubleshooting).

## **New cool features**
If you want some new feature, then you can say about that at [github.com/hawkeye116477/playnite-amazon-clientless-plugin/issues](https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/issues/new?template=features.yml).

## **Questions**
If you read [wiki](https://github.com/hawkeye116477/playnite-amazon-clientless-plugin/wiki) and still don't know something, then you can ask a question at Playnite's Discord server or [subreddit](https://www.reddit.com/r/playnite/).

## **Building**
 To build this extension, you can just use your favourite IDE like JetBrains Rider.
 After you compile, you can open `Playnite => Settings => For developers` and choose path where is dll located to load it or alternatively go to `make_scripts` directory and execute `make_extension.py` script with **Python** to make single .pext file which can be dropped to Playnite's window to install it.
