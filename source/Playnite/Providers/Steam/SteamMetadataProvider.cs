﻿using Playnite.MetaProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.Models;
using Playnite.Providers.Steam;

namespace Playnite.Providers.Steam
{
    public class SteamMetadataProvider : IMetadataProvider
    {
        public GameMetadata GetGameData(string gameId)
        {
            var gameData = new Game("SteamGame")
            {
                Provider = Provider.Steam,
                ProviderId = gameId
            };

            var steamLib = new SteamLibrary();
            var data = steamLib.UpdateGameWithMetadata(gameData);
            return new GameMetadata(gameData, data.Icon, data.Image, data.BackgroundImage);
        }

        public bool GetSupportsIdSearch()
        {
            return true;
        }

        public List<MetadataSearchResult> SearchGames(string gameName)
        {
            throw new NotSupportedException();
        }
    }
}
